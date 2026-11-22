using EternalWars.Godot.Shared.Battle;
using Godot;

namespace EternalWars.Godot.Shared.Towns;

/// <summary>
/// The town scene's greybox pieces besides the plot blocks (the town screen plan's step 3, task 3, ADR-301): scaffolding, citizens' homes, houses
/// and streets, wall sections, the gatehouse, towers, the moat and banners, in the town's siege look (<see cref="SiegeLook"/>, ADR-144) so the town
/// looks as it fights. Placeholder shapes and colours, replaced by the faction's kit (step 4).
/// </summary>
internal static class SceneryKit
{
    /// <summary>Scaffold timber.</summary>
    private static readonly Color Timber = new(0.58f, 0.44f, 0.27f);

    private static readonly Dictionary<(Color, bool), StandardMaterial3D> Materials = [];

    /// <summary>A solid material of <paramref name="color"/>, shared; <paramref name="glows"/> lights it from within (a lava moat).</summary>
    public static StandardMaterial3D Material(Color color, bool glows = false)
    {
        if (!Materials.TryGetValue((color, glows), out var material))
        {
            material = new StandardMaterial3D { AlbedoColor = color, Roughness = 0.9f };
            if (color.A < 1)
            {
                (material.Transparency, material.Roughness, material.Metallic) = (BaseMaterial3D.TransparencyEnum.Alpha, 0.1f, 0.1f);
            }

            if (glows)
            {
                (material.EmissionEnabled, material.Emission, material.EmissionEnergyMultiplier) = (true, color, 2.5f);
            }

            Materials[(color, glows)] = material;
        }

        return material;
    }

    /// <summary>A box of <paramref name="size"/> standing on <paramref name="at"/> (its bottom's centre).</summary>
    public static MeshInstance3D Box(string name, Vector3 at, Vector3 size, Color color, float yawDegrees = 0, bool glows = false) => new()
    {
        Name = name,
        Mesh = new BoxMesh { Size = size, Material = Material(color, glows) },
        Position = at + (Vector3.Up * size.Y / 2),
        RotationDegrees = new Vector3(0, yawDegrees, 0),
    };

    /// <summary>
    /// Scaffolding round <paramref name="bounds"/> (a built plot's box): poles at most 3 m apart a little outside it, planks every 2.5 m up to
    /// near its top.
    /// </summary>
    public static Node3D Scaffold(Aabb bounds)
    {
        const float Gap = 0.6f;
        var node = new Node3D { Name = "Scaffold" };
        var (min, max) = (bounds.Position - new Vector3(Gap, 0, Gap), bounds.End + new Vector3(Gap, 0, Gap));
        var height = Mathf.Clamp(bounds.Size.Y * 0.85f, 2.5f, 18f);
        var corners = new[] { new Vector3(min.X, 0, min.Z), new Vector3(max.X, 0, min.Z), new Vector3(max.X, 0, max.Z), new Vector3(min.X, 0, max.Z) };
        for (var side = 0; side < 4; side++)
        {
            var (from, to) = (corners[side], corners[(side + 1) % 4]);
            var length = from.DistanceTo(to);
            var poles = Mathf.Max(1, Mathf.CeilToInt(length / 3f));
            for (var i = 0; i < poles; i++)
            {
                node.AddChild(Box("Pole", from.Lerp(to, (float)i / poles) + (Vector3.Up * bounds.Position.Y), new Vector3(0.16f, height, 0.16f), Timber));
            }

            var yaw = Mathf.RadToDeg(Mathf.Atan2(to.X - from.X, to.Z - from.Z));
            for (var y = 2.5f; y < height; y += 2.5f)
            {
                node.AddChild(Box("Plank", from.Lerp(to, 0.5f) + (Vector3.Up * (bounds.Position.Y + y)), new Vector3(0.3f, 0.1f, length), Timber, yaw));
            }
        }

        return node;
    }

    /// <summary>The citizens' homes on a closed dwelling's plot (ADR-291): a few houses in the town's look within <paramref name="footprint"/> (width, depth).</summary>
    public static Node3D Homes(SiegeLook look, Vector2 footprint)
    {
        var node = new Node3D { Name = "Homes" };
        var (w, d) = (Mathf.Max(footprint.X, 5f), Mathf.Max(footprint.Y, 5f));
        foreach (var (x, z, size, yaw) in new[]
                 {
                     (-0.25f, -0.22f, new Vector3(2.6f, 2.6f, 2.2f), 0f), (0.26f, -0.2f, new Vector3(2.2f, 2.2f, 2.4f), 90f),
                     (-0.2f, 0.28f, new Vector3(2.4f, 2.0f, 2.0f), 90f), (0.27f, 0.3f, new Vector3(2.0f, 2.4f, 2.0f), 0f),
                 })
        {
            node.AddChild(House(look, new Vector3(x * w, 0, z * d), size, yaw));
        }

        return node;
    }

    /// <summary>A house of <paramref name="size"/> standing on <paramref name="at"/>: a block of the town's stone toward its yard, under its roof.</summary>
    public static Node3D House(SiegeLook look, Vector3 at, Vector3 size, float yawDegrees = 0)
    {
        var node = new Node3D { Name = "House", Position = at, RotationDegrees = new Vector3(0, yawDegrees, 0) };
        node.AddChild(Box("Walls", Vector3.Zero, size, look.Stone.Lerp(look.Yard, 0.35f)));
        Roof(node, look, size.X, size.Z, size.Y);
        return node;
    }

