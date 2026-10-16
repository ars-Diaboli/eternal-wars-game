using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>An action of the active battle unit.</summary>
public abstract record BattleAction;

public sealed record MoveAction(CellPosition To) : BattleAction;

/// <summary>Melee; <paramref name="From"/> is where to stand (null = the closest placement next to the target).</summary>
public sealed record AttackAction(int Target, CellPosition? From = null) : BattleAction;

public sealed record ShootAction(int Target) : BattleAction;

public sealed record WaitAction : BattleAction;

public sealed record DefendAction : BattleAction;

/// <summary>
/// Battles on the adventure map: starts them from encounters, lets the players (or the AI) command their
/// sides, and applies the result to the map (survivors, experience, defeated heroes and guards).
/// While a battle runs, a <see cref="BattleDecision"/> is pending, so the adventure map is frozen.
/// </summary>
public sealed class BattleService(GameState state, IEventPublisher events, ArmyService armies, ProgressionService progression)
{
    /// <summary>Experience for defeating (not merely routing) an enemy hero.</summary>
    public const int HeroDefeatExperience = 500;

    /// <summary>Who may command <paramref name="side"/> of the current battle right now (null: the AI does).</summary>
    public PlayerId? Commander(BattleSide side) =>
        state.Battle is { } battle && !battle.IsComputerControlled(side) ? battle.Participant(side).Owner : null;

    internal void BeginHeroBattle(Hero attacker, Hero defender, bool isAmbush)
    {
        if (defender.Army.IsEmpty)
        {
            // Nobody to fight: the defender is overrun without a battlefield.
            Award(attacker, HeroDefeatExperience);
            RemoveHero(defender, retreated: false);
            return;
        }

        var battle = Create(
            Participant(BattleSide.Attacker, attacker),
            Participant(BattleSide.Defender, defender),
            defender.Position,
            isAmbush);
        Begin(attacker, battle, Stacks(attacker), Stacks(defender));
    }

    internal void BeginGuardBattle(Hero attacker, MapObject guard, string creature, int count)
    {
        var neutral = new BattleParticipant(BattleSide.Defender, null, null, guard.Id, 0, 0, SkillLevel.None, IsComputer: true);
        var battle = Create(Participant(BattleSide.Attacker, attacker), neutral, guard.VisitPosition, isAmbush: false);
        var definition = state.Creatures.Get(creature);
        var stacks = BattleSetup.SplitGuard(count).Select(c => new BattleStackSpec(definition, c, -1)).ToList();
        Begin(attacker, battle, Stacks(attacker), stacks);
    }

    internal void ChooseWidth(PlayerId player, int width)
    {
        var battle = Current(BattlePhase.ChoosingWidth);
        if (battle.WidthChooser is not { } chooser || Commander(chooser) != player)
        {
            throw new InvalidOperationException("Only the side with the better Tactics chooses the width.");
        }

        if (!battle.WidthOptions.Contains(width))
        {
            throw new InvalidOperationException($"Width {width} is not one of {string.Join(", ", battle.WidthOptions)}.");
        }

        Deploy(battle, width);
    }

    internal void Act(PlayerId player, BattleAction action)
    {
        var battle = Current(BattlePhase.Fighting);
        var unit = battle.ActiveUnit ?? throw new InvalidOperationException("No unit is active.");
        if (Commander(unit.Side) != player)
        {
            throw new InvalidOperationException($"{unit.Creature.Name} is not under {player}'s command.");
        }

        var refusal = action switch
        {
            MoveAction move => Engine(battle).Move(move.To),
            AttackAction attack => Engine(battle).Attack(attack.Target, attack.From),
            ShootAction shoot => Engine(battle).Shoot(shoot.Target),
            WaitAction => Engine(battle).Wait(),
            DefendAction => Engine(battle).Defend(),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
        };
        if (refusal is not null)
        {
            throw new InvalidOperationException(refusal);
        }

        Continue(battle);
    }

