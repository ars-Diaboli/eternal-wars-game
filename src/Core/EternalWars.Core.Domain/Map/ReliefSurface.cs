namespace EternalWars.Core.Domain.Map;

/// <summary>
/// The ground as the 3D view shows it (ADR-209): a smooth surface through the relief's corner heights, so hills read rounded instead of
/// faceted. Rendering only; rules sample the relief bilinearly (<see cref="AdventureMap.GroundHeightAt(int, float, float)"/>).
/// </summary>
/// <remarks>
/// Monotone bicubic: a cubic along x through each of four rows of corners, then one along y through those, each with Catmull-Rom tangents
/// limited as Fritsch and Carlson limit them. So the surface passes through every corner, is smooth across tile edges, never rises above or
/// dips below a tile's own four corners (no ripple at a cliff's foot), and flat ground stays exactly flat. Corners past the map's edge take the
/// nearest edge corner's height, as the bilinear sampling does.
/// </remarks>
public static class ReliefSurface
{
    /// <summary>
    /// The shown ground's height in cell widths at a point given in tile units, where tile (x, y)'s centre is (x, y) and its corners are half a
    /// tile away; points outside the map take the nearest edge.
    /// </summary>
    public static float HeightAt(AdventureMap map, int level, float x, float y)
    {
        var u = Math.Clamp(x + 0.5f, 0f, map.Width);
        var v = Math.Clamp(y + 0.5f, 0f, map.Height);
        var cx = Math.Min((int)MathF.Floor(u), map.Width - 1);
        var cy = Math.Min((int)MathF.Floor(v), map.Height - 1);
        var (fx, fy) = (u - cx, v - cy);

        float Row(int row)
        {
            var cornerY = Math.Clamp(cy + row, 0, map.Height);
            float At(int dx) => map.GetCorner(level, Math.Clamp(cx + dx, 0, map.Width), cornerY);
            return Monotone(At(-1), At(0), At(1), At(2), fx);
        }

        return Monotone(Row(-1), Row(0), Row(1), Row(2), fy) / ReliefLimits.StepsPerCell;
    }

    /// <summary>
    /// <see cref="HeightAt"/> on a grid of <paramref name="columns"/> × <paramref name="rows"/> points, the first at (<paramref name="left"/>,
    /// <paramref name="top"/>) and the next <paramref name="step"/> on (tile units); indexed [column, row]. Each corner row's cubic along x is
    /// worked out once a column, so a mesh's worth of points costs about one cubic each.
    /// </summary>
    public static float[,] Grid(AdventureMap map, int level, float left, float top, float step, int columns, int rows)
    {
        // Where each point falls: its tile's first corner and how far across it.
        static (int Corner, float Fraction) Place(float at, int size)
        {
            var u = Math.Clamp(at + 0.5f, 0f, size);
            var corner = Math.Min((int)MathF.Floor(u), size - 1);
            return (corner, u - corner);
        }

        var across = new (int Corner, float Fraction)[columns];
        for (var i = 0; i < columns; i++)
        {
            across[i] = Place(left + (i * step), map.Width);
        }

        var down = new (int Corner, float Fraction)[rows];
        for (var j = 0; j < rows; j++)
        {
            down[j] = Place(top + (j * step), map.Height);
        }

        // The corner rows the points read: one before the first point's tile to two past the last's, within the map.
        var firstRow = Math.Max(down.Min(d => d.Corner) - 1, 0);
        var lastRow = Math.Min(down.Max(d => d.Corner) + 2, map.Height);
        var alongX = new float[columns, lastRow - firstRow + 1];
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var i = 0; i < columns; i++)
            {
                var (cx, fx) = across[i];
                float At(int dx) => map.GetCorner(level, Math.Clamp(cx + dx, 0, map.Width), row);
                alongX[i, row - firstRow] = Monotone(At(-1), At(0), At(1), At(2), fx);
            }
        }

        var heights = new float[columns, rows];
        for (var j = 0; j < rows; j++)
        {
            var (cy, fy) = down[j];
            var (r0, r1, r2, r3) = (Math.Max(cy - 1, 0) - firstRow, cy - firstRow, cy + 1 - firstRow, Math.Min(cy + 2, map.Height) - firstRow);
            for (var i = 0; i < columns; i++)
            {
                heights[i, j] = Monotone(alongX[i, r0], alongX[i, r1], alongX[i, r2], alongX[i, r3], fy) / ReliefLimits.StepsPerCell;
            }
        }

        return heights;
    }

    /// <summary>
    /// The corners whose tiles' shown ground moves when <paramref name="changed"/> corners change: the surface over a tile reads one corner
    /// past its own on every side, so a tile <see cref="CornerArea.Touches"/> the result when its shown ground depends on the change.
    /// </summary>
    public static CornerArea Reach(CornerArea changed) => changed.Grow(1);

    /// <summary>
    /// The cubic between <paramref name="p1"/> (at t = 0) and <paramref name="p2"/> (at t = 1), with <paramref name="p0"/> and
    /// <paramref name="p3"/> a step before and after, that stays between <paramref name="p1"/> and <paramref name="p2"/>.
    /// </summary>
    internal static float Monotone(float p0, float p1, float p2, float p3, float t)
    {
        var (before, across, after) = (p1 - p0, p2 - p1, p3 - p2);
        var (m1, m2) = (Tangent(before, across), Tangent(across, after));
        var (t2, t3) = (t * t, t * t * t);
        return (((2 * t3) - (3 * t2) + 1) * p1) + ((t3 - (2 * t2) + t) * m1) + (((-2 * t3) + (3 * t2)) * p2) + ((t3 - t2) * m2);
    }

    /// <summary>Catmull-Rom's tangent between two steps; none at a peak, a dip or a flat step, and at most three times the smaller step.</summary>
    private static float Tangent(float before, float after)
    {
        if (before * after <= 0)
        {
            return 0;
        }

        var limit = 3 * Math.Min(Math.Abs(before), Math.Abs(after));
        return Math.Sign(after) * Math.Min(Math.Abs(before + after) / 2, limit);
    }
}
