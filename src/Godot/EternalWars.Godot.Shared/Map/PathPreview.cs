using EternalWars.Core.Domain.Pathfinding;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>HoMM-style route preview: one marker per step coloured by the day it is reached, a bigger one at the end.</summary>
public partial class PathPreview : Node3D
{
    public HeroPath? Path { get; private set; }

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
                Mesh = new CylinderMesh
                {
                    TopRadius = size,
                    BottomRadius = size,
                    Height = 0.04f,
                    Material = new StandardMaterial3D
                    {
                        AlbedoColor = Palette.ForPathDay(step.Day),
                        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    },
                },
                Position = MapSpace.ToWorld(step.Position) + (Vector3.Up * 0.05f),
            });
        }
    }

    public void Clear()
    {
        Path = null;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }
    }
}