    /// <summary>The player's hero leaves the battle; the army is lost and the hero leaves the map.</summary>
    internal void Retreat(PlayerId player)
    {
        var battle = Current(BattlePhase.Fighting);
        var side = SideOf(battle, player) ?? throw new InvalidOperationException($"{player} does not fight in this battle.");
        if (battle.Participant(side).Hero is null)
        {
            throw new InvalidOperationException("Only a hero can retreat.");
        }

        Engine(battle).Retreat(side);
        Continue(battle);
    }

    /// <summary>Quick combat: the AI fights both sides to the end (either participant may ask, ADR-038).</summary>
    internal void AutoResolve(PlayerId player)
    {
        var battle = state.Battle ?? throw new InvalidOperationException("No battle is being fought.");
        if (SideOf(battle, player) is null)
        {
            throw new InvalidOperationException($"{player} does not fight in this battle.");
        }

        battle.AutoResolve = true;
        if (battle.Phase == BattlePhase.ChoosingWidth)
        {
            Deploy(battle, BattleState.DefaultWidth);
            return;
        }

        Continue(battle);
    }

    private static BattleSide? SideOf(BattleState battle, PlayerId player) =>
        battle.Attacker.Owner == player ? BattleSide.Attacker
        : battle.Defender.Owner == player ? BattleSide.Defender
        : null;

    private BattleParticipant Participant(BattleSide side, Hero hero) =>
        new(side, hero.Owner, hero.Id, null, hero.Attack, hero.Defense, hero.Tactics, !state.GetPlayer(hero.Owner).IsHuman,
            HeroBattleBonuses.For(hero, state.Creatures));

    private List<BattleStackSpec> Stacks(Hero hero) =>
        hero.Army.Stacks.Select(s => new BattleStackSpec(state.Creatures.Get(s.Stack.Creature), s.Stack.Count, s.Slot)).ToList();

    private BattleState Create(BattleParticipant attacker, BattleParticipant defender, MapPosition location, bool isAmbush) =>
        new(attacker, defender, state.Map.GetTerrain(location), location, isAmbush, state.Random.Fork());

    private void Begin(Hero attacker, BattleState battle, List<BattleStackSpec> attackers, List<BattleStackSpec> defenders)
    {
        if (state.Battle is not null)
        {
            throw new InvalidOperationException("A battle is already being fought.");
        }

        state.Battle = battle;
        battle.PendingStacks = (attackers, defenders);
        var decision = new BattleDecision(attacker.Id);
        state.PendingDecision = decision;

        // The phase is settled before anyone hears of the battle, so views see the width choice (publish after mutating).
        var (chooser, level) = BattleSetup.EffectiveTactics(battle.Attacker.Tactics, battle.Defender.Tactics);
        var choosing = chooser is { } side && !battle.IsComputerControlled(side);
        if (choosing)
        {
            battle.Phase = BattlePhase.ChoosingWidth;
            battle.WidthChooser = chooser;
            battle.WidthOptions = BattleSetup.WidthOptions(level);
        }

        events.Publish(new DecisionRequested(decision));
        if (!choosing)
        {
            // The AI keeps the default width.
            Deploy(battle, BattleState.DefaultWidth);
        }
    }

    private void Deploy(BattleState battle, int width)
    {
        battle.Width = width;
        BattleSetup.Deploy(battle, battle.PendingStacks.Attackers, battle.PendingStacks.Defenders);
        events.Publish(new BattleStarted(
            battle.Attacker.Hero,
            battle.Defender.Hero,
            battle.Defender.Guard,
            battle.Width,
            BattleState.Height,
            battle.IsAmbush,
            battle.Units.Select(u => new BattleUnitInfo(u.Id, u.Side, u.Creature.Id, u.Count, u.Position, u.Size)).ToList(),
            battle.Obstacles.ToList()));
        Engine(battle).StartFighting();
        Continue(battle);
    }

    /// <summary>Lets the AI play its units until a human has to act or the battle is over.</summary>
    private void Continue(BattleState battle)
    {
        while (battle.Phase == BattlePhase.Fighting && battle.ActiveUnit is { } unit && battle.IsComputerControlled(unit.Side))
        {
            BattleAi.Act(battle, Engine(battle));
        }

        if (battle.Phase == BattlePhase.Finished)
        {
            Finish(battle);
        }
    }

    private BattleEngine Engine(BattleState battle) => new(battle, events);

