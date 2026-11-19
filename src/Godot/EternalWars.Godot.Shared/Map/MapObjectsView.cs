using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
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
/// <remarks>
/// The boxes, plates and flags of all the level's objects are instances of one <see cref="MultiMesh"/> per size, coloured per instance, so a map
/// of a few thousand objects is a handful of draw calls; only the quest objects' shapes are nodes of their own. A caption shows within
/// <see cref="CaptionRange"/> of the camera, and on the object under the pointer (<see cref="MapViewContext.Pointed"/>) from any distance.
/// </remarks>
public partial class MapObjectsView : Node3D
{
    /// <summary>How far from the camera captions show (world units; the camera is 6 to 40 from what it looks at, 16 at first).</summary>
    public const float CaptionRange = 20f;

    private readonly Dictionary<MapObjectId, Node3D> _nodes = [];
    private readonly Dictionary<MapObjectId, List<(CellBatch Batch, int Slot)>> _cells = [];
    private readonly Dictionary<MapObjectId, Label3D> _captions = [];
    private readonly Dictionary<MapPosition, MapObjectId> _objectAt = [];
    private readonly Dictionary<MapObjectId, List<MapPosition>> _tilesOf = [];
    private readonly Dictionary<Vector3, CellBatch> _batches = [];
    private readonly MeshCache _meshes = new();
    private readonly StandardMaterial3D _cellMaterial = new() { VertexColorUseAsAlbedo = true, VertexColorIsSrgb = true };
    private MapViewContext _context = null!;
    private int _level;
    private Label3D? _pointedCaption;

    public void Bind(MapViewContext context, int level)
    {
        _context = context;
        _level = level;
        RebuildAll();

        context.ViewerChanged += RebuildAll;
        context.PointedChanged += ShowPointedCaption;
        TreeExiting += () =>
        {
            context.ViewerChanged -= RebuildAll;
            context.PointedChanged -= ShowPointedCaption;
        };
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
        this.SubscribeWhileInTree<DwellingOwnerChanged>(events, e => RebuildLive(e.Dwelling));
        this.SubscribeWhileInTree<ProviderOwnerChanged>(events, e => RebuildLive(e.Provider));
        // The editor reshaped the ground: what stands there is placed again on it.
        this.SubscribeWhileInTree<GroundHeightsChanged>(events, e =>
        {
            if (e.Level == _level)
            {
                Known().Where(k => k.Definition.Footprint.OccupiedCells.Any(c => ReliefSurface.Reach(e.Area).Touches(k.Position.Offset(c.Dx, c.Dy))))
                    .Select(k => k.Id).ToList().ForEach(Rebuild);
            }
        });
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
        _cells.Clear();
        _captions.Clear();
        _objectAt.Clear();
        _tilesOf.Clear();
        _pointedCaption = null;
        foreach (var batch in _batches.Values)
        {
            batch.Clear();
        }

        foreach (var known in Known())
        {
            Add(known);
        }

        ShowPointedCaption();
    }

    private void Rebuild(MapObjectId id)
    {
        Remove(id);
        if (Known().FirstOrDefault(k => k.Id == id) is { } known)
        {
            Add(known);
        }

        ShowPointedCaption();
    }

    private void Remove(MapObjectId id)
    {
        if (_nodes.Remove(id, out var old))
        {
            old.QueueFree();
        }

        if (_cells.Remove(id, out var cells))
        {
            foreach (var (batch, slot) in cells)
            {
                batch.Remove(slot);
            }
        }

        if (_captions.Remove(id, out var caption) && caption == _pointedCaption)
        {
            _pointedCaption = null;
        }

        foreach (var tile in _tilesOf.Remove(id, out var tiles) ? tiles : [])
        {
            if (_objectAt.GetValueOrDefault(tile) == id)
            {
                _objectAt.Remove(tile);
            }
        }
    }

    private IEnumerable<KnownObject> Known() =>
        (_context.Viewer is { } viewer ? _context.State.Memory.Of(viewer) : _context.State.Objects.Select(KnownObject.Of))
        .Where(k => k.Position.Level == _level);

