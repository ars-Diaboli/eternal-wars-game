using System.Text.Json.Serialization;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Data.Maps;

/// <summary>
/// On-disk representation of a map (<c>*.ewmap.json</c>). Terrain and roads are stored as one string per
/// row, one character per tile (see <see cref="TerrainCodes"/>, <see cref="RoadCodes"/>), so maps stay
/// readable and diff well in git.
/// </summary>
/// <remarks>
/// Version history: 1 = terrain, players, heroes. 2 = + description, map objects.
/// 3 = + roads, teams, one-way gates. 4 = + hero armies and stats, monsters, mine guards, guard mode,
/// optional chest values (omitted = rolled). 5 = + terrain codes b (beach), i (ice), = (shallow water),
/// c (cursed lands), k (rocky ground) (ADR-045); heroes may name a roster entry (<c>type</c>) and carry skill levels
/// (<c>skills</c>, replacing <c>tactics</c>) (ADR-048). 6 = + towns (<c>town</c> objects: owner, name, buildings, dwellings,
/// waiting creatures, garrison; ADR-046). 7 = + hero morale and luck (ADR-053). 8 = + hero Spell Power and Knowledge (ADR-055). 9 = + hero war machines (ADR-056). 10 = + victory and defeat conditions (ADR-057). 11 = + hero artifacts (worn and backpack) and artifact pickups (ADR-061). 12 = + creature dwellings (ADR-054).
/// 13 = guard mode per monster (<c>mode</c> on monsters) instead of the map-wide <c>guardMode</c>, which v4–v12 maps still
/// load onto every monster (ADR-065). 14 = + map marketplaces (<c>marketplace</c> objects, ADR-067).
/// 15 = + the most heroes a player may have (<c>rules.maxHeroes</c>, omitted = 8; ADR-068). 16 = + resource providers (<c>provider</c> objects:
/// water wheel, warehouses, windmill, mystical garden; ADR-068). 17 = + loot sites (<c>loot</c> objects: campfire, supply crates, traveler's wagon,
/// corpse, lean-to, warrior's tomb; ADR-068). 18 = + buff sites (<c>buff</c>) and training sites (<c>training</c>) and a player's own hero limit
/// (<c>maxHeroes</c> on a player slot; ADR-068). 19 = + signs (<c>sign</c>, with a message), observatories, hill forts, town gates, war machine
/// factories (<c>observatory</c>, <c>hillFort</c>, <c>townGate</c>, <c>factory</c>) and prisons (<c>prison</c>, optional hero and level; ADR-068).
/// 20 = + creature banks (<c>bank</c>; the tier is rolled when the game starts, ADR-068). 21 = a resource pile's <c>amount</c> may be left out,
/// to be rolled when the game starts (ADR-068).
/// Older versions still load.
/// </remarks>
public sealed record MapDocument
{
    public const int CurrentFormatVersion = 21;

    /// <summary>First version with a guard mode per monster; older maps had one map-wide <see cref="GuardMode"/>.</summary>
    public const int PerMonsterGuardModeVersion = 13;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required string Name { get; init; }

    public string Description { get; init; } = "";

    public required int Width { get; init; }

    public required int Height { get; init; }

    /// <summary>One entry per level (surface first); each is <see cref="Height"/> rows of <see cref="Width"/> chars.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Terrain { get; init; }

    /// <summary>Same shape as <see cref="Terrain"/>; omitted when the map has no roads.</summary>
    public IReadOnlyList<IReadOnlyList<string>>? Roads { get; init; }

    public IReadOnlyList<PlayerSlotDocument> Players { get; init; } = [];

    /// <summary>The setup screen may not change the teams in <see cref="Players"/>.</summary>
    public bool TeamsLocked { get; init; }

    /// <summary>
    /// Legacy (v4–v12): how every monster guarded its surroundings; omitted = <see cref="Domain.Objects.GuardMode.Classic"/>.
    /// Read only from older maps, where it becomes each monster's mode; never written (v13 keeps it per monster).
    /// </summary>
    public GuardMode? GuardMode { get; init; }

    /// <summary>Victory and defeat conditions (v10); omitted = the default: defeat every enemy.</summary>
    public ScenarioRulesDocument? Rules { get; init; }

    public IReadOnlyList<HeroPlacementDocument> Heroes { get; init; } = [];

    public IReadOnlyList<MapObjectDocument> Objects { get; init; } = [];
}

/// <summary>
/// How a scenario is won and lost (v10, ADR-057). <see cref="StandardVictory"/> (the last team standing wins) and
/// <see cref="Elimination"/> (no towns and no heroes: out) are on unless set to false; <see cref="GraceDays"/> omitted = 7
/// (the days a player that lost its last town has to own one again; 0 = no countdown). <see cref="MaxHeroes"/> (v15) omitted = 8
/// (the most heroes a player may have at once).
/// </summary>
public sealed record ScenarioRulesDocument(
    IReadOnlyList<ConditionDocument>? Victory = null,
    IReadOnlyList<ConditionDocument>? Defeat = null,
    bool StandardVictory = true,
    bool Elimination = true,
    int? GraceDays = null,
    int? MaxHeroes = null);

