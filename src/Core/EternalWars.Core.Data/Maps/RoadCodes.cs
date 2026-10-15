using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Data.Maps;

/// <summary>Single-character codes used in <see cref="MapDocument.Roads"/> rows.</summary>
public static class RoadCodes
{
    public static char ToCode(RoadType road) => road switch
    {
        RoadType.None => '.',
        RoadType.Dirt => 'd',
        RoadType.Gravel => 'g',
        RoadType.Cobblestone => 'c',
        _ => throw new ArgumentOutOfRangeException(nameof(road), road, null),
    };

    public static RoadType FromCode(char code) => code switch
    {
        '.' => RoadType.None,
        'd' => RoadType.Dirt,
        'g' => RoadType.Gravel,
        'c' => RoadType.Cobblestone,
        _ => throw new FormatException($"Unknown road code '{code}'."),
    };
}
