using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Data.Maps;

/// <summary>
/// Single-character codes used in <see cref="MapDocument.Heights"/> rows (v39, ADR-182): one per tile corner, the height in eighths of a
/// cell width. <c>0</c>–<c>9</c>, <c>A</c>–<c>Z</c> and <c>a</c>–<c>z</c> are 0 to 61 (grass <c>8</c>, snow <c>G</c>, the top of walkable
/// ground <c>W</c>), <c>^</c> and <c>~</c> 62 and 63; below sea level, <c>!#$%()*,-./:;=?@</c> are −16 to −1 (deep water <c>;</c>). None of
/// them is escaped in JSON.
/// </summary>
public static class HeightCodes
{
    private const string Codes = "!#$%()*,-./:;=?@0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz^~";

    public static char ToCode(int eighths) => Codes[Math.Clamp(eighths, ReliefLimits.StoredMin, ReliefLimits.StoredMax) - ReliefLimits.StoredMin];

    public static int FromCode(char code)
    {
        var index = Codes.IndexOf(code, StringComparison.Ordinal);
        return index < 0 ? throw new FormatException($"Unknown height code '{code}'.") : index + ReliefLimits.StoredMin;
    }
}