    /// <summary>A strip of street, <paramref name="size"/> wide × long, along the holder's Z.</summary>
    public static MeshInstance3D Street(SiegeLook look, Vector2 size) =>
        Box("Paving", new Vector3(0, 0.02f, 0), new Vector3(size.X, 0.06f, size.Y), look.Yard.Lerp(look.Stone, 0.3f));

    /// <summary>A round tower of <paramref name="height"/> standing on <paramref name="at"/>, under the town's roof.</summary>
    public static Node3D Tower(SiegeLook look, Vector3 at, float radius, float height)
    {
        var node = new Node3D { Name = "Tower", Position = at };
        node.AddChild(new MeshInstance3D
        {
            Name = "Body",
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius * 1.08f, Height = height, RadialSegments = 12, Material = Material(look.Stone.Darkened(0.05f)) },
            Position = Vector3.Up * height / 2,
        });
        Roof(node, look, radius * 2.2f, radius * 2.2f, height);
        return node;
    }

    /// <summary>
    /// A banner on a pole standing on <paramref name="at"/>: cloth of <paramref name="colour"/> bearing <paramref name="charge"/> (the faction's
    /// figure, when there is one). The garrison hero's banner is <paramref name="scale"/> times bigger.
    /// </summary>
    public static Node3D Banner(string name, Vector3 at, Color colour, Texture2D? charge, float scale = 1f)
    {
        var node = new Node3D { Name = name, Position = at, Scale = Vector3.One * scale };
        node.AddChild(new MeshInstance3D
        {
            Name = "Pole",
            Mesh = new CylinderMesh { TopRadius = 0.05f, BottomRadius = 0.07f, Height = 3.4f, RadialSegments = 6, Material = Material(new Color(0.3f, 0.24f, 0.18f)) },
            Position = Vector3.Up * 1.7f,
        });
        node.AddChild(Box("Cloth", new Vector3(0.6f, 1.75f, 0), new Vector3(1.1f, 1.5f, 0.05f), colour));
        if (charge is not null)
        {
            foreach (var side in new[] { 1f, -1f })
            {
                node.AddChild(new MeshInstance3D
                {
                    Name = "Charge",
                    Mesh = new QuadMesh
                    {
                        Size = new Vector2(0.85f, 0.85f),
                        Material = new StandardMaterial3D { AlbedoTexture = charge, Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, Roughness = 0.9f },
                    },
                    Position = new Vector3(0.6f, 2.5f, 0.03f * side),
                    RotationDegrees = new Vector3(0, side > 0 ? 0 : 180, 0),
                });
            }
        }

        return node;
    }

    /// <summary>The town's roof on a block or tower <paramref name="width"/> × <paramref name="depth"/> whose top is <paramref name="top"/> above its foot.</summary>
    private static void Roof(Node3D parent, SiegeLook look, float width, float depth, float top)
    {
        var size = Mathf.Max(width, depth);
        var stretch = new Vector3(width / size, 1, depth / size);
        var material = Material(look.Roof);
        switch (look.Roofs)
        {
            case RoofShape.Cone:
            case RoofShape.Pyramid:
            {
                var pyramid = look.Roofs == RoofShape.Pyramid;
                var height = size * 0.7f;
                var holder = new Node3D { Name = "Roof", Position = Vector3.Up * (top + (height / 2)), Scale = stretch };
                holder.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = pyramid ? size * 0.55f * Mathf.Sqrt2 : size * 0.56f, Height = height, RadialSegments = pyramid ? 4 : 12, Rings = 1, Material = material },
                    RotationDegrees = pyramid ? new Vector3(0, 45, 0) : Vector3.Zero,
                });
                parent.AddChild(holder);
                break;
            }

            case RoofShape.Dome:
            {
                var holder = new Node3D { Name = "Roof", Position = Vector3.Up * top, Scale = stretch };
                holder.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = size * 0.5f, Height = size, Material = material } });
                parent.AddChild(holder);
                break;
            }

            case RoofShape.Spikes:
            {
                var spikes = new Node3D { Name = "Roof", Position = Vector3.Up * top };
                foreach (var (x, z, height) in new[] { (-0.35f, -0.35f, 0.35f), (0.35f, -0.35f, 0.35f), (-0.35f, 0.35f, 0.35f), (0.35f, 0.35f, 0.35f), (0f, 0f, 0.6f) })
                {
                    spikes.AddChild(new MeshInstance3D
                    {
                        Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = size * 0.07f, Height = height * size, RadialSegments = 6, Rings = 1, Material = material },
                        Position = new Vector3(x * width, height * size / 2, z * depth),
                    });
                }

                parent.AddChild(spikes);
                break;
            }

            default:
            {
                // A flat top with a merlon at each corner and between them.
                var battlements = new Node3D { Name = "Roof", Position = Vector3.Up * top };
                var merlon = Mathf.Max(0.3f, size * 0.14f);
                foreach (var x in new[] { -0.5f, 0f, 0.5f })
                {
                    foreach (var z in new[] { -0.5f, 0f, 0.5f })
                    {
                        if (x != 0 || z != 0)
                        {
                            battlements.AddChild(Box("Merlon", new Vector3(x * (width - merlon), 0, z * (depth - merlon)), Vector3.One * merlon, look.Roof));
                        }
                    }
                }

                parent.AddChild(battlements);
                break;
            }
        }
    }
}
