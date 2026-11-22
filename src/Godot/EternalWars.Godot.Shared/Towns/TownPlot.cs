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
/// town's look), with nothing to pick. Without a <c>Hover</c> the plot picks by a box around what it shows.
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
    private (PlotState State, int Level)? _shown;

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

    /// <summary>The box around the building shown, in the marker's space (empty while none is).</summary>
    public Aabb ShownBounds { get; private set; }

    /// <summary>The <c>Focus</c> marker, or a point above the plot.</summary>
    public Vector3 Focus => Marker.GetNodeOrNull<Node3D>("Focus")?.GlobalPosition ?? Marker.GlobalPosition + new Vector3(0, 3, 0);

    /// <summary>Shows the plot as <paramref name="info"/> says; rebuilt only when its state or level changed.</summary>
    public void Show(PlotInfo info, BuildingKind kind, int? tier, string townType)
    {
        Info = info;
        if (_shown == (info.State, info.Level))
        {
            return;
        }

        _shown = (info.State, info.Level);
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
            _generatedHover = new Area3D { Name = "GeneratedHover", Monitoring = false, Monitorable = false, InputRayPickable = false };
            _generatedHover.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = bounds.Size }, Position = bounds.GetCenter() });
            Marker.AddChild(_generatedHover);
        }
    }

    /// <summary>How far along the ray its first hover shape is, or null when the ray misses (or nothing stands here).</summary>
    public float? Hit(Vector3 origin, Vector3 direction)
    {
        if (!IsShown || (_hover ?? _generatedHover) is not { } area)
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

    private void Clear()
    {
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
