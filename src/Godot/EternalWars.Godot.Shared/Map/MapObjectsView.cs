using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// Placeholder map objects on one level. With a viewer, objects come from that player's
/// <see cref="ObjectMemory"/> (last known state under refog, ADR-031); without one (editor) from live state.
/// Blocked cells are boxes, the entrance a flat plate, owned mines carry a flag in the owner's colour,
/// monsters show their creature name.
/// </summary>
public partial class MapObjectsView : Node3D
{
    private readonly Dictionary<MapObjectId, Node3D> _nodes = [];
    private MapViewContext _context = null!;
    private int _level;

    public void Bind(MapViewContext context, int level)
    {
        _context = context;
        _level = level;
        RebuildAll();

        context.Changed += RebuildAll;
        TreeExiting += () => context.Changed -= RebuildAll;
        var events = context.Events;
        this.SubscribeWhileInTree<KnownObjectsChanged>(events, e =>
        {
            if (e.Player == _context.Viewer)
            {
                e.Objects.ToList().ForEach(Rebuild);
            }
        });

        // Live changes matter only without a viewer; with one, memory updates arrive as KnownObjectsChanged.
        this.SubscribeWhileInTree<MapObjectPlaced>(events, e => RebuildLive(e.Object));
        this.SubscribeWhileInTree<MapObjectRemoved>(events, e => RebuildLive(e.Object));
        this.SubscribeWhileInTree<MapObjectChanged>(events, e => RebuildLive(e.Object));
        this.SubscribeWhileInTree<MineOwnerChanged>(events, e => RebuildLive(e.Mine));
    }

    private void RebuildLive(MapObjectId id)
    {
        if (_context.Viewer is null)
        {
            Rebuild(id);
        }
    }

    private void RebuildAll()
    {
        foreach (var node in _nodes.Values)
        {
            node.QueueFree();
        }

        _nodes.Clear();
        foreach (var known in Known())
        {
            Add(known);
        }
    }

    private void Rebuild(MapObjectId id)
    {
        if (_nodes.Remove(id, out var old))
        {
            old.QueueFree();
        }

        if (Known().FirstOrDefault(k => k.Id == id) is { } known)
        {
            Add(known);
        }
    }

    private IEnumerable<KnownObject> Known() =>
        (_context.Viewer is { } viewer ? _context.State.Memory.Of(viewer) : _context.State.Objects.Select(KnownObject.Of))
        .Where(k => k.Position.Level == _level);

    private void Add(KnownObject known)
    {
        var root = new Node3D { Name = $"Object_{known.Id.Value}" };
        var color = Palette.ForObject(known.Definition.Kind);
        foreach (var (dx, dy, cell) in known.Definition.Footprint.OccupiedCells)
        {
            var at = MapSpace.ToWorld(known.Position.Offset(dx, dy));
            if (cell == FootprintCell.Blocked)
            {
                root.AddChild(Box(new Vector3(0.9f, 0.5f, 0.9f), at + (Vector3.Up * 0.25f), color));
            }
            else if (cell == FootprintCell.Visit)
            {
                var height = known.Definition.Kind is MapObjectKind.Monster or MapObjectKind.ResourcePile or MapObjectKind.TreasureChest or MapObjectKind.Artifact ? 0.35f : 0.06f;
                root.AddChild(Box(new Vector3(0.6f, height, 0.6f), at + (Vector3.Up * height / 2), color.Lightened(0.15f)));
            }
        }

        var top = MapSpace.ToWorld(known.Position) + new Vector3((known.Definition.Footprint.Width - 1) / 2f, 0.9f, 0);
        if (known.Definition.Kind is MapObjectKind.Mine or MapObjectKind.Town)
        {
            root.AddChild(Box(new Vector3(0.3f, 0.3f, 0.05f), top, known.Owner is { } owner ? Palette.ForPlayer(owner) : Palette.Neutral));
        }

        if (Caption(known) is { } caption)
        {
            root.AddChild(new Label3D
            {
                Text = caption,
                Position = MapSpace.ToWorld(known.VisitPosition()) + (Vector3.Up * 0.8f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.006f,
                Modulate = Colors.White,
                OutlineSize = 8,
            });
        }

        AddChild(root);
        _nodes[known.Id] = root;
    }

    /// <summary>Monsters show their creature (and, in the editor, the count), towns their name; other objects stay unlabelled.</summary>
    private string? Caption(KnownObject known) => _context.State.FindObject(known.Id) switch
    {
        MonsterGuard monster => _context.Viewer is null && monster.Count is { } count ? $"{CreatureName(monster)} ×{count}" : CreatureName(monster),
        Core.Domain.Towns.Town town => town.Name,
        _ => null,
    };

    private string CreatureName(MonsterGuard monster) =>
        _context.State.Creatures.TryGet(monster.Creature, out var creature) ? creature.Name : monster.Creature;

    private static MeshInstance3D Box(Vector3 size, Vector3 position, Color color) => new()
    {
        Mesh = new BoxMesh { Size = size, Material = new StandardMaterial3D { AlbedoColor = color } },
        Position = position,
    };
}

internal static class KnownObjectExtensions
{
    public static MapPosition VisitPosition(this KnownObject known) =>
        known.Position.Offset(known.Definition.Footprint.VisitOffset.Dx, known.Definition.Footprint.VisitOffset.Dy);
}
