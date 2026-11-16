using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Turns;

/// <summary>
/// The turn timer (ADR-091, FLOW-3). A human player's turn lasts <see cref="GameOptions.TurnTimerMinutes"/>, the same every turn; a loaded game
/// starts the current turn with the full time. The clock is the front-end's: it calls <see cref="Tick"/> every frame with the time passed, and
/// warnings come at <see cref="Warnings"/> seconds left. The clock stops during a battle between two human players. When the time is up the
/// timer answers what is pending with its default (a chest gives gold; leave, stay, let go, close where a screen offers it) and then ends the
/// turn, all through ordinary commands, so a replay needs no clock. A level-up and a sphinx's riddle have no default: the turn ends once the
/// player has answered. A battle is finished first.
/// <para>
/// A simultaneous day (MP-2, ADR-198) has one clock for the day: it runs while any person's turn is open, and when it is up every open turn
/// ends. In a LAN game only the host runs a timer (<paramref name="execute"/> runs its commands for any colour); the clients show its
/// <see cref="Clock"/>.
/// </para>
/// </summary>
public sealed class TurnTimer(GameSession session, Func<GameCommand, CommandResult>? execute = null)
{
    public const int MaxMinutes = 30;

    /// <summary>Seconds left at which the player is warned.</summary>
    public static IReadOnlyList<int> Warnings { get; } = [60, 30, 15, 5];

    private readonly Func<GameCommand, CommandResult> _execute = execute ?? session.Execute;

    /// <summary>The timed turn: its player, or null for a simultaneous day's people together, and its date.</summary>
    private (PlayerId? Player, GameDate Date)? _turn;
    private double _left;

    /// <summary>The seconds left in the current turn; null when the turn is not timed (the timer is off, a computer plays, the game is over).</summary>
    public double? SecondsLeft => Timed ? Math.Max(0, _left) : null;

    /// <summary>The clock stands still: a battle between two human players is being fought.</summary>
    public bool IsStopped => session.State.Battle is { } battle && !battle.Attacker.IsComputer && !battle.Defender.IsComputer;

    /// <summary>The time is up and the turn waits to be ended (an answer the player still has to give, a battle, the front-end).</summary>
    public bool IsUp => Timed && _left <= 0;

    /// <summary>What the clock shows, in whole seconds (rounded up), as the LAN host sends it to the clients.</summary>
    public TurnClock Clock => new(SecondsLeft is { } left ? (int)Math.Ceiling(left) : null, IsStopped, IsUp);

    /// <summary>The players whose time is running: the current player, or every open turn of a simultaneous day; none when nothing is timed.</summary>
    public IEnumerable<PlayerId> TimedPlayers => Timed ? session.State.PlayersToAct : [];

    private bool Timed
    {
        get
        {
            var state = session.State;
            return state.Options.TurnTimerMinutes is not null && state.IsStarted && !state.IsOver
                && (state.IsSimultaneous ? state.OpenTurns.Any(p => state.GetPlayer(p).IsHuman) : state.CurrentPlayer.IsHuman);
        }
    }

    /// <summary>
    /// Runs the clock for <paramref name="seconds"/>. When the time is up and <paramref name="canEnd"/> (the front-end shows nothing that must
    /// finish first, such as a battle's last moves), answers what is pending and ends the turn. Returns the warning (seconds left) the time has
    /// just passed, if any.
    /// </summary>
    public int? Tick(double seconds, bool canEnd = true)
    {
        if (!Timed)
        {
            _turn = null;
            return null;
        }

        var state = session.State;
        (PlayerId? Player, GameDate Date) turn = (state.IsSimultaneous ? null : state.CurrentPlayer.Id, state.Date);
        if (_turn != turn)
        {
            _turn = turn;
            _left = state.Options.TurnTimerMinutes!.Value * 60.0;
        }

        int? warning = null;
        if (!IsStopped && _left > 0)
        {
            var before = _left;
            _left -= seconds;
            warning = WarningPassed(before, _left);
        }

        if (_left <= 0 && canEnd)
        {
            if (state.IsSimultaneous)
            {
                EndDay(state.Date);
            }
            else
            {
                EndTurn(state.CurrentPlayer.Id);
            }
        }

        return warning;
    }

    /// <summary>The warning (seconds left) passed when the time left went from <paramref name="before"/> to <paramref name="after"/>: the last one.</summary>
    public static int? WarningPassed(double before, double after)
    {
        int? warning = null;
        foreach (var mark in Warnings)
        {
            if (before > mark && after <= mark)
            {
                warning = mark; // the last one passed (the least time left)
            }
        }

        return warning;
    }

    /// <summary>Answers each pending decision that has a default, then ends the turn if nothing is left to answer.</summary>
    private void EndTurn(PlayerId player)
    {
        for (var i = 0; i < 32; i++)
        {
            if (session.State.PendingDecision is null)
            {
                _execute(new EndTurnCommand(player));
                return;
            }

            if (Default(player, session.State.PendingDecision) is not { } answer || !_execute(answer).IsAccepted)
            {
                return; // the player answers (a level-up, a riddle, a battle); the turn ends after that
            }
        }
    }

    /// <summary>
    /// The day's time is up (a simultaneous day): the question being asked gets its default, then each open turn ends, until the day is over. A
    /// question without a default or a battle holds every turn until it is answered or fought.
    /// </summary>
    private void EndDay(GameDate date)
    {
        var state = session.State;
        for (var i = 0; i < 64 && state.IsSimultaneous && state.Date == date; i++)
        {
            var command = state.PendingDecision is { } decision
                ? Default(state.DecisionPlayer ?? state.CurrentPlayer.Id, decision)
                : new EndTurnCommand(state.OpenTurns[0]);
            if (command is null || !_execute(command).IsAccepted)
            {
                return;
            }
        }
    }

    /// <summary>The answer a decision's screen offers as its default, or null when the player has to choose.</summary>
    public static GameCommand? Default(PlayerId player, PendingDecision decision) => decision switch
    {
        TreasureChestDecision => new ResolveTreasureChestCommand(player, TreasureChestReward.Gold),
        TrainingDecision { CanDecline: true } => new ChooseTrainingCommand(player, null),
        CartographerDecision => new AnswerCartographerCommand(player, Buy: false),
        DepositDecision => new AnswerDepositCommand(player, Buy: false),
        WitchHutDecision => new ChooseWitchHutSkillCommand(player, null),
        TownGateDecision => new ChooseTownGateCommand(player, null),
        HeroExchangeDecision => new CompleteExchangeCommand(player),
        GuardEncounterDecision => new RespondToGuardCommand(player, true), // take the offer: join, or let them go
        _ => null, // a level-up, a riddle, a battle
    };
}

/// <summary>
/// The turn timer's clock as shown (ADR-091, ADR-198): whole seconds left (null: no timed turn), whether it stands still for a battle between two
/// people, and whether the time is up.
/// </summary>
public sealed record TurnClock(int? SecondsLeft, bool Stopped, bool Up);
