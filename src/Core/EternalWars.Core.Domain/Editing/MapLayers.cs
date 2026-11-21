using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

/// <summary>The kinds of what the map editor draws, each shown or hidden and some locked as a layer (ADR-262).</summary>
[Flags]
public enum MapLayer
{
    None = 0,
    Terrain = 1,
    Roads = 2,
    Rivers = 4,

    /// <summary>Map objects that are neither monsters nor scenery: towns, mines, pickups, gates…</summary>
    Objects = 8,
    Scenery = 16,
    Monsters = 32,
    Heroes = 64,

    /// <summary>Map events' cells.</summary>
    Events = 128,
    StartZones = 256,
    All = Terrain | Roads | Rivers | Objects | Scenery | Monsters | Heroes | Events | StartZones,
}

/// <summary>
/// The map editor's layers (ADR-262): which kinds are shown and which are locked. A kind that is hidden or locked cannot be selected or
/// erased (<see cref="Editable"/>): the selection finds none of it and lets go of what it held, and the eraser leaves it. Only the kinds
/// something selects or erases lock (<see cref="Lockable"/>); the terrain and the start zones are only shown or hidden, as the painting
/// tools that change them are picked on purpose. The layers belong to the session: no undo step, not saved. A change publishes
/// <see cref="MapLayersChanged"/>.
/// </summary>
public sealed class MapLayers
{
    /// <summary>The kinds that can be locked.</summary>
    public const MapLayer Lockable = MapLayer.Roads | MapLayer.Rivers | MapLayer.Objects | MapLayer.Scenery | MapLayer.Monsters | MapLayer.Heroes | MapLayer.Events;

    private readonly EditorContext _context;
    private readonly SelectionEditor _selecting;

    internal MapLayers(EditorContext context, SelectionEditor selecting) => (_context, _selecting) = (context, selecting);

    /// <summary>The kinds that are not drawn.</summary>
    public MapLayer Hidden { get; private set; }

    /// <summary>The kinds that are drawn but cannot be selected or erased.</summary>
    public MapLayer Locked { get; private set; }

    /// <summary>The kinds that can be selected and erased: shown and not locked.</summary>
    public MapLayer Editable => MapLayer.All & ~(Hidden | Locked);

    /// <summary>Whether every kind in <paramref name="layer"/> is shown.</summary>
    public bool IsShown(MapLayer layer) => (Hidden & layer) == 0;

    /// <summary>Whether every kind in <paramref name="layer"/> is locked.</summary>
    public bool IsLocked(MapLayer layer) => layer != MapLayer.None && (Locked & layer) == layer;

    /// <summary>Whether every kind in <paramref name="layer"/> can be selected and erased.</summary>
    public bool IsEditable(MapLayer layer) => (Editable & layer) == layer;

    /// <summary>Shows or hides the kinds of <paramref name="layer"/>. Returns whether anything changed.</summary>
    public bool Show(MapLayer layer, bool shown) => Change(shown ? Hidden & ~layer : Hidden | (layer & MapLayer.All), Locked);

    /// <summary>Locks or unlocks the kinds of <paramref name="layer"/> that can be locked (<see cref="Lockable"/>). Returns whether anything changed.</summary>
    public bool Lock(MapLayer layer, bool locked) => Change(Hidden, locked ? Locked | (layer & Lockable) : Locked & ~layer);

    /// <summary>The layer an object is drawn on.</summary>
    public static MapLayer Of(MapObject mapObject)
    {
        ArgumentNullException.ThrowIfNull(mapObject);
        return Of(mapObject.Definition.Kind);
    }

    /// <summary>The layer objects of <paramref name="kind"/> are drawn on.</summary>
    public static MapLayer Of(MapObjectKind kind) => kind switch
    {
        MapObjectKind.Monster => MapLayer.Monsters,
        MapObjectKind.Scenery => MapLayer.Scenery,
        _ => MapLayer.Objects,
    };

    /// <summary>The layers of map objects.</summary>
    public const MapLayer ObjectLayers = MapLayer.Objects | MapLayer.Scenery | MapLayer.Monsters;

    private bool Change(MapLayer hidden, MapLayer locked)
    {
        if (hidden == Hidden && locked == Locked)
        {
            return false;
        }

        (Hidden, Locked) = (hidden, locked);
        _selecting.KeepEditable(); // what can no longer be selected leaves the selection, before anyone hears of the change
        _context.Publish(new MapLayersChanged(Hidden, Locked));
        return true;
    }
}
