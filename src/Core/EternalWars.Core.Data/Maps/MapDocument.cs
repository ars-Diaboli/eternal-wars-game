namespace EternalWars.Core.Data.Maps;

/// <summary>
/// On-disk representation of a map (<c>*.ewmap.json</c>). Terrain is stored as one string per row,
/// one character per tile (see <see cref="TerrainCodes"/>), so maps stay readable and diff well in git.
/// </summary>
public sealed record MapDocument
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; init; } = CurrentFormatVersion;

    public required string Name { get; init; }

    public string Description { get; init; } = "";

    public required int Width { get; init; }

    public required int Height { get; init; }

    /// <summary>One entry per level (surface first); each is <see cref="Height"/> rows of <see cref="Width"/> chars.</summary>
    public required IReadOnlyList<IReadOnlyList<string>> Terrain { get; init; }

    public IReadOnlyList<PlayerSlotDocument> Players { get; init; } = [];

    public IReadOnlyList<HeroPlacementDocument> Heroes { get; init; } = [];
}

public sealed record PlayerSlotDocument(string Name, bool IsHuman);

public sealed record HeroPlacementDocument(int Id, string Name, int Owner, int X, int Y, int Level = 0);
