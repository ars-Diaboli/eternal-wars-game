using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// Copies the selection into a <see cref="MapFragment"/> and pastes fragments (EDIT-4, ADR-250). <see cref="Copy"/> changes nothing;
/// <see cref="Cut"/>, <see cref="Paste"/> and <see cref="DuplicateAt"/> are one undo step each. A paste checks the objects as placement
/// does, gives them new ids and remaps the links between them.
/// </summary>
public sealed class FragmentEditor
{
    private readonly EditorContext _context;
    private readonly TerrainEditor _terrain;
    private readonly ObjectEditor _objects;
    private readonly SelectionEditor _selecting;
    private readonly IMapObjectCopier? _copier;

    internal FragmentEditor(EditorContext context, TerrainEditor terrain, ObjectEditor objects, SelectionEditor selecting, IMapObjectCopier? copier) =>
        (_context, _terrain, _objects, _selecting, _copier) = (context, terrain, objects, selecting, copier);

    private Game.GameState Scenario => _context.Scenario;

    private AdventureMap Map => Scenario.Map;

    private IMapObjectCopier Copier =>
        _copier ?? throw new InvalidOperationException("The editor session was made without an object copier, so it cannot copy objects.");

    /// <summary>
    /// The selected tiles and objects as a fragment; null when neither is selected. A selection over several levels gives the lowest
    /// level's part. Selected heroes and events are left out, and so are roads and rivers while their layers are hidden or locked
    /// (ADR-262), which a paste then leaves as they are too.
    /// </summary>
    public MapFragment? Copy()
    {
        var selection = _context.Selection;
        var selected = Scenario.Objects.Where(o => selection.Contains(o.Id)).ToList();
        if (selection.Tiles.Count == 0 && selected.Count == 0)
        {
            return null;
        }

        var level = selection.Tiles.Select(t => t.Level).Concat(selected.Select(o => o.Position.Level)).Min();
        var tiles = selection.Tiles.Where(t => t.Level == level).OrderBy(t => t.Y).ThenBy(t => t.X).ToList();
        selected = [.. selected.Where(o => o.Position.Level == level)];
        var cells = tiles.Concat(selected.SelectMany(o => o.OccupiedCells().Select(c => c.Position))).ToList();
        var (left, top) = (cells.Min(c => c.X), cells.Min(c => c.Y));

        var inside = selected.Select(o => o.Id).ToHashSet();
        var objects = new List<MapObject>();
        foreach (var mapObject in selected)
        {
            var copy = Copier.Copy(mapObject, mapObject.Id, new MapPosition(mapObject.Position.X - left, mapObject.Position.Y - top), Scenario);
            Relink(copy, id => inside.Contains(id) ? id : null);
            objects.Add(copy);
        }

        var corners = tiles
            .SelectMany(t => new[] { (t.X, t.Y), (t.X + 1, t.Y), (t.X, t.Y + 1), (t.X + 1, t.Y + 1) })
            .Distinct()
            .Select(c => new FragmentCorner(c.Item1 - left, c.Item2 - top, Map.GetCorner(level, c.Item1, c.Item2)))
            .ToList();
        return new MapFragment(
            cells.Max(c => c.X) - left + 1,
            cells.Max(c => c.Y) - top + 1,
            [.. tiles.Select(t => new FragmentTile(
                t.X - left, t.Y - top, Map.GetTerrain(t),
                Locked().HasFlag(FragmentLayers.Roads) ? RoadType.None : Map.GetRoad(t),
                Locked().HasFlag(FragmentLayers.Rivers) ? RiverType.None : Map.GetRiver(t)))],
            corners,
            objects);
    }

