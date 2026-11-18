using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Turns;

/// <summary>
/// The simultaneous days (MP-2, ADR-197, ADR-217; <see cref="GameOptions.SimultaneousFirstWeek"/>) in one place: which days are played at once,
/// opening and ending each person's turn, whose turn a command runs in, who must wait, the conflict watch (<see cref="SimultaneousDayWatch"/>) and
/// where the day's commands begin (<see cref="DayStart"/>). It alone changes the game's record of them (<see cref="GameState.OpenTurns"/>, the
/// turns ended today, <see cref="GameState.SimultaneousEnded"/>, <see cref="GameState.SimultaneousConflict"/>); <see cref="TurnService"/> begins
/// and passes the turns, and <see cref="GameSession"/> tells it when a command is done.
/// </summary>
internal sealed class SimultaneousDayCoordinator
{
    private readonly GameState _state;
    private readonly IEventPublisher _events;
    private readonly SimultaneousDayWatch _watch;
    private bool _opened;

    internal SimultaneousDayCoordinator(GameState state, IEventPublisher events)
    {
        _state = state;
        _events = events;
        _watch = new SimultaneousDayWatch(state, events);
    }

    /// <summary>
    /// Where today's commands begin in the session's command log (MP-2, ADR-197): the commands before it rebuild the game as the day opened, which a
    /// LAN host plays again turn by turn after a conflict. 0 before the first simultaneous day.
    /// </summary>
    internal int DayStart { get; private set; }

    /// <summary>Listens for the visits the conflict watch checks; a LAN client's session, which runs no rules, does not.</summary>
    internal void SubscribeTo(IEventSubscriber bus) => bus.Subscribe<MapObjectVisited>(e => _watch.Visited(e.Hero, e.Object));

    // The calendar (TurnService) ------------------------------------------------------------------------------

    /// <summary>A new day: nobody has ended today's turn yet.</summary>
    internal void NewDay() => _state.DoneToday.Clear();

    /// <summary>Whether <paramref name="player"/> ended its turn today on a simultaneous day; it does not play again until the next day.</summary>
    internal bool HasEnded(PlayerId player) => _state.DoneToday.Contains(player);

    /// <summary>
    /// Opens today's turns at once when today is simultaneous: every person still in the game, in order, each turn begun by
    /// <paramref name="beginTurn"/> as the current player. False on a day played turn by turn.
    /// </summary>
    internal bool TryOpen(Action beginTurn)
    {
        if (!IsSimultaneousDay())
        {
            return false;
        }

        var people = _state.Players.Where(p => p.IsHuman && !p.IsDefeated).Select(p => p.Id).ToList();
        _state.OpenTurnList.AddRange(people);
        foreach (var person in people)
        {
            _state.CurrentPlayerIndex = _state.IndexOf(person);
            beginTurn();
        }

        PointAtOpenTurn();
        _opened = true;
        _events.Publish(new SimultaneousDayStarted(_state.Date, people));
        return true;
    }

    /// <summary>
    /// <paramref name="player"/> ended its turn of a simultaneous day: the others go on. Called before <see cref="TurnEnded"/> tells of it
    /// (ADR-214).
    /// </summary>
    internal void EndTurn(PlayerId player)
    {
        _state.OpenTurnList.Remove(player);
        _state.DoneToday.Add(player);
        PointAtOpenTurn();
    }

    /// <summary>
    /// <paramref name="player"/>'s turn leaves today's open turns (the computer took it over, ADR-195, or it is out). True when that closed the
    /// last open turn, so the computer players now play in order.
    /// </summary>
    internal bool Leave(PlayerId player)
    {
        if (!_state.OpenTurnList.Remove(player))
        {
            return false;
        }

        PointAtOpenTurn();
        return !_state.IsSimultaneous;
    }

    /// <summary>
    /// Today is played turn by turn from its start, and every day after it (<see cref="PlayTurnByTurnCommand"/>): the conflict is settled, and who
    /// has ended today's turn keeps it ended.
    /// </summary>
    internal void EndDays(string reason)
    {
        if (!_state.IsSimultaneous)
        {
            throw new CommandRefusedException("Today is not played at once.");
        }

        _state.SimultaneousEnded = true;
        _state.SimultaneousConflict = null;
        _state.OpenTurnList.Clear();
        _events.Publish(new SimultaneousDaysEnded(_state.Date, reason));
    }

