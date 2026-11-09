using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The arena's preparation day (ADR-097, ADR-160): there is no tomorrow, so the AI spends everything on the army that fights at the start of day 2.
/// Towns recruit what waits, then build, by army gained per gold, the dwellings (with what they need) whose first creatures they can still buy,
/// and upgrades worth their price, then hire with what is left. Heroes skip what pays only later (mines, providers, flags), fight only for small
/// losses, and see no danger (the map keeps the players apart). At the end of the day the strongest hero collects the towns' troops and every
/// other hero brings it its army: that hero is the one <see cref="ArenaRules.Champion"/> sends.
/// </summary>
public sealed partial class AdventureAi
{
    /// <summary>Most moves one hero makes on the preparation day, when movement does not stop it.</summary>
    private const int ArenaMovesPerHero = 64;

    /// <summary>The share of its army power a hero accepts to lose in a fight on the preparation day: the army is what the battle needs.</summary>
    private const double ArenaLoss = 0.15;

    /// <summary>Most buildings one town raises on the preparation day (a safety net; the money runs out first).</summary>
    private const int ArenaBuildsPerTown = 40;

    /// <summary>Whether today is an arena map's preparation day.</summary>
    private bool Preparing => ArenaRules.IsPreparationDay(_state);

    /// <summary>The armies are being gathered into the champion (<see cref="GatherForBattle"/>): no more heroes are hired.</summary>
    private bool _gathering;

    /// <summary>A town on the preparation day: everything for the army, nothing saved.</summary>
    private void PrepareTown(PlayerId player, Town town)
    {
        Recruit(player, town, reserve: 0);
        for (var i = 0; i < ArenaBuildsPerTown && Active(player) && _state.PendingDecision is null && ArenaBuild(player, town); i++)
        {
            Recruit(player, town, reserve: 0);
        }

        TransformStacks(player, town);
        UpgradeStacks(player, town, reserve: 0);
        Recruit(player, town, reserve: 0);
        BuyWarMachines(player, town, reserve: 0);
        if (!_gathering)
        {
            Hire(player, town, reserve: 0); // with what is left, for the visits; not once the armies are being gathered
        }
        TakeWholeGarrison(player, town);
    }

    /// <summary>One building step of a town on the preparation day (<see cref="ArenaPlans"/>); false when nothing is worth building.</summary>
    private bool ArenaBuild(PlayerId player, Town town)
    {
        var best = ArenaPlans(player, town).OrderByDescending(p => p.Ratio).ThenBy(p => p.Steps[^1].Id, StringComparer.Ordinal).FirstOrDefault();
        if (best.Steps is not { Count: > 0 } steps)
        {
            return false;
        }

        var first = steps[0];
        var command = first.Dwelling ? (GameCommand)new BuildDwellingCommand(player, town.Id, first.Id) : new BuildCommand(player, town.Id, first.Id);
        return Execute(command).IsAccepted;
    }

    /// <summary>
    /// What a town could build today for the battle, with the army each plan gains per gold: every dwelling it lacks, with the buildings it needs
    /// first, whose first week of creatures the town can then still buy at least half of; and every dwelling upgrade, for the creatures waiting or
    /// in the town's armies. Income, growth and the like are worth nothing on the last day.
    /// </summary>
    private IEnumerable<(double Ratio, List<ArenaStep> Steps)> ArenaPlans(PlayerId player, Town town)
    {
        var have = _state.GetPlayer(player).Resources;
        foreach (var line in TownRules.Lines(_state, town))
        {
            if (TownRules.NextDwellingLevel(_state, town, line) is not { } next
                || (next.Level == 1 && TownRules.CheckDwellingChoice(_state, town, line.Id, 1) is not null)
                || Steps(town, new ArenaStep(true, line.Id, line.Tier, next)) is not { } steps
                || steps.Any(s => s.Dwelling ? TownRules.CheckDwellingChoice(_state, town, s.Id, s.Level.Level) is not null : TownRules.CheckCapitol(_state, town, s.Level) is not null))
            {
                continue;
            }

            var build = steps.Aggregate(ResourceSet.Empty, (sum, s) => sum + TownRules.BuildCost(_state, town, s.Level));
            if (!have.CanAfford(build))
            {
                continue;
            }

            long gain;
            var spend = build;
            if (next.Level == 1)
            {
                var growth = TownRules.WeeklyGrowth(_state, town, line);
                var each = TownRules.CreatureCost(_state, town, line);
                var count = TownRules.MaxAffordable(have - build, each, growth);
                if (count == 0 || count * 2 < growth)
                {
                    continue; // the dwelling would stand there with nothing bought from it
                }

                gain = (long)count * line.Value;
                spend += new ResourceSet(Gold: each.Gold * count);
            }
            else
            {
                var upgraded = _state.Creatures.Get(line.UpgradesTo!);
                var count = town.AvailableOf(line.Id) + ArmiesOf(town).Sum(a => a.Slots.Stacks.Where(s => s.Stack.Creature == line.Id).Sum(s => s.Stack.Count));
                gain = (long)count * (upgraded.Value - line.Value);
                if (gain <= 0)
                {
                    continue;
                }
            }

            yield return (gain / Math.Max(1, Value(spend)), steps);
        }
    }

    /// <summary>One building step: a building's next level, or a creature line's dwelling (<paramref name="Tier"/> its tier).</summary>
    private sealed record ArenaStep(bool Dwelling, string Id, int Tier, BuildingLevel Level);

