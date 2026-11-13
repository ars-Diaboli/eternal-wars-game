using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// The map editor's markers for the invisible event cells (EVT-2, EVT-6): a violet plate with the event's name on every cell of the shown level. The
/// game never adds this view (players do not see event cells). Redrawn when the events, the level or the ground change.
/// </summary>
public partial class EventCellsView : Node3D
{
    private static readonly Color Marker = new(0.65f, 0.35f, 0.95f, 0.75f);
    private MapViewContext _context = null!;
    private string? _highlighted;

    /// <summary>The event whose cells stand out (the one selected in the Events tab), or null.</summary>
    public string? Highlighted
    {
        get => _highlighted;
        set
        {
            _highlighted = value;
            Rebuild();
        }
    }

    public void Bind(MapViewContext context)
    {
        _context = context;
        context.Changed += Rebuild;
        TreeExiting += () => context.Changed -= Rebuild;
        this.SubscribeWhileInTree<MapEventsChanged>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<GroundHeightsChanged>(context.Events, _ => Rebuild());
        Rebuild();
    }

    private void Rebuild()
    {
        if (_context is null)
        {
            return;
        }

        this.ClearChildren();

        var material = new StandardMaterial3D { AlbedoColor = Marker, Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        var highlighted = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.85f, 0.3f, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha };
        foreach (var cell in _context.State.MapEvents.Set.Cells.Where(c => c.Level == _context.Level))
        {
            var at = MapSpace.ToGround(_context.State.Map, cell.Position);
            AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.7f, 0.04f, 0.7f), Material = cell.Event == _highlighted ? highlighted : material },
                Transform = new Transform3D(MapSpace.OnSlope(_context.State.Map, cell.Position), at + (MapSpace.GroundNormal(_context.State.Map, cell.Position) * 0.05f)),
            });
            AddChild(new Label3D
            {
                Text = cell.Event,
                Position = at + (Vector3.Up * 0.35f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.005f,
                Modulate = new Color(0.9f, 0.8f, 1f),
                OutlineSize = 8,
            });
        }
    }
}
