using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>
/// Builds the battlefield's nodes under <paramref name="field"/>: the ground in the battle terrain's texture, the cell grid, obstacles, a
/// siege's fortifications in the town's look (ADR-106, ADR-144), field zones (ADR-149), and the projectiles and floating numbers the
/// animations show. Each material is made once per builder and shared by every node in that look; the one change made to a material
/// later (a destroyed structure darkening, <see cref="Ruin"/>) works on that node's own copy.
/// </summary>
public sealed class BattleFieldBuilder(Node3D field)
{
    private const float GroundMargin = 6f;

    private readonly Dictionary<(Look Look, Color Color), StandardMaterial3D> _materials = [];
    private readonly Dictionary<TerrainType, (StandardMaterial3D Ground, StandardMaterial3D Surround)> _grounds = [];

    private enum Look
    {
        /// <summary>Opaque and matt: obstacles, structures, roofs, houses.</summary>
        Solid,

        /// <summary>Unshaded and translucent: the grid, field zones.</summary>
        Glass,

        /// <summary>Translucent and matt: a siege's yard.</summary>
        Wash,

        /// <summary>A moat's water.</summary>
        Water,

        /// <summary>A moat that glows.</summary>
        GlowingWater,

        /// <summary>Unshaded and opaque: projectiles.</summary>
        Bright,
    }

    /// <summary>The field in the battle terrain's texture, inside a darker band of the same ground.</summary>
    public void Ground(TerrainType terrain, int width, int height)
    {
        var size = new Vector2(width, height) * BattleSpace.CellSize;
        var center = BattleSpace.ToWorld(width / 2f, height / 2f);
        if (!_grounds.TryGetValue(terrain, out var materials))
        {
            var ground = AdventureMapView.TerrainMaterial(terrain, 4f);
            var surround = (StandardMaterial3D)ground.Duplicate();
            surround.AlbedoColor = ground.AlbedoColor.Darkened(0.55f);
            _grounds[terrain] = materials = (ground, surround);
        }

        field.AddChild(new MeshInstance3D { Name = "Ground", Mesh = new PlaneMesh { Size = size, Material = materials.Ground }, Position = center });
        field.AddChild(new MeshInstance3D
        {
            Name = "Surround",
            Mesh = new PlaneMesh { Size = size + (Vector2.One * GroundMargin * 2), Material = materials.Surround },
            Position = center + (Vector3.Down * 0.02f),
        });
    }

