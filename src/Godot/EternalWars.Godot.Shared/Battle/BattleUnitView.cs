using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>
/// Placeholder creature stack on the battlefield: a base plate in the side's colour covering the footprint, a body
/// whose shape tells the role (walkers are capsules, shooters cylinders, flyers hovering spheres) and whose colour comes
/// from the creature id, a nose pointing at the enemy side, the stack count, the top creature's health bar (ADR-145) and an active-unit ring. War machines are
/// a wooden box: with a barrel for offensive machines, a pale roof for the tent, wheels for the cart. A machine's footprint is one
/// column (1 × 3 or 1 × 2, ADR-073); its model is sized by the footprint's length and overhangs the grid's edge.
/// <see cref="BattleView3D"/> moves and animates it; the view never reads the core state itself.
/// </summary>
public partial class BattleUnitView : Node3D
{
    private readonly MeshInstance3D _ring = new() { Visible = false };
    private readonly Label3D _count = new();
    private StandardMaterial3D _bodyMaterial = null!;

    public int Unit { get; private set; }

    public BattleSide Side { get; private set; }

    public CreatureDefinition Creature { get; private set; } = null!;

    /// <summary>Footprint in cells (creatures are square).</summary>
    public int Width { get; private set; }

    public int Height { get; private set; }

    /// <summary>The body, which attacks lunge and hits flash.</summary>
    public Node3D Body { get; } = new() { Name = "Body" };

    /// <summary>Height of the body's centre: where projectiles start and land.</summary>
    public float BodyHeight { get; private set; }

    public Color BaseColor { get; private set; }

    public WarMachineKind? Machine { get; private set; }

