using EternalWars.Core.Domain.Towns;
using Godot;

namespace EternalWars.Godot.Shared.Towns;

/// <summary>
/// The greybox stand-in for a building without a model (the town screen plan, ADR-297): a block sized and coloured by the building's kind (a
/// dwelling by its tier), a gabled roof (a magic building's is a cone) and its name on a sign above it.
/// </summary>
internal static class PlotBlock
{
    private static readonly Dictionary<BuildingKind, (StandardMaterial3D Wall, StandardMaterial3D Roof)> Materials = [];

    /// <summary>The block's footprint and height: core buildings are broad, magic ones tall, dwellings grow with their tier.</summary>
    public static Vector3 Size(BuildingKind kind, int? tier) => kind switch
    {
        BuildingKind.Core => new Vector3(8, 4.5f, 6),
        BuildingKind.Military => new Vector3(6, 4, 5),
        BuildingKind.Magic => new Vector3(4, 8, 4),
        BuildingKind.Dwelling => new Vector3(3, 2.5f, 3) + new Vector3(0.5f, 0.5f, 0.5f) * (tier ?? 2),
        _ => new Vector3(5, 6, 5),
    };

    /// <summary>The block for a building named <paramref name="name"/>, and the box around it in its own space.</summary>
    public static (Node3D Node, Aabb Bounds) Build(string name, BuildingKind kind, int? tier)
    {
        var size = Size(kind, tier);
        var (wall, roof) = MaterialsOf(kind);
        var node = new Node3D { Name = "Block" };
        node.AddChild(new MeshInstance3D { Name = "Walls", Mesh = new BoxMesh { Size = size, Material = wall }, Position = new Vector3(0, size.Y / 2, 0) });
        float roofHeight;
        if (kind == BuildingKind.Magic)
        {
            roofHeight = size.X * 0.9f;
            node.AddChild(new MeshInstance3D
            {
                Name = "Roof",
                Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = size.X * 0.65f, Height = roofHeight, RadialSegments = 8, Material = roof },
                Position = new Vector3(0, size.Y + roofHeight / 2, 0),
            });
        }
        else
        {
            roofHeight = Mathf.Max(1.2f, size.X * 0.35f);
            node.AddChild(new MeshInstance3D
            {
                Name = "Roof",
                Mesh = new PrismMesh { Size = new Vector3(size.X + 0.5f, roofHeight, size.Z + 0.5f), Material = roof },
                Position = new Vector3(0, size.Y + roofHeight / 2, 0),
            });
        }

        var top = size.Y + roofHeight;
        node.AddChild(new Label3D
        {
            Name = "Sign",
            Text = name,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 48,
            OutlineSize = 12,
            PixelSize = 0.02f,
            Position = new Vector3(0, top + 0.9f, 0),
        });
        return (node, new Aabb(new Vector3(-size.X / 2 - 0.25f, 0, -size.Z / 2 - 0.25f), new Vector3(size.X + 0.5f, top, size.Z + 0.5f)));
    }

    private static (StandardMaterial3D Wall, StandardMaterial3D Roof) MaterialsOf(BuildingKind kind)
    {
        if (!Materials.TryGetValue(kind, out var pair))
        {
            var (wall, roof) = kind switch
            {
                BuildingKind.Core => (new Color(0.70f, 0.64f, 0.55f), new Color(0.45f, 0.24f, 0.18f)),
                BuildingKind.Military => (new Color(0.55f, 0.55f, 0.58f), new Color(0.24f, 0.26f, 0.30f)),
                BuildingKind.Magic => (new Color(0.52f, 0.56f, 0.70f), new Color(0.22f, 0.26f, 0.48f)),
                BuildingKind.Dwelling => (new Color(0.66f, 0.58f, 0.44f), new Color(0.40f, 0.30f, 0.20f)),
                _ => (new Color(0.78f, 0.70f, 0.46f), new Color(0.55f, 0.42f, 0.22f)),
            };
            pair = (new StandardMaterial3D { AlbedoColor = wall, Roughness = 0.9f }, new StandardMaterial3D { AlbedoColor = roof, Roughness = 0.8f });
            Materials[kind] = pair;
        }

        return pair;
    }
}
