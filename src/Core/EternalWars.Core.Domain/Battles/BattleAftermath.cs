using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// The end of a battle on the adventure map: the arena's result, the heroes' mana, the survivors back into their slots, Necromancy and Raise Dead,
/// experience, the loser's artifacts, a beaten hero leaving the map and what a won or lost battle does to a guard (<see cref="IBattleGuard"/>).
/// </summary>
internal sealed class BattleAftermath(
    GameState state, IEventPublisher events, ArmyService armies, ProgressionService progression, ArtifactService artifacts, Economy.EconomyService economy,
    TownVisitService towns, PandorasBoxService boxes)
{
    /// <summary>Experience for defeating (not merely routing) an enemy hero.</summary>
    public const int HeroDefeatExperience = 500;

    /// <summary>Whether the battle being fought is an arena map's battle (ADR-097).</summary>
    public bool IsArenaBattle => state.Arena is { Result: ArenaResult.Fighting };

    public void Finish(BattleState battle)
    {
        var outcome = BattleOutcome.Of(state, battle);
        var (winner, reason) = (outcome.Winner, outcome.Reason);
        var loser = winner.Opponent();
        var winning = battle.Participant(winner);
        var losing = battle.Participant(loser);

        // The arena's battle gives no experience (BattleOutcome) and decides the game (ADR-097): both armies fallen is a draw, a retreat or surrender loses.
        var arena = IsArenaBattle ? state.Arena : null;
        if (arena is not null)
        {
            var draw = reason == BattleEndReason.Defeated && !battle.OwnedArmyOf(BattleSide.Attacker).Any() && !battle.OwnedArmyOf(BattleSide.Defender).Any();
            state.Arena = draw ? arena with { Result = ArenaResult.Draw } : arena with { Result = ArenaResult.Won, Winner = winning.Owner };
        }

        // The mana the heroes spent is gone (ADR-110); mana lent for a siege (ADR-138) is spent first and does not outlast the battle.
        foreach (var side in new[] { BattleSide.Attacker, BattleSide.Defender })
        {
            if (battle.Participant(side) is { Hero: { } id, Caster: { } caster } && Math.Min(battle.ManaOf(side), caster.Mana - caster.LentMana) is var left
                && left != caster.Mana - caster.LentMana)
            {
                var hero = state.GetHero(id);
                hero.Mana = left;
                events.Publish(new HeroManaSpent(id, caster.Mana - caster.LentMana - hero.Mana, hero.Mana, hero.MaxMana));
            }
        }

        state.Battle = null;
        events.Publish(new BattleEnded(winner, reason, outcome.Casualties, outcome.Experience));

        // "For the next battle" statuses are used up by this one, whatever its outcome (ADR-068).
        progression.ExpireStatuses(new[] { winning.Hero, losing.Hero }.OfType<HeroId>().Select(state.GetHero), StatusExpiry.NextBattle);

        // Winner: survivors back into their slots, then Necromancy, then the experience (its level-ups come after the raised undead).
        if (winning.Hero is { } winnerId)
        {
            var hero = state.GetHero(winnerId);
            var held = HeldTown(winning);
            if (held is null)
            {
                ApplySurvivors(battle, winner, hero);
            }
            else
            {
                // A garrison hero's troops are the town's garrison (ADR-112).
                LoseMachines(battle, winner, hero);
                ApplySurvivors(battle, winner, held);
            }

            RaiseUndead(battle, loser, hero, held, reason == BattleEndReason.Defeated);

            Award(hero, outcome.Experience);
        }
        else if (winning.Guard is { } guardId)
        {
            // A guard keeps what lived.
            if (state.GetObject(guardId) is IBattleGuard guard)
            {
                guard.KeepSurvivors(Outcome(battle, winner));
            }
            else
            {
                events.Publish(new MapObjectChanged(guardId));
            }
        }

        // Loser: a hero leaves the map, a guard is gone.
        if (losing.Hero is { } loserId)
        {
            // A defeated hero's artifacts go to the winning hero (ADR-061); a hero that flees or surrenders takes them away.
            if (reason == BattleEndReason.Defeated && winning.Hero is { } looterId)
            {
                artifacts.Loot(state.GetHero(loserId), state.GetHero(looterId));
            }

            var loserHero = state.GetHero(loserId);
            if (HeldTown(losing) is { } fallen && winning.Hero is { } takerId)
            {
                ApplySurvivors(battle, loser, fallen);
                DefeatGarrisonHero(fallen, loserHero, state.GetHero(takerId));
            }
            else if (reason != BattleEndReason.Defeated)
            {
                // It keeps what lived (a surrender) and loses the machines destroyed in the battle.
                ApplySurvivors(battle, loser, loserHero);
            }

            if (state.Heroes.Contains(loserHero))
            {
                RemoveHero(loserHero, reason);
            }
        }
        else if (losing.Guard is { } guardId)
        {
            var guard = state.GetObject(guardId);
            var taker = winning.Hero is { } heroId ? state.GetHero(heroId) : null;
            (guard as IBattleGuard)?.Defeated(Outcome(battle, loser), taker);
        }

        var decision = state.PendingDecision;
        state.PendingDecision = null;
        if (decision is not null)
        {
            events.Publish(new DecisionResolved(decision));
        }

        progression.OfferNext();
    }

    private BattleGuardOutcome Outcome(BattleState battle, BattleSide side) => new(state, events, economy, progression, artifacts, towns, boxes, battle, side);

    /// <summary>The town a garrison hero side holds (ADR-112), or null for any other side.</summary>
    private Town? HeldTown(BattleParticipant participant) =>
        SurrenderRules.IsGarrisonHero(participant) && state.FindObject(participant.Guard!.Value) is Town town ? town : null;

    /// <summary>Destroyed war machines are lost; the blacksmith sells new ones (ADR-056).</summary>
    private void LoseMachines(BattleState battle, BattleSide side, Hero hero)
    {
        foreach (var machine in battle.Units.Where(u => u.Owner == side && u.Machine is not null && !u.IsAlive))
        {
            var slot = WarMachines.SlotOf(machine.Machine!.Value);
            if (hero.RemoveWarMachine(slot))
            {
                events.Publish(new WarMachineLost(hero.Id, slot));
            }
        }
    }

    private void ApplySurvivors(BattleState battle, BattleSide side, Hero hero)
    {
        LoseMachines(battle, side, hero);
        BattleOutcome.KeepSurvivors(battle, side, hero.Army);
        armies.Publish(hero);
    }

    /// <summary>The town's garrison keeps what lived.</summary>
    private void ApplySurvivors(BattleState battle, BattleSide side, Town town) => BattleGuardOutcome.KeepTroops(battle, side, events, town.Id, town.Garrison);

    /// <summary>
    /// Necromancy (ADR-072, MAG-7): after a victory over a defeated enemy (not one that retreated), the base undead raised from the enemy's
    /// slain creatures join a stack of their own or a free slot; with neither, an army stack of the upgrade takes 3/4 of them; otherwise they
    /// are lost.
    /// </summary>
    private void RaiseUndead(BattleState battle, BattleSide enemy, Hero hero, Town? held, bool defeated)
    {
        if (!Raises(battle, enemy.Opponent(), hero, defeated))
        {
            return;
        }

        // A garrison hero raises them into the town's garrison (ADR-112).
        var joined = JoinUndead(battle, enemy, hero, held?.Garrison ?? hero.Army, state.Random, defeated);
        if (joined.Count == 0)
        {
            return;
        }

        if (held is null)
        {
            armies.Publish(hero);
        }
        else
        {
            events.Publish(new GarrisonChanged(held.Id, held.Garrison.Slots.ToList()));
        }

        events.Publish(new UndeadRaised(hero.Id, joined));
    }

    /// <summary>
    /// The undead the winning hero would raise into the army it fought with (its own, or a held town's garrison), with the survivors in it and a
    /// copy of the game's random numbers: the real end of the battle draws the same (auto combat's preview). Nothing changes.
    /// </summary>
    public List<RaisedUndead> PreviewRaised(BattleState battle, BattleOutcome outcome)
    {
        var defeated = outcome.Reason == BattleEndReason.Defeated;
        var winning = battle.Participant(outcome.Winner);
        if (winning.Hero is not { } id || state.GetHero(id) is not { } hero || !Raises(battle, outcome.Winner, hero, defeated))
        {
            return [];
        }

        var army = new Army((HeldTown(winning)?.Garrison ?? hero.Army).Slots);
        BattleOutcome.KeepSurvivors(battle, outcome.Winner, army);
        return JoinUndead(battle, outcome.Winner.Opponent(), hero, army, new GameRandom(state.Random.State), defeated);
    }

    /// <summary>
    /// Whether the winning hero raises anything: Necromancy after a defeated enemy, Raise Dead's skeletons (ADR-152) after a defeated enemy, and the
    /// skeletons it raised in the battle that still stand, however the battle ended.
    /// </summary>
    private static bool Raises(BattleState battle, BattleSide side, Hero hero, bool defeated) =>
        (defeated && (hero.SkillLevelOf(NecromancyRules.Skill) != SkillLevel.None || battle.Raising.ContainsKey(side)))
        || battle.Units.Any(u => u.IsAlive && u.IsRaised && u.Owner == side);

    /// <summary>
    /// Raises the undead from the enemy's slain creatures into <paramref name="army"/> and returns what joined it: Necromancy's, then Raise Dead's
    /// skeletons (ADR-152): its percent of the health of the enemy creatures slain (those Necromancy counts, not the stacks it raised in the battle)
    /// and the skeletons it raised that still stand.
    /// </summary>
    private List<RaisedUndead> JoinUndead(BattleState battle, BattleSide enemy, Hero hero, Army army, GameRandom random, bool defeated = true)
    {
        // The table's creature: the slain count with their own health, not the health an enemy's artifacts gave them in battle.
        var level = defeated ? hero.SkillLevelOf(NecromancyRules.Skill) : SkillLevel.None;
        var slain = battle.Units.Where(u => u.Owner == enemy && !u.IsMachine && !u.IsSummoned).Select(u => (state.Creatures.Get(u.Creature.Id), u.InitialCount - u.KeptCount));
        var raising = NecromancyRules.Raise(slain, level, NecromancyRules.IsAmplified(state, hero.Owner), state.Creatures, random, NecromancyRules.HeroBonus(hero)).ToList();
        var side = enemy.Opponent();
        if (state.Creatures.TryGet(BattleMagic.RaisedCreature, out var skeleton))
        {
            var skeletons = battle.Units.Where(u => u.Owner == side && u.IsRaised).Sum(u => u.KeptCount);
            if (defeated && battle.Raising.TryGetValue(side, out var raise))
            {
                var health = battle.Units
                    .Where(u => u.Owner == enemy && !u.IsMachine && !u.IsSummoned && !battle.Risen.Contains(u.Id) && NecromancyRules.Counts(state.Creatures.Get(u.Creature.Id)))
                    .Sum(u => (long)(u.InitialCount - u.KeptCount) * state.Creatures.Get(u.Creature.Id).Health);
                skeletons += (int)Math.Min(int.MaxValue, health * raise.Percent / 100 / skeleton.Health);
            }

            if (skeletons > 0)
            {
                var index = raising.FindIndex(r => r.Creature == skeleton.Id);
                if (index < 0)
                {
                    raising.Add(new RaisedUndead(skeleton.Id, skeletons));
                }
                else
                {
                    raising[index] = raising[index] with { Count = raising[index].Count + skeletons };
                }
            }
        }

        var joined = new List<RaisedUndead>();
        foreach (var raised in raising)
        {
            if (army.TryAdd(new CreatureStack(raised.Creature, raised.Count)))
            {
                joined.Add(raised);
            }
            else if (state.Creatures.Get(raised.Creature).UpgradesTo is { } upgrade && army.Stacks.Any(s => s.Stack.Creature == upgrade)
                     && raised.Count * 3 / 4 is > 0 and var count)
            {
                army.TryAdd(new CreatureStack(upgrade, count));
                joined.Add(new RaisedUndead(upgrade, count));
            }
        }

        return joined;
    }

    public void Award(Hero hero, int experience)
    {
        if (experience <= 0)
        {
            return;
        }

        progression.GrantExperience(hero, experience);
    }

    /// <summary>A beaten garrison hero is gone (it never flees, ADR-112) and the town falls to the winner.</summary>
    public void DefeatGarrisonHero(Town town, Hero inside, Hero winner)
    {
        town.GarrisonHero = null;
        RemoveHero(inside, BattleEndReason.Defeated);
        towns.Capture(town, winner.Owner);
    }

    /// <summary>
    /// The losing hero leaves the map. One that retreated or surrendered and comes from the roster waits in its player's taverns as it is
    /// (ADR-081): a retreat costs it its army, a surrender does not. It stops governing.
    /// </summary>
    public void RemoveHero(Hero hero, BattleEndReason reason)
    {
        var governed = StewardshipRules.TownGovernedBy(state, hero);
        state.RemoveHero(hero);
        var left = reason != BattleEndReason.Defeated;
        if (left && hero.Definition is not null)
        {
            if (reason == BattleEndReason.Retreated)
            {
                for (var slot = 0; slot < Army.SlotCount; slot++)
                {
                    hero.Army.Set(slot, null);
                }
            }

            state.Retire(hero);
        }

        if (governed is not null)
        {
            governed.Governor = null;
            events.Publish(new GovernorChanged(governed.Id, hero.Id, null));
        }

        events.Publish(new HeroDefeated(hero.Id, hero.Owner, left));
    }
}
