using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Data.Maps;

/// <summary>Single-character codes used in <see cref="MapDocument.Rivers"/> rows (v22, ADR-079).</summary>
public static class RiverCodes
{
    public static char ToCode(RiverType river) => river switch
    {
        RiverType.None => '.',
        RiverType.Clear => 'c',
        RiverType.Icy => 'i',
        RiverType.Muddy => 'm',
        RiverType.Lava => 'l',
        _ => throw new ArgumentOutOfRangeException(nameof(river), river, null),
    };

    public static RiverType FromCode(char code) => code switch
    {
        '.' => RiverType.None,
        'c' => RiverType.Clear,
        'i' => RiverType.Icy,
        'm' => RiverType.Muddy,
        'l' => RiverType.Lava,
        _ => throw new FormatException($"Unknown river code '{code}'."),
    };
}