/// <summary>One condition; which fields apply depends on <see cref="Kind"/> (see <see cref="ConditionKind"/>).</summary>
public sealed record ConditionDocument(
    ConditionKind Kind,
    int? Player = null,
    int? Town = null,
    int? Hero = null,
    ResourceType? Resource = null,
    int? Amount = null,
    int? Day = null);

/// <param name="Name">Player name.</param>
/// <param name="IsHuman">Human or AI by default.</param>
/// <param name="Team">Default team; null = a team of its own (free-for-all).</param>
/// <param name="MaxHeroes">The player's own hero limit (v18, ADR-068); null = the map's.</param>
public sealed record PlayerSlotDocument(string Name, bool IsHuman, int? Team = null, int? MaxHeroes = null);

/// <summary>
/// A starting hero. Stats and army are v4; older maps place heroes without an army. <see cref="Type"/> (v5) names a hero
/// of the roster, whose class, preferred creature and specialty come with it; <see cref="Skills"/> (v5) are skill levels
/// by skill id, and v4's <see cref="Tactics"/> is read into them. <see cref="Level"/> is the map level (surface 0).
/// <see cref="Morale"/> and <see cref="Luck"/> (v7, −5…+5) are omitted when 0. <see cref="SpellPower"/> and
/// <see cref="Knowledge"/> (v8) are omitted when 0; a roster hero then keeps the roster's values. <see cref="WarMachines"/>
/// (v9) is omitted for the starting set (the offensive machine only). <see cref="Artifacts"/> (worn, each in the slot its
/// definition names) and <see cref="Backpack"/> are artifact ids (v11), omitted when empty.
/// </summary>
public sealed record HeroPlacementDocument(
    int Id,
    string Name,
    int Owner,
    int X,
    int Y,
    int Level = 0,
    int Attack = 0,
    int Defense = 0,
    SkillLevel? Tactics = null,
    IReadOnlyList<ArmySlotDocument>? Army = null,
    string? Type = null,
    IReadOnlyDictionary<string, SkillLevel>? Skills = null,
    int? Morale = null,
    int? Luck = null,
    int? SpellPower = null,
    int? Knowledge = null,
    IReadOnlyList<WarMachineSlot>? WarMachines = null,
    IReadOnlyList<string>? Artifacts = null,
    IReadOnlyList<string>? Backpack = null);

/// <summary>One occupied army slot (0–6).</summary>
public sealed record ArmySlotDocument(int Slot, string Creature, int Count);

/// <summary>A guard stack; <see cref="Count"/> omitted = rolled from the creature tier at game start.</summary>
public sealed record GuardDocument(string Creature, int? Count = null);

/// <summary>
/// A placed object. <see cref="Definition"/> refers to a <c>MapObjectDefinition</c> id; the
/// <c>kind</c> discriminator selects the subtype carrying per-instance state.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ResourcePileDocument), "pile")]
[JsonDerivedType(typeof(MineDocument), "mine")]
[JsonDerivedType(typeof(TreasureChestDocument), "chest")]
[JsonDerivedType(typeof(GateDocument), "gate")]
[JsonDerivedType(typeof(GateEntranceDocument), "gateEntrance")]
[JsonDerivedType(typeof(GateExitDocument), "gateExit")]
[JsonDerivedType(typeof(MonsterDocument), "monster")]
[JsonDerivedType(typeof(TownDocument), "town")]
[JsonDerivedType(typeof(ArtifactPickupDocument), "artifact")]
[JsonDerivedType(typeof(DwellingDocument), "dwelling")]
[JsonDerivedType(typeof(MarketplaceDocument), "marketplace")]
[JsonDerivedType(typeof(ResourceProviderDocument), "provider")]
[JsonDerivedType(typeof(LootSiteDocument), "loot")]
[JsonDerivedType(typeof(BuffSiteDocument), "buff")]
[JsonDerivedType(typeof(TrainingSiteDocument), "training")]
[JsonDerivedType(typeof(SignDocument), "sign")]
[JsonDerivedType(typeof(ObservatoryDocument), "observatory")]
[JsonDerivedType(typeof(HillFortDocument), "hillFort")]
[JsonDerivedType(typeof(TownGateDocument), "townGate")]
[JsonDerivedType(typeof(WarMachineFactoryDocument), "factory")]
[JsonDerivedType(typeof(PrisonDocument), "prison")]
[JsonDerivedType(typeof(CreatureBankDocument), "bank")]
public abstract record MapObjectDocument
{
    public required int Id { get; init; }

    public required string Definition { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public int Level { get; init; }
}

/// <summary>A creature dwelling on the map (v12); <see cref="Available"/> omitted = one week's growth when the game starts.</summary>
public sealed record DwellingDocument : MapObjectDocument
{
    public required string Creature { get; init; }

