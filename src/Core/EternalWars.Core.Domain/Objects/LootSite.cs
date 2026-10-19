using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Objects;

/// <summary>What a loot site gives (ADR-068); the definition's <see cref="MapObjectDefinition.Loot"/>.</summary>
public enum LootRule
{
    /// <summary>400–600 gold (in hundreds) and 4–6 of one random resource other than gold; vanishes.</summary>
    Campfire,

    /// <summary>Two different random resources other than gold, 4–6 of each; vanishes.</summary>
    SupplyCrates,

    /// <summary>10 % nothing, 30 % a Minor artifact, 60 % 2–5 of one random resource other than gold; vanishes.</summary>
    TravelersWagon,

    /// <summary>First visitor: 80 % nothing, 20 % a Minor artifact; stays.</summary>
    Corpse,

    /// <summary>First visitor: 1–6 of one random resource other than gold; stays.</summary>
    LeanTo,

    /// <summary>First visitor: an artifact (80 % Minor, 15 % Major, 5 % Relic); every visitor: −3 morale for the next battle; stays.</summary>
    WarriorsTomb,
}

/// <summary>
/// One-time loot on the adventure map (ADR-068): only the first visitor finds something. Campfires, supply crates and wagons are picked up
/// and vanish like resource piles; corpses, lean-tos and warrior's tombs stay (their <see cref="MapObject.Visits"/> say they are spent).
/// </summary>
public sealed class LootSite(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.Loot)
{
    public LootRule Rule => Definition.Loot!.Value;

    /// <summary>Whether the site disappears when visited (and counts as a pickup a Classic guard lets be stolen, ADR-065).</summary>
    public bool Vanishes => LootRules.Vanishes(Rule);
}

/// <summary>What a loot site rolled: resources and an artifact tier to draw from (either may be absent: nothing found).</summary>
public sealed record LootRoll(ResourceSet Resources, ArtifactTier? Artifact = null)
{
    public static LootRoll Nothing { get; } = new(ResourceSet.Empty);
}

/// <summary>The rewards of <see cref="LootSite"/>s (ADR-068); the artifact itself is drawn by <c>ArtifactService.Draw</c>.</summary>
public static class LootRules
{
    /// <summary>The Warrior's Tomb's morale for the next battle, for the first visitor and everyone after.</summary>
    public const int TombMorale = -3;

    public static bool Vanishes(LootRule rule) => rule is LootRule.Campfire or LootRule.SupplyCrates or LootRule.TravelersWagon;

    public static LootRoll Roll(LootRule rule, GameRandom random)
    {
        ArgumentNullException.ThrowIfNull(random);
        var nonGold = ResourceProviderRules.NonGold;
        ResourceSet Some(int min, int max) => ResourceSet.Of(nonGold[random.Next(nonGold.Count)], random.NextInclusive(min, max));

        switch (rule)
        {
            case LootRule.Campfire:
                return new(new ResourceSet(Gold: 100 * random.NextInclusive(4, 6)) + Some(4, 6));
            case LootRule.SupplyCrates:
                var first = random.Next(nonGold.Count);
                var second = (first + 1 + random.Next(nonGold.Count - 1)) % nonGold.Count; // a different one
                return new(ResourceSet.Of(nonGold[first], random.NextInclusive(4, 6)) + ResourceSet.Of(nonGold[second], random.NextInclusive(4, 6)));
            case LootRule.TravelersWagon:
                return WeightedRoll.Pick<int>([(10, 0), (30, 1), (60, 2)], random) switch
                {
                    0 => LootRoll.Nothing,
                    1 => new(ResourceSet.Empty, ArtifactTier.Minor),
                    _ => new(Some(2, 5)),
                };
            case LootRule.Corpse:
                return random.Next(100) < 20 ? new(ResourceSet.Empty, ArtifactTier.Minor) : LootRoll.Nothing;
            case LootRule.LeanTo:
                return new(Some(1, 6));
            case LootRule.WarriorsTomb:
                return new(ResourceSet.Empty, WeightedRoll.Pick<ArtifactTier>([(80, ArtifactTier.Minor), (15, ArtifactTier.Major), (5, ArtifactTier.Relic)], random));
            default:
                throw new ArgumentOutOfRangeException(nameof(rule), rule, null);
        }
    }
}
