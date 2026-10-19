using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// Neutral guards on the adventure map (monsters and guarded mines): unset counts are rolled at game start,
/// and meeting one leads to a join offer, a flee offer or a battle (ADR-040, deterministic HoMM3 dispositions).
/// </summary>
/// <remarks>
/// R = power of the hero's army ÷ power of the guard (power = Σ count × creature value). Join is offered when
/// the army has room and Compliant: always, Friendly: R ≥ 1, Aggressive: R ≥ 2, Hostile: R ≥ 4, Savage: never.
/// Otherwise the guard tries to flee when R ≥ 3 unless it never flees. Mine guards always fight.
/// </remarks>
public sealed class GuardService(GameState state, IEventPublisher events, BattleService battles)
{
    /// <summary>Flee offered when the hero's army is at least this many times stronger.</summary>
    public const int FleeRatio = 3;

    /// <summary>Count range rolled for an unset guard, by creature tier (1–5).</summary>
    public static (int Min, int Max) CountRange(int tier) => tier switch
    {
        1 => (20, 50),
        2 => (12, 25),
        3 => (8, 16),
        4 => (4, 10),
        _ => (1, 4),
    };

    /// <summary>Join threshold for a disposition, or null if it never joins.</summary>
    public static int? JoinRatio(GuardDisposition disposition) => disposition switch
    {
        GuardDisposition.Compliant => 0,
        GuardDisposition.Friendly => 1,
        GuardDisposition.Aggressive => 2,
        GuardDisposition.Hostile => 4,
        _ => null,
    };

    /// <summary>Σ count × creature value of a hero's army.</summary>
    public long ArmyPower(Hero hero) =>
        hero.Army.Stacks.Sum(s => (long)s.Stack.Count * state.Creatures.Get(s.Stack.Creature).Value);

    /// <summary>Rolls every guard count the map left open: game setup, before <see cref="GameStarted"/>, so no events.</summary>
    internal void RollUnsetCounts()
    {
        foreach (var mapObject in state.Objects)
        {
            switch (mapObject)
            {
                case MonsterGuard { Count: null } monster:
                    monster.Count = Roll(monster.Creature);
                    break;
                case Mine { Guard: null, Definition.GuardCreature: { } creature } abandoned:
                    // An abandoned mine is always guarded (ADR-068) unless the map set a guard of its own.
                    abandoned.Guard = new GuardStack(creature, state.Random.NextInclusive(abandoned.Definition.GuardMin, abandoned.Definition.GuardMax));
                    break;
                case CreatureBank { Guard.Count: 0, PlunderedOn: null } bank:
                    CreatureBankRules.Fill(bank, state.Random);
                    break;
                case CreatureDwelling { Owner: null, Guard: null } dwelling
                    when DwellingGuards.Count(state.Creatures.Get(dwelling.Creature).Tier) is { } guards:
                    dwelling.Guard = new GuardStack(dwelling.Creature, guards);
                    break;
                case Mine { Guard: { Count: null } guard } mine:
                    mine.Guard = guard with { Count = Roll(guard.Creature) };
                    break;
            }
        }

        int Roll(string creature)
        {
            var (min, max) = CountRange(state.Creatures.Get(creature).Tier);
            return state.Random.NextInclusive(min, max);
        }
    }

    /// <summary>A new day: plundered banks whose time has come fill again with a new roll (ADR-068).</summary>
    internal void RefillBanks()
    {
        foreach (var bank in state.ObjectsOf<CreatureBank>().Where(b => CreatureBankRules.RefillsOn(b, state.Date)).OrderBy(b => b.Id.Value).ToList())
        {
            CreatureBankRules.Fill(bank, state.Random);
            events.Publish(new CreatureBankRefilled(bank.Id));
            events.Publish(new MapObjectChanged(bank.Id));
        }
    }

    /// <summary>The hero stepped onto a monster or into its guarded area.</summary>
    internal void Encounter(Hero hero, MonsterGuard monster)
    {
        var count = monster.Count ?? CountRange(state.Creatures.Get(monster.Creature).Tier).Min;
        var heroPower = ArmyPower(hero);
        var guardPower = (long)count * state.Creatures.Get(monster.Creature).Value;

        GuardOffer? offer = null;
        if (JoinRatio(monster.Disposition) is { } ratio && heroPower >= ratio * guardPower && hero.Army.CanAccept(monster.Creature))
        {
            offer = GuardOffer.Join;
        }
        else if (!monster.NeverFlees && heroPower >= FleeRatio * guardPower)
        {
            offer = GuardOffer.Flee;
        }

        if (offer is { } o)
        {
            var decision = new GuardEncounterDecision(hero.Id, monster.Id, o, monster.Creature, count);
            state.PendingDecision = decision;
            events.Publish(new DecisionRequested(decision));
            return;
        }

        battles.BeginGuardBattle(hero, monster, monster.Creature, count);
    }

    /// <summary>A hero visits a mine that still has its guard: always a fight.</summary>
    internal void Encounter(Hero hero, Mine mine) => Fight(hero, mine, mine.Guard!);

    /// <summary>A hero visits a dwelling that still has its guard (ADR-068): always a fight.</summary>
    internal void Encounter(Hero hero, CreatureDwelling dwelling) => Fight(hero, dwelling, dwelling.Guard!);

    private void Fight(Hero hero, MapObject guarded, GuardStack guard) =>
        battles.BeginGuardBattle(hero, guarded, guard.Creature, guard.Count ?? CountRange(state.Creatures.Get(guard.Creature).Tier).Min);

    /// <summary>Accept the offer (join / let them go), or decline it and fight.</summary>
    internal void Respond(bool accept)
    {
        if (state.PendingDecision is not GuardEncounterDecision decision)
        {
            throw new InvalidOperationException("No guard is waiting for an answer.");
        }

        var hero = state.GetHero(decision.Hero);
        var monster = (MonsterGuard)state.GetObject(decision.Guard);
        state.PendingDecision = null;
        events.Publish(new DecisionResolved(decision));

        if (!accept)
        {
            battles.BeginGuardBattle(hero, monster, decision.Creature, decision.Count);
            return;
        }

        state.RemoveObject(monster);
        if (decision.Offer == GuardOffer.Join)
        {
            hero.Army.TryAdd(new Armies.CreatureStack(decision.Creature, decision.Count));
            events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
            events.Publish(new GuardJoined(hero.Id, monster.Id, decision.Creature, decision.Count));
        }
        else
        {
            events.Publish(new GuardFled(hero.Id, monster.Id));
        }

        events.Publish(new MapObjectRemoved(monster.Id));
    }
}