    // The commands (GameSession) ------------------------------------------------------------------------------

    /// <summary>Why every command but <see cref="PlayTurnByTurnCommand"/> is refused: the day's conflict, until the host plays it again turn by turn.</summary>
    internal string? Stopped(GameCommand command) =>
        _state.SimultaneousConflict is { } conflict && command is not PlayTurnByTurnCommand ? $"The day is played again turn by turn: {conflict}" : null;

    /// <summary>
    /// Why <paramref name="player"/> may not give a map command now: not its turn (or ended today), or on a simultaneous day another's battle or
    /// question holds the day (one at a time, ADR-197). Null when it may.
    /// </summary>
    internal string? Refusal(PlayerId player)
    {
        if (!_state.IsTurnOf(player))
        {
            return HasEnded(player) ? $"{_state.GetPlayer(player).Name} has ended the turn: the others still play today." : $"It is not {player}'s turn.";
        }

        if (!_state.IsSimultaneous)
        {
            return null;
        }

        if (_state.Battle is not null)
        {
            return "Wait: a battle is being fought.";
        }

        return _state.PendingDecision is not null && _state.DecisionPlayer is { } asked && asked != player
            ? $"Wait: {_state.GetPlayer(asked).Name} answers a question."
            : null;
    }

    /// <summary>On a simultaneous day a command runs as its own player's turn; battle commands and the settings any player gives at any time do not.</summary>
    internal void TakeTurnOf(GameCommand command)
    {
        if (_state.IsSimultaneous && command is not (IBattleCommand or SetAutoCombatCommand or HandToComputerCommand or PlayTurnByTurnCommand))
        {
            _state.CurrentPlayerIndex = _state.IndexOf(command.Player);
        }
    }

    /// <summary>The game started: when its first day opened at once, its commands begin at <paramref name="logged"/>.</summary>
    internal void Started(int logged) => NoteOpenedDay(logged);

    /// <summary>A command was refused: the current player is again the host's open turn.</summary>
    internal void Refused() => PointAtOpenTurn();

    /// <summary>
    /// A command was done (<paramref name="logged"/> commands in the log): the current player is again the host's open turn, a day it opened
    /// begins its commands here, and the watch looks for a conflict.
    /// </summary>
    internal void Executed(int logged)
    {
        PointAtOpenTurn();
        NoteOpenedDay(logged);
        _watch.Check();
    }

    /// <summary>
    /// Between commands of a simultaneous day the current player is the host's colour while its turn is open, else the first open turn
    /// (<see cref="GameOptions.HostPlayer"/>).
    /// </summary>
    private void PointAtOpenTurn()
    {
        if (!_state.IsSimultaneous)
        {
            return;
        }

        var anchor = _state.Options.HostPlayer is { } host && _state.OpenTurns.Contains(host) ? host : _state.OpenTurns[0];
        _state.CurrentPlayerIndex = _state.IndexOf(anchor);
    }

    /// <summary>A simultaneous day just opened: where its commands begin, and what is no conflict.</summary>
    private void NoteOpenedDay(int logged)
    {
        if (!_opened)
        {
            return;
        }

        _opened = false;
        DayStart = logged;
        _watch.DayOpened();
    }

    /// <summary>
    /// Whether today is played at once: the option is on, no conflict has ended it, it is the first week (an arena map's preparation day, ADR-199),
    /// and at least two people still play, each with a start zone (MP-2 a).
    /// </summary>
    private bool IsSimultaneousDay()
    {
        if (!_state.Options.SimultaneousFirstWeek || _state.SimultaneousEnded || _state.Date.AbsoluteDay > GameDate.DaysPerWeek)
        {
            return false;
        }

        var people = _state.Players.Where(p => p.IsHuman && !p.IsDefeated).Select(p => p.Id).ToList();
        var zones = _state.Map.StartZoneOwners;
        return people.Count >= 2 && people.All(zones.Contains);
    }
}
