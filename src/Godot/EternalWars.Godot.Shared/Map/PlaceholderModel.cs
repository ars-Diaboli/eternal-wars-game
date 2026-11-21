using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using EternalWars.Core.Domain.Objects;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>The shape of a <see cref="ModelPart"/>, and what its <see cref="ModelPart.Size"/> holds.</summary>
public enum ModelShape
{
    /// <summary>Size: width, height, depth.</summary>
    Box,

    /// <summary>Size: the base's radius, the height.</summary>
    Cone,

    /// <summary>Size: the radius, the height.</summary>
    Cylinder,

    /// <summary>Size: the radius.</summary>
    Sphere,
}

/// <summary>
/// One piece of a <see cref="PlaceholderModel"/>: its centre is <see cref="Position"/> from the ground under the object's top-left
/// tile. A piece that stands on a tile of the footprint names it (<see cref="Cell"/>), so the map can put it on that tile's own ground.
/// </summary>
public readonly record struct ModelPart(ModelShape Shape, Vector3 Size, Vector3 Position, Color Color, (int Dx, int Dy)? Cell = null)
{
    /// <summary>Half of the box the piece fills.</summary>
    public Vector3 HalfExtent => Shape switch
    {
        ModelShape.Box => Size / 2,
        ModelShape.Sphere => new Vector3(Size.X, Size.X, Size.X),
        _ => new Vector3(Size.X, Size.Y / 2, Size.X),
    };
}

/// <summary>
/// The stand-in model of a map object until its art exists: a low block on every blocked tile, a lighter plate or block on the
/// entrance, and for the quest objects (EVT-5) a shape that tells them apart: a Seer's Hut gets a pointed roof with a lamp, a Quest
/// Guard is a standing figure with a spear, a Quest Gate two posts and a lintel. The map draws it (<see cref="MapObjectsView"/>) and the
/// editor's thumbnails are rendered from it (ADR-254), so the two show the same thing.
/// </summary>
public static class PlaceholderModel
{
    /// <summary>The pieces of <paramref name="definition"/>'s model lying as <paramref name="footprint"/> says (its own, or turned for scenery).</summary>
    public static IReadOnlyList<ModelPart> Of(MapObjectDefinition definition, Footprint footprint)
    {
        var color = definition.Colour is { } colour ? Palette.ForColour(colour) : Palette.ForObject(definition.Kind);
        var parts = new List<ModelPart>();
        foreach (var (dx, dy, cell) in footprint.OccupiedCells)
        {
            var at = new Vector3(dx, 0, dy) * MapSpace.TileSize;
            if (cell == FootprintCell.Blocked)
            {
                parts.Add(new ModelPart(ModelShape.Box, new Vector3(0.9f, 0.5f, 0.9f), at + (Vector3.Up * 0.25f), color, (dx, dy)));
            }
            else if (cell == FootprintCell.Visit)
            {
                var height = definition.Kind is MapObjectKind.Monster or MapObjectKind.ResourcePile or MapObjectKind.TreasureChest or MapObjectKind.Artifact or MapObjectKind.Loot ? 0.35f : 0.06f;
                parts.Add(new ModelPart(ModelShape.Box, new Vector3(0.6f, height, 0.6f), at + (Vector3.Up * height / 2), color.Lightened(0.15f), (dx, dy)));
            }
        }

        switch (definition.Kind)
        {
            case MapObjectKind.SeerHut:
                var blocked = footprint.OccupiedCells.Where(c => c.Cell == FootprintCell.Blocked).ToList();
                if (blocked.Count > 0)
                {
                    var middle = new Vector3((float)blocked.Average(c => c.Dx), 0, (float)blocked.Average(c => c.Dy)) * MapSpace.TileSize;
                    var width = blocked.Max(c => c.Dx) - blocked.Min(c => c.Dx) + 1;
                    parts.Add(new ModelPart(ModelShape.Cone, new Vector3(0.4f * width, 0.7f, 0), middle + (Vector3.Up * 0.85f), color.Darkened(0.3f)));
                    parts.Add(new ModelPart(ModelShape.Sphere, new Vector3(0.12f, 0, 0), middle + (Vector3.Up * 1.3f), Colors.Gold)); // the seer's lamp
                }

                break;
            case MapObjectKind.QuestGuard:
                parts.Add(new ModelPart(ModelShape.Cylinder, new Vector3(0.18f, 0.6f, 0), Vector3.Up * 0.36f, color));
                parts.Add(new ModelPart(ModelShape.Sphere, new Vector3(0.14f, 0, 0), Vector3.Up * 0.8f, color.Lightened(0.3f)));
                parts.Add(new ModelPart(ModelShape.Box, new Vector3(0.04f, 1.0f, 0.04f), new Vector3(0.26f, 0.55f, 0), Colors.Silver));
                break;
            case MapObjectKind.QuestGate:
                parts.Add(new ModelPart(ModelShape.Box, new Vector3(0.14f, 0.9f, 0.14f), new Vector3(-0.38f, 0.45f, 0), color));
                parts.Add(new ModelPart(ModelShape.Box, new Vector3(0.14f, 0.9f, 0.14f), new Vector3(0.38f, 0.45f, 0), color));
                parts.Add(new ModelPart(ModelShape.Box, new Vector3(0.95f, 0.14f, 0.18f), Vector3.Up * 0.95f, color.Darkened(0.2f)));
                parts.Add(new ModelPart(ModelShape.Sphere, new Vector3(0.09f, 0, 0), Vector3.Up * 1.1f, Colors.Gold)); // the seal: meet the quest to pass
                break;
        }

        return parts;
    }

    /// <summary>
    /// A name for the model that is the same on every run and changes when any piece does: two definitions drawn alike share it, and a
    /// picture kept under it is stale as soon as the model changes.
    /// </summary>
    public static string Hash(IReadOnlyList<ModelPart> parts)
    {
        var text = new StringBuilder();
        foreach (var part in parts)
        {
            text.Append(CultureInfo.InvariantCulture, $"{(int)part.Shape}:{Text(part.Size)}:{Text(part.Position)}:{part.Color.ToHtml()};");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())), 0, 8).ToLowerInvariant();
    }

    /// <summary>The model as nodes of its own, on flat ground with the top-left tile at the origin.</summary>
    public static Node3D Build(IReadOnlyList<ModelPart> parts)
    {
        var root = new Node3D();
        foreach (var part in parts)
        {
            PrimitiveMesh mesh = part.Shape switch
            {
                ModelShape.Box => new BoxMesh { Size = part.Size },
                ModelShape.Cone => new CylinderMesh { TopRadius = 0, BottomRadius = part.Size.X, Height = part.Size.Y },
                ModelShape.Cylinder => new CylinderMesh { TopRadius = part.Size.X, BottomRadius = part.Size.X, Height = part.Size.Y },
                _ => new SphereMesh { Radius = part.Size.X, Height = part.Size.X * 2 },
            };
            mesh.Material = new StandardMaterial3D { AlbedoColor = part.Color };
            root.AddChild(new MeshInstance3D { Mesh = mesh, Position = part.Position });
        }

        return root;
    }

    private static string Text(Vector3 v) => string.Create(CultureInfo.InvariantCulture, $"{v.X:F3},{v.Y:F3},{v.Z:F3}");
}
