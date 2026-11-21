using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Turns;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// Who plays a game shown on this computer (ADR-222): the colours played here, and what this computer runs. The default is a game on one
/// computer: every colour is played here (hot-seat), nothing pauses it, the turn timer runs the commands itself. A LAN game's host and client
/// make their own (<c>LanSeats</c> in Core.Network).
/// </summary>
public sealed record LocalSeats
{
    /// <summary>A game on one computer.</summary>
    public static LocalSeats OneComputer { get; } = new();

    /// <summary>Whether a colour is played on another computer: on a LAN host the remote colours, on a client every colour but its own.</summary>
    public Func<PlayerId, bool> PlaysElsewhere { get; init; } = _ => false;

    /// <summary>Whether the game waits for a colour nobody plays (ADR-195): the computer players and the turn timer wait.</summary>
    public Func<bool> IsPaused { get; init; } = () => false;

    /// <summary>Runs the turn timer's commands; null runs them as the session's own. A LAN host runs them for any colour (ADR-198).</summary>
    public Func<GameCommand, CommandResult>? Execute { get; init; }

    /// <summary>Hears the turn timer's clock once a frame (a LAN host sends it to its clients when it changes, ADR-198).</summary>
    public Action<TurnClock>? ShareClock { get; init; }

    /// <summary>The clock this computer only shows: a LAN client's, the host's (ADR-198); null when this computer runs its own.</summary>
    public Func<TurnClock?>? SharedClock { get; init; }

    /// <summary>
    /// Whether the computer players wait while a battle is fought: on a LAN host a battle this computer does not show waits for its players
    /// (ADR-195).
    /// </summary>
    public bool ComputersWaitForBattles { get; init; }
}

/// <summary>
/// What the turn clock shows this frame (ADR-091, ADR-198): the seconds left (null: no timed turn), whether it stands still for a battle between
/// two people, whether the time is up, and the warning (seconds left) the time has just passed.
/// </summary>
public readonly record struct ClockFrame(double? SecondsLeft, bool Stopped, bool Up, int? Warning);

/// <summary>
/// Runs a game shown on this computer, frame by frame (ADR-222): which colours are played here, when the computer players play (ADR-059), when
/// the turn timer runs and ends a turn (ADR-091), and which person's turn the map turns to (ADR-090). It changes the game only through commands:
/// the computer players' (<see cref="AdventureAi"/>) and the timer's (<see cref="TurnTimer"/>). The front-end says, each frame, what it shows:
/// whether computer heroes' moves are still being shown and whether a screen is open that holds the game (the menu, the battle scene, the auto
/// combat question).
/// <para>
/// Once a frame, in this order: <see cref="RunClock"/>; then, unless computer moves are being shown, <see cref="TakeTurnToShow"/> and
/// <see cref="PlayComputer"/>. The replay viewer's director (<c>watching</c>) runs neither the computer players nor the clock. A session that
/// follows a LAN host runs no rules: the host's computer players and clock are the game's.
/// </para>
/// </summary>
public sealed class GameDirector
{
    private readonly GameSession _session;
    private readonly LocalSeats _seats;

    /// <summary>The seconds left on the shared clock last shown, for its warnings.</summary>
    private int? _clockShown;

    /// <summary>A person's turn that started while computer moves were still shown: the map turns to them once they are.</summary>
    private PlayerId? _turnToShow;

    public GameDirector(GameSession session, LocalSeats? seats = null, bool watching = false)
    {
        _session = session;
        _seats = seats ?? LocalSeats.OneComputer;
        var runs = !watching && !session.FollowsHost;
        Ai = runs ? new AdventureAi(session) : null;
        Timer = runs && session.State.Options.TurnTimerMinutes is not null ? new TurnTimer(session, _seats.Execute) : null;
    }

    /// <summary>The computer players, when this computer plays them.</summary>
    public AdventureAi? Ai { get; }

    /// <summary>The turn timer, when the game has one and this computer runs it.</summary>
    public TurnTimer? Timer { get; }

    /// <summary>Whether the game waits for a colour nobody plays.</summary>
    public bool IsPaused => _seats.IsPaused();