    public void Bind(int unit, BattleSide side, CreatureDefinition creature, int count, int width, int height, Color sideColor, WarMachineKind? machine = null)
    {
        Machine = machine;
        Unit = unit;
        Side = side;
        Creature = creature;
        Width = width;
        Height = height;
        Name = $"Unit_{unit}";

        var extent = Math.Max(width, height) * BattleSpace.CellSize;
        var plateMaterial = _plateMaterial;
        plateMaterial.AlbedoColor = sideColor;
        _noseMaterial.AlbedoColor = sideColor;
        AddChild(new MeshInstance3D
        {
            Mesh = width == height
                ? new CylinderMesh { TopRadius = extent * 0.45f, BottomRadius = extent * 0.45f, Height = 0.04f, Material = plateMaterial }
                : new BoxMesh { Size = new Vector3(width * BattleSpace.CellSize * 0.9f, 0.04f, height * BattleSpace.CellSize * 0.9f), Material = plateMaterial },
            Position = new Vector3(0, 0.02f, 0),
        });

        BaseColor = machine is null ? ColorOf(creature.Id) : new Color(0.50f, 0.36f, 0.22f);
        _bodyMaterial = new StandardMaterial3D { AlbedoColor = BaseColor, Roughness = 0.8f };
        var scale = extent / (2 * BattleSpace.CellSize); // 1 for small, 1.5 medium, 2 large
        var (mesh, bodyHeight) = machine is not null
            ? ((PrimitiveMesh)new BoxMesh { Size = new Vector3(0.7f, 0.4f, 0.55f) * scale }, 0.2f * scale)
            : creature switch
        {
            { IsFlying: true } => ((PrimitiveMesh)new SphereMesh { Radius = 0.28f * scale, Height = 0.56f * scale }, 0.75f * scale),
            { IsRanged: true } => (new CylinderMesh { TopRadius = 0.16f * scale, BottomRadius = 0.24f * scale, Height = 0.8f * scale }, 0.4f * scale),
            _ => (new CapsuleMesh { Radius = 0.24f * scale, Height = 0.85f * scale }, 0.43f * scale),
        };
        mesh.Material = _bodyMaterial;
        BodyHeight = bodyHeight;
        AddChild(Body);
        Body.AddChild(new MeshInstance3D { Mesh = mesh, Position = new Vector3(0, bodyHeight, 0) });

        // A nose towards the enemy: attackers face +X, defenders −X.
        var facing = side == BattleSide.Attacker ? 1 : -1;

        // A machine's model is as long as its footprint and sticks out over its side's edge (ADR-073).
        Body.Position = new Vector3(-facing * (Math.Max(width, height) - width) * 0.5f * BattleSpace.CellSize, 0, 0);
        if (machine is { } kind)
        {
            Body.AddChild(MachinePart(kind, scale, facing, bodyHeight));
        }

        Body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.08f, 0.08f) * scale, Material = _noseMaterial },
            Position = new Vector3(facing * 0.24f * scale, bodyHeight + (0.12f * scale), 0),
        });

        _ring.Mesh = new TorusMesh
        {
            InnerRadius = extent * 0.46f,
            OuterRadius = extent * 0.52f,
            Material = new StandardMaterial3D { AlbedoColor = Colors.White, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
        };
        _ring.Position = new Vector3(0, 0.05f, 0);
        AddChild(_ring);

        _count.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
        _count.PixelSize = 0.005f;
        _count.FontSize = 40;
        _count.OutlineSize = 10;
        _count.NoDepthTest = true;
        _count.Position = new Vector3(0, (bodyHeight * 2) + 0.36f, 0);
        AddChild(_count);
        SetCount(count);

        // The health bar under the count (ADR-145): a dark back and a fill that shrinks towards its left end.
        var barY = (bodyHeight * 2) + 0.14f;
        AddChild(new MeshInstance3D
        {
            Mesh = new QuadMesh { Size = new Vector2(BarWidth + 0.03f, BarHeight + 0.03f), Material = BarMaterial(new Color(0.05f, 0.05f, 0.05f, 0.85f), 0) },
            Position = new Vector3(0, barY, 0),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        _barFill.Material = _barMaterial;
        _bar.Mesh = _barFill;
        _bar.Position = new Vector3(0, barY, 0);
        _bar.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_bar);
        SetHealth(creature.Health);
    }

    private readonly StandardMaterial3D _plateMaterial = new();
    private readonly StandardMaterial3D _noseMaterial = new();

    /// <summary>
    /// The stack fights for another side now (a control spell, ADR-150): the plate and nose take that side's colour and the body turns to face
    /// that side's enemy. Only creature stacks change sides.
    /// </summary>
    public void SetSide(BattleSide side, Color sideColor)
    {
        if (side == Side)
        {
            return;
        }

        Side = side;
        _plateMaterial.AlbedoColor = sideColor;
        _noseMaterial.AlbedoColor = sideColor;
        Body.RotationDegrees = Body.RotationDegrees with { Y = Body.RotationDegrees.Y + 180 };
    }

    private const float BarWidth = 0.6f;
    private const float BarHeight = 0.09f;
    private readonly MeshInstance3D _bar = new() { Name = "HealthBar" };
    private readonly QuadMesh _barFill = new();
    private readonly StandardMaterial3D _barMaterial = BarMaterial(Colors.Green, 1);

    /// <summary>The top creature's hit points (ADR-145): the bar's length, and green, yellow or red.</summary>
    public void SetHealth(int topHealth)
    {
        var fraction = StackHealth.Fraction(topHealth, Creature.Health);
        var width = BarWidth * fraction;
        _barFill.Size = new Vector2(Math.Max(width, 0.001f), BarHeight);
        _barFill.CenterOffset = new Vector3(-(BarWidth - width) / 2, 0, 0);
        _barMaterial.AlbedoColor = StackHealth.Band(topHealth, Creature.Health) switch
        {
            HealthBand.Green => new Color(0.30f, 0.80f, 0.25f),
            HealthBand.Yellow => new Color(0.95f, 0.80f, 0.20f),
            _ => new Color(0.90f, 0.20f, 0.15f),
        };
    }

    /// <summary>An unlit quad that faces the camera and draws over the field; <paramref name="priority"/> puts the fill over the back.</summary>
    private static StandardMaterial3D BarMaterial(Color color, int priority) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        NoDepthTest = true,
        RenderPriority = priority,
    };

    public bool Active
    {
        get => _ring.Visible;
        set => _ring.Visible = value;
    }

    public void SetCount(int count) => _count.Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The body material, for hit flashes.</summary>
    public StandardMaterial3D BodyMaterial => _bodyMaterial;

    /// <summary>Barrel (offensive machines), roof (tent) or wheels (cart) on top of the machine's box.</summary>
    private static MeshInstance3D MachinePart(WarMachineKind kind, float scale, int facing, float height) => kind switch
    {
        WarMachineKind.HealingTent => new MeshInstance3D
        {
            Mesh = new PrismMesh { Size = new Vector3(0.75f, 0.45f, 0.6f) * scale, Material = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.88f, 0.8f) } },
            Position = new Vector3(0, (height * 2) + (0.22f * scale), 0),
        },
        WarMachineKind.AmmoCart => new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.16f * scale, BottomRadius = 0.16f * scale, Height = 0.66f * scale, Material = new StandardMaterial3D { AlbedoColor = new Color(0.2f, 0.16f, 0.12f) } },
            Position = new Vector3(0, 0.16f * scale, 0),
            RotationDegrees = new Vector3(90, 0, 0),
        },
        _ => new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.07f * scale, BottomRadius = 0.1f * scale, Height = 0.8f * scale, Material = new StandardMaterial3D { AlbedoColor = new Color(0.25f, 0.25f, 0.28f), Metallic = 0.6f } },
            Position = new Vector3(facing * 0.3f * scale, (height * 2) + (0.1f * scale), 0),
            RotationDegrees = new Vector3(0, 0, -facing * 75),
        },
    };

    /// <summary>A stable, fairly saturated colour per creature id, so different creatures are told apart.</summary>
    private static Color ColorOf(string id)
    {
        var hash = 17;
        foreach (var c in id)
        {
            hash = unchecked((hash * 31) + c);
        }

        return Color.FromHsv((hash & 0x7fffffff) % 360 / 360f, 0.45f, 0.75f);
    }
}
