using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// Content that ships with the game. Extra or overriding definitions can be loaded from JSON
/// (<c>EternalWars.Core.Data.Content.MapObjectDefinitionSerializer</c>).
/// </summary>
public static class BuiltInMapObjects
{
    public const string TreasureChest = "chest";
    public const string SubterraneanGate = "gate.subterranean";
    public const string TwoWayMonolith = "monolith.two-way";
    public const string OneWayMonolithEntrance = "monolith.entrance";
    public const string OneWayMonolithExit = "monolith.exit";

    private static readonly Footprint MineFootprint = new(["BBB", "BVB"]);
    private static readonly Footprint GateFootprint = new(["BBB", "BVB"]);
    private static readonly Footprint MonolithFootprint = new(["B", "V"]);

    public static IReadOnlyList<MapObjectDefinition> All { get; } =
    [
        .. Enum.GetValues<ResourceType>().Select(r =>
            new MapObjectDefinition(PileId(r), $"{r} pile", MapObjectKind.ResourcePile, Footprint.Single) { Resource = r }),

        Mine(ResourceType.Gold, "Gold mine", 1000),
        Mine(ResourceType.Lumber, "Sawmill", 2),
        Mine(ResourceType.Ore, "Ore pit", 2),
        Mine(ResourceType.Crystal, "Crystal cavern", 1),
        Mine(ResourceType.Mercury, "Alchemist's lab", 1),
        Mine(ResourceType.Sulfur, "Sulfur dune", 1),

        new(TreasureChest, "Treasure chest", MapObjectKind.TreasureChest, Footprint.Single),
        new(SubterraneanGate, "Subterranean gate", MapObjectKind.TwoWayGate, GateFootprint),
        new(TwoWayMonolith, "Two-way monolith", MapObjectKind.TwoWayGate, MonolithFootprint),
        new(OneWayMonolithEntrance, "One-way monolith entrance", MapObjectKind.OneWayGateEntrance, MonolithFootprint),
        new(OneWayMonolithExit, "One-way monolith exit", MapObjectKind.OneWayGateExit, MonolithFootprint),
    ];

    public static string PileId(ResourceType resource) => $"pile.{resource.ToString().ToLowerInvariant()}";

    public static string MineId(ResourceType resource) => $"mine.{resource.ToString().ToLowerInvariant()}";

    private static MapObjectDefinition Mine(ResourceType resource, string name, int dailyYield) =>
        new(MineId(resource), name, MapObjectKind.Mine, MineFootprint) { Resource = resource, DailyYield = dailyYield };
}
