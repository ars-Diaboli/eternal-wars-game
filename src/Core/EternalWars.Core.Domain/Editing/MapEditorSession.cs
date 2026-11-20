using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// The map editor's model: a scenario (<see cref="GameState"/> that never starts) plus undoable edit operations, grouped by area
/// (<see cref="Terrain"/>, <see cref="Objects"/>, <see cref="Towns"/>, …). Every change goes through an <see cref="IEditorCommand"/> and
/// publishes events, so the editor UI is a thin view exactly like the game's. An edit that would not change anything records nothing.
/// </summary>
public sealed class MapEditorSession
{
    private readonly EditorContext _context;

    /// <summary><paramref name="copier"/> copies objects with their settings, for <see cref="Fragments"/>; without one, copying an object throws.</summary>
    public MapEditorSession(GameState scenario, MapObjectCatalog? catalog = null, IEventBus? bus = null, IMapObjectCopier? copier = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        _context = new EditorContext(scenario, catalog ?? MapObjectCatalog.Default, bus ?? new EventBus());
        var mover = new MapMover(_context);
        Heights = new HeightEditor(_context);
        Terrain = new TerrainEditor(_context, Heights);
        Objects = new ObjectEditor(_context, Heights, mover);
        Contents = new ObjectContentsEditor(_context);
        Monsters = new MonsterEditor(_context, Objects);
        Towns = new TownEditor(_context);
        Heroes = new HeroEditor(_context, mover);
        MapEvents = new MapEventEditor(_context);
        Players = new PlayerEditor(_context);
        Map = new MapPropertiesEditor(_context);
        Selecting = new SelectionEditor(_context, Objects, Heroes, mover);
        _context.Selecting = Selecting;
        Fragments = new FragmentEditor(_context, Terrain, Objects, Selecting, copier);
        Erasing = new EraseEditor(_context, Terrain, Objects, Heroes, MapEvents, Selecting);
    }

    public GameState Scenario => _context.Scenario;

    public MapObjectCatalog Catalog => _context.Catalog;

    public IEventSubscriber Events => _context.Bus;

    public UndoHistory History => _context.History;

    /// <summary>What is selected: read here, changed through <see cref="Selecting"/> (<c>SelectionChanged</c> tells when).</summary>
    public MapSelection Selection => _context.Selection;

    /// <summary>Finds things to select, changes the <see cref="Selection"/>, and deletes or moves what is selected.</summary>
    public SelectionEditor Selecting { get; }

    /// <summary>Copy, cut, paste and duplicate: the selection as a <see cref="MapFragment"/> and back onto a map.</summary>
    public FragmentEditor Fragments { get; }

    /// <summary>Erase by kind, under a brush or over the selection.</summary>
    public EraseEditor Erasing { get; }

    /// <summary>Terrain, roads and rivers.</summary>
    public TerrainEditor Terrain { get; }

    /// <summary>The ground's height (ADR-182 g): raise, lower, level, smooth, heights from terrain, and the Auto height setting.</summary>
    public HeightEditor Heights { get; }

    /// <summary>Placing and removing objects, gate links and owned objects.</summary>
    public ObjectEditor Objects { get; }

    /// <summary>What chests, pickups, signs, boxes, quest sites, sphinxes, huts, shrines and prisons hold.</summary>
    public ObjectContentsEditor Contents { get; }

    /// <summary>Neutral monsters.</summary>
    public MonsterEditor Monsters { get; }

    public TownEditor Towns { get; }

    public HeroEditor Heroes { get; }

    /// <summary>Map events, variables and event cells (EVT-6).</summary>
    public MapEventEditor MapEvents { get; }

    /// <summary>Player slots and teams.</summary>
    public PlayerEditor Players { get; }

    /// <summary>Map name and description, scenario rules and the Grail.</summary>
    public MapPropertiesEditor Map { get; }

    /// <summary>Groups all following edits into a single undo step until <see cref="EndStroke"/> (mouse down … mouse up).</summary>
    public void BeginStroke(string description) => _context.BeginStroke(description);

    public void EndStroke() => _context.EndStroke();

    public bool Undo() => _context.ChangeHistory(History.Undo);

    public bool Redo() => _context.ChangeHistory(History.Redo);

    /// <summary>
    /// Jumps in the history until <paramref name="step"/> steps are applied (0: everything undone; <c>History.Count</c>: everything redone),
    /// the same as that many single undos or redos, and published as one change. Returns whether anything changed.
    /// </summary>
    public bool UndoTo(int step) => _context.ChangeHistory(() => History.MoveTo(step));

    /// <summary>
    /// The map has work that is not saved: the history is not at the step of the last <see cref="MarkSaved"/> (a new session counts as
    /// saved). Undoing or redoing back to that step makes it clean again. <c>UnsavedWorkChanged</c> tells when this changes.
    /// </summary>
    public bool IsDirty => History.IsDirty;

    /// <summary>The front-end wrote the map as it is now to its file.</summary>
    public void MarkSaved() => _context.MarkSaved();

    /// <summary>The map as it is now is in no file (the front-end restored it from an autosave): it is unsaved work until it is saved.</summary>
    public void MarkUnsaved() => _context.MarkUnsaved();
}
