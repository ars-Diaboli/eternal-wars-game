using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// The players' commands during a battle: the width choice, deployment, the active unit's actions, warcries and spells, retreat, surrender,
/// quick combat and the auto combat setting and preview. Each checks who may give it, refuses what the rules forbid, and lets the battle go on
/// (<see cref="BattleFlow.Continue"/>).
/// </summary>
internal sealed class BattleCommandHandler(GameState state, IEventPublisher events, Economy.EconomyService economy, BattleFlow flow, BattleAftermath aftermath)
{
    public void ChooseWidth(PlayerId player, int width)
    {
        var battle = flow.Current(BattlePhase.ChoosingWidth);
        if (battle.WidthChooser is not { } chooser || flow.Commander(chooser) != player)
        {
            throw new CommandRefusedException("Only the side with the better Tactics chooses the width.");
        }

        if (!battle.WidthOptions.Contains(width))
        {
            throw new CommandRefusedException($"Width {width} is not one of {string.Join(", ", battle.WidthOptions)}.");
        }

        flow.Deploy(battle, width);
    }

    /// <summary>
    /// Deployment step (ADR-072): the deployer moves one of its stacks within its zone. Machines keep their column; another
    /// stack's cells, obstacles and cells outside the zone are refused.
    /// </summary>
    public void Redeploy(PlayerId player, int unitId, CellPosition to)
    {
        var battle = Deploying(player);
        var unit = battle.FindUnit(unitId) ?? throw new CommandRefusedException($"No unit {unitId}.");
        if (BattleSetup.DeploymentRefusal(battle, unit, to) is { } refusal)
        {
            throw new CommandRefusedException(refusal);
        }

        var from = unit.Position;
        unit.Position = to;
        events.Publish(new BattleUnitDeployed(unit.Id, from, to));
    }

    /// <summary>"Start battle": the deployment step ends and the first round begins.</summary>
    public void EndDeployment(PlayerId player) => flow.StartFighting(Deploying(player));

    private BattleState Deploying(PlayerId player)
    {
        var battle = flow.Current(BattlePhase.Deploying);
        if (flow.Commander(battle.Deployer!.Value) != player)
        {
            throw new CommandRefusedException("Only the side with the higher Tactics deploys.");
        }

        return battle;
    }

