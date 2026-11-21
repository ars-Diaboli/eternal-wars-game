using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// Changes the session's <see cref="MapSelection"/> (EDIT-1, ADR-250) and works on what is selected. Finding things (<see cref="At"/>,
/// <see cref="In"/>, <see cref="OfKind"/>, <see cref="Everything"/>) changes nothing; <see cref="Set"/>, <see cref="Add"/>,
/// <see cref="Remove"/> and <see cref="Clear"/> change the selection and publish <see cref="SelectionChanged"/> when it differs, with no
/// undo step; <see cref="Delete"/> and <see cref="Move"/> edit the map as one undo step. Nothing of a kind that is hidden or locked
/// (<see cref="MapLayers"/>, ADR-262) is found or selected.
/// </summary>
public sealed class SelectionEditor
{
    private static readonly (MapLayer Layer, SelectionFilter Kind)[] SelectableKinds =
    [
        (MapLayer.Objects, SelectionFilter.Objects), (MapLayer.Heroes, SelectionFilter.Heroes), (MapLayer.Monsters, SelectionFilter.Monsters),
        (MapLayer.Scenery, SelectionFilter.Scenery), (MapLayer.Events, SelectionFilter.Events),
    ];

    private readonly EditorContext _context;
    private readonly ObjectEditor _objects;
    private readonly HeroEditor _heroes;
    private readonly MapMover _mover;

    internal SelectionEditor(EditorContext context, ObjectEditor objects, HeroEditor heroes, MapMover mover)
    {
        (_context, _objects, _heroes, _mover) = (context, objects, heroes, mover);

        // An event that was removed or renamed is no longer selected.
        context.Bus.Subscribe<MapEventsChanged>(_ => Changed(Selection.KeepEvents(Scenario.MapEvents.Set.Events.Select(e => e.Name))));
    }

    private Game.GameState Scenario => _context.Scenario;

    private MapSelection Selection => _context.Selection;

    /// <summary>
    /// The one thing on <paramref name="tile"/> that <paramref name="filter"/> lets through: the hero there, else the object, else the
    /// events with a cell there. Empty when there is none.
    /// </summary>
    public SelectionItems At(MapPosition tile, SelectionFilter filter = SelectionFilter.All)
    {
        filter &= Selectable();
        if (filter.HasFlag(SelectionFilter.Heroes) && Scenario.HeroAt(tile) is { } hero)
        {
            return SelectionItems.Of(hero.Id);
        }

        if (Scenario.ObjectCellAt(tile)?.Object is { } mapObject && filter.HasFlag(FilterOf(mapObject)))
        {
            return SelectionItems.Of(mapObject.Id);
        }

        return filter.HasFlag(SelectionFilter.Events)
            ? SelectionItems.Empty with { Events = [.. EventsOn(c => c.Position == tile)] }
            : SelectionItems.Empty;
    }

    /// <summary>
    /// The tiles of <paramref name="area"/> that are on the map, and what <paramref name="filter"/> lets through of the things there: an
    /// object with any of its cells in the area, a hero standing in it, an event with a cell in it.
    /// </summary>
    public SelectionItems In(TileArea area, SelectionFilter filter = SelectionFilter.All) => InArea(area, filter & Selectable());

    private SelectionItems InArea(TileArea area, SelectionFilter filter) => new(
        [.. area.Tiles().Where(Scenario.Map.Contains)],
        [.. Scenario.Objects.Where(o => filter.HasFlag(FilterOf(o)) && o.OccupiedCells().Any(c => area.Contains(c.Position))).Select(o => o.Id)],
        filter.HasFlag(SelectionFilter.Heroes) ? [.. Scenario.Heroes.Where(h => area.Contains(h.Position)).Select(h => h.Id)] : [],
        filter.HasFlag(SelectionFilter.Events) ? [.. EventsOn(c => area.Contains(c.Position))] : []);

