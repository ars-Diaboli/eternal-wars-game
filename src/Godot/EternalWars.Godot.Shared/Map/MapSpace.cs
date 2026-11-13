using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Converts between core grid coordinates and Godot world space.
/// Tile (x, y) occupies the unit square centred at (x, 0, y); levels are stacked vertically. The ground rises above a level's base by the
/// map's relief (ADR-182, <see cref="AdventureMap.GroundHeightAt(int, float, float)"/>): <see cref="ToGround"/> puts things on it.
/// </summary>
public static class MapSpace
{
    public const float TileSize = 1f;
    public const float LevelSpacing = 20f;

    /// <summary>How far a picking ray moves across the map per test, in tiles, before it is refined.</summary>
    private const float PickStep = 0.2f;

    /// <summary>The level's base (the flat ground's height): relief is measured from it.</summary>
    public static Vector3 ToWorld(MapPosition position) =>
        new(position.X * TileSize, -position.Level * LevelSpacing, position.Y * TileSize);

    /// <summary>The ground at the tile's centre.</summary>
    public static Vector3 ToGround(AdventureMap map, MapPosition position) =>
        ToWorld(position) + (Vector3.Up * map.GroundHeightAt(position) * TileSize);

    /// <summary>The ground's world height on <paramref name="level"/> below the world point (<paramref name="x"/>, <paramref name="z"/>).</summary>
    public static float GroundY(AdventureMap map, int level, float x, float z) =>
        (-level * LevelSpacing) + (map.GroundHeightAt(level, x / TileSize, z / TileSize) * TileSize);

    /// <summary>The ground's slope at a tile's centre, as its up direction: from the heights half a tile either way.</summary>
    public static Vector3 GroundNormal(AdventureMap map, MapPosition position)
    {
        var (x, z) = (position.X * TileSize, position.Y * TileSize);
        var half = TileSize / 2;
        return new Vector3(
            GroundY(map, position.Level, x - half, z) - GroundY(map, position.Level, x + half, z),
            TileSize,
            GroundY(map, position.Level, x, z - half) - GroundY(map, position.Level, x, z + half)).Normalized();
    }

    /// <summary>Turns a flat thing (a plate, a marker) to lie on the ground's slope at a tile's centre.</summary>
    public static Basis OnSlope(AdventureMap map, MapPosition position) => new(new Quaternion(Vector3.Up, GroundNormal(map, position)));

    /// <summary>The highest and lowest ground of a level, in world height.</summary>
    public static (float Low, float High) GroundRange(AdventureMap map, int level)
    {
        int low = int.MaxValue, high = int.MinValue;
        for (var cy = 0; cy <= map.Height; cy++)
        {
            for (var cx = 0; cx <= map.Width; cx++)
            {
                var h = map.GetCorner(level, cx, cy);
                low = Math.Min(low, h);
                high = Math.Max(high, h);
            }
        }

        var floor = -level * LevelSpacing;
        return (floor + (low * TileSize / ReliefLimits.StepsPerCell), floor + (high * TileSize / ReliefLimits.StepsPerCell));
    }

    public static MapPosition ToMap(Vector3 world, int level = 0) =>
        new(Mathf.RoundToInt(world.X / TileSize), Mathf.RoundToInt(world.Z / TileSize), level);

    /// <summary>
    /// The tile under a screen point on <paramref name="level"/>'s ground, slopes included: the ray is marched down from the level's highest
    /// ground to its lowest and the first crossing is refined by bisection. Null when the ray never comes down to the ground.
    /// </summary>
    public static MapPosition? PickTile(Camera3D camera, Vector2 screenPoint, AdventureMap map, int level = 0)
    {
        var origin = camera.ProjectRayOrigin(screenPoint);
        var direction = camera.ProjectRayNormal(screenPoint);
        if (direction.Y >= -1e-4f)
        {
            return null;
        }

        var (low, high) = GroundRange(map, level);
        var start = Math.Max(0f, (high - origin.Y) / direction.Y);
        var end = (low - origin.Y) / direction.Y;
        if (end < 0)
        {
            return null;
        }

        float Above(float t)
        {
            var point = origin + (direction * t);
            return point.Y - GroundY(map, level, point.X, point.Z);
        }

        var across = Math.Max(new Vector2(direction.X, direction.Z).Length(), 0.05f);
        var step = PickStep * TileSize / across;
        var before = start;
        if (Above(start) > 0)
        {
            var hit = end;
            for (var t = start + step; ; t += step)
            {
                var at = Math.Min(t, end);
                if (Above(at) <= 0)
                {
                    hit = at;
                    break;
                }

                before = at;
                if (at >= end)
                {
                    break;
                }
            }

            for (var i = 0; i < 16; i++)
            {
                var middle = (before + hit) / 2;
                if (Above(middle) > 0)
                {
                    before = middle;
                }
                else
                {
                    hit = middle;
                }
            }

            before = hit;
        }

        return ToMap(origin + (direction * before), level);
    }
}
