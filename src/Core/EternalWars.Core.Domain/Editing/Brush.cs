using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Editing;

public enum BrushShape
{
    Square,
    Round,
}

/// <summary>
/// The editor's brush (EDIT-1, ADR-250): <see cref="Size"/> tiles across (1 … <see cref="MaxSize"/>), square or round. An odd size is
/// centred on the tile it is put on; an even one reaches one tile further right and down. A round brush takes the tiles whose middle is
/// within half the size, less a quarter tile, of the brush's middle (size 3 is a plus, sizes 1 and 2 are the square's).
/// </summary>
public readonly record struct Brush(int Size, BrushShape Shape = BrushShape.Square)
{
    public const int MaxSize = 9;

    /// <summary>One tile.</summary>
    public static Brush One { get; } = new(1);

    /// <summary>The square brush that reaches <paramref name="radius"/> tiles every way from its middle (0: one tile).</summary>
    public static Brush OfRadius(int radius) => new((2 * radius) + 1);

    /// <summary>The tiles under the brush put on <paramref name="center"/>, row by row; some may lie off the map.</summary>
    public IEnumerable<MapPosition> Tiles(MapPosition center)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(Size, 1);
        var low = -((Size - 1) / 2);
        var high = Size / 2;
        var middle = (low + high) / 2.0;
        var reach = Math.Pow((Size / 2.0) - 0.25, 2);
        for (var dy = low; dy <= high; dy++)
        {
            for (var dx = low; dx <= high; dx++)
            {
                if (Shape == BrushShape.Square || Math.Pow(dx - middle, 2) + Math.Pow(dy - middle, 2) <= reach)
                {
                    yield return center.Offset(dx, dy);
                }
            }
        }
    }

    /// <summary>The tiles under the brush drawn along the straight line from <paramref name="from"/> to <paramref name="to"/>, each once.</summary>
    public IReadOnlyCollection<MapPosition> Line(MapPosition from, MapPosition to)
    {
        var brush = this;
        return [.. LineTiles(from, to).SelectMany(brush.Tiles).Distinct()];
    }

    /// <summary>
    /// The tiles of the straight line from <paramref name="from"/> to <paramref name="to"/>, both included, on <paramref name="from"/>'s
    /// level: one tile for every step along the longer side, so the line has no gaps.
    /// </summary>
    public static IEnumerable<MapPosition> LineTiles(MapPosition from, MapPosition to)
    {
        var (dx, dy) = (Math.Abs(to.X - from.X), Math.Abs(to.Y - from.Y));
        var (sx, sy) = (Math.Sign(to.X - from.X), Math.Sign(to.Y - from.Y));
        var (x, y) = (from.X, from.Y);
        var error = dx - dy;
        while (true)
        {
            yield return new MapPosition(x, y, from.Level);
            if (x == to.X && y == to.Y)
            {
                yield break;
            }

            var twice = 2 * error;
            if (twice > -dy)
            {
                error -= dy;
                x += sx;
            }

            if (twice < dx)
            {
                error += dx;
                y += sy;
            }
        }
    }
}