    public int? Owner { get; init; }

    public int? Available { get; init; }
}

/// <summary>A marketplace on the map (v14); it has no state besides its place.</summary>
public sealed record MarketplaceDocument : MapObjectDocument;

/// <summary>A resource provider (v16, ADR-068); what it gives comes from its definition, the weekly visits are game state.</summary>
public sealed record ResourceProviderDocument : MapObjectDocument;

/// <summary>A loot site (v17, ADR-068); what it gives comes from its definition and is rolled on the first visit.</summary>
public sealed record LootSiteDocument : MapObjectDocument;

/// <summary>A buff site (v18, ADR-068); what it gives comes from its definition.</summary>
public sealed record BuffSiteDocument : MapObjectDocument;

/// <summary>A training site (v18, ADR-068); what it gives comes from its definition.</summary>
public sealed record TrainingSiteDocument : MapObjectDocument;

/// <summary>A sign post (v19, ADR-068) and its message.</summary>
public sealed record SignDocument : MapObjectDocument
{
    public string Message { get; init; } = "";
}

/// <summary>A creature bank (v20, ADR-068); which bank comes from its definition, the tier is rolled in the game.</summary>
public sealed record CreatureBankDocument : MapObjectDocument;

/// <summary>An observatory (v19, ADR-068).</summary>
public sealed record ObservatoryDocument : MapObjectDocument;

/// <summary>A hill fort (v19, ADR-068).</summary>
public sealed record HillFortDocument : MapObjectDocument;

/// <summary>A town gate (v19, ADR-068).</summary>
public sealed record TownGateDocument : MapObjectDocument;

/// <summary>A war machine factory (v19, ADR-068).</summary>
public sealed record WarMachineFactoryDocument : MapObjectDocument;

/// <summary>A prison (v19, ADR-068); <see cref="Hero"/> (a roster id) and <see cref="PrisonerLevel"/> omitted = drawn when the game starts.</summary>
public sealed record PrisonDocument : MapObjectDocument
{
    public string? Hero { get; init; }

    public int? PrisonerLevel { get; init; }
}

/// <summary>An artifact on the map (v11); <see cref="Artifact"/> omitted = rolled from the definition's tier at game start.</summary>
public sealed record ArtifactPickupDocument : MapObjectDocument
{
    public string? Artifact { get; init; }
}

public sealed record ResourcePileDocument : MapObjectDocument
{
    /// <summary>The fixed amount; left out (v21) when it is rolled at game start (ADR-068). Every pile before v21 has one.</summary>
    public int? Amount { get; init; }
}

public sealed record MineDocument : MapObjectDocument
{
    public int? Owner { get; init; }

    public GuardDocument? Guard { get; init; }
}

/// <summary>Gold and experience are both set (fixed by the author) or both omitted (rolled when opened, v4).</summary>
public sealed record TreasureChestDocument : MapObjectDocument
{
    public int? Gold { get; init; }

    public int? Experience { get; init; }
}

/// <summary>A two-way gate (subterranean gate, two-way monolith). Kept as "gate" for v2 compatibility.</summary>
public sealed record GateDocument : MapObjectDocument
{
    public int? LinkedGate { get; init; }
}

public sealed record GateEntranceDocument : MapObjectDocument
{
    public int? Exit { get; init; }
}

public sealed record GateExitDocument : MapObjectDocument;

/// <summary>A neutral monster (v4).</summary>
public sealed record MonsterDocument : MapObjectDocument
{
    public required string Creature { get; init; }

    /// <summary>Omitted = rolled from the creature tier at game start.</summary>
    public int? Count { get; init; }

    public GuardDisposition Disposition { get; init; } = GuardDisposition.Aggressive;

    public bool NeverFlees { get; init; }

    /// <summary>How this stack guards its surroundings (v13, ADR-065); omitted = <see cref="GuardMode.Classic"/>.</summary>
    public GuardMode? Mode { get; init; }
}

/// <summary>
/// A town (v6). <see cref="Buildings"/> omitted = the default start (town hall 1, fort 1, tavern 1); <see cref="Dwellings"/> map
/// creature lines (base creature ids) to 1 built or 2 upgraded; <see cref="Available"/> are creatures waiting per line.
/// </summary>
public sealed record TownDocument : MapObjectDocument
{
    public int? Owner { get; init; }

    /// <summary>Omitted = the definition's name.</summary>
    public string? Name { get; init; }

    public IReadOnlyDictionary<string, int>? Buildings { get; init; }

    public IReadOnlyDictionary<string, int>? Dwellings { get; init; }

    public IReadOnlyDictionary<string, int>? Available { get; init; }

    public IReadOnlyList<ArmySlotDocument>? Garrison { get; init; }

    /// <summary>Absolute day of the town's last build (saved games; one build per day).</summary>
    public int? LastBuilt { get; init; }
}
