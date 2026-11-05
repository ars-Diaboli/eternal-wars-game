using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Turns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// The holes heroes have dug (ADR-140), as dark pits on the explored cells of the shown level; in the editor (no viewer) also the buried Grail, a
/// gold marker with its name, which players never see. Redrawn when a hole is dug, the Grail moves, a turn starts or the level changes.
/// </summary>
public partial class GrailView : Node3D
{
    private static readonly Color Pit = new(0.12f, 0.09f, 0.06f);
    private MapViewContext _context = null!;

    public void Bind(MapViewContext context)
    {
        _context = context;
        context.Changed += Rebuild;
        TreeExiting += () => context.Changed -= Rebuild;
        this.SubscribeWhileInTree<HoleDug>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<GrailMoved>(context.Events, _ => Rebuild());
        this.SubscribeWhileInTree<TurnStarted>(context.Events, _ => Rebuild());
        Rebuild();
    }

    private void Rebuild()
    {
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var state = _context.State;
        foreach (var hole in state.Grail.Holes.Where(h => h.Level == _context.Level))
        {
            if (_context.Viewer is { } viewer && !state.Fog.IsExplored(viewer, hole))
            {
                continue;
            }

            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.3f, BottomRadius = 0.22f, Height = 0.03f, Material = new StandardMaterial3D { AlbedoColor = Pit } },
                Position = MapSpace.ToWorld(hole) + (Vector3.Up * 0.02f),
            });
        }

        if (_context.Viewer is null && state.Grail.Position is { } grail && grail.Level == _context.Level)
        {
            var at = MapSpace.ToWorld(grail);
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.18f, BottomRadius = 0.1f, Height = 0.3f, Material = new StandardMaterial3D { AlbedoColor = Colors.Gold } },
                Position = at + (Vector3.Up * 0.15f),
            });
            AddChild(new Label3D
            {
                Text = "Grail",
                Position = at + (Vector3.Up * 0.6f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.005f,
                Modulate = Colors.Gold,
                OutlineSize = 8,
            });
        }
    }
}
