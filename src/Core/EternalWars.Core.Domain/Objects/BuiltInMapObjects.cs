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

    /// <summary>The creature dwelling of the adventure map (ADR-054); the creature is per instance.</summary>
    public const string Dwelling = "dwelling";

    /// <summary>The marketplace of the adventure map (ADR-067).</summary>
    public const string Marketplace = "marketplace";

    /// <summary>Resource providers (ADR-068); the warehouses are <see cref="WarehouseId"/>.</summary>
    public const string WaterWheel = "provider.water-wheel";
    public const string Windmill = "provider.windmill";
    public const string MysticalGarden = "provider.mystical-garden";

    /// <summary>Loot sites (ADR-068).</summary>
    public const string Campfire = "loot.campfire";
    public const string SupplyCrates = "loot.supply-crates";
    public const string TravelersWagon = "loot.travelers-wagon";
    public const string Corpse = "loot.corpse";
    public const string LeanTo = "loot.lean-to";
    public const string WarriorsTomb = "loot.warriors-tomb";

    /// <summary>Buff sites (ADR-068).</summary>
    public const string FaerieRing = "buff.faerie-ring";
    public const string FountainOfYouth = "buff.fountain-of-youth";
    public const string Temple = "buff.temple";
    public const string MineralSpring = "buff.mineral-spring";
    public const string Stables = "buff.stables";
    public const string MagicWell = "buff.magic-well";

    /// <summary>Training sites (ADR-068).</summary>
    public const string LearningStone = "training.learning-stone";
    public const string Gazebo = "training.gazebo";
    public const string LibraryOfEnlightenment = "training.library";
    public const string TreeOfKnowledge = "training.tree-of-knowledge";
    public const string Arena = "training.arena";
    public const string ColosseumOfTheMagi = "training.colosseum";

    /// <summary>Definition id of the pickup that rolls an artifact of a tier: <c>artifact.minor</c>.</summary>
    public static string ArtifactId(Artifacts.ArtifactTier tier) => $"artifact.{tier.ToString().ToLowerInvariant()}";

    private static readonly Footprint MineFootprint = new(["BBB", "BVB"]);
    private static readonly Footprint GateFootprint = new(["BBB", "BVB"]);
    private static readonly Footprint MonolithFootprint = new(["B", "V"]);
    private static readonly Footprint ProviderFootprint = new(["BB", "BV"]);

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
        new(Dwelling, "Creature dwelling", MapObjectKind.Dwelling, MineFootprint),
        new(Marketplace, "Marketplace", MapObjectKind.Marketplace, MineFootprint),

        Provider(WaterWheel, "Water wheel", ProviderRule.WaterWheel),
        Provider(Windmill, "Windmill", ProviderRule.Windmill),
        Provider(MysticalGarden, "Mystical garden", ProviderRule.MysticalGarden),
        Warehouse(ResourceType.Lumber, 8),
        Warehouse(ResourceType.Ore, 8),
        Warehouse(ResourceType.Sulfur, 5),
        Warehouse(ResourceType.Mercury, 5),
        Warehouse(ResourceType.Crystal, 5),
        Warehouse(ResourceType.Gold, 2000),

        // Picked up like piles (single tile); the others stay and are visited from their entrance.
        Loot(Campfire, "Campfire", LootRule.Campfire, Footprint.Single),
        Loot(SupplyCrates, "Supply crates", LootRule.SupplyCrates, Footprint.Single),
        Loot(TravelersWagon, "Traveler's wagon", LootRule.TravelersWagon, Footprint.Single),
        Loot(Corpse, "Corpse", LootRule.Corpse, Footprint.Single),
        Loot(LeanTo, "Lean-to", LootRule.LeanTo, new(["BV"])),
        Loot(WarriorsTomb, "Warrior's tomb", LootRule.WarriorsTomb, ProviderFootprint),

        Buff(FaerieRing, "Faerie ring", BuffRule.FaerieRing, VisitScope.DailyPerHero),
        Buff(FountainOfYouth, "Fountain of youth", BuffRule.FountainOfYouth, VisitScope.DailyPerHero),
        Buff(Temple, "Temple", BuffRule.Temple, VisitScope.DailyPerHero),
        Buff(MineralSpring, "Mineral spring", BuffRule.MineralSpring, VisitScope.DailyPerHero),
        Buff(Stables, "Stables", BuffRule.Stables, VisitScope.WeeklyPerHero),
        Buff(MagicWell, "Magic well", BuffRule.MagicWell, VisitScope.DailyPerHero),

        Training(LearningStone, "Learning stone", TrainingRule.LearningStone),
        Training(Gazebo, "Gazebo", TrainingRule.Gazebo),
        Training(LibraryOfEnlightenment, "Library of enlightenment", TrainingRule.LibraryOfEnlightenment),
        Training(TreeOfKnowledge, "Tree of knowledge", TrainingRule.TreeOfKnowledge),
        Training(Arena, "Arena", TrainingRule.Arena),
        Training(ColosseumOfTheMagi, "Colosseum of the magi", TrainingRule.ColosseumOfTheMagi),
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

    /// <summary>Definition id of a resource's warehouse: <c>warehouse.lumber</c>.</summary>
    public static string WarehouseId(ResourceType resource) => $"warehouse.{resource.ToString().ToLowerInvariant()}";

    private static MapObjectDefinition Provider(string id, string name, ProviderRule rule) =>
        new(id, name, MapObjectKind.ResourceProvider, ProviderFootprint) { Provider = rule, VisitScope = VisitScope.Weekly };

    private static MapObjectDefinition Warehouse(ResourceType resource, int amount) =>
        Provider(WarehouseId(resource), $"Warehouse of {resource.ToString().ToLowerInvariant()}", ProviderRule.Warehouse) with { Resource = resource, Amount = amount };

    private static MapObjectDefinition Buff(string id, string name, BuffRule rule, VisitScope scope) =>
        new(id, name, MapObjectKind.Buff, ProviderFootprint) { Buff = rule, VisitScope = scope };

    private static MapObjectDefinition Training(string id, string name, TrainingRule rule) =>
        new(id, name, MapObjectKind.Training, ProviderFootprint) { Training = rule, VisitScope = VisitScope.OncePerHero };

    private static MapObjectDefinition Loot(string id, string name, LootRule rule, Footprint footprint) =>
        new(id, name, MapObjectKind.Loot, footprint) { Loot = rule, VisitScope = VisitScope.FirstVisitor };

    private static MapObjectDefinition Mine(ResourceType resource, string name, int dailyYield) =>
        new(MineId(resource), name, MapObjectKind.Mine, MineFootprint) { Resource = resource, DailyYield = dailyYield };
}
