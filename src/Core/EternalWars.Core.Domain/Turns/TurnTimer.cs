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
/// </summary>
public sealed class TurnTimer(GameSession session)
{
    public const int MaxMinutes = 30;

    /// <summary>Seconds left at which the player is warned.</summary>
    public static IReadOnlyList<int> Warnings { get; } = [60, 30, 15, 5];

    private (PlayerId Player, GameDate Date)? _turn;
    private double _left;

    /// <summary>The seconds left in the current turn; null when the turn is not timed (the timer is off, a computer plays, the game is over).</summary>
    public double? SecondsLeft => Timed ? Math.Max(0, _left) : null;

    /// <summary>The clock stands still: a battle between two human players is being fought.</summary>
    public bool IsStopped => session.State.Battle is { } battle && !battle.Attacker.IsComputer && !battle.Defender.IsComputer;

    /// <summary>The time is up and the turn waits to be ended (an answer the player still has to give, a battle, the front-end).</summary>
    public bool IsUp => Timed && _left <= 0;

    private bool Timed =>
        session.State.Options.TurnTimerMinutes is not null && session.State.IsStarted && !session.State.IsOver && session.State.CurrentPlayer.IsHuman;

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
        var turn = (state.CurrentPlayer.Id, state.Date);
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
            foreach (var mark in Warnings)
            {
                if (before > mark && _left <= mark)
                {
                    warning = mark; // the last one passed (the least time left)
                }
            }
        }

        if (_left <= 0 && canEnd)
        {
            EndTurn(state.CurrentPlayer.Id);
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
                session.Execute(new EndTurnCommand(player));
                return;
            }

            if (Default(player, session.State.PendingDecision) is not { } answer || !session.Execute(answer).IsAccepted)
            {
                return; // the player answers (a level-up, a riddle, a battle); the turn ends after that
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
