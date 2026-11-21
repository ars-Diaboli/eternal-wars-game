using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Editing;

/// <summary>What a selection can hold of the map's things, as the Select tool's filter (EDIT-1, ADR-250).</summary>
[Flags]
public enum SelectionFilter
{
    None = 0,

    /// <summary>Map objects that are neither monsters nor scenery: towns, mines, pickups, gates…</summary>
    Objects = 1,
    Heroes = 2,
    Monsters = 4,
    Scenery = 8,

    /// <summary>Map events, picked by their cells.</summary>
    Events = 16,
    All = Objects | Heroes | Monsters | Scenery | Events,
}

/// <summary>A rectangle of tiles on one level, its edges included.</summary>
public readonly record struct TileArea(int Left, int Top, int Right, int Bottom, int Level)
{
    /// <summary>The rectangle with <paramref name="a"/> and <paramref name="b"/> as opposite corners, on <paramref name="a"/>'s level.</summary>
    public static TileArea Between(MapPosition a, MapPosition b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y), a.Level);

    public bool Contains(MapPosition tile) =>
        tile.Level == Level && tile.X >= Left && tile.X <= Right && tile.Y >= Top && tile.Y <= Bottom;

    public IEnumerable<MapPosition> Tiles()
    {
        for (var y = Top; y <= Bottom; y++)
        {
            for (var x = Left; x <= Right; x++)
            {
                yield return new MapPosition(x, y, Level);
            }
        }
    }
}

/// <summary>Tiles and things to select, add to the selection or take out of it: tiles, objects, heroes, and map events by name.</summary>
public sealed record SelectionItems(
    IReadOnlyCollection<MapPosition> Tiles,
    IReadOnlyCollection<MapObjectId> Objects,
    IReadOnlyCollection<HeroId> Heroes,
    IReadOnlyCollection<string> Events)
{
    public static SelectionItems Empty { get; } = new([], [], [], []);

    public bool IsEmpty => Tiles.Count == 0 && Objects.Count == 0 && Heroes.Count == 0 && Events.Count == 0;

    public static SelectionItems Of(MapObjectId mapObject) => Empty with { Objects = [mapObject] };

    public static SelectionItems Of(HeroId hero) => Empty with { Heroes = [hero] };
}

/// <summary>
/// What is selected in the map editor (EDIT-1, ADR-250): tiles (each with its level), objects, heroes and map events. The session owns
/// it and it changes only through the session's <see cref="SelectionEditor"/>, which publishes <c>SelectionChanged</c>. It is no undo
/// step. A thing that leaves the map leaves the selection.
/// </summary>
public sealed class MapSelection
{
    private readonly HashSet<MapPosition> _tiles = [];
    private readonly HashSet<MapObjectId> _objects = [];
    private readonly HashSet<HeroId> _heroes = [];
    private readonly HashSet<string> _events = new(StringComparer.Ordinal);

    public IReadOnlyCollection<MapPosition> Tiles => _tiles;

    public IReadOnlyCollection<MapObjectId> Objects => _objects;

    public IReadOnlyCollection<HeroId> Heroes => _heroes;

    /// <summary>The selected map events' names.</summary>
    public IReadOnlyCollection<string> Events => _events;

    public bool IsEmpty => _tiles.Count == 0 && _objects.Count == 0 && _heroes.Count == 0 && _events.Count == 0;

    /// <summary>Whether an object or a hero is selected: something Move and Delete work on.</summary>
    public bool HasThings => _objects.Count > 0 || _heroes.Count > 0;

    public bool Contains(MapPosition tile) => _tiles.Contains(tile);

    public bool Contains(MapObjectId mapObject) => _objects.Contains(mapObject);

    public bool Contains(HeroId hero) => _heroes.Contains(hero);

    public bool ContainsEvent(string name) => _events.Contains(name);

    /// <summary>
    /// The map cells the selection covers in <paramref name="state"/>, each once: its tiles, every cell of its objects, its heroes' tiles
    /// and its events' cells. What the editor's camera frames for "focus the selection" (ADR-260).
    /// </summary>
    public IReadOnlyList<MapPosition> Cells(GameState state) =>
    [
        .. _tiles
            .Concat(state.Objects.Where(o => _objects.Contains(o.Id)).SelectMany(o => o.OccupiedCells().Select(c => c.Position)))
            .Concat(state.Heroes.Where(h => _heroes.Contains(h.Id)).Select(h => h.Position))
            .Concat(state.MapEvents.Set.Cells.Where(c => _events.Contains(c.Event)).Select(c => c.Position))
            .Distinct(),
    ];

    /// <summary>Makes the selection exactly <paramref name="items"/>. Returns whether it changed.</summary>
    internal bool Set(SelectionItems items)
    {
        if (_tiles.SetEquals(items.Tiles) && _objects.SetEquals(items.Objects) && _heroes.SetEquals(items.Heroes) && _events.SetEquals(items.Events))
        {
            return false;
        }

        Replace(_tiles, items.Tiles);
        Replace(_objects, items.Objects);
        Replace(_heroes, items.Heroes);
        Replace(_events, items.Events);
        return true;
    }

    internal bool Add(SelectionItems items)
    {
        var before = Count;
        _tiles.UnionWith(items.Tiles);
        _objects.UnionWith(items.Objects);
        _heroes.UnionWith(items.Heroes);
        _events.UnionWith(items.Events);
        return Count != before;
    }

    internal bool Remove(SelectionItems items)
    {
        var before = Count;
        _tiles.ExceptWith(items.Tiles);
        _objects.ExceptWith(items.Objects);
        _heroes.ExceptWith(items.Heroes);
        _events.ExceptWith(items.Events);
        return Count != before;
    }

    internal bool Forget(MapObjectId mapObject) => _objects.Remove(mapObject);

    internal bool Forget(HeroId hero) => _heroes.Remove(hero);

    /// <summary>Drops the events that are not among <paramref name="names"/> any more.</summary>
    internal bool KeepEvents(IEnumerable<string> names)
    {
        var before = _events.Count;
        _events.IntersectWith(names);
        return _events.Count != before;
    }

    private int Count => _tiles.Count + _objects.Count + _heroes.Count + _events.Count;

    private static void Replace<T>(HashSet<T> set, IEnumerable<T> items)
    {
        set.Clear();
        set.UnionWith(items);
    }
}
