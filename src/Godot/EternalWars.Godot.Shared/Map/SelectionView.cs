using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.Shared.Map;

/// <summary>
/// The map editor's selection on the shown level (EDIT-1, ADR-250): selected tiles are tinted and outlined; a selected object, hero or
/// event cell gets an outline round its cells, and an object or hero a handle at each corner of its box. While the Select tool drags,
/// the tool also shows the marquee (<see cref="ShowMarquee"/>) or the ghost of a move (<see cref="ShowMove"/>): the cells the things
/// would take, red where they do not fit. Redrawn when the selection, the things' places, the events, the ground or the level change,
/// once a frame however many events asked for it (a move of many things, a stroke's dabs).
/// </summary>
public partial class SelectionView : Node3D
{
    private static readonly Color TileFill = new(0.35f, 0.65f, 1f, 0.22f);
    private static readonly Color TileEdge = new(0.45f, 0.75f, 1f, 0.9f);
    private static readonly Color ThingEdge = new(1f, 0.85f, 0.3f, 0.95f);
    private static readonly Color EventEdge = new(0.8f, 0.5f, 1f, 0.95f);
    private static readonly Color MarqueeEdge = new(1f, 1f, 1f, 0.9f);
    private static readonly Color GhostFits = new(0.35f, 0.9f, 0.4f, 0.5f);
    private static readonly Color GhostBlocked = new(0.95f, 0.25f, 0.2f, 0.6f);

    /// <summary>How thick an outline's bar is, in tiles.</summary>
    private const float EdgeWidth = 0.07f;

    /// <summary>A handle's side, in tiles.</summary>
    private const float HandleSize = 0.2f;

    /// <summary>How far above the ground the marks lie, so they do not flicker in it.</summary>
    private const float Lift = 0.06f;

    private static readonly (int Dx, int Dy)[] Sides = [(0, -1), (1, 0), (0, 1), (-1, 0)];

    private readonly Node3D _selection = new() { Name = "Selection" };
    private readonly Node3D _marquee = new() { Name = "Marquee" };
    private readonly Node3D _ghost = new() { Name = "Ghost" };
    private MapViewContext _context = null!;
    private MapSelection _selected = null!;
    private TileArea? _marqueeArea;
    private MoveCheck? _move;

    public void Bind(MapViewContext context, MapSelection selection)
    {
        _context = context;
        _selected = selection;
        AddChild(_selection);
        AddChild(_marquee);
        AddChild(_ghost);
        context.LevelChanged += RebuildAll;
        TreeExiting += () => context.LevelChanged -= RebuildAll;
        new RefreshScheduler(this, RebuildAll)
            .On<SelectionChanged>(context.Events)
            .On<MapObjectMoved>(context.Events)
            .On<HeroRepositioned>(context.Events)
            .On<MapEventsChanged>(context.Events)
            .On<GroundHeightsChanged>(context.Events);
        RebuildSelection();
    }

    /// <summary>Shows the rectangle the Select tool drags (null: none).</summary>
    public void ShowMarquee(TileArea? area)
    {
        if (_marqueeArea != area)
        {
            _marqueeArea = area;
            RebuildMarquee();
        }
    }

    /// <summary>Shows where the dragged things would land (null: no move is being dragged).</summary>
    public void ShowMove(MoveCheck? move)
    {
        _move = move;
        RebuildGhost();
    }

    private AdventureMap Map => _context.State.Map;

    private void RebuildAll()
    {
        RebuildSelection();
        RebuildMarquee();
        RebuildGhost();
    }

    private void RebuildSelection()
    {
        Clear(_selection);
        var level = _context.Level;
        var state = _context.State;

        var tiles = _selected.Tiles.Where(t => t.Level == level).ToHashSet();
        Plates(_selection, tiles, TileFill, 1f);
        Outline(_selection, tiles, TileEdge);

        var things = new List<Transform3D>();
        var handles = new List<Transform3D>();
        foreach (var mapObject in state.Objects.Where(o => o.Position.Level == level && _selected.Contains(o.Id)))
        {
            Thing([.. mapObject.OccupiedCells().Select(c => c.Position)], things, handles);
        }

        foreach (var hero in state.Heroes.Where(h => h.Position.Level == level && _selected.Contains(h.Id)))
        {
            Thing([hero.Position], things, handles);
        }

        Batch(_selection, things, ThingEdge);
        Batch(_selection, handles, ThingEdge);
        Outline(_selection, [.. state.MapEvents.Set.Cells.Where(c => c.Level == level && _selected.ContainsEvent(c.Event)).Select(c => c.Position)], EventEdge);
    }

