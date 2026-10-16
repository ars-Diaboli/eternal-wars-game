using EternalWars.Core.Domain.Battles;
using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>
/// Converts between battlefield cells and Godot world space. Cell (x, y) is a <see cref="CellSize"/> square whose
/// top-left corner is at <see cref="Origin"/> + (x, 0, y) · size: attackers on the left (−X), the field's top row away
/// from the camera (−Z). The battlefield sits far from the adventure map, which is hidden while a battle is shown.
/// </summary>
public static class BattleSpace
{
    /// <summary>World units per cell: a 2×2 creature covers one adventure-map tile.</summary>
    public const float CellSize = 0.5f;

    public static readonly Vector3 Origin = new(0, 200, 0);

    /// <summary>Centre of a cell on the ground.</summary>
    public static Vector3 ToWorld(CellPosition cell) => ToWorld(cell.X + 0.5f, cell.Y + 0.5f);

    /// <summary>A point in cell units (fractions allowed) on the ground.</summary>
    public static Vector3 ToWorld(float x, float y) => Origin + new Vector3(x * CellSize, 0, y * CellSize);

    /// <summary>Centre of a footprint on the ground.</summary>
    public static Vector3 Center(CellRect rect) => ToWorld(rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));

    /// <summary>
    /// Projects a screen point onto the battlefield's ground plane: the point in cell units, or null when the ray
    /// misses the plane.
    /// </summary>
    public static Vector2? PickPoint(Camera3D camera, Vector2 screenPoint)
    {
        var ground = new Plane(Vector3.Up, Origin.Y);
        var hit = ground.IntersectsRay(camera.ProjectRayOrigin(screenPoint), camera.ProjectRayNormal(screenPoint));
        return hit is { } point ? new Vector2(point.X - Origin.X, point.Z - Origin.Z) / CellSize : null;
    }

    public static CellPosition ToCell(Vector2 point) => new(Mathf.FloorToInt(point.X), Mathf.FloorToInt(point.Y));
}