    public void Act(PlayerId player, BattleAction action)
    {
        var battle = flow.Current(BattlePhase.Fighting);
        var unit = battle.ActiveUnit ?? throw new CommandRefusedException("No unit is active.");
        if (flow.Commander(unit.Side) != player)
        {
            throw new CommandRefusedException($"{unit.Creature.Name} is not under {player}'s command.");
        }

        var refusal = action switch
        {
            MoveAction move => flow.Engine(battle).Move(move.To),
            AttackAction attack => flow.Engine(battle).Attack(attack.Target, attack.From),
            ShootAction shoot => flow.Engine(battle).Shoot(shoot.Target),
            AttackStructureAction hit => flow.Engine(battle).AttackStructure(hit.Structure, hit.From),
            CreatureCastAction cast => flow.Engine(battle).CreatureCast(cast.Target, cast.Cell),
            ShootStructureAction structure => WarMachines.IsControlled(battle.Participant(unit.Side).Bonus.Ballistics)
                ? flow.Engine(battle).ShootStructure(structure.Structure)
                : $"Without Ballistics the {unit.Creature.Name} fires by itself.",
            WaitAction => flow.Engine(battle).Wait(),
            DefendAction => flow.Engine(battle).Defend(),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
        if (refusal is not null)
        {
            throw new CommandRefusedException(refusal);
        }

        flow.Continue(battle);
    }

    /// <summary>
    /// The player's hero uses a warcry (ADR-067) while one of the player's units is up; it does not use up the unit's action.
    /// </summary>
    public void UseWarcry(PlayerId player, Warcry warcry)
    {
        var battle = flow.Current(BattlePhase.Fighting);
        var unit = battle.ActiveUnit ?? throw new CommandRefusedException("No unit is active.");
        if (flow.Commander(unit.Side) != player)
        {
            throw new CommandRefusedException($"{unit.Creature.Name} is not under {player}'s command.");
        }

        if (battle.Participant(unit.Side).Hero is null)
        {
            throw new CommandRefusedException("Only a hero uses warcries.");
        }

        if (flow.Engine(battle).UseWarcry(unit.Side, warcry) is { } refusal)
        {
            throw new CommandRefusedException(refusal);
        }
    }

    /// <summary>
    /// The player's hero casts a spell (ADR-110) while one of the player's units is up, at a stack or a cell as the spell needs; the unit keeps
    /// its action.
    /// </summary>
    public void Cast(PlayerId player, string spell, int? target, CellPosition? cell, int? second = null)
    {
        var battle = flow.Current(BattlePhase.Fighting);
        var unit = battle.ActiveUnit ?? throw new CommandRefusedException("No unit is active.");
        if (flow.Commander(unit.Side) != player)
        {
            throw new CommandRefusedException($"{unit.Creature.Name} is not under {player}'s command.");
        }

        if (flow.Engine(battle).Cast(unit.Side, spell, target, cell, second) is { } refusal)
        {
            throw new CommandRefusedException(refusal);
        }

        flow.Continue(battle);
    }

    /// <summary>The player's hero leaves the battle; the army is lost and the hero leaves the map for the tavern (ADR-081).</summary>
    public void Retreat(PlayerId player)
    {
        var battle = flow.Current(BattlePhase.Fighting);
        var side = SideOf(battle, player) ?? throw new CommandRefusedException($"{player} does not fight in this battle.");
        if (SurrenderRules.CheckRetreat(battle, side) is { } refusal)
        {
            throw new CommandRefusedException(refusal);
        }

        flow.Engine(battle).Retreat(side);
        flow.Continue(battle);
    }

    /// <summary>
    /// The player's hero surrenders to the other player (ADR-070, CMB-1): it pays half the gold cost of its surviving creatures to that player
    /// and leaves the map with its army for the tavern (ADR-081).
    /// </summary>
    public void Surrender(PlayerId player)
    {
        var battle = flow.Current(BattlePhase.Fighting);
        var side = SideOf(battle, player) ?? throw new CommandRefusedException($"{player} does not fight in this battle.");
        if (SurrenderRules.Check(state, battle, side) is { } refusal)
        {
            throw new CommandRefusedException(refusal);
        }

        var price = SurrenderRules.Price(battle, side);
        var winner = battle.Participant(side.Opponent()).Owner!.Value;
        if (!economy.TrySpend(player, new Economy.ResourceSet(Gold: price)))
        {
            throw new CommandRefusedException($"Surrendering costs {price} gold.");
        }

        economy.Grant(winner, new Economy.ResourceSet(Gold: price));
        events.Publish(new HeroSurrendered(battle.Participant(side).Hero!.Value, player, winner, price));
        flow.Engine(battle).Surrender(side);
        flow.Continue(battle);
    }

    /// <summary>Quick combat: the AI fights both sides to the end (either participant may ask, ADR-038).</summary>
    public void AutoResolve(PlayerId player)
    {
        var battle = state.Battle ?? throw new CommandRefusedException("No battle is being fought.");
        if (SideOf(battle, player) is null)
        {
            throw new CommandRefusedException($"{player} does not fight in this battle.");
        }

        battle.AutoResolve = true;
        if (battle.Phase == BattlePhase.ChoosingWidth)
        {
            flow.Deploy(battle, BattleState.DefaultWidth);
            return;
        }

        if (battle.Phase == BattlePhase.Deploying)
        {
            flow.StartFighting(battle);
            return;
        }

        flow.Continue(battle);
    }

    /// <summary>
    /// Auto combat (ADR-090, CMB-10): the human player whose auto combat setting applies to the current battle, or null. That is a battle between
    /// one human player and a computer player or neutrals, not yet handed to quick combat; two humans fight by hand.
    /// </summary>
    public PlayerId? AutoCombatPlayer()
    {
        if (state.Battle is not { AutoResolve: false } battle || battle.Phase == BattlePhase.Finished || battle.Attacker.IsComputer == battle.Defender.IsComputer)
        {
            return null;
        }

        return battle.Participant(battle.Attacker.IsComputer ? BattleSide.Defender : BattleSide.Attacker).Owner;
    }

    /// <summary>
    /// Auto combat's preview (ADR-090, CMB-10): plays a copy of the current battle to its end as quick combat would, with the same random numbers,
    /// and tells how it ends. The game is not touched, so the player may still fight the battle by hand; quick combat
    /// (<see cref="Commands.AutoResolveBattleCommand"/>) right after this ends the battle exactly so.
    /// </summary>
    public AutoCombatPreview Preview()
    {
        var battle = (state.Battle ?? throw new CommandRefusedException("No battle is being fought.")).Copy();
        var unheard = new EventBus();
        battle.AutoResolve = true;
        switch (battle.Phase)
        {
            case BattlePhase.ChoosingWidth:
                flow.Deploy(battle, BattleState.DefaultWidth, unheard);
                break;
            case BattlePhase.Deploying:
                flow.StartFighting(battle, unheard);
                break;
            default:
                flow.Continue(battle, unheard);
                break;
        }

        var outcome = BattleOutcome.Of(state, battle);
        return new AutoCombatPreview(
            outcome.Winner, outcome.Reason, outcome.Casualties, outcome.Experience, aftermath.PreviewRaised(battle, outcome), outcome.Loot);
    }

    /// <summary>A human player's auto combat setting (ADR-090, CMB-10): a player setting that changes no rule.</summary>
    public void SetAutoCombat(PlayerId player, AutoCombatMode mode)
    {
        var who = state.GetPlayer(player);
        if (!who.IsHuman)
        {
            throw new CommandRefusedException("Only a human player sets auto combat.");
        }

        if (who.AutoCombat == mode)
        {
            return;
        }

        who.AutoCombat = mode;
        events.Publish(new AutoCombatChanged(player, mode));
    }

    private static BattleSide? SideOf(BattleState battle, PlayerId player) =>
        battle.Attacker.Owner == player ? BattleSide.Attacker
        : battle.Defender.Owner == player ? BattleSide.Defender
        : null;
}
