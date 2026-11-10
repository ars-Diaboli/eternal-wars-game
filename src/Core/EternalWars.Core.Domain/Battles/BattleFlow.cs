using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// The course of a battle between commands: the chosen width deploys the stacks, deployment ends, the AI plays its units (and a frenzied,
/// confused or self-firing one) until a person has to act, and a finished game battle goes to <see cref="BattleAftermath"/>. The methods that take
/// an events' bus let the auto combat preview play a copy unheard (null: the game's bus).
/// </summary>
internal sealed class BattleFlow(GameState state, IEventPublisher events, BattleAftermath aftermath)
{
    /// <summary>Whether computer players' heroes retreat from lost battles (off in the AI's throwaway estimate fights).</summary>
    public bool AiMayRetreat { get; set; } = true;

    /// <summary>Who may command <paramref name="side"/> of the current battle right now (null: the AI does).</summary>
    public PlayerId? Commander(BattleSide side) =>
        state.Battle is { } battle && !battle.IsComputerControlled(side) ? battle.Participant(side).Owner : null;

    public BattleEngine Engine(BattleState battle, IEventPublisher? bus = null) => new(battle, bus ?? events);

    /// <summary>The current battle in <paramref name="phase"/>; refused otherwise.</summary>
    public BattleState Current(BattlePhase phase) =>
        state.Battle is { } battle && battle.Phase == phase
            ? battle
            : throw new CommandRefusedException(phase switch
            {
                BattlePhase.Fighting => "No battle is being fought.",
                BattlePhase.Deploying => "No stacks to deploy.",
                _ => "No battlefield width to choose.",
            });

    public void StartFighting(BattleState battle, IEventPublisher? bus = null)
    {
        if (battle.Phase == BattlePhase.Deploying)
        {
            (bus ?? events).Publish(new BattleDeploymentEnded(battle.Deployer!.Value));
        }

        Engine(battle, bus).StartFighting();
        Continue(battle, bus);
    }

    public void Deploy(BattleState battle, int width, IEventPublisher? bus = null)
    {
        battle.Width = width;

        // Only the side with the higher Tactics deploys, and only when a person commands it (ADR-072); a tie lets nobody.
        var (chooser, _) = BattleSetup.EffectiveTactics(battle.Attacker.Tactics, battle.Defender.Tactics);
        if (battle.DeploymentAllowed && chooser is { } side && !battle.IsComputerControlled(side))
        {
            battle.Deployer = side;
            battle.DeploymentColumns = BattleSetup.DeploymentColumns(battle.Participant(side).Tactics) + battle.Participant(side).Bonus.TacticsColumns;
        }

        BattleSetup.Deploy(battle, battle.PendingStacks.Attackers, battle.PendingStacks.Defenders);
        (bus ?? events).Publish(new BattleStarted(
            battle.Attacker.Hero,
            battle.Defender.Hero,
            battle.Defender.Guard,
            battle.Width,
            BattleState.Height,
            battle.IsAmbush,
            battle.Units.Select(u => new BattleUnitInfo(u.Id, u.Side, u.Creature.Id, u.Count, u.Position, u.Width, u.Height, u.Machine)).ToList(),
            battle.Obstacles.ToList(),
            battle.Siege is { } siege ? SiegeLayout.Of(siege) : null));
        if (battle.Deployer is { } deployer)
        {
            battle.Phase = BattlePhase.Deploying;
            (bus ?? events).Publish(new BattleDeploymentStarted(deployer, battle.DeploymentColumns));
            return;
        }

        StartFighting(battle, bus);
    }

    /// <summary>
    /// Lets the AI play its units until a human has to act or the battle is over; only the game's own battle is then finished. A siege engine
    /// without Ballistics fires by itself for a person too (ADR-106).
    /// </summary>
    public void Continue(BattleState battle, IEventPublisher? bus = null)
    {
        while (battle.Phase == BattlePhase.Fighting && battle.ActiveUnit is { } unit
               && (battle.IsComputerControlled(unit.Side) || SiegeRules.FiresByItself(battle, unit) || unit.Has(Magic.SpellStatus.Confused | Magic.SpellStatus.Frenzied)))
        {
            // A frenzied stack goes for the nearest stack (ADR-150), a confused one acts at random (ADR-123), whoever commands it.
            if (unit.Has(Magic.SpellStatus.Frenzied))
            {
                BattleAi.ActFrenzied(battle, Engine(battle, bus));
                continue;
            }

            if (unit.Has(Magic.SpellStatus.Confused))
            {
                BattleAi.ActConfused(battle, Engine(battle, bus));
                continue;
            }

            // A computer player's hero gives up a lost battle (ADR-070), but never the arena's (ADR-097); quick combat never retreats for a person.
            if (AiMayRetreat && !aftermath.IsArenaBattle && battle.Participant(unit.Side).IsComputer && SurrenderRules.ShouldRetreat(battle, unit.Side))
            {
                Engine(battle, bus).Retreat(unit.Side);
                break;
            }

            BattleAi.Act(battle, Engine(battle, bus));
        }

        if (battle.Phase == BattlePhase.Finished && ReferenceEquals(battle, state.Battle))
        {
            aftermath.Finish(battle);
        }
    }
}
