using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// A town on the arena's preparation day (ADR-097, ADR-160), or in a tournament's last days once no week begins before its battle (ADR-366,
/// <see cref="AiContext.Arming"/>): there is no growth to come, so it recruits what waits, then builds, by army gained per
/// gold, the dwellings (with what they need) whose first creatures it can still buy, and upgrades worth their price, then hires with what is
/// left; the hero in the town takes the whole garrison.
/// </summary>
internal sealed partial class TownManager
{
    /// <summary>Most buildings one town raises on the preparation day (a safety net; the money runs out first).</summary>
    private const int ArenaBuildsPerTown = 40;

    /// <summary>The armies are being gathered into the champion (<see cref="AdventureAi"/> at the day's end): no more heroes are hired.</summary>
    public bool Gathering { get; set; }

    /// <summary>A town on the preparation day or in a tournament's last days (<see cref="AiContext.Arming"/>): everything for the army, nothing saved.</summary>
    public void PrepareTown(PlayerId player, Town town)
    {
        Recruit(player, town, reserve: 0);
        for (var i = 0; i < ArenaBuildsPerTown && _ai.Active(player) && _state.PendingDecision is null && ArenaBuild(player, town); i++)
        {
            Recruit(player, town, reserve: 0);
        }

        TransformStacks(player, town);
        UpgradeStacks(player, town, reserve: 0);
        Recruit(player, town, reserve: 0);
        BuyWarMachines(player, town, reserve: 0);
        if (!Gathering)
        {
            Hire(player, town, reserve: 0); // with what is left, for the visits; not once the armies are being gathered
        }
        TakeWholeGarrison(player, town);
    }

    /// <summary>
    /// One building step of a town on the preparation day (<see cref="ArenaPlans"/>); false when nothing is worth building, or the town has built
    /// today (a tournament's last day builds once, as every day, ADR-366).
    /// </summary>
    private bool ArenaBuild(PlayerId player, Town town)
    {
        if (TownRules.HasBuiltToday(_state, town))
        {
            return false;
        }

        var best = ArenaPlans(player, town).OrderByDescending(p => p.Ratio).ThenBy(p => p.Steps[^1].Id, StringComparer.Ordinal).FirstOrDefault();
        if (best.Steps is not { Count: > 0 } steps)
        {
            return false;
        }

        var first = steps[0];
        var command = first.Dwelling ? (GameCommand)new BuildDwellingCommand(player, town.Id, first.Id) : new BuildCommand(player, town.Id, first.Id);
        return _ai.Execute(command).IsAccepted;
    }

