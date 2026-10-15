using System.Text.Json.Serialization;

namespace EternalWars.Core.Data.Maps;

/// <summary>
/// On-disk representation of a map (<c>*.ewmap.json</c>). Terrain is stored as one string per row,
/// one character per tile (see <see cref="TerrainCodes"/>), so maps stay readable and diff well in git.
/// </summary>
/// <remarks>Version history: 1 = terrain, players, heroes. 2 = + description, map objects.</remarks>
public sealed record MapDocument
{
    public const int CurrentFormatVersion = 2;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required string Name { get; init; }

    public string Description { get; init; } = "";

    public required int Width { get; init; }

    public required int Height { get; init; }

    /// <summary>One entry per level (surface first); each is <see cref="Height"/> rows of <see cref="Width"/> chars.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Terrain { get; init; }

    public IReadOnlyList<PlayerSlotDocument> Players { get; init; } = [];

    public IReadOnlyList<HeroPlacementDocument> Heroes { get; init; } = [];

    public IReadOnlyList<MapObjectDocument> Objects { get; init; } = [];
}

public sealed record PlayerSlotDocument(string Name, bool IsHuman);

public sealed record HeroPlacementDocument(int Id, string Name, int Owner, int X, int Y, int Level = 0);

/// <summary>
/// A placed object. <see cref="Definition"/> refers to a <c>MapObjectDefinition</c> id; the
/// <c>kind</c> discriminator selects the subtype carrying per-instance state.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ResourcePileDocument), "pile")]
[JsonDerivedType(typeof(MineDocument), "mine")]
[JsonDerivedType(typeof(TreasureChestDocument), "chest")]
[JsonDerivedType(typeof(GateDocument), "gate")]
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
}

public sealed record TreasureChestDocument : MapObjectDocument
{
    public required int Gold { get; init; }

    public required int Experience { get; init; }
}

public sealed record GateDocument : MapObjectDocument
{
    public int? LinkedGate { get; init; }
}
