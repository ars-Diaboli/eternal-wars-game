using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Data.Maps;

/// <summary>Single-character codes used in <see cref="MapDocument.Terrain"/> rows.</summary>
public static class TerrainCodes
{
    private static readonly Dictionary<TerrainType, char> ToCodeMap = new()
    {
        [TerrainType.Grass] = 'g',
        [TerrainType.Dirt] = 'd',
        [TerrainType.Sand] = 's',
        [TerrainType.Snow] = 'n',
        [TerrainType.Swamp] = 'w',
        [TerrainType.Rough] = 'r',
        [TerrainType.Highlands] = 'h',
        [TerrainType.ForestFloor] = 'f',
        [TerrainType.Wasteland] = 'x',
        [TerrainType.Lava] = 'l',
        [TerrainType.Subterranean] = 'u',
        [TerrainType.Water] = '~',
        [TerrainType.Rock] = '#',
    };

    private static readonly Dictionary<char, TerrainType> FromCodeMap =
        ToCodeMap.ToDictionary(pair => pair.Value, pair => pair.Key);

    public static char ToCode(TerrainType terrain) => ToCodeMap[terrain];

    public static TerrainType FromCode(char code) =>
        FromCodeMap.TryGetValue(code, out var terrain)
            ? terrain
            : throw new FormatException($"Unknown terrain code '{code}'.");
}
