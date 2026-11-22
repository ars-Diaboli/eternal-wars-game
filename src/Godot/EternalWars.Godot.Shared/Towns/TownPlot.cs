using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Battle;
using Godot;

namespace EternalWars.Godot.Shared.Towns;

/// <summary>
/// One plot of a town layout (ADR-297): a <c>Marker3D</c> named <c>plot_&lt;id&gt;</c> with optional children: <c>Model</c> (the layout's own greybox
/// of the building), <c>Hover</c> (an <see cref="Area3D"/> whose <see cref="CollisionShape3D"/>s are what the pointer picks) and <c>Focus</c> (where
/// the camera looks at the building). A built plot shows, first found: the model for its level (<c>models/towns/&lt;town&gt;/&lt;id&gt;_&lt;level&gt;.glb</c>,
/// or the nearest lower level's), the layout's <c>Model</c>, or a procedural block sized by the building's kind with a roof and a sign
/// (<see cref="PlotBlock"/>). What a plot shows follows its state (TOWN-15, ADR-291, ADR-301): a plot not built, or a closed line, is unused
/// land (its <c>Land</c> child when the layout has one, else the layout's ground), with nothing to pick; a plot built today stands in
/// scaffolding; a closed line once the town has the Capitol shows the citizens' homes (its <c>Homes</c> child, else a few greybox houses in the
/// town's look), with nothing to pick. Without a <c>Hover</c> the plot picks by a box around what it shows. Unused land the town can still build
/// on (<see cref="PlotInfo.Buildable"/>) picks too (ADR-304), by its <c>Hover</c> or a box the size of its greybox block. The scene outlines the
/// building under the pointer (<see cref="Outline"/>), shows a translucent ghost of a level not built yet (<see cref="ShowGhost"/>) and raises
/// a new building out of the ground in its construction scene (<see cref="BeginRise"/>).
/// </summary>
internal sealed class TownPlot
{
    /// <summary>Where the building models are: one folder per town type.</summary>
    public const string ModelFolder = "res://shared/models/towns/";

    private readonly Node3D? _model;
    private readonly Area3D? _hover;
    private readonly Node3D? _land;
    private readonly Node3D? _homes;
    private Node3D? _look;
    private Node3D? _extra;
    private Area3D? _generatedHover;
    private Node3D? _ghost;
    private (PlotState State, int Level, bool Buildable)? _shown;
    private readonly List<(Node3D Node, Vector3 At)> _rising = [];
    private float _riseHeight;
    private (BuildingKind Kind, int? Tier, string TownType) _kind = (BuildingKind.Core, null, "");

    public TownPlot(Marker3D marker)
    {
        Marker = marker;
        _model = marker.GetNodeOrNull<Node3D>("Model");
        _hover = marker.GetNodeOrNull<Area3D>("Hover");
        _land = marker.GetNodeOrNull<Node3D>("Land");
        _homes = marker.GetNodeOrNull<Node3D>("Homes");
        if (_homes is not null)
        {
            _homes.Visible = false;
        }
        foreach (var area in new[] { _hover }.OfType<Area3D>())
        {
            (area.Monitoring, area.Monitorable, area.InputRayPickable) = (false, false, false); // picked by TownScene3D's own ray, not physics
        }
    }

    public Marker3D Marker { get; }

    /// <summary>The plot as the town has it; null until the town was shown.</summary>
    public PlotInfo? Info { get; private set; }

    /// <summary>Whether a building stands here (shown and pickable).</summary>
    public bool IsShown { get; private set; }

    /// <summary>Whether the pointer picks the plot: a building stands here, or the town can still build on its land.</summary>
    public bool IsPickable => IsShown || Info is { Buildable: true };

    /// <summary>The ghost's level while one shows (<see cref="ShowGhost"/>).</summary>
    public int? GhostLevel { get; private set; }

    /// <summary>The box around the building shown, in the marker's space (empty while none is).</summary>
    public Aabb ShownBounds { get; private set; }

    /// <summary>The <c>Focus</c> marker, or a point above the plot.</summary>
    public Vector3 Focus => Marker.GetNodeOrNull<Node3D>("Focus")?.GlobalPosition ?? Marker.GlobalPosition + new Vector3(0, 3, 0);