    /// <summary>
    /// <see cref="Copy"/>, then the copied objects leave the map as one undo step (the ground, the terrain, roads and rivers stay).
    /// Null when no tile and no object is selected.
    /// </summary>
    public MapFragment? Cut()
    {
        if (Copy() is not { } fragment)
        {
            return null;
        }

        var cut = fragment.Objects.Select(o => o.Id).ToList();
        if (cut.Count > 0)
        {
            _context.AsOneStep(cut.Count == 1 ? $"Cut {fragment.Objects[0].Definition.Name}" : $"Cut {cut.Count} objects", () =>
            {
                _selecting.Remove(SelectionItems.Empty with { Objects = cut }); // out of the selection at once: one change, not one for each
                cut.ForEach(id => _objects.Remove(id));
            });
        }

        return fragment;
    }

    /// <summary>
    /// Whether <paramref name="fragment"/> can be pasted with its top-left tile on <paramref name="origin"/>, and the cells it would
    /// take (for a ghost): its tiles that are on the map, and its objects' cells, which fit or not.
    /// </summary>
    public MoveCheck CheckPaste(MapFragment fragment, MapPosition origin, FragmentLayers layers = FragmentLayers.All)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        var cells = new Dictionary<MapPosition, bool>();
        var terrain = new Dictionary<MapPosition, TerrainType>();
        foreach (var tile in fragment.Tiles)
        {
            var at = origin.Offset(tile.X, tile.Y);
            if (Map.Contains(at))
            {
                cells[at] = true;
                if (layers.HasFlag(FragmentLayers.Terrain))
                {
                    terrain[at] = tile.Terrain;
                }
            }
        }

        var result = EditResult.Ok;
        if (!layers.HasFlag(FragmentLayers.Objects))
        {
            return new MoveCheck(result, [.. cells.Select(c => new MoveCell(c.Key, c.Value))]);
        }

        foreach (var mapObject in fragment.Objects)
        {
            foreach (var (from, kind) in mapObject.OccupiedCells())
            {
                var to = origin.Offset(from.X, from.Y);
                var check =
                    !Map.Contains(to) ? EditResult.OutOfBounds
                    : Scenario.ObjectCellAt(to) is not null || Scenario.HeroAt(to) is not null ? EditResult.Overlaps
                    : kind == FootprintCell.Visit && !Scenario.Terrains.IsPassable(terrain.TryGetValue(to, out var painted) ? painted : Map.GetTerrain(to))
                        ? EditResult.ImpassableTerrain
                    : EditResult.Ok;
                cells[to] = check == EditResult.Ok;
                result = result == EditResult.Ok ? check : result;
            }
        }

        // The limits placement keeps (ADR-070, ADR-140).
        if (result == EditResult.Ok)
        {
            var eyes = fragment.Objects.OfType<MagiEye>().GroupBy(e => e.Colour);
            if (eyes.Any(colour => colour.Count() + Scenario.Objects.OfType<MagiEye>().Count(e => e.Colour == colour.Key) > MagiWindow.MaxEyes))
            {
                result = EditResult.TooManyEyes;
            }
            else if (fragment.Objects.OfType<Obelisk>().Count() is > 0 and var obelisks && obelisks + Scenario.ObjectsOf<Obelisk>().Count() > GrailRules.MaxObelisks)
            {
                result = EditResult.TooManyObelisks;
            }
        }

