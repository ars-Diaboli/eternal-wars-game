using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;
using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>
/// Placeholder creature stack on the battlefield: a base plate in the side's colour covering the footprint, a body
/// whose shape tells the role (walkers are capsules, shooters cylinders, flyers hovering spheres) and whose colour comes
/// from the creature id, a nose pointing at the enemy side, the stack count and an active-unit ring.
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

    public int Size { get; private set; }

    /// <summary>The body, which attacks lunge and hits flash.</summary>
    public Node3D Body { get; } = new() { Name = "Body" };

    /// <summary>Height of the body's centre: where projectiles start and land.</summary>
    public float BodyHeight { get; private set; }

    public Color BaseColor { get; private set; }

    public void Bind(int unit, BattleSide side, CreatureDefinition creature, int count, int size, Color sideColor)
    {
        Unit = unit;
        Side = side;
        Creature = creature;
        Size = size;
        Name = $"Unit_{unit}";

        var extent = size * BattleSpace.CellSize;
        AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh
            {
                TopRadius = extent * 0.45f,
                BottomRadius = extent * 0.45f,
                Height = 0.04f,
                Material = new StandardMaterial3D { AlbedoColor = sideColor },
            },
            Position = new Vector3(0, 0.02f, 0),
        });

        BaseColor = ColorOf(creature.Id);
        _bodyMaterial = new StandardMaterial3D { AlbedoColor = BaseColor, Roughness = 0.8f };
        var scale = extent / (2 * BattleSpace.CellSize); // 1 for small, 1.5 medium, 2 large
        var (mesh, height) = creature switch
        {
            { IsFlying: true } => ((PrimitiveMesh)new SphereMesh { Radius = 0.28f * scale, Height = 0.56f * scale }, 0.75f * scale),
            { IsRanged: true } => (new CylinderMesh { TopRadius = 0.16f * scale, BottomRadius = 0.24f * scale, Height = 0.8f * scale }, 0.4f * scale),
            _ => (new CapsuleMesh { Radius = 0.24f * scale, Height = 0.85f * scale }, 0.43f * scale),
        };
        mesh.Material = _bodyMaterial;
        BodyHeight = height;
        AddChild(Body);
        Body.AddChild(new MeshInstance3D { Mesh = mesh, Position = new Vector3(0, height, 0) });

        // A nose towards the enemy: attackers face +X, defenders −X.
        var facing = side == BattleSide.Attacker ? 1 : -1;
        Body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.18f, 0.08f, 0.08f) * scale, Material = new StandardMaterial3D { AlbedoColor = sideColor } },
            Position = new Vector3(facing * 0.24f * scale, height + (0.12f * scale), 0),
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
        _count.Position = new Vector3(0, (height * 2) + 0.25f, 0);
        AddChild(_count);
        SetCount(count);
    }

    public bool Active
    {
        get => _ring.Visible;
        set => _ring.Visible = value;
    }

    public void SetCount(int count) => _count.Text = count.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The body material, for hit flashes.</summary>
    public StandardMaterial3D BodyMaterial => _bodyMaterial;

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