    /// <summary>Every object of the same definition as <paramref name="like"/> with a cell in <paramref name="area"/> (a double click: all of that kind in view).</summary>
    public SelectionItems OfKind(MapObjectId like, TileArea area) =>
        _context.Object<MapObject>(like) is not { } sample || !IsEditable(sample)
            ? SelectionItems.Empty
            : SelectionItems.Empty with
            {
                Objects =
                [
                    .. Scenario.Objects
                        .Where(o => o.Definition.Id == sample.Definition.Id && o.OccupiedCells().Any(c => area.Contains(c.Position)))
                        .Select(o => o.Id),
                ],
            };

    /// <summary>
    /// Every thing on <paramref name="level"/> that <paramref name="filter"/> lets through (no tiles): the objects and heroes there,
    /// and the events with a cell there.
    /// </summary>
    public SelectionItems Everything(int level, SelectionFilter filter = SelectionFilter.All) => EverythingOn(level, filter & Selectable());

    private SelectionItems EverythingOn(int level, SelectionFilter filter) => new(
        [],
        [.. Scenario.Objects.Where(o => o.Position.Level == level && filter.HasFlag(FilterOf(o))).Select(o => o.Id)],
        filter.HasFlag(SelectionFilter.Heroes) ? [.. Scenario.Heroes.Where(h => h.Position.Level == level).Select(h => h.Id)] : [],
        filter.HasFlag(SelectionFilter.Events) ? [.. EventsOn(c => c.Level == level)] : []);

    /// <summary>Selects exactly <paramref name="items"/>. Returns whether the selection changed.</summary>
    public bool Set(SelectionItems items) => Changed(Selection.Set(Existing(items)));

    /// <summary>Adds <paramref name="items"/> to the selection.</summary>
    public bool Add(SelectionItems items) => Changed(Selection.Add(Existing(items)));

    /// <summary>Takes <paramref name="items"/> out of the selection.</summary>
    public bool Remove(SelectionItems items) => Changed(Selection.Remove(items));

    /// <summary>Selects nothing.</summary>
    public bool Clear() => Set(SelectionItems.Empty);

    /// <summary>
    /// Removes the selected objects and heroes from the map as one undo step. Selected tiles and events are not touched. Returns how many
    /// things were removed.
    /// </summary>
    public int Delete()
    {
        var objects = Selection.Objects.ToList();
        var heroes = Selection.Heroes.ToList();
        if (objects.Count + heroes.Count == 0)
        {
            return 0;
        }

        _context.BeginStroke($"Delete {objects.Count + heroes.Count} things");
        Remove(new SelectionItems([], objects, heroes, [])); // out of the selection at once: one change, not one for each thing
        try
        {
            return heroes.Count(h => _heroes.Remove(h) == EditResult.Ok) + objects.Count(o => _objects.Remove(o) == EditResult.Ok);
        }
        finally
        {
            _context.EndStroke();
        }
    }

    /// <summary>Whether the selected objects and heroes can move by (<paramref name="dx"/>, <paramref name="dy"/>), and the cells they would take.</summary>
    public MoveCheck CheckMove(int dx, int dy) => _mover.Check(Objects(), Heroes(), dx, dy);

    /// <summary>
    /// Moves the selected objects and heroes by (<paramref name="dx"/>, <paramref name="dy"/>) as one undo step, when they all fit. They
    /// stay selected; selected tiles and events are let go.
    /// </summary>
    public EditResult Move(int dx, int dy)
    {
        var objects = Objects();
        var heroes = Heroes();
        var count = objects.Count + heroes.Count;
        if (count == 0)
        {
            return EditResult.InvalidTarget;
        }

        var description = count > 1 ? $"Move {count} things"
            : objects.Count == 1 ? $"Move {objects[0].Definition.Name}"
            : $"Move hero {heroes[0].Name}";
        var result = _mover.Move(description, objects, heroes, dx, dy);
        if (result == EditResult.Ok && (dx != 0 || dy != 0))
        {
            Set(new SelectionItems([], [.. objects.Select(o => o.Id)], [.. heroes.Select(h => h.Id)], []));
        }

        return result;
    }

