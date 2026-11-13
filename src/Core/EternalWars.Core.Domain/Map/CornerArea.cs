using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Map;

/// <summary>
/// A rectangle of tile corners on one level, both ends included: corner (cx, cy) is the top-left corner of tile (cx, cy), so tile (x, y)
/// has corners x … x + 1 and y … y + 1 (<see cref="AdventureMap"/>). Empty when <see cref="Left"/> &gt; <see cref="Right"/> or
/// <see cref="Top"/> &gt; <see cref="Bottom"/>.
/// </summary>
public readonly record struct CornerArea(int Left, int Top, int Right, int Bottom)
{
    public bool IsEmpty => Left > Right || Top > Bottom;

    /// <summary>Every corner of the map.</summary>
    public static CornerArea Whole(AdventureMap map) => new(0, 0, map.Width, map.Height);

    /// <summary>The corners of the tiles (their level is not checked); empty for no tiles.</summary>
    public static CornerArea Of(IEnumerable<MapPosition> tiles)
    {
        int left = int.MaxValue, top = int.MaxValue, right = int.MinValue, bottom = int.MinValue;
        foreach (var tile in tiles)
        {
            left = Math.Min(left, tile.X);
            top = Math.Min(top, tile.Y);
            right = Math.Max(right, tile.X + 1);
            bottom = Math.Max(bottom, tile.Y + 1);
        }

        return new(left, top, right, bottom);
    }

    /// <summary>The area <paramref name="by"/> corners larger on every side.</summary>
    public CornerArea Grow(int by) => new(Left - by, Top - by, Right + by, Bottom + by);

    /// <summary>The part of the area inside <paramref name="map"/>.</summary>
    public CornerArea Clip(AdventureMap map) => new(Math.Max(Left, 0), Math.Max(Top, 0), Math.Min(Right, map.Width), Math.Min(Bottom, map.Height));

    public bool Contains(int cx, int cy) => cx >= Left && cx <= Right && cy >= Top && cy <= Bottom;

    public bool Overlaps(CornerArea other) =>
        !IsEmpty && !other.IsEmpty && Left <= other.Right && other.Left <= Right && Top <= other.Bottom && other.Top <= Bottom;

    /// <summary>Whether one of the tile's corners is in the area, so the ground under the tile depends on it.</summary>
    public bool Touches(MapPosition tile) => tile.X + 1 >= Left && tile.X <= Right && tile.Y + 1 >= Top && tile.Y <= Bottom;
}
