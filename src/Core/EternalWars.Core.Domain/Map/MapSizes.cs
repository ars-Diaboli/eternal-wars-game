namespace EternalWars.Core.Domain.Map;

/// <summary>The sizes a map may have, and the square presets offered by the editor and the random map generator.</summary>
public static class MapSizes
{
    public const int MaxDimension = 256;

    /// <summary>The smallest side the editor makes a map (EDIT-8); smaller maps (tests) still load and play.</summary>
    public const int MinDimension = 16;

    public static IReadOnlyList<int> Standard { get; } = [48, 72, 96, 128, 160, 192, 256];

    /// <summary>The one of <paramref name="sizes"/> nearest <paramref name="size"/>, the smaller of two as near (ADR-259: a template's size for the wizard's).</summary>
    public static int Nearest(IReadOnlyList<int> sizes, int size)
    {
        ArgumentNullException.ThrowIfNull(sizes);
        if (sizes.Count == 0)
        {
            throw new ArgumentException("There is no size to choose from.", nameof(sizes));
        }

        return sizes.OrderBy(s => Math.Abs(s - size)).ThenBy(s => s).First();
    }
}