    /// <summary>The steps to <paramref name="target"/>, what it needs first (recursively, lowest levels first), or null when it cannot be planned.</summary>
    private List<ArenaStep>? Steps(Town town, ArenaStep target)
    {
        var planned = new Dictionary<string, int>(StringComparer.Ordinal);
        var steps = new List<ArenaStep>();
        return Add(target, 0) ? steps : null;

        int LevelOf(string building) => planned.TryGetValue(building, out var level) ? level : TownRules.Level(_state, town, building);

        bool Add(ArenaStep step, int depth)
        {
            if (depth > 16)
            {
                return false;
            }

            foreach (var requirement in step.Level.Requires)
            {
                while (LevelOf(requirement.Building) < requirement.Level)
                {
                    if (NextStep(town, requirement.Building, LevelOf(requirement.Building) + 1) is not { } needed || !Add(needed, depth + 1))
                    {
                        return false;
                    }
                }
            }

            steps.Add(step);
            planned[step.Dwelling ? BuiltInBuildings.Dwelling(step.Tier) : step.Id] = step.Level.Level;
            return true;
        }
    }

    /// <summary>The step that raises <paramref name="building"/> to <paramref name="level"/>; for a dwelling tier, its most valuable line that may take it.</summary>
    private ArenaStep? NextStep(Town town, string building, int level)
    {
        if (BuiltInBuildings.DwellingTier(building) is { } tier)
        {
            var lines = TownRules.Lines(_state, town).Where(l => l.Tier == tier);
            var line = level == 1
                ? lines.Where(l => TownRules.CheckDwellingChoice(_state, town, l.Id, 1) is null)
                    .OrderByDescending(l => (long)l.Value * TownRules.WeeklyGrowth(_state, town, l)).ThenBy(l => l.Id, StringComparer.Ordinal).FirstOrDefault()
                : lines.Where(l => town.DwellingLevel(l.Id) == level - 1 && l.UpgradesTo is not null).OrderByDescending(l => l.Value).ThenBy(l => l.Id, StringComparer.Ordinal).FirstOrDefault();
            return line is not null && TownRules.NextDwellingLevel(_state, town, line) is { } next && next.Level == level ? new ArenaStep(true, line.Id, tier, next) : null;
        }

        var catalog = TownRules.Buildings(_state, town);
        return catalog.Contains(building) && level <= catalog.MaxLevel(building) ? new ArenaStep(false, building, 0, catalog.Get(building, level)) : null;
    }

    /// <summary>The hero in the town takes every garrison stack (nobody attacks the town before the battle): into the same creature, a free slot, or in place of a weaker stack.</summary>
    private void TakeWholeGarrison(PlayerId player, Town town)
    {
        if (TownRules.VisitingHero(_state, town) is not { } hero)
        {
            return;
        }

        foreach (var (slot, stack) in town.Garrison.Stacks.OrderByDescending(s => Power(s.Stack)).ThenBy(s => s.Slot).ToList())
        {
            if (!Active(player))
            {
                return;
            }

            var to = Enumerable.Range(0, Army.SlotCount).FirstOrDefault(s => hero.Army[s]?.Creature == stack.Creature, -1);
            to = to >= 0 ? to : Enumerable.Range(0, Army.SlotCount).FirstOrDefault(s => hero.Army[s] is null, -1);
            if (to < 0 && hero.Army.Stacks.OrderBy(s => Power(s.Stack)).ThenBy(s => s.Slot).First() is var (weakSlot, weak) && Power(weak) < Power(stack))
            {
                to = weakSlot; // the weaker stack goes to the garrison in its place
            }

            if (to >= 0)
            {
                Execute(new MoveTownStackCommand(player, town.Id, TownArmy.Garrison, slot, TownArmy.VisitingHero, to));
            }
        }
    }

    /// <summary>
    /// The end of the preparation day: the strongest hero goes round the own towns, buying what waits and taking the garrisons, then every other
    /// hero walks to it and hands over its army and war machines (<see cref="Exchange"/>). False when it has to wait for a person.
    /// </summary>
    private bool GatherForBattle(PlayerId player)
    {
        if (_state.HeroesOf(player).OrderByDescending(h => ArenaRules.ArmyValue(_state, h)).ThenBy(h => h.Id.Value).FirstOrDefault() is not { } champion)
        {
            return true;
        }

        _main = champion.Id;
        _gathering = true;
        foreach (var town in _state.ObjectsOf<Town>().Where(t => t.Owner == player).OrderBy(t => t.Id.Value).ToList())
        {
            if (!Active(player) || !_state.Heroes.Contains(champion))
            {
                return true;
            }

            // Every town is visited: what it builds and buys now goes straight into the army (another own hero at the entrance takes it, and brings it).
            if (TownRules.VisitingHero(_state, town) is null && session.Movement.FindPath(champion.Id, town.VisitPosition) is not null)
            {
                Execute(new MoveHeroCommand(player, champion.Id, town.VisitPosition));
                if (!ResolvePending(player))
                {
                    return false;
                }
            }

            if (Active(player) && TownRules.VisitingHero(_state, town) is not null)
            {
                PrepareTown(player, town);
            }
        }

        foreach (var hero in _state.HeroesOf(player).Where(h => h != champion).OrderBy(h => h.Id.Value).ToList())
        {
            if (!Active(player) || !_state.Heroes.Contains(champion))
            {
                return true;
            }

            // A hero keeps one creature (a hero must lead a stack), so one with a single creature and no war machine missing has nothing to bring.
            var bringsSomething = hero.Army.Stacks.Sum(s => s.Stack.Count) > 1 || hero.WarMachines.Except(champion.WarMachines).Any();
            if (_state.Heroes.Contains(hero) && bringsSomething && session.Movement.FindPath(hero.Id, champion.Position) is not null)
            {
                Execute(new MoveHeroCommand(player, hero.Id, champion.Position));
                if (!ResolvePending(player))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
