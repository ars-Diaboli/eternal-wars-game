using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Objects;

/// <summary>What a resource provider gives (ADR-068); the definition's <see cref="MapObjectDefinition.Provider"/>.</summary>
public enum ProviderRule
{
    /// <summary>500 gold in the first week of the game, 1000 gold in every later week.</summary>
    WaterWheel,

    /// <summary>
    /// <see cref="MapObjectDefinition.Amount"/> of <see cref="MapObjectDefinition.Resource"/>; for a resource other than gold the visitor may take
    /// <see cref="ResourceProviderRules.WarehouseGold"/> gold instead.
    /// </summary>
    Warehouse,

    /// <summary>3–6 of one random resource other than gold.</summary>
    Windmill,

    /// <summary>At random: 500 gold, or 5 of one random magic resource (crystal, mercury, sulfur); no choice (MAP-3).</summary>
    MysticalGarden,
}

/// <summary>
/// A visitable building that hands out resources once a week (Water Wheel, Warehouses, Windmill, Mystical Garden; ADR-068). It has no owner
/// and no per-instance data: the definition says what it gives and its <see cref="MapObject.Visits"/> log when it was last taken.
/// </summary>
public sealed class ResourceProvider(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.ResourceProvider)
{
    public ProviderRule Rule => Definition.Provider!.Value;
}

/// <summary>The rewards of <see cref="ResourceProvider"/>s (ADR-068).</summary>
public static class ResourceProviderRules
{
    /// <summary>What a warehouse of a resource other than gold offers instead of the resource.</summary>
    public const int WarehouseGold = 500;

    public const int WaterWheelFirstWeek = 500;

    public const int WaterWheelGold = 1000;

    public const int WindmillMin = 3;

    public const int WindmillMax = 6;

    public const int GardenGold = 500;

    public const int GardenMagic = 5;

    /// <summary>Lumber, ore and the three magic resources.</summary>
    public static IReadOnlyList<ResourceType> NonGold { get; } =
        [ResourceType.Lumber, ResourceType.Ore, ResourceType.Crystal, ResourceType.Mercury, ResourceType.Sulfur];

    /// <summary>Crystal, mercury and sulfur (ADR-067).</summary>
    public static IReadOnlyList<ResourceType> Magic { get; } = [ResourceType.Crystal, ResourceType.Mercury, ResourceType.Sulfur];

    /// <summary>
    /// The reward of a visit on <paramref name="date"/>: one option is simply given, two let the visitor choose (warehouses). Draws from
    /// <paramref name="random"/> only for the Windmill and the Mystical Garden.
    /// </summary>
    public static IReadOnlyList<ResourceSet> Roll(MapObjectDefinition definition, GameDate date, GameRandom random)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(random);
        return definition.Provider switch
        {
            ProviderRule.WaterWheel => [new ResourceSet(Gold: date.AbsoluteWeek == 1 ? WaterWheelFirstWeek : WaterWheelGold)],
            ProviderRule.Warehouse when definition.Resource is ResourceType.Gold => [new ResourceSet(Gold: definition.Amount)],
            ProviderRule.Warehouse => [ResourceSet.Of(definition.Resource!.Value, definition.Amount), new ResourceSet(Gold: WarehouseGold)],
            ProviderRule.Windmill => [ResourceSet.Of(NonGold[random.Next(NonGold.Count)], random.NextInclusive(WindmillMin, WindmillMax))],
            ProviderRule.MysticalGarden => random.Next(2) == 0
                ? [new ResourceSet(Gold: GardenGold)]
                : [ResourceSet.Of(Magic[random.Next(Magic.Count)], GardenMagic)],
            _ => throw new ArgumentException($"'{definition.Id}' is not a resource provider.", nameof(definition)),
        };
    }

    /// <summary>"8 lumber", "500 gold", "2 crystal and 3 ore" for prompts and logs.</summary>
    public static string Describe(ResourceSet resources)
    {
        var parts = Enum.GetValues<ResourceType>().Where(r => resources[r] != 0)
            .Select(r => string.Create(CultureInfo.InvariantCulture, $"{resources[r]} {r.ToString().ToLowerInvariant()}"))
            .ToList();
        return parts.Count == 0 ? "nothing" : string.Join(" and ", parts);
    }
}
