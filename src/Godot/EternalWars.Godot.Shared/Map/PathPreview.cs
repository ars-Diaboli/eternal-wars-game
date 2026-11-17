using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>HoMM-style route preview: one marker per step coloured by the day it is reached, a bigger one at the end.</summary>
public partial class PathPreview : Node3D
{
    private readonly MeshCache _meshes = new();
    private AdventureMap? _map;

    public HeroPath? Path { get; private set; }

    /// <summary>The map whose ground the markers sit on.</summary>
    public void Bind(AdventureMap map) => _map = map;

    public void Show(HeroPath path)
    {
        Clear();
        Path = path;
        foreach (var step in path.Steps)
        {
            var last = step == path.Steps[^1];
            var size = last ? 0.35f : 0.16f;
            AddChild(new MeshInstance3D
            {
                Mesh = _meshes.Cylinder(size, size, 0.04f, Palette.ForPathDay(step.Day), unshaded: true), // one per size and day colour
                Transform = _map is null
                    ? new Transform3D(Basis.Identity, MapSpace.ToWorld(step.Position) + (Vector3.Up * 0.05f))
                    : new Transform3D(MapSpace.OnSlope(_map, step.Position), MapSpace.ToGround(_map, step.Position) + (MapSpace.GroundNormal(_map, step.Position) * 0.05f)),
            });
        }
    }

    public void Clear()
    {
        Path = null;
        this.ClearChildren();
    }
}