    private void Add(KnownObject known)
    {
        var map = _context.State.Map;
        var color = known.Definition.Colour is { } colour ? Palette.ForColour(colour) : Palette.ForObject(known.Definition.Kind);
        var cells = new List<(CellBatch Batch, int Slot)>();
        var tiles = new List<MapPosition>();
        void Cell(Vector3 size, Vector3 position, Color tint) => cells.Add(Batch(size).Add(position, tint));
        foreach (var (dx, dy, cell) in known.Definition.Footprint.OccupiedCells)
        {
            var tile = known.Position.Offset(dx, dy);
            _objectAt[tile] = known.Id;
            tiles.Add(tile);
            var at = MapSpace.ToGround(map, tile);
            if (cell == FootprintCell.Blocked)
            {
                Cell(new Vector3(0.9f, 0.5f, 0.9f), at + (Vector3.Up * 0.25f), color);
            }
            else if (cell == FootprintCell.Visit)
            {
                var height = known.Definition.Kind is MapObjectKind.Monster or MapObjectKind.ResourcePile or MapObjectKind.TreasureChest or MapObjectKind.Artifact or MapObjectKind.Loot ? 0.35f : 0.06f;
                Cell(new Vector3(0.6f, height, 0.6f), at + (Vector3.Up * height / 2), color.Lightened(0.15f));
            }
        }

        var top = MapSpace.ToGround(map, known.Position) + new Vector3((known.Definition.Footprint.Width - 1) / 2f, 0.9f, 0);
        if (known.Definition.Kind is MapObjectKind.Mine or MapObjectKind.Town or MapObjectKind.Dwelling or MapObjectKind.ResourceProvider)
        {
            Cell(new Vector3(0.3f, 0.3f, 0.05f), top, known.Owner is { } owner ? Palette.ForPlayer(_context.State, owner) : Palette.Neutral);
        }

        _cells[known.Id] = cells;
        _tilesOf[known.Id] = tiles;
        var root = new Node3D { Name = $"Object_{known.Id.Value}" };
        AddShape(root, known, color, MapSpace.ToGround(map, known.Position));
        if (Caption(known) is { } caption)
        {
            var label = new Label3D
            {
                Text = caption,
                Position = MapSpace.ToGround(map, known.VisitPosition()) + (Vector3.Up * 0.8f),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                PixelSize = 0.006f,
                Modulate = Colors.White,
                OutlineSize = 8,
                VisibilityRangeEnd = CaptionRange,
                VisibilityRangeEndMargin = 2f,
                VisibilityRangeFadeMode = GeometryInstance3D.VisibilityRangeFadeModeEnum.Self,
            };
            root.AddChild(label);
            _captions[known.Id] = label;
        }

        if (root.GetChildCount() == 0)
        {
            root.Free();
            return;
        }

        AddChild(root);
        _nodes[known.Id] = root;
    }

    /// <summary>The caption of the object under the pointer shows from any distance; the one that was there goes back to showing only nearby.</summary>
    private void ShowPointedCaption()
    {
        var pointed = _context.Pointed is { } tile && tile.Level == _level && _objectAt.TryGetValue(tile, out var id) ? _captions.GetValueOrDefault(id) : null;
        if (pointed == _pointedCaption)
        {
            return;
        }

        if (_pointedCaption is not null && IsInstanceValid(_pointedCaption))
        {
            _pointedCaption.VisibilityRangeEnd = CaptionRange;
        }

        _pointedCaption = pointed;
        if (pointed is not null)
        {
            pointed.VisibilityRangeEnd = 0; // no limit
        }
    }

    private CellBatch Batch(Vector3 size)
    {
        if (!_batches.TryGetValue(size, out var batch))
        {
            batch = new CellBatch(new BoxMesh { Size = size, Material = _cellMaterial });
            AddChild(batch.Node);
            _batches[size] = batch;
        }

        return batch;
    }

    /// <summary>Monsters show their creature (and, in the editor, the count), towns, dwellings and buildings their name; pickups stay unlabelled.</summary>
    private string? Caption(KnownObject known) => _context.State.FindObject(known.Id) switch
    {
        MonsterGuard { RandomTier: { } tier } monster => (tier == 0 ? "Random monster" : $"Random monster, tier {tier}") + (monster.Count is { } n ? $" ×{n}" : ""),
        MonsterGuard monster => _context.Viewer is null && monster.Count is { } count ? $"{CreatureName(monster)} ×{count}" : CreatureName(monster),
        Core.Domain.Towns.Town { IsRandom: true } town => $"{town.Name} (random town)",
        Core.Domain.Towns.Town town => town.Name,
        CreatureDwelling dwelling => _context.State.Creatures.TryGet(dwelling.Creature, out var dwelt) ? $"{dwelt.Name} dwelling" : dwelling.Creature.Value,
        Marketplace => "Marketplace",
        ResourceProvider provider => provider.Definition.Name,
        LootSite { Vanishes: false } site => site.Definition.Name,
        BuffSite or TrainingSite or Observatory or HillFort or TownGate or WarMachineFactory or Prison or CreatureBank or DenOfThieves or AstrologersTower or Sanctuary
            or Cartographer or ArtifactMerchant or RefugeeCamp or SacrificialAltar or Sphinx or WitchHut or Shrine or SkillSchool or Obelisk or CombatPlace or PandorasBox or QuestSite
            or Deposit or MagiWindow or MagiEye or KeymasterTent or BorderPost or MapTavern or MapGarrison => _context.State.FindObject(known.Id)!.Definition.Name,
        _ => null,
    };

    private string CreatureName(MonsterGuard monster) =>
        _context.State.Creatures.TryGet(monster.Creature, out var creature) ? creature.Name : monster.Creature.Value;

