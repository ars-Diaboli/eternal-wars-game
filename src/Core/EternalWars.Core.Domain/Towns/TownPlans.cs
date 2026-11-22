using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Towns;

/// <summary>One line of a recruitment: <paramref name="Count"/> of <paramref name="Creature"/> into the garrison, or into the visiting hero <paramref name="Hero"/>.</summary>
public sealed record RecruitOrder(CreatureId Creature, int Count, HeroId? Hero = null);

/// <summary>What Recruit all would buy (ADR-296), order by order, and what it costs in all; empty when it would buy nothing.</summary>
public sealed record RecruitPlan(IReadOnlyList<RecruitOrder> Orders, ResourceSet Cost)
{
    public bool IsEmpty => Orders.Count == 0;
}

/// <summary>One stack Upgrade all would upgrade: where it stands, what it becomes, how many and the price difference.</summary>
public sealed record UpgradeOrder(TownArmy Army, int Slot, CreatureId From, CreatureId To, int Count, ResourceSet Cost);

/// <summary>What Upgrade all would upgrade (ADR-296) and what it costs in all; empty when it would upgrade nothing.</summary>
public sealed record UpgradePlan(IReadOnlyList<UpgradeOrder> Orders, ResourceSet Cost)
{
    public bool IsEmpty => Orders.Count == 0;
}

/// <summary>
/// The town screen's Recruit all and Upgrade all (TOWN-17 a and b, ADR-296) as plans: read models the screen shows before paying and the
/// commands carry out, so the screen, the AI and the commands agree.
/// </summary>
public static class TownPlans
{
    /// <summary>
    /// Recruit all: line by line, top tier first (the town's line order within a tier), the most the treasury still pays of the best creature the
    /// dwelling offers (the upgraded one in an upgraded dwelling), into the garrison while it has room for that creature, else into the visiting
    /// hero. A line with nothing waiting, nothing affordable or no room is skipped. Nothing for a town without an owner.
    /// </summary>
    public static RecruitPlan Recruit(GameState state, Town town)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(town);
        if (town.Owner is not { } owner)
        {
            return new RecruitPlan([], ResourceSet.Empty);
        }

        var left = state.GetPlayer(owner).Resources;
        var garrison = new Army(town.Garrison.Slots);
        var hero = TownRules.VisitingHero(state, town);
        var heroArmy = hero is null ? null : new Army(hero.Army.Slots);
        var orders = new List<RecruitOrder>();
        var cost = ResourceSet.Empty;
        foreach (var line in TownRules.Lines(state, town).Select((l, i) => (Line: l, Index: i)).OrderByDescending(p => p.Line.Tier).ThenBy(p => p.Index).Select(p => p.Line))
        {
            var level = town.DwellingLevel(line.Id);
            var waiting = town.AvailableOf(line.Id);
            if (level == 0 || waiting == 0)
            {
                continue;
            }

            var creature = level >= 2 && line.UpgradesTo is { } up ? state.Creatures.Get(up) : line;
            var each = TownRules.CreatureCost(state, town, creature);
            var count = TownRules.MaxAffordable(left, each, waiting);
            var into = garrison.CanAccept(creature.Id) ? garrison : heroArmy is not null && heroArmy.CanAccept(creature.Id) ? heroArmy : null;
            if (count == 0 || into is null)
            {
                continue;
            }

            into.TryAdd(new CreatureStack(creature.Id, count));
            orders.Add(new RecruitOrder(creature.Id, count, into == garrison ? null : hero!.Id));
            left -= each * count;
            cost += each * count;
        }

        return new RecruitPlan(orders, cost);
    }

    /// <summary>
    /// Upgrade all: every stack of the garrison and the visiting hero the town can upgrade (<see cref="TownRules.CheckUpgradeLine"/>), best first
    /// (highest tier, then the larger stack, the garrison before the hero, then by slot), each taken while the treasury still pays for the whole
    /// stack; a stack it cannot pay for is skipped and cheaper ones after it still count. Nothing for a town without an owner.
    /// </summary>
    public static UpgradePlan Upgrade(GameState state, Town town)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(town);
        if (town.Owner is not { } owner)
        {
            return new UpgradePlan([], ResourceSet.Empty);
        }

        var armies = new List<(TownArmy Which, Army Army)> { (TownArmy.Garrison, town.Garrison) };
        if (TownRules.VisitingHero(state, town) is { } hero)
        {
            armies.Add((TownArmy.VisitingHero, hero.Army));
        }

        var candidates = armies
            .SelectMany(a => a.Army.Stacks.Select(s => (a.Which, s.Slot, s.Stack)))
            .Select(s => (s.Which, s.Slot, s.Stack, Refusal: TownRules.CheckUpgradeLine(state, town, s.Stack.Creature, out var line), Line: line))
            .Where(s => s.Refusal is null)
            .OrderByDescending(s => s.Line!.Tier).ThenByDescending(s => s.Stack.Count).ThenBy(s => s.Which).ThenBy(s => s.Slot)
            .ToList();

        var left = state.GetPlayer(owner).Resources;
        var orders = new List<UpgradeOrder>();
        var total = ResourceSet.Empty;
        foreach (var (which, slot, stack, _, line) in candidates)
        {
            var cost = TownRules.UpgradeCost(state, town, line!, stack.Count);
            if (!left.CanAfford(cost))
            {
                continue;
            }

            orders.Add(new UpgradeOrder(which, slot, stack.Creature, line!.UpgradesTo!.Value, stack.Count, cost));
            left -= cost;
            total += cost;
        }

        return new UpgradePlan(orders, total);
    }
}
