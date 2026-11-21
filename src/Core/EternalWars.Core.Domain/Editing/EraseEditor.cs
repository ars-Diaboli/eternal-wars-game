using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>The kinds of things the eraser takes off the map.</summary>
[Flags]
public enum EraseKinds
{
    None = 0,

    /// <summary>Map objects that are neither monsters nor scenery.</summary>
    Objects = 1,
    Scenery = 2,
    Monsters = 4,
    Heroes = 8,
    Roads = 16,
    Rivers = 32,

    /// <summary>Event cells (the events themselves stay).</summary>
    Events = 64,
    All = Objects | Scenery | Monsters | Heroes | Roads | Rivers | Events,
}

/// <summary>
/// Erases by kind (EDIT-1, ADR-250): under a set of tiles (a brush) or over the selection. An object goes when any of its cells is
/// there; what pointed at a removed gate loses its link, as with any removal. A kind that is hidden or locked (<see cref="MapLayers"/>,
/// ADR-262) is left as it is. One call is one undo step; inside a stroke it joins the stroke.
/// </summary>
public sealed class EraseEditor
{
    private static readonly (MapLayer Layer, EraseKinds Kind)[] ErasableKinds =
    [
        (MapLayer.Objects, EraseKinds.Objects), (MapLayer.Scenery, EraseKinds.Scenery), (MapLayer.Monsters, EraseKinds.Monsters),
        (MapLayer.Heroes, EraseKinds.Heroes), (MapLayer.Roads, EraseKinds.Roads), (MapLayer.Rivers, EraseKinds.Rivers),
        (MapLayer.Events, EraseKinds.Events),
    ];

    private readonly EditorContext _context;
    private readonly TerrainEditor _terrain;
    private readonly ObjectEditor _objects;
    private readonly HeroEditor _heroes;
    private readonly MapEventEditor _events;
    private readonly SelectionEditor _selecting;

    internal EraseEditor(
        EditorContext context, TerrainEditor terrain, ObjectEditor objects, HeroEditor heroes, MapEventEditor events, SelectionEditor selecting) =>
        (_context, _terrain, _objects, _heroes, _events, _selecting) = (context, terrain, objects, heroes, events, selecting);

    private Game.GameState Scenario => _context.Scenario;

    /// <summary>
    /// Removes what is on <paramref name="tiles"/> of the <paramref name="kinds"/>. Returns how many things went: objects, heroes, road
    /// and river tiles, event cells.
    /// </summary>
    public int Erase(IEnumerable<MapPosition> tiles, EraseKinds kinds)
    {
        var where = tiles.Where(Scenario.Map.Contains).ToHashSet();
        return Erase("Erase", where, [], [], [], kinds);
    }

    /// <summary>
    /// Removes what is selected of the <paramref name="kinds"/>: the selected objects and heroes, every cell of the selected events, and
    /// what is on the selected tiles. Returns how many things went.
    /// </summary>
    public int EraseSelection(EraseKinds kinds)
    {
        var selection = _context.Selection;
        return Erase("Erase in the selection", [.. selection.Tiles], [.. selection.Objects], [.. selection.Heroes], [.. selection.Events], kinds);
    }

    private int Erase(
        string description, HashSet<MapPosition> tiles, List<MapObjectId> selectedObjects, List<HeroId> selectedHeroes, List<string> selectedEvents,
        EraseKinds kinds)
    {
        kinds &= Erasable(_context.Editable);
        var objects = tiles
            .Select(t => Scenario.ObjectCellAt(t)?.Object)
            .OfType<MapObject>()
            .Concat(selectedObjects.Select(id => _context.Object<MapObject>(id)).OfType<MapObject>())
            .Distinct()
            .Where(o => kinds.HasFlag(KindOf(o)))
            .Select(o => o.Id)
            .ToList();
        var heroes = kinds.HasFlag(EraseKinds.Heroes)
            ? Scenario.Heroes.Where(h => tiles.Contains(h.Position) || selectedHeroes.Contains(h.Id)).Select(h => h.Id).ToList()
            : [];
        var events = selectedEvents.ToHashSet(StringComparer.Ordinal);

        var erased = 0;
        _context.AsOneStep(description, () =>
        {
            // Out of the selection at once: one change, not one for each thing.
            _selecting.Remove(new SelectionItems([], objects, heroes, []));
            erased += heroes.Count(h => _heroes.Remove(h) == EditResult.Ok);
            erased += objects.Count(o => _objects.Remove(o) == EditResult.Ok);
            if (kinds.HasFlag(EraseKinds.Roads))
            {
                erased += _terrain.PaintRoad(tiles, RoadType.None);
            }

            if (kinds.HasFlag(EraseKinds.Rivers))
            {
                erased += _terrain.PaintRiver(tiles, RiverType.None);
            }

            if (kinds.HasFlag(EraseKinds.Events))
            {
                erased += _events.RemoveCells(c => tiles.Contains(c.Position) || events.Contains(c.Event));
            }
        });
        return erased;
    }

    private static EraseKinds KindOf(MapObject mapObject) => MapLayers.Of(mapObject) switch
    {
        MapLayer.Monsters => EraseKinds.Monsters,
        MapLayer.Scenery => EraseKinds.Scenery,
        _ => EraseKinds.Objects,
    };

    /// <summary>The kinds the eraser may take with <paramref name="editable"/> layers.</summary>
    private static EraseKinds Erasable(MapLayer editable)
    {
        var kinds = EraseKinds.None;
        foreach (var (layer, kind) in ErasableKinds)
        {
            if (editable.HasFlag(layer))
            {
                kinds |= kind;
            }
        }

        return kinds;
    }
}
