using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Towns;

/// <summary>What one stack becomes at the Undead Transformer: <paramref name="Count"/> of <paramref name="From"/> into <paramref name="Result"/> of <paramref name="To"/>.</summary>
public sealed record UndeadTransform(string From, int Count, string To, int Result);

/// <summary>
/// The Necropolis's Undead Transformer (ADR-137). A living creature of tier 1–2 becomes the tier-1 undead of its class (ADR-135: A to Skeletons,
/// B to Plague Bearers), of tier 3–4 the tier-2 undead (Ghouls, Wights), one for one; constructs and elementals never. A Necropolis tier 1–2
/// undead swaps to the other class's line of its tier, × 0.75 rounded down (at least 1). An upgraded creature counts as its base; the result is
/// the upgraded undead when the town has that line's upgraded dwelling. A creature without a class (neutrals, Conflux, Dragon Utopia) picks a
/// line; tier 5 is refused. Free, for the garrison's and the visiting hero's stacks, several at a time and any number of times a day.
/// </summary>
public static class UndeadTransformer
{
    public const string Building = "undead-transformer";

    /// <summary>The share of a swapped undead stack that is left.</summary>
    public const int SwapPercent = 75;

    /// <summary>Whether the creature is alive: not undead, not a construct, not an elemental.</summary>
    public static bool IsLiving(CreatureDefinition creature) =>
        !creature.HasTag(CreatureTags.Undead) && !creature.HasTag(CreatureTags.Construct) && !creature.HasTag(CreatureTags.Elemental);

    /// <summary>Whether the creature has to be told which line to become (a living tier 1–4 creature without a class).</summary>
    public static bool NeedsLine(GameState state, string creature)
    {
        var definition = state.Creatures.Get(state.Creatures.BaseOf(creature));
        return IsLiving(definition) && definition.Tier <= 4 && definition.Category == CreatureCategory.None;
    }

    /// <summary>
    /// What <paramref name="count"/> of <paramref name="creature"/> become in <paramref name="town"/>, along <paramref name="line"/> for a creature
    /// without a class (ignored for the others); the refusal when it cannot be transformed.
    /// </summary>
    public static (UndeadTransform? Transform, string? Refusal) Of(GameState state, Town town, string creature, int count, CreatureCategory line = CreatureCategory.None)
    {
        var definition = state.Creatures.Get(state.Creatures.BaseOf(creature));
        int tier;
        CreatureCategory category;
        bool swap;
        if (definition.Town == TownIds.Necropolis && definition.Tier <= 2 && definition.Category != CreatureCategory.None)
        {
            (tier, category, swap) = (definition.Tier, definition.Category == CreatureCategory.A ? CreatureCategory.B : CreatureCategory.A, true);
        }
        else if (!IsLiving(definition))
        {
            return (null, $"{definition.Name} cannot be transformed: only living creatures and the first two undead tiers can.");
        }
        else if (definition.Tier > 4)
        {
            return (null, $"{definition.Name} is too mighty to be transformed.");
        }
        else
        {
            category = definition.Category != CreatureCategory.None ? definition.Category : line;
            if (category == CreatureCategory.None)
            {
                return (null, $"Pick the line {definition.Name} becomes.");
            }

            (tier, swap) = (definition.Tier <= 2 ? 1 : 2, false);
        }

        var target = state.Creatures.BaseCreaturesOf(TownIds.Necropolis).FirstOrDefault(c => c.Tier == tier && c.Category == category);
        if (target is null)
        {
            return (null, $"There is no undead line for {definition.Name}.");
        }

        var to = target.UpgradesTo is { } upgraded && town.DwellingLevel(target.Id) >= 2 ? upgraded : target.Id;
        var result = swap ? Math.Max(1, count * SwapPercent / 100) : count;
        return to == creature ? (null, $"{definition.Name} is already of that line.") : (new UndeadTransform(creature, count, to, result), null);
    }

    /// <summary>Why the town cannot transform now, or null.</summary>
    public static string? CheckTown(Town town) =>
        town.Level(Building) > 0 ? null : $"{town.Name} has no Undead Transformer.";
}
