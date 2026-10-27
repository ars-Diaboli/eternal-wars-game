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

    /// <summary>The second catalogue's buffs and mana sites (ADR-069, ADR-070).</summary>
    public const string CloverField = "buff.clover-field";
    public const string Rainbow = "buff.rainbow";
    public const string RallyFlag = "buff.rally-flag";
    public const string ManaVortex = "buff.mana-vortex";
    public const string FountainOfMagic = "buff.fountain-of-magic";

    /// <summary>The second catalogue's power-ups (ADR-069).</summary>
    public const string DreamTeacher = "training.dream-teacher";
    public const string TrainingGrounds = "training.training-grounds";
    public const string MarlettoTower = "training.marletto-tower";
    public const string StarAxis = "training.star-axis";
    public const string GardenOfRevelation = "training.garden-of-revelation";

    /// <summary>The Mage's Chest (ADR-070, MAP-15).</summary>
    public const string MagesChest = "loot.mages-chest";

    /// <summary>Training sites (ADR-068).</summary>
    public const string LearningStone = "training.learning-stone";
    public const string Gazebo = "training.gazebo";
    public const string LibraryOfEnlightenment = "training.library";
    public const string TreeOfKnowledge = "training.tree-of-knowledge";
    public const string Arena = "training.arena";
    public const string ColosseumOfTheMagi = "training.colosseum";

    /// <summary>Utility objects and the prison (ADR-068).</summary>
    public const string Sign = "sign";
    public const string Observatory = "observatory";
    public const string HillFort = "hill-fort";
    public const string TownGate = "town-gate";
    public const string WarMachineFactory = "war-machine-factory";
    public const string Prison = "prison";

    /// <summary>The third catalogue (ADR-077, ADR-079).</summary>
    public const string DenOfThieves = "den-of-thieves";
    public const string Sanctuary = "sanctuary";
    public const string SurfaceCartographer = "cartographer.surface";
    public const string UndergroundCartographer = "cartographer.underground";
    public const string ArtifactMerchant = "artifact-merchant";
    public const string RefugeeCamp = "refugee-camp";
    public const string SacrificialAltar = "sacrificial-altar";
    public const string Sphinx = "sphinx";
    public const string WitchHut = "witch-hut";

    /// <summary>The second catalogue's travel and utility objects (ADR-069 … ADR-071); the coloured ones are <see cref="ColouredId"/>.</summary>
    public const string MapTavern = "tavern";
    public const string Garrison = "garrison";
    public const string VerticalGarrison = "garrison.vertical";
    public const string MagiWindow = "magi.window";
    public const string MagiEye = "magi.eye";
    public const string KeymasterTent = "keymaster";
    public const string BorderGuard = "border.guard";
    public const string BorderGate = "border.gate";

    /// <summary>Definition id of a coloured object: <c>magi.eye.red</c>, <c>keymaster.blue</c>, <c>border.gate.black</c>.</summary>
    public static string ColouredId(string kind, ObjectColour colour) => $"{kind}.{colour.ToString().ToLowerInvariant()}";

    /// <summary>Creature banks (ADR-068).</summary>
    public const string Churchyard = "bank.churchyard";
    public const string Mansion = "bank.mansion";
    /// <summary>The undead bank, called Ruins until ADR-070 (older maps' <c>bank.ruins</c> load as it).</summary>
    public const string Crypt = "bank.crypt";

    /// <summary>The second catalogue's banks (ADR-069, ADR-070); the orc bank took the name Ruins.</summary>
    public const string Ruins = "bank.orc-ruins";
    public const string Lair = "bank.lair";
    public const string Keep = "bank.keep";
    public const string Hideout = "bank.hideout";
    public const string ForestGlen = "bank.forest-glen";
    public const string DragonCity = "bank.dragon-city";
    public const string DwarvenTreasury = "bank.dwarven-treasury";
    public const string ImpCache = "bank.imp-cache";
    public const string MedusaStores = "bank.medusa-stores";
    public const string NagaBank = "bank.naga-bank";

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

        AbandonedMine(ResourceType.Gold, "Abandoned gold mine", 1000, 150, 295, underground: true),
        AbandonedMine(ResourceType.Lumber, "Abandoned sawmill", 2, 100, 245, underground: false),
        AbandonedMine(ResourceType.Ore, "Abandoned ore pit", 2, 100, 245, underground: true),
        AbandonedMine(ResourceType.Crystal, "Abandoned crystal cavern", 1, 100, 245, underground: false),
        AbandonedMine(ResourceType.Mercury, "Abandoned alchemist's lab", 1, 100, 245, underground: false),
        AbandonedMine(ResourceType.Sulfur, "Abandoned sulfur dune", 1, 100, 245, underground: false),

        new(TreasureChest, "Treasure chest", MapObjectKind.TreasureChest, Footprint.Single),
        new(SubterraneanGate, "Subterranean gate", MapObjectKind.TwoWayGate, GateFootprint),
        // Called portals since ADR-070 (MAP-18); the ids keep the old monolith names so older maps load.
        new(TwoWayMonolith, "Portal", MapObjectKind.TwoWayGate, MonolithFootprint),
        new(OneWayMonolithEntrance, "One-way portal", MapObjectKind.OneWayGateEntrance, MonolithFootprint),
        new(OneWayMonolithExit, "One-way portal exit", MapObjectKind.OneWayGateExit, MonolithFootprint),
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
        Loot(MagesChest, "Mage's chest", LootRule.MagesChest, Footprint.Single),

        Buff(FaerieRing, "Faerie ring", BuffRule.FaerieRing, VisitScope.DailyPerHero),
        Buff(FountainOfYouth, "Fountain of youth", BuffRule.FountainOfYouth, VisitScope.DailyPerHero),
        Buff(Temple, "Temple", BuffRule.Temple, VisitScope.DailyPerHero),
        Buff(MineralSpring, "Mineral spring", BuffRule.MineralSpring, VisitScope.DailyPerHero),
        Buff(Stables, "Stables", BuffRule.Stables, VisitScope.WeeklyPerHero),
        Buff(MagicWell, "Magic well", BuffRule.MagicWell, VisitScope.DailyPerHero),
        Buff(CloverField, "Clover field", BuffRule.CloverField, VisitScope.DailyPerHero),
        Buff(Rainbow, "Rainbow", BuffRule.Rainbow, VisitScope.DailyPerHero),
        Buff(RallyFlag, "Rally flag", BuffRule.RallyFlag, VisitScope.DailyPerHero),
        Buff(ManaVortex, "Mana vortex", BuffRule.ManaVortex, VisitScope.WeeklyPerHero),
        Buff(FountainOfMagic, "Fountain of magic", BuffRule.FountainOfMagic, VisitScope.WeeklyPerHero),

        Training(LearningStone, "Learning stone", TrainingRule.LearningStone),
        Training(Gazebo, "Gazebo", TrainingRule.Gazebo),
        Training(LibraryOfEnlightenment, "Library of enlightenment", TrainingRule.LibraryOfEnlightenment),
        Training(TreeOfKnowledge, "Tree of knowledge", TrainingRule.TreeOfKnowledge),
        Training(Arena, "Arena", TrainingRule.Arena),
        Training(ColosseumOfTheMagi, "Colosseum of the magi", TrainingRule.ColosseumOfTheMagi),
        Training(DreamTeacher, "Dream teacher", TrainingRule.DreamTeacher),
        Training(TrainingGrounds, "Training grounds", TrainingRule.TrainingGrounds),
        Training(MarlettoTower, "Marletto tower", TrainingRule.MarlettoTower),
        Training(StarAxis, "Star axis", TrainingRule.StarAxis),
        Training(GardenOfRevelation, "Garden of revelation", TrainingRule.GardenOfRevelation),

        new(Sign, "Sign", MapObjectKind.Sign, new(["BV"])),
        new(Observatory, "Observatory", MapObjectKind.Observatory, ProviderFootprint) { VisitScope = VisitScope.OncePerHero },
        new(HillFort, "Hill fort", MapObjectKind.HillFort, MineFootprint),
        new(TownGate, "Town gate", MapObjectKind.TownGate, MonolithFootprint),
        new(WarMachineFactory, "War machine factory", MapObjectKind.WarMachineFactory, MineFootprint),
        new(Prison, "Prison", MapObjectKind.Prison, new(["BV"])),
        new(DenOfThieves, "Den of thieves", MapObjectKind.DenOfThieves, ProviderFootprint),
        new(Sanctuary, "Sanctuary", MapObjectKind.Sanctuary, MineFootprint),
        new(SurfaceCartographer, "Cartographer", MapObjectKind.Cartographer, ProviderFootprint),
        new(UndergroundCartographer, "Cartographer of the deep", MapObjectKind.Cartographer, ProviderFootprint) { RevealsLevel = 1 },
        new(ArtifactMerchant, "Artifact merchant", MapObjectKind.ArtifactMerchant, MineFootprint),
        new(RefugeeCamp, "Refugee camp", MapObjectKind.RefugeeCamp, ProviderFootprint),
        new(SacrificialAltar, "Sacrificial altar", MapObjectKind.SacrificialAltar, ProviderFootprint),
        new(Sphinx, "Sphinx", MapObjectKind.Sphinx, MineFootprint) { VisitScope = VisitScope.OncePerHero },
        new(WitchHut, "Witch hut", MapObjectKind.WitchHut, ProviderFootprint),

        // The second catalogue (ADR-069 … ADR-071).
        .. new[] { ResourceType.Lumber, ResourceType.Ore, ResourceType.Gold, ResourceType.Crystal, ResourceType.Mercury, ResourceType.Sulfur }.Select(Deposit),
        new(MapTavern, "Tavern", MapObjectKind.MapTavern, MineFootprint),
        new(Garrison, "Garrison", MapObjectKind.Garrison, new(["BVB"])),
        new(VerticalGarrison, "Garrison (vertical)", MapObjectKind.Garrison, new(["B", "V", "B"])),
        .. Enum.GetValues<ObjectColour>().SelectMany(c => new MapObjectDefinition[]
        {
            new(ColouredId(MagiWindow, c), $"Window of the magi ({Lower(c)})", MapObjectKind.MagiWindow, ProviderFootprint) { Colour = c },
            new(ColouredId(MagiEye, c), $"Eye of the magi ({Lower(c)})", MapObjectKind.MagiEye, Footprint.Single) { Colour = c },
            new(ColouredId(KeymasterTent, c), $"Keymaster's tent ({Lower(c)})", MapObjectKind.KeymasterTent, ProviderFootprint) { Colour = c },
            new(ColouredId(BorderGuard, c), $"Border guard ({Lower(c)})", MapObjectKind.BorderPost, Footprint.Single) { Colour = c },
            new(ColouredId(BorderGate, c), $"Border gate ({Lower(c)})", MapObjectKind.BorderPost, Footprint.Single) { Colour = c, Stays = true },
        }),

        Bank(Churchyard, "Churchyard", BankRule.Churchyard),
        Bank(Mansion, "Mansion", BankRule.Mansion),
        Bank(Crypt, "Crypt", BankRule.Crypt),
        Bank(Ruins, "Ruins", BankRule.Ruins),
        Bank(Lair, "Lair", BankRule.Lair),
        Bank(Keep, "Keep", BankRule.Keep),
        Bank(Hideout, "Hideout", BankRule.Hideout),
        Bank(ForestGlen, "Forest glen", BankRule.ForestGlen),
        Bank(DragonCity, "Dragon city", BankRule.DragonCity),
        Bank(DwarvenTreasury, "Dwarven treasury", BankRule.DwarvenTreasury),
        Bank(ImpCache, "Imp cache", BankRule.ImpCache),
        Bank(MedusaStores, "Medusa stores", BankRule.MedusaStores),
        Bank(NagaBank, "Naga bank", BankRule.NagaBank),
        .. Enum.GetValues<Artifacts.ArtifactTier>().Select(t =>
            new MapObjectDefinition(ArtifactId(t), $"{t} artifact", MapObjectKind.Artifact, Footprint.Single) { ArtifactTier = t }),

        .. PlayableTowns.Select(t => new MapObjectDefinition(TownId(t), TownName(t), MapObjectKind.Town, TownFootprint) { Town = t }),

        // Scenery comes from data (scenery.csv, ADR-079).
        .. SceneryTable.Parse(SceneryTable.EmbeddedTable()),
    ];

    /// <summary>Definition id of a town type's town: <c>town.haven</c>.</summary>
    public static string TownId(string town) => $"town.{town}";

    private static string TownName(string town) =>
        string.Join(' ', town.Split('-').Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    public static string PileId(ResourceType resource) => $"pile.{resource.ToString().ToLowerInvariant()}";

    public static string MineId(ResourceType resource) => $"mine.{resource.ToString().ToLowerInvariant()}";

    /// <summary>Definition id of a resource's abandoned mine (ADR-068): <c>mine.abandoned.gold</c>.</summary>
    public static string AbandonedMineId(ResourceType resource) => $"mine.abandoned.{resource.ToString().ToLowerInvariant()}";

    /// <summary>The creature of abandoned mines' guards (the notes' Troglodytes).</summary>
    public const string AbandonedMineGuard = "dungeon.troglodyte";

    private static MapObjectDefinition AbandonedMine(ResourceType resource, string name, int dailyYield, int min, int max, bool underground) =>
        Mine(resource, name, dailyYield) with
        {
            Id = AbandonedMineId(resource),
            Becomes = MineId(resource),
            GuardCreature = AbandonedMineGuard,
            GuardMin = min,
            GuardMax = max,
            BattleTerrain = underground ? Map.TerrainType.Subterranean : null,
        };

    private static string Lower(ObjectColour colour) => colour.ToString().ToLowerInvariant();

    private static string DepositName(ResourceType resource) => resource switch
    {
        ResourceType.Lumber => "Wood pile",
        ResourceType.Ore => "Ore deposit",
        ResourceType.Gold => "Gold vein",
        ResourceType.Crystal => "Crystal vein",
        ResourceType.Mercury => "Cinnabar deposit",
        _ => "Sulfur vent",
    };

    /// <summary>A deposit that becomes the resource's mine (ADR-070, MAP-21).</summary>
    private static MapObjectDefinition Deposit(ResourceType resource) =>
        new(DepositRules.Id(resource), DepositName(resource), MapObjectKind.Deposit, MineFootprint)
        {
            Resource = resource, Becomes = MineId(resource), Price = DepositRules.Price(resource),
        };

    /// <summary>Definition id of a resource's warehouse: <c>warehouse.lumber</c>.</summary>
    public static string WarehouseId(ResourceType resource) => $"warehouse.{resource.ToString().ToLowerInvariant()}";

    private static MapObjectDefinition Provider(string id, string name, ProviderRule rule) =>
        new(id, name, MapObjectKind.ResourceProvider, ProviderFootprint) { Provider = rule };

    private static MapObjectDefinition Warehouse(ResourceType resource, int amount) =>
        Provider(WarehouseId(resource), $"Warehouse of {resource.ToString().ToLowerInvariant()}", ProviderRule.Warehouse) with { Resource = resource, Amount = amount };

    private static MapObjectDefinition Bank(string id, string name, BankRule rule) =>
        new(id, name, MapObjectKind.CreatureBank, MineFootprint) { Bank = rule };

    private static MapObjectDefinition Buff(string id, string name, BuffRule rule, VisitScope scope) =>
        new(id, name, MapObjectKind.Buff, ProviderFootprint) { Buff = rule, VisitScope = scope };

    private static MapObjectDefinition Training(string id, string name, TrainingRule rule) =>
        new(id, name, MapObjectKind.Training, ProviderFootprint) { Training = rule, VisitScope = VisitScope.OncePerHero };

    private static MapObjectDefinition Loot(string id, string name, LootRule rule, Footprint footprint) =>
        new(id, name, MapObjectKind.Loot, footprint) { Loot = rule, VisitScope = VisitScope.FirstVisitor };

    private static MapObjectDefinition Mine(ResourceType resource, string name, int dailyYield) =>
        new(MineId(resource), name, MapObjectKind.Mine, MineFootprint) { Resource = resource, DailyYield = dailyYield };
}