        return new MoveCheck(result, [.. cells.Select(c => new MoveCell(c.Key, c.Value))]);
    }

    /// <summary>
    /// Pastes the <paramref name="layers"/> of <paramref name="fragment"/> with its top-left tile on <paramref name="origin"/>, as one
    /// undo step. Tiles off the map are left out; an object that does not fit (<see cref="CheckPaste"/>) refuses the whole paste. The
    /// objects get new ids and the links between them follow; an owner, an event or a quest this map does not have (or a quest another
    /// object has) is taken from the copy. The ground under the pasted objects is levelled as placement does, and so is the ground of
    /// the objects the pasted heights reach. With the heights pasted, the terrain's Auto height does not shape the ground. What was
    /// pasted is selected.
    /// </summary>
    public EditResult Paste(MapFragment fragment, MapPosition origin, FragmentLayers layers = FragmentLayers.All) =>
        PasteAs(fragment, origin, layers, "Paste");

    /// <summary>Pastes a copy of the selection (<see cref="Copy"/>) at <paramref name="origin"/>, as one undo step, without a clipboard.</summary>
    public EditResult DuplicateAt(MapPosition origin, FragmentLayers layers = FragmentLayers.All) =>
        Copy() is { } fragment ? PasteAs(fragment, origin, layers, "Duplicate") : EditResult.InvalidTarget;

    /// <summary>The roads and rivers when their layers are hidden or locked (ADR-262): a paste leaves them, a copy does not take them.</summary>
    private FragmentLayers Locked() =>
        (_context.Editable.HasFlag(MapLayer.Roads) ? FragmentLayers.None : FragmentLayers.Roads)
        | (_context.Editable.HasFlag(MapLayer.Rivers) ? FragmentLayers.None : FragmentLayers.Rivers);

    private EditResult PasteAs(MapFragment fragment, MapPosition origin, FragmentLayers layers, string description)
    {
        if (CheckPaste(fragment, origin, layers).Result is var check && check != EditResult.Ok)
        {
            return check;
        }

        // The objects first: when one cannot be copied into this map, nothing has changed yet.
        var pasted = new List<MapObject>();
        if (layers.HasFlag(FragmentLayers.Objects) && fragment.Objects.Count > 0)
        {
            var next = Scenario.NextObjectId().Value;
            var ids = fragment.Objects.Select((o, i) => (o.Id, New: new MapObjectId(next + i))).ToDictionary(p => p.Id, p => p.New);
            try
            {
                foreach (var source in fragment.Objects)
                {
                    var copy = Copier.Copy(source, ids[source.Id], origin.Offset(source.Position.X, source.Position.Y), Scenario);
                    Relink(copy, id => ids.TryGetValue(id, out var mapped) ? mapped : null);
                    FitToMap(copy, pasted);
                    pasted.Add(copy);
                }
            }
            catch (FormatException)
            {
                return EditResult.InvalidValue;
            }
        }

        layers &= ~Locked(); // what is hidden or locked is not painted over (ADR-262)
        var tiles = fragment.Tiles.Select(t => (Tile: t, At: origin.Offset(t.X, t.Y))).Where(t => Map.Contains(t.At)).ToList();
        var withHeights = layers.HasFlag(FragmentLayers.Heights);
        _context.AsOneStep(description, () =>
        {
            if (layers.HasFlag(FragmentLayers.Terrain))
            {
                foreach (var terrain in tiles.GroupBy(t => t.Tile.Terrain))
                {
                    _terrain.Paint(terrain.Select(t => t.At), terrain.Key, shapeGround: !withHeights);
                }
            }

            if (layers.HasFlag(FragmentLayers.Roads))
            {
                foreach (var road in tiles.GroupBy(t => t.Tile.Road))
                {
                    _terrain.PaintRoad(road.Select(t => t.At), road.Key);
                }
            }

            if (layers.HasFlag(FragmentLayers.Rivers))
            {
                foreach (var river in tiles.GroupBy(t => t.Tile.River))
                {
                    _terrain.PaintRiver(river.Select(t => t.At), river.Key);
                }
            }

            PasteGroundAndObjects(fragment, origin, withHeights && fragment.Tiles.Count > 0, pasted);
        });

        if (tiles.Count > 0 || pasted.Count > 0)
        {
            _selecting.Set(new SelectionItems([.. tiles.Select(t => t.At)], [.. pasted.Select(o => o.Id)], [], []));
        }

        return EditResult.Ok;
    }

    /// <summary>The fragment's heights (when asked for), the ground levelled under the pasted objects, and the objects: one command.</summary>
    private void PasteGroundAndObjects(MapFragment fragment, MapPosition origin, bool heights, List<MapObject> pasted)
    {
        var levelled = pasted.Where(ReliefShaper.IsLevelled).ToList();
        var area = fragment.Corners.Count == 0
            ? default
            : new CornerArea(
                origin.X + fragment.Corners.Min(c => c.X), origin.Y + fragment.Corners.Min(c => c.Y),
                origin.X + fragment.Corners.Max(c => c.X), origin.Y + fragment.Corners.Max(c => c.Y));
        var ground = !heights && levelled.Count == 0
            ? null
            : new GroundEdit(_context, () =>
            {
                if (heights)
                {
                    foreach (var corner in fragment.Corners)
                    {
                        var (x, y) = (origin.X + corner.X, origin.Y + corner.Y);
                        if (Map.ContainsCorner(origin.Level, x, y))
                        {
                            Map.SetCorner(origin.Level, x, y, ReliefLimits.Clamp(Map, origin.Level, x, y, corner.Height));
                        }
                    }
                }

                // The objects whose levelled ground the pasted heights moved are levelled again, as after a terrain paint.
                var touched = heights
                    ? Scenario.Objects.Where(o => o.Position.Level == origin.Level && ReliefShaper.IsLevelled(o) && ReliefShaper.Reach(o).Overlaps(area)).ToList()
                    : [];
                if (levelled.Count + touched.Count > 0)
                {
                    ReliefShaper.Level(Map, [.. touched, .. levelled], Scenario.Objects.Except(touched));
                }
            });

        if (pasted.Count == 0)
        {
            // Heights alone: a step only when a corner moved.
            ground?.Apply();
            if (ground is { Count: > 0 })
            {
                _context.Done(new DelegateCommand("Paste heights", ground.Apply, ground.Revert));
            }

            return;
        }

        _context.Run(
            pasted.Count == 1 ? $"Paste {pasted[0].Definition.Name}" : $"Paste {pasted.Count} objects",
            () =>
            {
                ground?.Apply(); // the ground first, so the objects' views are built on it
                pasted.ForEach(o => _context.AddObject(o));
            },
            () =>
            {
                for (var i = pasted.Count - 1; i >= 0; i--)
                {
                    _context.RemoveObject(pasted[i]);
                }

                ground?.Revert();
            });
    }

    /// <summary>
    /// Takes from a pasted copy what this map does not have: an owner that is not one of its players (the object becomes neutral), a
    /// Pandora's box's event that is not a hero event here, and a quest that is not a quest here or that another quest object has
    /// already (on the map or among <paramref name="pasted"/>): every quest object has its own.
    /// </summary>
    private void FitToMap(MapObject copy, IReadOnlyList<MapObject> pasted)
    {
        bool Gone(PlayerId? owner) => owner is { } player && !Scenario.HasPlayer(player);
        var events = Scenario.MapEvents.Set;
        switch (copy)
        {
            case Town town when Gone(town.Owner):
                town.Owner = null;
                break;
            case Mine mine when Gone(mine.Owner):
                mine.Owner = null;
                break;
            case CreatureDwelling dwelling when Gone(dwelling.Owner):
                dwelling.Owner = null;
                break;
            case MapGarrison garrison when Gone(garrison.Owner):
                garrison.Owner = null;
                break;
            case ResourceProvider provider when Gone(provider.Owner):
                provider.Owner = null;
                provider.WasClaimed = false;
                break;
            case PandorasBox { Event: { } name } box when events.Find(name) is not { Kind: MapEventKind.Hero }:
                box.Event = null;
                break;
            case QuestSite { Quest: { } quest } site
                when events.Find(quest) is not { Kind: MapEventKind.Quest }
                     || Scenario.Objects.Concat(pasted).OfType<QuestSite>().Any(q => q.Quest == quest):
                site.Quest = null;
                break;
        }
    }

    /// <summary>Gives a copied gate the link <paramref name="map"/> makes of its old one (null: no link).</summary>
    private static void Relink(MapObject copy, Func<MapObjectId, MapObjectId?> map)
    {
        switch (copy)
        {
            case TwoWayGate { LinkedGate: { } partner } gate:
                gate.LinkedGate = map(partner);
                break;
            case OneWayGateEntrance { Exit: { } exit } entrance:
                entrance.Exit = map(exit);
                break;
        }
    }
}