    /// <summary>
    /// Placeholder shapes that tell the quest objects apart (EVT-5) until there are models: a Seer's Hut gets a pointed roof over its building, a
    /// Quest Guard a standing figure with a spear, a Quest Gate two posts and a lintel over its cell.
    /// </summary>
    private void AddShape(Node3D root, KnownObject known, Color color, Vector3 at)
    {
        switch (known.Definition.Kind)
        {
            case MapObjectKind.SeerHut:
                var blocked = known.Definition.Footprint.OccupiedCells.Where(c => c.Cell == FootprintCell.Blocked).ToList();
                if (blocked.Count > 0)
                {
                    var middle = at + (new Vector3((float)blocked.Average(c => c.Dx), 0, (float)blocked.Average(c => c.Dy)) * MapSpace.TileSize);
                    var width = blocked.Max(c => c.Dx) - blocked.Min(c => c.Dx) + 1;
                    root.AddChild(Cone(0.4f * width, 0.7f, middle + (Vector3.Up * 0.85f), color.Darkened(0.3f)));
                    root.AddChild(Sphere(0.12f, middle + (Vector3.Up * 1.3f), Colors.Gold)); // the seer's lamp
                }

                break;
            case MapObjectKind.QuestGuard:
                root.AddChild(Cylinder(0.18f, 0.6f, at + (Vector3.Up * 0.36f), color));
                root.AddChild(Sphere(0.14f, at + (Vector3.Up * 0.8f), color.Lightened(0.3f)));
                root.AddChild(Box(new Vector3(0.04f, 1.0f, 0.04f), at + new Vector3(0.26f, 0.55f, 0), Colors.Silver));
                break;
            case MapObjectKind.QuestGate:
                root.AddChild(Box(new Vector3(0.14f, 0.9f, 0.14f), at + new Vector3(-0.38f, 0.45f, 0), color));
                root.AddChild(Box(new Vector3(0.14f, 0.9f, 0.14f), at + new Vector3(0.38f, 0.45f, 0), color));
                root.AddChild(Box(new Vector3(0.95f, 0.14f, 0.18f), at + (Vector3.Up * 0.95f), color.Darkened(0.2f)));
                root.AddChild(Sphere(0.09f, at + (Vector3.Up * 1.1f), Colors.Gold)); // the seal: meet the quest to pass
                break;
        }
    }

    // Objects of one colour and size share their mesh and material (_meshes).
    private MeshInstance3D Box(Vector3 size, Vector3 position, Color color) => new() { Mesh = _meshes.Box(size, color), Position = position };

    private MeshInstance3D Cone(float radius, float height, Vector3 position, Color color) =>
        new() { Mesh = _meshes.Cylinder(0, radius, height, color), Position = position };

    private MeshInstance3D Cylinder(float radius, float height, Vector3 position, Color color) =>
        new() { Mesh = _meshes.Cylinder(radius, radius, height, color), Position = position };

    private MeshInstance3D Sphere(float radius, Vector3 position, Color color) => new() { Mesh = _meshes.Sphere(radius, color), Position = position };
}

/// <summary>
/// Boxes of one size as instances of one <see cref="MultiMesh"/>, each with its place and colour. A freed instance is hidden (scaled to nothing)
/// and given to the next box; when they are all taken the instances are doubled and written again (a new count clears them).
/// </summary>
internal sealed class CellBatch
{
    private static readonly Transform3D Hidden = new(Basis.Identity.Scaled(Vector3.Zero), Vector3.Zero);

    private readonly MultiMesh _multi;
    private readonly List<(Transform3D Place, Color Color)> _slots = [];
    private readonly Stack<int> _free = new();

    public CellBatch(Mesh mesh)
    {
        _multi = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = mesh };
        Node = new MultiMeshInstance3D { Multimesh = _multi, Name = "Cells" };
    }

    public MultiMeshInstance3D Node { get; }

    public (CellBatch Batch, int Slot) Add(Vector3 position, Color color)
    {
        var place = new Transform3D(Basis.Identity, position);
        if (!_free.TryPop(out var slot))
        {
            slot = _slots.Count;
            _slots.Add(default);
        }

        _slots[slot] = (place, color);
        if (_slots.Count > _multi.InstanceCount)
        {
            _multi.InstanceCount = Math.Max(64, _multi.InstanceCount * 2);
            for (var i = 0; i < _slots.Count; i++)
            {
                Write(i);
            }
        }
        else
        {
            Write(slot);
        }

        _multi.VisibleInstanceCount = _slots.Count;
        return (this, slot);
    }

    public void Remove(int slot)
    {
        _slots[slot] = (Hidden, default);
        Write(slot);
        _free.Push(slot);
    }

    public void Clear()
    {
        _slots.Clear();
        _free.Clear();
        _multi.VisibleInstanceCount = 0;
    }

    private void Write(int slot)
    {
        _multi.SetInstanceTransform(slot, _slots[slot].Place);
        _multi.SetInstanceColor(slot, _slots[slot].Color);
    }
}

internal static class KnownObjectExtensions
{
    public static MapPosition VisitPosition(this KnownObject known) =>
        known.Position.Offset(known.Definition.Footprint.VisitOffset.Dx, known.Definition.Footprint.VisitOffset.Dy);
}
