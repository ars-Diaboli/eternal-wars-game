using System.Text.Json.Serialization;
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
/// waiting creatures, garrison; ADR-046). Older versions still load.
/// </remarks>
public sealed record MapDocument
{
    public const int CurrentFormatVersion = 6;

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

    /// <summary>How monsters guard their surroundings; omitted = <see cref="Domain.Objects.GuardMode.Classic"/>.</summary>
    public GuardMode? GuardMode { get; init; }

    public IReadOnlyList<HeroPlacementDocument> Heroes { get; init; } = [];

    public IReadOnlyList<MapObjectDocument> Objects { get; init; } = [];
}

/// <param name="Name">Player name.</param>
/// <param name="IsHuman">Human or AI by default.</param>
/// <param name="Team">Default team; null = a team of its own (free-for-all).</param>
public sealed record PlayerSlotDocument(string Name, bool IsHuman, int? Team = null);

/// <summary>
/// A starting hero. Stats and army are v4; older maps place heroes without an army. <see cref="Type"/> (v5) names a hero
/// of the roster, whose class, preferred creature and specialty come with it; <see cref="Skills"/> (v5) are skill levels
/// by skill id, and v4's <see cref="Tactics"/> is read into them. <see cref="Level"/> is the map level (surface 0).
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
    IReadOnlyDictionary<string, SkillLevel>? Skills = null);

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
public abstract record MapObjectDocument
{
    public required int Id { get; init; }

    public required string Definition { get; init; }

    public required int X { get; init; }

    public required int Y { get; init; }

    public int Level { get; init; }
}

public sealed record ResourcePileDocument : MapObjectDocument
{
    public required int Amount { get; init; }
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