    /// <summary>
    /// Turns the selected scenery a quarter turn clockwise as one undo step (<see cref="ObjectEditor.Rotate"/>); the other selected
    /// things stay as they are. <see cref="EditResult.InvalidTarget"/> when no scenery is selected.
    /// </summary>
    public EditResult Rotate() => _objects.Rotate(SelectedScenery());

    /// <summary>Mirrors the selected scenery left to right as one undo step (<see cref="ObjectEditor.Mirror"/>).</summary>
    public EditResult Mirror() => _objects.Mirror(SelectedScenery());

    /// <summary>Whether the selected scenery can be turned (<paramref name="mirror"/>: mirrored), and the cells it would take.</summary>
    public MoveCheck CheckTurn(bool mirror = false) => _objects.CheckTurn(SelectedScenery(), mirror);

    private List<MapObjectId> SelectedScenery() => [.. Objects().OfType<Scenery>().Select(s => s.Id)];

    /// <summary>The layers changed: what is of a kind now hidden or locked leaves the selection.</summary>
    internal void KeepEditable()
    {
        var editable = _context.Editable;
        Remove(new SelectionItems(
            [],
            [.. Scenario.Objects.Where(o => Selection.Contains(o.Id) && !IsEditable(o)).Select(o => o.Id)],
            editable.HasFlag(MapLayer.Heroes) ? [] : [.. Selection.Heroes],
            editable.HasFlag(MapLayer.Events) ? [] : [.. Selection.Events]));
    }

    /// <summary>A thing left the map: it is no longer selected.</summary>
    internal void Forget(MapObjectId mapObject) => Changed(Selection.Forget(mapObject));

    /// <inheritdoc cref="Forget(MapObjectId)"/>
    internal void Forget(HeroId hero) => Changed(Selection.Forget(hero));

    private List<MapObject> Objects() => [.. Scenario.Objects.Where(o => Selection.Contains(o.Id))];

    private List<Hero> Heroes() => [.. Scenario.Heroes.Where(h => Selection.Contains(h.Id))];

    /// <summary><paramref name="items"/> without what is not on the map (any more) and what is of a kind hidden or locked.</summary>
    private SelectionItems Existing(SelectionItems items)
    {
        var editable = _context.Editable;
        return new(
            [.. items.Tiles.Where(Scenario.Map.Contains)],
            [.. items.Objects.Where(o => _context.Object<MapObject>(o) is { } mapObject && IsEditable(mapObject))],
            editable.HasFlag(MapLayer.Heroes) ? [.. items.Heroes.Where(h => _context.Hero(h) is not null)] : [],
            editable.HasFlag(MapLayer.Events) ? [.. items.Events.Where(name => Scenario.MapEvents.Set.Events.Any(e => e.Name == name))] : []);
    }

    private bool IsEditable(MapObject mapObject) => _context.Editable.HasFlag(MapLayers.Of(mapObject));

    /// <summary>What the layers let the selection hold now.</summary>
    private SelectionFilter Selectable()
    {
        var editable = _context.Editable;
        var filter = SelectionFilter.None;
        foreach (var (layer, kind) in SelectableKinds)
        {
            if (editable.HasFlag(layer))
            {
                filter |= kind;
            }
        }

        return filter;
    }

    private IEnumerable<string> EventsOn(Func<MapEvents.EventCell, bool> where) =>
        Scenario.MapEvents.Set.Cells.Where(where).Select(c => c.Event).Distinct(StringComparer.Ordinal);

    private static SelectionFilter FilterOf(MapObject mapObject) => MapLayers.Of(mapObject) switch
    {
        MapLayer.Monsters => SelectionFilter.Monsters,
        MapLayer.Scenery => SelectionFilter.Scenery,
        _ => SelectionFilter.Objects,
    };

    private bool Changed(bool changed)
    {
        if (changed)
        {
            _context.Publish(new SelectionChanged());
        }

        return changed;
    }
}
