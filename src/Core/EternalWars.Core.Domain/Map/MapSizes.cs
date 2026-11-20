namespace EternalWars.Core.Domain.Map;

/// <summary>The sizes a map may have, and the square presets offered by the editor and the random map generator.</summary>
public static class MapSizes
{
    public const int MaxDimension = 256;

    /// <summary>The smallest side the editor makes a map (EDIT-8); smaller maps (tests) still load and play.</summary>
    public const int MinDimension = 16;

    public static IReadOnlyList<int> Standard { get; } = [48, 72, 96, 128, 160, 192, 256];
}
