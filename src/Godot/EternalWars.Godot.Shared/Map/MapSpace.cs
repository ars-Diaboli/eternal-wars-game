using EternalWars.Core.Domain.Common;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Converts between core grid coordinates and Godot world space.
/// Tile (x, y) occupies the unit square centred at (x, 0, y); levels are stacked vertically.
/// </summary>
public static class MapSpace
{
    public const float TileSize = 1f;
    public const float LevelSpacing = 20f;

    public static Vector3 ToWorld(MapPosition position) =>
        new(position.X * TileSize, -position.Level * LevelSpacing, position.Y * TileSize);

    public static MapPosition ToMap(Vector3 world, int level = 0) =>
        new(Mathf.RoundToInt(world.X / TileSize), Mathf.RoundToInt(world.Z / TileSize), level);

    /// <summary>Projects a screen point onto the ground plane of <paramref name="level"/>.</summary>
    public static MapPosition? PickTile(Camera3D camera, Vector2 screenPoint, int level = 0)
    {
        var ground = new Plane(Vector3.Up, -level * LevelSpacing);
        var hit = ground.IntersectsRay(camera.ProjectRayOrigin(screenPoint), camera.ProjectRayNormal(screenPoint));
        return hit is { } point ? ToMap(point, level) : null;
    }
}