    public void Grid(int width, int height)
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, Material(Look.Glass, new Color(1, 1, 1, 0.12f)));
        var lift = Vector3.Up * 0.01f;
        for (var x = 0; x <= width; x++)
        {
            mesh.SurfaceAddVertex(BattleSpace.ToWorld(x, 0) + lift);
            mesh.SurfaceAddVertex(BattleSpace.ToWorld(x, height) + lift);
        }

        for (var y = 0; y <= height; y++)
        {
            mesh.SurfaceAddVertex(BattleSpace.ToWorld(0, y) + lift);
            mesh.SurfaceAddVertex(BattleSpace.ToWorld(width, y) + lift);
        }

        mesh.SurfaceEnd();
        field.AddChild(new MeshInstance3D { Name = "Grid", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>A field zone (ADR-149) as a translucent block over its cells, in its spell's placeholder look (<see cref="ZoneLook"/>).</summary>
    public MeshInstance3D Zone(int id, string spell, CellRect rect)
    {
        var height = ZoneLook.Height(spell);
        var node = new MeshInstance3D
        {
            Name = $"Zone_{id}",
            Mesh = new BoxMesh
            {
                Size = new Vector3(rect.Width * BattleSpace.CellSize, height, rect.Height * BattleSpace.CellSize),
                Material = Material(Look.Glass, ZoneLook.Of(spell)),
            },
            Position = BattleSpace.Center(rect) + (Vector3.Up * ((height / 2) + 0.01f)),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        field.AddChild(node);
        return node;
    }

    /// <summary>Small obstacles are boulders, big ones crags, cliffs (ADR-146) long ridges, rubble (ADR-177) low heaps, wall segments blocks.</summary>
    public void Obstacle(BattleObstacle obstacle)
    {
        var extent = obstacle.Rect.Width * BattleSpace.CellSize;
        if (obstacle.Class == ObstacleClass.Cliff)
        {
            // A ridge as long as the cliff, its top narrower than its foot: a prism along the field's depth.
            const float ridge = 1.1f;
            field.AddChild(new MeshInstance3D
            {
                Name = $"Obstacle_{obstacle.Id}",
                Mesh = new PrismMesh
                {
                    Size = new Vector3(extent, ridge, obstacle.Rect.Height * BattleSpace.CellSize),
                    Material = Material(Look.Solid, new Color(0.36f, 0.32f, 0.28f)),
                },
                Position = BattleSpace.Center(obstacle.Rect) + (Vector3.Up * (ridge / 2)),
            });
            return;
        }

        var (mesh, height) = obstacle.Class switch
        {
            ObstacleClass.Small => ((PrimitiveMesh)new SphereMesh { Radius = extent * 0.42f, Height = extent * 0.55f }, extent * 0.2f),
            ObstacleClass.Big => (new CylinderMesh { TopRadius = extent * 0.15f, BottomRadius = extent * 0.45f, Height = extent * 0.8f }, extent * 0.4f),
            ObstacleClass.Rubble => (new CylinderMesh { TopRadius = extent * 0.2f, BottomRadius = extent * 0.48f, Height = extent * 0.35f, RadialSegments = 6 }, extent * 0.17f),
            _ => (new BoxMesh { Size = new Vector3(extent, extent * 1.5f, extent) }, extent * 0.75f),
        };
        mesh.Material = Material(Look.Solid, new Color(0.40f, 0.36f, 0.31f));
        field.AddChild(new MeshInstance3D
        {
            Name = $"Obstacle_{obstacle.Id}",
            Mesh = mesh,
            Position = BattleSpace.Center(obstacle.Rect) + (Vector3.Up * height),
        });
    }

    /// <summary>
    /// A siege's fortifications (ADR-106) in the town's own look (ADR-144, <see cref="SiegeLook"/>) on a field <paramref name="width"/> cells
    /// wide: the moat in front of the walls, the wall sections and gate on the wall line, the keep and towers beside the field, the yard
    /// behind the walls in the town's ground, and a few of the town's houses beyond the defenders' edge. Destroyed structures stand as
    /// rubble. Gives the structures' nodes by id.
    /// </summary>
    public IReadOnlyList<(SiegeStructureInfo Structure, MeshInstance3D Node)> Siege(SiegeLayout layout, int width)
    {
        var look = SiegeLook.Of(layout.Town);
        var water = Material(look.MoatGlows ? Look.GlowingWater : Look.Water, look.Moat);
        var yard = new CellRect(SiegeRules.WallX + 1, 0, width - SiegeRules.WallX - 1, BattleState.Height);
        field.AddChild(new MeshInstance3D
        {
            Name = "Yard",
            Mesh = new PlaneMesh
            {
                Size = new Vector2(yard.Width, yard.Height) * BattleSpace.CellSize,
                Material = Material(Look.Wash, look.Yard with { A = 0.45f }),
            },
            Position = BattleSpace.Center(yard) + (Vector3.Up * 0.006f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });

        foreach (var moat in layout.Moat)
        {
            field.AddChild(new MeshInstance3D
            {
                Name = "Moat",
                Mesh = new BoxMesh { Size = new Vector3(moat.Width * BattleSpace.CellSize, 0.02f, moat.Height * BattleSpace.CellSize), Material = water },
                Position = BattleSpace.Center(moat) + (Vector3.Up * 0.012f),
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        }

        var structures = new List<(SiegeStructureInfo, MeshInstance3D)>();
        foreach (var structure in layout.Structures)
        {
            var node = StructureNode(structure, look);
            field.AddChild(node);
            structures.Add((structure, node));
            if (structure.Health == 0)
            {
                Ruin(node);
            }
        }

        // The town beyond the defenders' edge: houses round the keep, more behind a stronger fort.
        var houses = new (float X, float Y, float Width, float Depth, float Height)[]
        {
            (width + 3.5f, 3f, 2.5f, 2f, 0.8f), (width + 3f, 19.5f, 2f, 2.5f, 0.7f), (width + 6.5f, 7f, 3f, 2.5f, 1.0f),
            (width + 6f, 16.5f, 2.5f, 3f, 0.9f), (width + 7.5f, 2.5f, 2f, 2f, 0.6f), (width + 8f, 12f, 3f, 3f, 1.2f),
            (width + 7.5f, 21f, 2.5f, 2f, 0.7f), (width + 4f, 8f, 2f, 1.5f, 0.6f), (width + 4f, 15.5f, 1.5f, 2f, 0.6f),
        };
        foreach (var (x, y, houseWidth, depth, height) in houses.Take(5 + (2 * layout.FortLevel)))
        {
            field.AddChild(House(look, BattleSpace.ToWorld(x, y), houseWidth * BattleSpace.CellSize, depth * BattleSpace.CellSize, height));
        }

        return structures;
    }

    /// <summary>A destroyed structure: a low, dark heap of rubble where it stood; its roof and battlements are gone.</summary>
    public static void Ruin(MeshInstance3D node)
    {
        foreach (var child in node.GetChildren().OfType<Node3D>())
        {
            child.Visible = false;
        }

        var height = node.GetAabb().Size.Y;
        node.Scale = new Vector3(1, 0.2f, 1);
        node.Position = node.Position with { Y = BattleSpace.Origin.Y + (height * 0.1f) };
        if (node.Mesh is PrimitiveMesh { Material: StandardMaterial3D material } mesh)
        {
            // The material is shared with the structures still standing in the same look: darken a copy.
            var rubble = (StandardMaterial3D)material.Duplicate();
            rubble.AlbedoColor = material.AlbedoColor.Darkened(0.45f);
            mesh.Material = rubble;
        }
    }

    /// <summary>A hidden projectile at <paramref name="at"/>; the animation shows it, moves it and frees it.</summary>
    public MeshInstance3D Projectile(Vector3 at)
    {
        var projectile = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.06f, Height = 0.12f, Material = Material(Look.Bright, new Color(1f, 0.9f, 0.6f)) },
            Position = at,
            Visible = false,
        };
        field.AddChild(projectile);
        return projectile;
    }

    /// <summary>A hidden caption at <paramref name="at"/> facing the camera; the animation shows it, floats it up and frees it.</summary>
    public Label3D FloatingText(string text, Vector3 at)
    {
        var label = new Label3D
        {
            Text = text,
            Position = at,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.005f,
            FontSize = 44,
            OutlineSize = 10,
            Modulate = new Color(1f, 0.85f, 0.4f),
            NoDepthTest = true,
            Visible = false,
        };
        field.AddChild(label);
        return label;
    }

    /// <summary>Walls and keep as blocks with battlements, the gate, round towers; roofs and colours from the town's look (ADR-144).</summary>
    private MeshInstance3D StructureNode(SiegeStructureInfo structure, SiegeLook look)
    {
        var width = structure.Rect.Width * BattleSpace.CellSize;
        var depth = structure.Rect.Height * BattleSpace.CellSize;
        var (mesh, height, color) = structure.Kind switch
        {
            SiegeStructureKind.Gate => ((PrimitiveMesh)new BoxMesh { Size = new Vector3(width, 0.9f, depth) }, 0.9f, look.Gate),
            SiegeStructureKind.Keep => (new BoxMesh { Size = new Vector3(width, 1.8f, depth) }, 1.8f, look.Stone.Darkened(0.1f)),
            SiegeStructureKind.Tower => (new CylinderMesh { TopRadius = depth * 0.45f, BottomRadius = depth * 0.5f, Height = 1.5f }, 1.5f, look.Stone.Darkened(0.05f)),
            _ => (new BoxMesh { Size = new Vector3(width, 1.1f, depth) }, 1.1f, look.Stone),
        };
        mesh.Material = Material(Look.Solid, color);
        var node = new MeshInstance3D
        {
            Name = $"{structure.Kind}_{structure.Structure}",
            Mesh = mesh,
            Position = BattleSpace.Center(structure.Rect) + (Vector3.Up * height / 2),
        };
        switch (structure.Kind)
        {
            case SiegeStructureKind.Wall:
                Battlements(node, look.Stone, 0, depth, height / 2);
                break;
            case SiegeStructureKind.Gate:
                // The gatehouse's lintel in the town's stone over the gate.
                node.AddChild(new MeshInstance3D
                {
                    Name = "Lintel",
                    Mesh = new BoxMesh { Size = new Vector3(width * 1.2f, 0.2f, depth), Material = Material(Look.Solid, look.Stone) },
                    Position = Vector3.Up * ((height / 2) + 0.1f),
                });
                break;
            default:
                Roof(node, look, width, depth, height / 2);
                break;
        }

        return node;
    }

    /// <summary>One of the town's houses behind the walls: a block of its stone under its roof.</summary>
    private MeshInstance3D House(SiegeLook look, Vector3 at, float width, float depth, float height)
    {
        var node = new MeshInstance3D
        {
            Name = "House",
            Mesh = new BoxMesh { Size = new Vector3(width, height, depth), Material = Material(Look.Solid, look.Stone.Lerp(look.Yard, 0.35f)) },
            Position = at + (Vector3.Up * height / 2),
        };
        Roof(node, look, width, depth, height / 2);
        return node;
    }

    /// <summary>The town's roof on a block or tower <paramref name="width"/> × <paramref name="depth"/> whose top is <paramref name="top"/> above its centre.</summary>
    private void Roof(Node3D parent, SiegeLook look, float width, float depth, float top)
    {
        var size = Mathf.Max(width, depth);
        var stretch = new Vector3(width / size, 1, depth / size);
        switch (look.Roofs)
        {
            case RoofShape.Cone:
            case RoofShape.Pyramid:
            {
                // A pyramid is a four-sided cone turned so its base is square to the block; the holder stretches either to the block's shape.
                var pyramid = look.Roofs == RoofShape.Pyramid;
                var radius = pyramid ? size * 0.55f * Mathf.Sqrt2 : size * 0.56f;
                var height = size * 0.7f;
                var holder = new Node3D { Name = "Roof", Position = Vector3.Up * (top + (height / 2)), Scale = stretch };
                holder.AddChild(new MeshInstance3D
                {
                    Mesh = new CylinderMesh { TopRadius = 0, BottomRadius = radius, Height = height, RadialSegments = pyramid ? 4 : 16, Rings = 1, Material = Material(Look.Solid, look.Roof) },
                    RotationDegrees = pyramid ? new Vector3(0, 45, 0) : Vector3.Zero,
                });
                parent.AddChild(holder);
                break;
            }

            case RoofShape.Dome:
            {
                // A sphere sunk to its middle in the block below.
                var radius = size * 0.5f;
                var holder = new Node3D { Name = "Roof", Position = Vector3.Up * top, Scale = stretch };
                holder.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = radius, Height = radius * 2, Material = Material(Look.Solid, look.Roof) } });
                parent.AddChild(holder);
                break;
            }

            case RoofShape.Spikes:
            {
                var spikes = new Node3D { Name = "Roof", Position = Vector3.Up * top };
                var material = Material(Look.Solid, look.Roof);
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
                Battlements(parent, look.Roof, width, depth, top);
                break;
        }
    }

    /// <summary>Merlons along the top: round the edge of a block, or in one line along a wall (<paramref name="width"/> 0).</summary>
    private void Battlements(Node3D parent, Color color, float width, float depth, float top)
    {
        const float Merlon = 0.12f;
        var material = Material(Look.Solid, color);
        var points = new List<Vector2>();
        var along = Mathf.Max(1, Mathf.FloorToInt(depth / (Merlon * 2)));
        for (var i = 0; i < along; i++)
        {
            var z = -depth / 2 + (Merlon * 1.5f) + (i * (depth - (Merlon * 3)) / Mathf.Max(1, along - 1));
            if (width == 0)
            {
                points.Add(new Vector2(0, z));
            }
            else
            {
                points.Add(new Vector2(-width / 2 + (Merlon / 2), z));
                points.Add(new Vector2(width / 2 - (Merlon / 2), z));
            }
        }

        if (width > 0)
        {
            var across = Mathf.Max(0, Mathf.FloorToInt(width / (Merlon * 2)) - 2);
            for (var i = 1; i <= across; i++)
            {
                var x = -width / 2 + (i * width / (across + 1));
                points.Add(new Vector2(x, -depth / 2 + (Merlon / 2)));
                points.Add(new Vector2(x, depth / 2 - (Merlon / 2)));
            }
        }

        var battlements = new Node3D { Name = "Battlements", Position = Vector3.Up * (top + (Merlon / 2)) };
        foreach (var point in points)
        {
            battlements.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = Vector3.One * Merlon, Material = material },
                Position = new Vector3(point.X, 0, point.Y),
            });
        }

        parent.AddChild(battlements);
    }

    /// <summary>The material of <paramref name="look"/> in <paramref name="color"/>, made on first use.</summary>
    private StandardMaterial3D Material(Look look, Color color)
    {
        if (_materials.TryGetValue((look, color), out var cached))
        {
            return cached;
        }

        var material = look switch
        {
            Look.Glass => new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                AlbedoColor = color,
            },
            Look.Wash => new StandardMaterial3D { AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 1f },
            Look.Water => new StandardMaterial3D { AlbedoColor = color, Transparency = BaseMaterial3D.TransparencyEnum.Alpha, Roughness = 0.2f },
            Look.GlowingWater => new StandardMaterial3D
            {
                AlbedoColor = color,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                Roughness = 0.8f,
                EmissionEnabled = true,
                Emission = new Color(color.R, color.G, color.B),
                EmissionEnergyMultiplier = 0.7f,
            },
            Look.Bright => new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            _ => new StandardMaterial3D { AlbedoColor = color, Roughness = 1f },
        };
        _materials[(look, color)] = material;
        return material;
    }
}
