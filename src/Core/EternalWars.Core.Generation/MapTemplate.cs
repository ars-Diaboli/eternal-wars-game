using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Generation;

/// <summary>What a zone is for (docs/game-design.md §4.9).</summary>
public enum ZoneType
{
    /// <summary>A player's starting area; <see cref="TemplateZone.Owner"/> says whose.</summary>
    PlayerStart,

    /// <summary>A start area only the computer can take (the slot is left empty for humans).</summary>
    AiStart,

    /// <summary>Rich ground worth fighting for.</summary>
    Treasure,

    /// <summary>A crossing: little in it, many ways through.</summary>
    Junction,
}

/// <summary>How hard the neutral monsters of a zone are, relative to the creature tiers (the generator turns it into army power, ADR-064 follow-up).</summary>
public enum MonsterStrength
{
    None,
    Weak,
    Average,
    Strong,
}

/// <summary>How two zones are joined.</summary>
public enum ConnectionKind
{
    /// <summary>A passage on the border, guarded by <see cref="TemplateConnection.GuardValue"/> of neutral army power.</summary>
    Normal,

    /// <summary>A wide, unguarded passage.</summary>
    Wide,

    /// <summary>A pair of linked two-way gates, one in each zone (also the way across levels), guarded like a normal passage.</summary>
    Gate,
}

/// <summary>How much rock lies about inside a zone besides the walls on its borders.</summary>
public enum ObstacleDensity
{
    None,
    Sparse,
    Normal,
    Dense,
}

/// <summary>Whether the mines of a zone have a guard on them.</summary>
public enum MineGuarding
{
    /// <summary>HoMM3 style: guarded by the zone's monster strength, except in start zones (and where the strength is none).</summary>
    Auto,

    /// <summary>Always guarded (a zone with no monsters uses weak ones).</summary>
    Always,

    Never,
}

/// <summary>A minimum and a maximum count (both inclusive).</summary>
public sealed record CountRange(int Min = 0, int Max = 0)
{
    public static CountRange None { get; } = new();

    public static CountRange Exactly(int count) => new(count, count);
}

/// <summary>Treasure of a zone: <see cref="Count"/> piles or chests worth between <see cref="MinValue"/> and <see cref="MaxValue"/> gold each.</summary>
public sealed record TreasureBand(int MinValue, int MaxValue, int Count);

/// <summary>
/// One area of the map. Its weight (<see cref="Size"/>) is relative to the other zones: the layout gives every zone a share of
/// the map in proportion.
/// </summary>
public sealed record TemplateZone
{
    public required string Id { get; init; }

    public ZoneType Type { get; init; } = ZoneType.Treasure;

    /// <summary>Player slot (0-based) of a start zone; null for the other types.</summary>
    public int? Owner { get; init; }

    /// <summary>Relative size, at least 1.</summary>
    public int Size { get; init; } = 1;

    /// <summary>Map level: 0 is the surface, 1 the underground (only with <see cref="MapTemplate.Underground"/>).</summary>
    public int Level { get; init; }

    /// <summary>Towns of the zone owner's start (start zones: at least one).</summary>
    public CountRange PlayerTowns { get; init; } = CountRange.None;

    /// <summary>Towns nobody owns at the start.</summary>
    public CountRange NeutralTowns { get; init; } = CountRange.None;

    /// <summary>Town types the zone's neutral towns may be; null or empty = any playable town. A start zone's own town always follows its player.</summary>
    public IReadOnlyList<string>? TownTypes { get; init; }

    /// <summary>At least this many mines of a resource (the generator may add more by richness).</summary>
    public IReadOnlyDictionary<ResourceType, int> Mines { get; init; } = new Dictionary<ResourceType, int>();

    /// <summary>Resource piles lying about besides the mines.</summary>
    public int ExtraPiles { get; init; }

    public IReadOnlyList<TreasureBand> Treasure { get; init; } = [];

    public MonsterStrength Monsters { get; init; } = MonsterStrength.Average;

    /// <summary>Guards on the zone's mines (see <see cref="MineGuarding"/>).</summary>
    public MineGuarding MineGuards { get; init; } = MineGuarding.Auto;

    /// <summary>Rock clumps inside the zone (see <see cref="TerrainPainter"/>).</summary>
    public ObstacleDensity Obstacles { get; init; } = ObstacleDensity.Sparse;

    /// <summary>Terrains the zone may be painted with; null or empty = any passable terrain.</summary>
    public IReadOnlyList<TerrainType>? Terrains { get; init; }
}

/// <summary>A passage between two zones (undirected).</summary>
public sealed record TemplateConnection
{
    public required string From { get; init; }

    public required string To { get; init; }

    public ConnectionKind Kind { get; init; } = ConnectionKind.Normal;

    /// <summary>Neutral army power (creature value × count, as the AI measures it) guarding a normal or gate passage; 0 = unguarded.</summary>
    public int GuardValue { get; init; }

    /// <summary>How many passages the connection makes through the border (or pairs of gates, for a gate), spread along it. At least 1.</summary>
    public int Passages { get; init; } = 1;
}

/// <summary>
/// A random map template (ADR-023, docs/game-design.md §4.9): the zone graph a map is generated from. Pure data; the
/// <see cref="TemplateValidator"/> checks it, the generator (later) turns it into a map for a size, a player count and a seed.
/// </summary>
public sealed record MapTemplate
{
    /// <summary>Version of the <c>*.ewtemplate.json</c> format this model reads and writes; older files keep loading (bump on a format change).</summary>
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required string Name { get; init; }

    public string Description { get; init; } = "";

    /// <summary>The player counts the template supports; the start zones are for <see cref="MaxPlayers"/>.</summary>
    public int MinPlayers { get; init; } = 2;

    public int MaxPlayers { get; init; } = 2;

    /// <summary>At most this many of the players may be human; null = no limit.</summary>
    public int? MaxHumans { get; init; }

    /// <summary>The map sizes (<see cref="MapSizes.Standard"/>) the template is made for.</summary>
    public IReadOnlyList<int> Sizes { get; init; } = MapSizes.Standard;

    /// <summary>Whether the map has an underground level.</summary>
    public bool Underground { get; init; }

    public IReadOnlyList<TemplateZone> Zones { get; init; } = [];

    public IReadOnlyList<TemplateConnection> Connections { get; init; } = [];

    public int Levels => Underground ? 2 : 1;

    public TemplateZone? Zone(string id) => Zones.FirstOrDefault(z => z.Id == id);
}
