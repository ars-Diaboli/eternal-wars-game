namespace EternalWars.Core.Domain.Map;

/// <summary>Square map size presets offered by the editor and the random map generator.</summary>
public static class MapSizes
{
    public const int MaxDimension = 256;

    public static IReadOnlyList<int> Standard { get; } = [48, 72, 96, 128, 160, 192, 256];
}