    private void RebuildMarquee()
    {
        Clear(_marquee);
        if (_marqueeArea is { } area && area.Level == _context.Level)
        {
            Outline(_marquee, [.. area.Tiles().Where(Map.Contains)], MarqueeEdge);
        }
    }

    private void RebuildGhost()
    {
        Clear(_ghost);
        if (_move is not { } move)
        {
            return;
        }

        var cells = move.Cells.Where(c => c.Position.Level == _context.Level && Map.Contains(c.Position)).ToList();
        Plates(_ghost, [.. cells.Where(c => c.Fits).Select(c => c.Position)], GhostFits, 0.9f);
        Plates(_ghost, [.. cells.Where(c => !c.Fits).Select(c => c.Position)], GhostBlocked, 0.9f);
    }

    /// <summary>One thing's outline (into <paramref name="edges"/>) and the handles at the corners of the box round its cells.</summary>
    private void Thing(HashSet<MapPosition> cells, List<Transform3D> edges, List<Transform3D> handles)
    {
        edges.AddRange(Edges(cells));
        var (left, right) = (cells.Min(c => c.X), cells.Max(c => c.X));
        var (top, bottom) = (cells.Min(c => c.Y), cells.Max(c => c.Y));
        var level = cells.First().Level;
        foreach (var (x, y) in new[] { (left - 0.5f, top - 0.5f), (right + 0.5f, top - 0.5f), (left - 0.5f, bottom + 0.5f), (right + 0.5f, bottom + 0.5f) })
        {
            var at = new Vector3(x * MapSpace.TileSize, 0, y * MapSpace.TileSize);
            at.Y = MapSpace.GroundY(Map, level, at.X, at.Z) + Lift + (HandleSize / 2);
            handles.Add(new Transform3D(Basis.Identity.ScaledLocal(Vector3.One * HandleSize), at));
        }
    }

    private void Outline(Node3D parent, HashSet<MapPosition> cells, Color colour) => Batch(parent, [.. Edges(cells)], colour);

    /// <summary>A bar along every side of a cell that is not shared with another of the <paramref name="cells"/>, lying on the ground there.</summary>
    private IEnumerable<Transform3D> Edges(HashSet<MapPosition> cells)
    {
        foreach (var cell in cells)
        {
            foreach (var (dx, dy) in Sides)
            {
                if (cells.Contains(cell.Offset(dx, dy)) || !Map.Contains(cell))
                {
                    continue;
                }

                var along = dx == 0 ? new Vector3(MapSpace.TileSize, EdgeWidth, EdgeWidth) : new Vector3(EdgeWidth, EdgeWidth, MapSpace.TileSize);
                var at = MapSpace.ToWorld(cell) + new Vector3(dx * MapSpace.TileSize / 2, 0, dy * MapSpace.TileSize / 2);
                at.Y = MapSpace.GroundY(Map, cell.Level, at.X, at.Z) + Lift;
                yield return new Transform3D(MapSpace.OnSlope(Map, cell).ScaledLocal(along), at);
            }
        }
    }

    /// <summary>A flat plate on every cell, <paramref name="size"/> of a tile wide.</summary>
    private void Plates(Node3D parent, HashSet<MapPosition> cells, Color colour, float size)
    {
        var plate = new Vector3(size * MapSpace.TileSize, 0.02f, size * MapSpace.TileSize);
        Batch(
            parent,
            [.. cells.Select(c => new Transform3D(MapSpace.OnSlope(Map, c).ScaledLocal(plate), MapSpace.ToGround(Map, c) + (MapSpace.GroundNormal(Map, c) * Lift)))],
            colour);
    }

    /// <summary>Draws a unit box at every transform, all in one draw call.</summary>
    private static void Batch(Node3D parent, List<Transform3D> transforms, Color colour)
    {
        if (transforms.Count == 0)
        {
            return;
        }

        var mesh = new BoxMesh
        {
            Size = Vector3.One,
            Material = new StandardMaterial3D
            {
                AlbedoColor = colour,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        var boxes = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = transforms.Count };
        for (var i = 0; i < transforms.Count; i++)
        {
            boxes.SetInstanceTransform(i, transforms[i]);
        }

        parent.AddChild(new MultiMeshInstance3D { Multimesh = boxes, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    private static void Clear(Node3D parent)
    {
        foreach (var child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