    /// <summary>Shows the plot as <paramref name="info"/> says; rebuilt only when its state or level changed.</summary>
    public void Show(PlotInfo info, BuildingKind kind, int? tier, string townType)
    {
        Info = info;
        _kind = (kind, tier, townType);
        if (_shown == (info.State, info.Level, info.Buildable))
        {
            return;
        }

        _shown = (info.State, info.Level, info.Buildable);
        EndRise();
        Clear();
        IsShown = info.State is PlotState.Built or PlotState.BuiltToday;
        if (_land is not null)
        {
            _land.Visible = info.State is PlotState.NotBuilt or PlotState.Closed;
        }

        if (info.State == PlotState.CitizensHomes)
        {
            if (_homes is not null)
            {
                _homes.Visible = true;
            }
            else
            {
                var block = PlotBlock.Size(kind, tier);
                _extra = SceneryKit.Homes(SiegeLook.Of(townType), new Vector2(Mathf.Max(block.X, 7), Mathf.Max(block.Z, 7)));
                Marker.AddChild(_extra);
            }

            return;
        }

        if (!IsShown)
        {
            if (info.Buildable && _hover is null)
            {
                AddGeneratedHover(BlockBounds(kind, tier)); // the land the building would stand on
            }

            return;
        }

        Aabb bounds;
        if (LoadModel(townType, info) is { } model)
        {
            _look = model;
            Marker.AddChild(model);
            bounds = BoundsOf(model);
        }
        else if (_model is not null)
        {
            _model.Visible = true;
            bounds = BoundsOf(_model);
        }
        else
        {
            var block = PlotBlock.Build(info.Name, kind, tier);
            _look = block.Node;
            Marker.AddChild(block.Node);
            bounds = block.Bounds;
        }

        ShownBounds = bounds;
        if (info.State == PlotState.BuiltToday)
        {
            _extra = SceneryKit.Scaffold(bounds);
            Marker.AddChild(_extra);
        }

        if (_hover is null)
        {
            AddGeneratedHover(bounds);
        }
    }

    /// <summary>
    /// Draws <paramref name="overlay"/> (the scene's bronze outline) over every mesh of the building shown, or takes it off (null). The citizens'
    /// homes and the scaffolding are not the building, and stay as they are.
    /// </summary>
    public void Outline(Material? overlay)
    {
        foreach (var look in new[] { _look, _model is { Visible: true } ? _model : null }.OfType<Node3D>())
        {
            foreach (var mesh in Meshes(look))
            {
                mesh.MaterialOverlay = overlay;
            }
        }
    }

    /// <summary>
    /// Shows a translucent ghost of the plot's building at <paramref name="level"/> (<paramref name="material"/> on every mesh): the model for that
    /// level, the layout's <c>Model</c>, or the greybox block, as <see cref="Show"/> would show it once built. Its sign stays readable.
    /// </summary>
    public void ShowGhost(int level, Material material)
    {
        if (Info is not { } info || GhostLevel == level)
        {
            return;
        }

        HideGhost();
        var (kind, tier, townType) = _kind;
        var ghost = LoadModel(townType, info with { Level = level })
                    ?? (_model?.Duplicate() as Node3D)
                    ?? PlotBlock.Build(info.Name, kind, tier).Node;
        ghost.Name = "Ghost";
        ghost.Visible = true; // the layout's Model, duplicated, keeps its place under the marker
        foreach (var mesh in Meshes(ghost))
        {
            (mesh.MaterialOverride, mesh.MaterialOverlay, mesh.CastShadow) = (material, null, GeometryInstance3D.ShadowCastingSetting.Off);
        }

        _ghost = ghost;
        GhostLevel = level;
        Marker.AddChild(ghost);
    }

    public void HideGhost()
    {
        if (_ghost is not null)
        {
            Marker.RemoveChild(_ghost);
            _ghost.QueueFree();
        }

        (_ghost, GhostLevel) = (null, null);
    }

    /// <summary>
    /// The construction scene starts (ADR-304): what the plot shows now (the building, or the citizens' homes; not the scaffolding) sinks into the
    /// ground, to come up again by <see cref="Rise"/>. False when nothing shows here.
    /// </summary>
    public bool BeginRise()
    {
        EndRise();
        var nodes = new[] { _look, _model is { Visible: true } ? _model : null, _homes is { Visible: true } ? _homes : null }
            .Append(Info?.State == PlotState.CitizensHomes ? _extra : null)
            .OfType<Node3D>()
            .ToList();
        if (nodes.Count == 0)
        {
            return false;
        }

        _riseHeight = nodes.Select(BoundsOf).Aggregate((a, b) => a.Merge(b)).End.Y + 0.2f;
        _rising.AddRange(nodes.Select(n => (n, n.Position)));
        Rise(0);
        return true;
    }

    /// <summary>How far out of the ground what <see cref="BeginRise"/> sank has come, from 0 (under it) to 1 (where it stands).</summary>
    public void Rise(float progress)
    {
        var drop = Vector3.Up * ((1 - Mathf.Clamp(progress, 0, 1)) * _riseHeight);
        foreach (var (node, at) in _rising)
        {
            if (GodotObject.IsInstanceValid(node))
            {
                node.Position = at - drop;
            }
        }
    }

    /// <summary>Everything risen stands where it stood before (the construction scene ends, or is cut short).</summary>
    public void EndRise()
    {
        Rise(1);
        _rising.Clear();
    }