    /// <summary>
    /// What a town could build today for the battle, with the army each plan gains per gold: every dwelling it lacks, with the buildings it needs
    /// first, whose first week of creatures the town can then still buy at least half of (with the income still to come before a tournament's
    /// battle, ADR-366); and every dwelling upgrade, for the creatures waiting or in the town's armies. In a tournament, which builds once a
    /// day, only what is finished by the last day. Income, growth and the like are worth nothing now.
    /// </summary>
    private IEnumerable<(double Ratio, List<ArenaStep> Steps)> ArenaPlans(PlayerId player, Town town)
    {
        var have = _state.GetPlayer(player).Resources;
        var coming = _ai.DaysLeft is null ? ResourceSet.Empty : _session.Economy.DailyIncome(player) * _ai.IncomeDays;
        foreach (var line in TownRules.Lines(_state, town))
        {
            if (TownRules.NextDwellingLevel(_state, town, line) is not { } next
                || (next.Level == 1 && TownRules.CheckDwellingChoice(_state, town, line.Id, 1) is not null)
                || Steps(town, new ArenaStep(true, line.Id.Value, line.Tier, next)) is not { } steps
                || steps.Any(s => s.Dwelling ? TownRules.CheckDwellingChoice(_state, town, s.Id, s.Level.Level) is not null : TownRules.CheckCapitol(_state, town, s.Level) is not null)
                || (_ai.DaysLeft is { } left && steps.Count > left)) // a tournament builds once a day: what cannot be finished before the battle
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
                var count = TownRules.MaxAffordable(have - build + coming, each, growth);
                if (count == 0 || count * 2 < growth)
                {
                    continue; // the dwelling would stand there with nothing bought from it
                }

                gain = (long)count * line.FightValue;
                spend += new ResourceSet(Gold: each.Gold * count);
            }
            else
            {
                var upgraded = _state.Creatures.Get(line.UpgradesTo!.Value);
                var count = town.AvailableOf(line.Id) + ArmiesOf(town).Sum(a => a.Slots.Stacks.Where(s => s.Stack.Creature == line.Id).Sum(s => s.Stack.Count));
                gain = (long)count * (upgraded.FightValue - line.FightValue);
                if (gain <= 0)
                {
                    continue;
                }
            }

            yield return (gain / Math.Max(1, AiContext.Value(spend)), steps);
        }
    }

    /// <summary>One building step: a building's next level, or a creature line's dwelling (<paramref name="Tier"/> its tier).</summary>
    private sealed record ArenaStep(bool Dwelling, string Id, int Tier, BuildingLevel Level);

    /// <summary>The steps to <paramref name="target"/>, what it needs first (recursively, lowest levels first), or null when it cannot be planned.</summary>
    private List<ArenaStep>? Steps(Town town, ArenaStep target)
    {
        var planned = new Dictionary<Common.BuildingId, int>();
        var steps = new List<ArenaStep>();
        return Add(target, 0) ? steps : null;

        int LevelOf(Common.BuildingId building) => planned.TryGetValue(building, out var level) ? level : TownRules.Level(_state, town, building);

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
    private ArenaStep? NextStep(Town town, Common.BuildingId building, int level)
    {
        if (BuiltInBuildings.DwellingTier(building) is { } tier)
        {
            var lines = TownRules.Lines(_state, town).Where(l => l.Tier == tier);
            var line = level == 1
                ? lines.Where(l => TownRules.CheckDwellingChoice(_state, town, l.Id, 1) is null)
                    .OrderByDescending(l => (long)l.FightValue * TownRules.WeeklyGrowth(_state, town, l)).ThenBy(l => l.Id).FirstOrDefault()
                : lines.Where(l => town.DwellingLevel(l.Id) == level - 1 && l.UpgradesTo is not null).OrderByDescending(l => l.FightValue).ThenBy(l => l.Id).FirstOrDefault();
            return line is not null && TownRules.NextDwellingLevel(_state, town, line) is { } next && next.Level == level ? new ArenaStep(true, line.Id.Value, tier, next) : null;
        }

        var catalog = TownRules.Buildings(_state, town);
        return catalog.Contains(building) && level <= catalog.MaxLevel(building) ? new ArenaStep(false, building.Value, 0, catalog.Get(building, level)) : null;
    }

    /// <summary>The hero in the town takes every garrison stack (nobody attacks the town before the battle): into the same creature, a free slot, or in place of a weaker stack.</summary>
    private void TakeWholeGarrison(PlayerId player, Town town)
    {
        if (TownRules.VisitingHero(_state, town) is not { } hero)
        {
            return;
        }

        foreach (var (slot, stack) in town.Garrison.Stacks.OrderByDescending(s => _ai.Power(s.Stack)).ThenBy(s => s.Slot).ToList())
        {
            if (!_ai.Active(player))
            {
                return;
            }

            var to = Enumerable.Range(0, Army.SlotCount).FirstOrDefault(s => hero.Army[s]?.Creature == stack.Creature, -1);
            to = to >= 0 ? to : Enumerable.Range(0, Army.SlotCount).FirstOrDefault(s => hero.Army[s] is null, -1);
            if (to < 0 && hero.Army.Stacks.OrderBy(s => _ai.Power(s.Stack)).ThenBy(s => s.Slot).First() is var (weakSlot, weak) && _ai.Power(weak) < _ai.Power(stack))
            {
                to = weakSlot; // the weaker stack goes to the garrison in its place
            }

            if (to >= 0)
            {
                _ai.Execute(new MoveTownStackCommand(player, town.Id, TownArmy.Garrison, slot, TownArmy.VisitingHero, to));
            }
        }
    }
}
