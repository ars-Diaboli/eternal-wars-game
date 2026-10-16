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
    public const string Monster = "monster";

    /// <summary>Definition id of the pickup that rolls an artifact of a tier: <c>artifact.minor</c>.</summary>
    public static string ArtifactId(Artifacts.ArtifactTier tier) => $"artifact.{tier.ToString().ToLowerInvariant()}";

    private static readonly Footprint MineFootprint = new(["BBB", "BVB"]);
    private static readonly Footprint GateFootprint = new(["BBB", "BVB"]);
    private static readonly Footprint MonolithFootprint = new(["B", "V"]);

    /// <summary>7×7 with the corners free, entered through the bottom-middle cell (ADR-046).</summary>
    public static Footprint TownFootprint { get; } = new([".BBBBB.", "BBBBBBB", "BBBBBBB", "BBBBBBB", "BBBBBBB", "BBBBBBB", ".BBVBB."]);

    /// <summary>The playable town types; each has a town definition <see cref="TownId"/>.</summary>
    public static IReadOnlyList<string> PlayableTowns { get; } =
    [
        Towns.TownIds.Haven, Towns.TownIds.Sylvan, Towns.TownIds.Academy, Towns.TownIds.Necropolis,
        Towns.TownIds.Inferno, Towns.TownIds.Dungeon, Towns.TownIds.Stronghold, Towns.TownIds.SwampBestiary,
    ];

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
        new(Monster, "Monster", MapObjectKind.Monster, Footprint.Single),
        .. Enum.GetValues<Artifacts.ArtifactTier>().Select(t =>
            new MapObjectDefinition(ArtifactId(t), $"{t} artifact", MapObjectKind.Artifact, Footprint.Single) { ArtifactTier = t }),

        .. PlayableTowns.Select(t => new MapObjectDefinition(TownId(t), TownName(t), MapObjectKind.Town, TownFootprint) { Town = t }),
    ];

    /// <summary>Definition id of a town type's town: <c>town.haven</c>.</summary>
    public static string TownId(string town) => $"town.{town}";

    private static string TownName(string town) =>
        string.Join(' ', town.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    public static string PileId(ResourceType resource) => $"pile.{resource.ToString().ToLowerInvariant()}";

    public static string MineId(ResourceType resource) => $"mine.{resource.ToString().ToLowerInvariant()}";

    private static MapObjectDefinition Mine(ResourceType resource, string name, int dailyYield) =>
        new(MineId(resource), name, MapObjectKind.Mine, MineFootprint) { Resource = resource, DailyYield = dailyYield };
}