    /// <summary>How far along the ray its first hover shape is, or null when the ray misses (or nothing here can be picked).</summary>
    public float? Hit(Vector3 origin, Vector3 direction)
    {
        if (!IsPickable || (_hover ?? _generatedHover) is not { } area)
        {
            return null;
        }

        float? nearest = null;
        foreach (var shape in area.FindChildren("*", nameof(CollisionShape3D), recursive: true, owned: false).OfType<CollisionShape3D>())
        {
            if (shape.Shape is null || shape.Disabled)
            {
                continue;
            }

            var box = shape.Shape is BoxShape3D b ? new Aabb(-b.Size / 2, b.Size) : shape.Shape.GetDebugMesh().GetAabb();
            var toShape = shape.GlobalTransform.AffineInverse();
            if (RayBox(toShape * origin, toShape.Basis * direction, box) is { } t && (nearest is null || t < nearest))
            {
                nearest = t;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Where a ray (<paramref name="origin"/> + t · <paramref name="direction"/>, t ≥ 0) enters <paramref name="box"/>: t, or null when it misses.
    /// An origin inside the box meets it at 0.
    /// </summary>
    public static float? RayBox(Vector3 origin, Vector3 direction, Aabb box)
    {
        var (near, far) = (0f, float.MaxValue);
        var end = box.End;
        for (var axis = 0; axis < 3; axis++)
        {
            if (Mathf.Abs(direction[axis]) < 1e-8f)
            {
                if (origin[axis] < box.Position[axis] || origin[axis] > end[axis])
                {
                    return null;
                }

                continue;
            }

            var t1 = (box.Position[axis] - origin[axis]) / direction[axis];
            var t2 = (end[axis] - origin[axis]) / direction[axis];
            if (t1 > t2)
            {
                (t1, t2) = (t2, t1);
            }

            (near, far) = (Mathf.Max(near, t1), Mathf.Min(far, t2));
            if (near > far)
            {
                return null;
            }
        }

        return near;
    }

    /// <summary>A pick box for a plot without its own <c>Hover</c>: <paramref name="bounds"/>, in the marker's space.</summary>
    private void AddGeneratedHover(Aabb bounds)
    {
        _generatedHover = new Area3D { Name = "GeneratedHover", Monitoring = false, Monitorable = false, InputRayPickable = false };
        _generatedHover.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = bounds.Size }, Position = bounds.GetCenter() });
        Marker.AddChild(_generatedHover);
    }

    /// <summary>The box the greybox block of a building of <paramref name="kind"/> would fill, roof and all.</summary>
    private static Aabb BlockBounds(BuildingKind kind, int? tier)
    {
        var size = PlotBlock.Size(kind, tier);
        var height = size.Y * 1.3f;
        return new Aabb(new Vector3(-size.X / 2, 0, -size.Z / 2), new Vector3(size.X, height, size.Z));
    }

    /// <summary>The meshes under <paramref name="look"/> (and itself) that show it: not its signs or lights.</summary>
    private static IEnumerable<GeometryInstance3D> Meshes(Node3D look) =>
        look.FindChildren("*", nameof(GeometryInstance3D), recursive: true, owned: false)
            .Prepend(look)
            .OfType<GeometryInstance3D>()
            .Where(g => g is MeshInstance3D or CsgShape3D);

    private void Clear()
    {
        HideGhost();
        if (_model is not null)
        {
            _model.Visible = false;
        }

        if (_homes is not null)
        {
            _homes.Visible = false;
        }

        foreach (var node in new[] { _look, _extra, _generatedHover })
        {
            if (node is not null)
            {
                Marker.RemoveChild(node);
                node.QueueFree();
            }
        }

        _look = null;
        _extra = null;
        ShownBounds = default;
        _generatedHover = null;
    }

    /// <summary>The model for the plot's level, or the nearest lower level's; null when there is none.</summary>
    private static Node3D? LoadModel(string townType, PlotInfo info)
    {
        for (var level = info.Level; level >= 1; level--)
        {
            var path = $"{ModelFolder}{townType}/{info.Id}_{level}.glb";
            if (ResourceLoader.Exists(path))
            {
                return GD.Load<PackedScene>(path).Instantiate<Node3D>();
            }
        }

        return null;
    }

    /// <summary>The box around what <paramref name="look"/> shows, in the marker's space.</summary>
    private Aabb BoundsOf(Node3D look)
    {
        var toMarker = Marker.GlobalTransform.AffineInverse();
        Aabb? bounds = null;
        foreach (var visual in look.FindChildren("*", nameof(VisualInstance3D), recursive: true, owned: false).OfType<VisualInstance3D>().Prepend(look as VisualInstance3D).OfType<VisualInstance3D>())
        {
            if (visual is Light3D)
            {
                continue;
            }

            var box = (toMarker * visual.GlobalTransform) * visual.GetAabb();
            bounds = bounds is { } sum ? sum.Merge(box) : box;
        }

        return bounds ?? new Aabb(new Vector3(-2, 0, -2), new Vector3(4, 4, 4));
    }
}
