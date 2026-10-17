using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Core.Generation;

/// <summary>
/// Picks the neutral stack that guards something worth a given army power (<c>count × creature value</c>, the measure of the AI
/// and the dispositions). The tier follows from the power, with <b>overlapping</b> ranges: every tier has the counts that
/// look like a guard for it (<see cref="CountsFor"/>), a creature qualifies when the stack it would need falls inside that range,
/// so 500 power can be a lot of a weak creature or a few of a stronger one and 1000 can be the same third-tier creature in twice
/// the number, or one of the adjacent tiers. One of the qualifying creatures is drawn; when none qualifies (a very small or very large
/// value) the nearest one is taken with its count held to the limit of its range.
/// </summary>
public static class GuardPicker
{
    /// <summary>The counts a guard of a tier comes in (inclusive): many weak creatures, few strong ones.</summary>
    public static (int Min, int Max) CountsFor(int tier) => tier switch
    {
        1 => (6, 60),
        2 => (2, 40),
        3 => (1, 25),
        4 => (1, 15),
        _ => (1, 10),
    };

    /// <summary>The tiers whose creatures could guard <paramref name="value"/>, judged by the average creature of each tier.</summary>
    public static IReadOnlyList<int> EligibleTiers(CreatureCatalog creatures, int value) =>
        Enumerable.Range(CreatureDefinition.MinTier, CreatureDefinition.MaxTier).Where(t => Candidates(creatures, value).Any(c => c.Creature.Tier == t)).ToList();

    /// <summary>A creature and a count for the power; the count is within 5 % of <c>value / creature value</c> unless the range limits it.</summary>
    public static (CreatureDefinition Creature, int Count) Pick(CreatureCatalog creatures, int value, GameRandom random)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(value, 1);
        var candidates = Candidates(creatures, value);
        if (candidates.Count == 0)
        {
            // Nothing fits: the creature whose own range is nearest to the stack the value asks for.
            var nearest = BaseCreatures(creatures)
                .OrderBy(c => Distance(Needed(value, c), CountsFor(c.Tier)) > 0 ? 1 : 0).ThenBy(c => Error(value, c, Needed(value, c))).ThenBy(c => Distance(Needed(value, c), CountsFor(c.Tier)))
                .ThenBy(c => c.Id, StringComparer.Ordinal).First();
            var (min, max) = CountsFor(nearest.Tier);
            return (nearest, Math.Clamp(Needed(value, nearest), min, max));
        }

        var picked = candidates[random.Next(candidates.Count)].Creature;
        var (low, high) = CountsFor(picked.Tier);
        var jitter = 95 + random.Next(11); // percent
        return (picked, Math.Clamp(Math.Max(1, (int)Math.Round((double)value / picked.Value * jitter / 100)), low, high));
    }

    private static List<(CreatureDefinition Creature, int Count)> Candidates(CreatureCatalog creatures, int value) =>
        BaseCreatures(creatures).Select(c => (Creature: c, Count: Needed(value, c)))
            .Where(c => c.Count >= CountsFor(c.Creature.Tier).Min && c.Count <= CountsFor(c.Creature.Tier).Max && Error(value, c.Creature, c.Count) <= MaxError)
            .OrderBy(c => c.Creature.Id, StringComparer.Ordinal).ToList();

    /// <summary>How far (relative) a stack of this many is from the wanted power; a stack of one dragon is no guard for 3000.</summary>
    private static double Error(int value, CreatureDefinition creature, int count) => Math.Abs((double)count * creature.Value - value) / value;

    /// <summary>The largest relative difference between a guard's power and the wanted power before the creature is not considered.</summary>
    private const double MaxError = 0.2;

    /// <summary>The base forms (not upgrades) of the playable towns' creatures and the neutrals, in id order.</summary>
    private static IEnumerable<CreatureDefinition> BaseCreatures(CreatureCatalog creatures)
    {
        var upgrades = creatures.All.Where(c => c.UpgradesTo is not null).Select(c => c.UpgradesTo!).ToHashSet(StringComparer.Ordinal);
        return creatures.All.Where(c => !upgrades.Contains(c.Id) && (c.Town is null || Domain.Objects.BuiltInMapObjects.PlayableTowns.Contains(c.Town)))
            .OrderBy(c => c.Id, StringComparer.Ordinal);
    }

    private static int Needed(int value, CreatureDefinition creature) => Math.Max(1, (int)Math.Round((double)value / creature.Value));

    private static int Distance(int count, (int Min, int Max) range) => count < range.Min ? range.Min - count : count > range.Max ? count - range.Max : 0;
}