    private BattleState Current(BattlePhase phase) =>
        state.Battle is { } battle && battle.Phase == phase
            ? battle
            : throw new InvalidOperationException(phase == BattlePhase.Fighting ? "No battle is being fought." : "No battlefield width to choose.");

    private void Finish(BattleState battle)
    {
        var winner = battle.Winner!.Value;
        var loser = winner.Opponent();
        var reason = battle.EndReason!.Value;
        var winning = battle.Participant(winner);
        var losing = battle.Participant(loser);

        var killedHealth = battle.Units.Where(u => u.Side == loser).Sum(u => (long)(u.InitialCount - u.Count) * u.Creature.Health);
        var experience = (int)Math.Min(int.MaxValue, killedHealth + (losing.Hero is not null && reason == BattleEndReason.Defeated ? HeroDefeatExperience : 0));
        var casualties = battle.Units
            .GroupBy(u => (u.Side, u.Creature.Id))
            .Select(g => new BattleCasualty(g.Key.Side, g.Key.Id, g.Sum(u => u.InitialCount - u.Count)))
            .Where(c => c.Lost > 0)
            .ToList();

        state.Battle = null;
        events.Publish(new BattleEnded(winner, reason, casualties, winning.Hero is null ? 0 : experience));

        // Winner: survivors back into their slots, then the experience.
        if (winning.Hero is { } winnerId)
        {
            var hero = state.GetHero(winnerId);
            ApplySurvivors(battle, winner, hero);
            Award(hero, experience);
        }
        else if (winning.Guard is { } guardId)
        {
            SetGuardCount(state.GetObject(guardId), battle.LivingUnitsOf(winner).Sum(u => u.Count));
        }

        // Loser: a hero leaves the map, a guard is gone.
        if (losing.Hero is { } loserId)
        {
            RemoveHero(state.GetHero(loserId), reason == BattleEndReason.Retreated);
        }
        else if (losing.Guard is { } guardId)
        {
            DefeatGuard(state.GetObject(guardId), winning.Hero is { } heroId ? state.GetHero(heroId) : null);
        }

        var decision = state.PendingDecision;
        state.PendingDecision = null;
        if (decision is not null)
        {
            events.Publish(new DecisionResolved(decision));
        }

        progression.OfferNext();
    }

    private void ApplySurvivors(BattleState battle, BattleSide side, Hero hero)
    {
        foreach (var (slot, stack) in hero.Army.Stacks.ToList())
        {
            var survivors = battle.Units.Where(u => u.Side == side && u.SourceSlot == slot).Sum(u => u.Count);
            hero.Army.Set(slot, survivors > 0 ? new CreatureStack(stack.Creature, survivors) : null);
        }

        armies.Publish(hero);
    }

    private void Award(Hero hero, int experience)
    {
        if (experience <= 0)
        {
            return;
        }

        progression.GrantExperience(hero, experience);
    }

    private void RemoveHero(Hero hero, bool retreated)
    {
        state.RemoveHero(hero);
        events.Publish(new HeroDefeated(hero.Id, hero.Owner, retreated));
    }

    private void SetGuardCount(MapObject guard, int count)
    {
        switch (guard)
        {
            case MonsterGuard monster:
                monster.Count = count;
                break;
            case Mine { Guard: { } stack } mine:
                mine.Guard = stack with { Count = count };
                break;
        }

        events.Publish(new MapObjectChanged(guard.Id));
    }

    /// <summary>A beaten monster leaves the map; a beaten mine guard leaves the mine to the winner.</summary>
    private void DefeatGuard(MapObject guard, Hero? winner)
    {
        switch (guard)
        {
            case MonsterGuard:
                state.RemoveObject(guard);
                events.Publish(new MapObjectRemoved(guard.Id));
                break;
            case Mine mine:
                mine.Guard = null;
                events.Publish(new MineGuardDefeated(mine.Id));
                if (winner is not null && mine.Owner != winner.Owner)
                {
                    var previous = mine.Owner;
                    mine.Owner = winner.Owner;
                    events.Publish(new MineOwnerChanged(mine.Id, previous, winner.Owner));
                }

                break;
        }
    }
}