    /// <summary>Whether a person's turn waits for the computer's moves to be shown before the map turns to it.</summary>
    public bool HasTurnToShow => _turnToShow is not null;

    /// <summary>Whether a colour is played on another computer.</summary>
    public bool PlaysElsewhere(PlayerId player) => _seats.PlaysElsewhere(player);

    /// <summary>
    /// Whether a colour is played at this screen, so the map turns to it and its questions open here: every person (hot-seat), except in a LAN
    /// game, where only this computer's colours are.
    /// </summary>
    public bool PlaysHere(PlayerId player) => _session.State.GetPlayer(player).IsHuman && !PlaysElsewhere(player);

    /// <summary>Whose eyes the map is drawn through when the game is shown: the current player if it plays here, else the first who does.</summary>
    public PlayerId Viewer()
    {
        var state = _session.State;
        return PlaysHere(state.CurrentPlayer.Id) ? state.CurrentPlayer.Id : state.Players.FirstOrDefault(p => PlaysHere(p.Id))?.Id ?? state.CurrentPlayer.Id;
    }

    /// <summary>
    /// Runs the clock for <paramref name="seconds"/>: this computer's turn timer, or the shared clock it only shows; null when there is neither.
    /// The timer stands still while computer moves are shown and while the game is paused. When its time is up it ends the turn unless computer
    /// moves are shown, the game is paused, or <paramref name="screensOpen"/> on this computer while a timed colour plays here (a LAN host ends a
    /// client's turn whatever it shows).
    /// </summary>
    public ClockFrame? RunClock(double seconds, bool showingComputerMoves, bool screensOpen)
    {
        if (Timer is { } timer)
        {
            var paused = IsPaused;
            var canEnd = !showingComputerMoves && !paused && (!screensOpen || !timer.TimedPlayers.Any(PlaysHere));
            var warning = timer.Tick(showingComputerMoves || paused ? 0 : seconds, canEnd);
            var frame = new ClockFrame(timer.SecondsLeft, timer.IsStopped, timer.IsUp, warning);
            _seats.ShareClock?.Invoke(timer.Clock);
            return frame;
        }

        if (_seats.SharedClock?.Invoke() is not { } clock)
        {
            return null;
        }

        int? passed = _clockShown is { } before && clock.SecondsLeft is { } left ? TurnTimer.WarningPassed(before, left) : null;
        _clockShown = clock.SecondsLeft;
        return new ClockFrame(clock.SecondsLeft, clock.Stopped, clock.Up, passed);
    }

    /// <summary>
    /// A turn started: true when the map turns to its player now. A colour not played here leaves the map with the last person here; while
    /// computer moves are shown, the turn waits for them (<see cref="TakeTurnToShow"/>).
    /// </summary>
    public bool TurnStarted(PlayerId player, bool showingComputerMoves)
    {
        if (!PlaysHere(player))
        {
            return false;
        }

        if (showingComputerMoves)
        {
            _turnToShow = player;
            return false;
        }

        return true;
    }

    /// <summary>The person's turn that waited for the computer's moves to be shown, once; null when none waits.</summary>
    public PlayerId? TakeTurnToShow()
    {
        var player = _turnToShow;
        _turnToShow = null;
        return player;
    }

    /// <summary>
    /// The computer players play one turn when it is theirs (in a play-test whose others are off, they end it at once), unless <paramref name="screensOpen"/> (a person's battle or question may be open),
    /// the game is paused, or a battle waits for players elsewhere. True when the computer played.
    /// </summary>
    public bool PlayComputer(bool screensOpen)
    {
        var state = _session.State;
        if (Ai is null || screensOpen || !AdventureAi.IsComputerTurn(state) || IsPaused || (_seats.ComputersWaitForBattles && state.Battle is not null))
        {
            return false;
        }

        if (state.Options.PlayTest is { OthersIdle: true })
        {
            // A play-test's others are off (ADR-267): what they are asked is answered, then their turn ends at once.
            return Ai.PassTurn();
        }

        Ai.PlayTurn();
        return true;
    }
}
