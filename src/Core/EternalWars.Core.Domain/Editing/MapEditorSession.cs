using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
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
    private readonly MapResizer _resizer;

    /// <summary><paramref name="copier"/> copies objects with their settings, for <see cref="Fragments"/>; without one, copying an object throws.</summary>
    public MapEditorSession(GameState scenario, MapObjectCatalog? catalog = null, IEventBus? bus = null, IMapObjectCopier? copier = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        _context = new EditorContext(scenario, catalog ?? MapObjectCatalog.Default, bus ?? new EventBus());
        var mover = new MapMover(_context);
        Heights = new HeightEditor(_context);
        Terrain = new TerrainEditor(_context, Heights);
        Objects = new ObjectEditor(_context, Heights, mover, new SceneryTurner(_context));
        Contents = new ObjectContentsEditor(_context);
        Monsters = new MonsterEditor(_context, Objects);
        Towns = new TownEditor(_context);
        Heroes = new HeroEditor(_context, mover);
        MapEvents = new MapEventEditor(_context);
        Players = new PlayerEditor(_context);
        Map = new MapPropertiesEditor(_context);
        Texts = new MapTextEditor(this);
        Placing = new EntryEditor(_context, Objects, Contents, Monsters, Heroes);
        Selecting = new SelectionEditor(_context, Objects, Heroes, mover);
        _context.Selecting = Selecting;
        Layers = new MapLayers(_context, Selecting);
        _context.Layers = Layers;
        Fragments = new FragmentEditor(_context, Terrain, Objects, Selecting, copier);
        Erasing = new EraseEditor(_context, Terrain, Objects, Heroes, MapEvents, Selecting);
        _resizer = new MapResizer(_context, Objects, Heroes, MapEvents, Map);
    }

    public GameState Scenario => _context.Scenario;

    public MapObjectCatalog Catalog => _context.Catalog;

    public IEventSubscriber Events => _context.Bus;

    public UndoHistory History => _context.History;

    /// <summary>What is selected: read here, changed through <see cref="Selecting"/> (<c>SelectionChanged</c> tells when).</summary>
    public MapSelection Selection => _context.Selection;

    /// <summary>Finds things to select, changes the <see cref="Selection"/>, and deletes or moves what is selected.</summary>
    public SelectionEditor Selecting { get; }

    /// <summary>Which kinds are shown and which locked (ADR-262); what is hidden or locked cannot be selected or erased.</summary>
    public MapLayers Layers { get; }

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

    /// <summary>Places what the object browser offers (<see cref="BrowserCatalog"/>).</summary>
    public EntryEditor Placing { get; }

    /// <summary>Map events, variables and event cells (EVT-6).</summary>
    public MapEventEditor MapEvents { get; }

    /// <summary>Player slots and teams.</summary>
    public PlayerEditor Players { get; }

    /// <summary>Map name and description, scenario rules and the Grail.</summary>
    public MapPropertiesEditor Map { get; }

    /// <summary>Every text of the map in one list, each set with its own panel's edit (EDIT-11).</summary>
    public MapTextEditor Texts { get; }

    /// <summary>
    /// What <see cref="Resize"/> would do: refused (<see cref="EditResult.InvalidValue"/>) unless both sides are
    /// <see cref="MapSizes.MinDimension"/> to <see cref="MapSizes.MaxDimension"/> tiles; else what would leave the map.
    /// </summary>
    public ResizeCheck CheckResize(int width, int height, ResizeAnchor anchor = ResizeAnchor.TopLeft) => _resizer.CheckResize(width, height, anchor);

    /// <summary>
    /// Gives the map a new size (EDIT-8, ADR-250), square or not, as one undo step: the old map stays at <paramref name="anchor"/> of the
    /// new one. Ground that falls outside is cut with its roads, rivers, heights and start zones; new ground is grass (subterranean
    /// below the surface) at its terrain's height. Objects with a cell outside, heroes and event cells outside are removed in the same
    /// step, and so is a Grail that would lie too near the new edge (<see cref="CheckResize"/> lists them beforehand). What stays keeps
    /// its ids and links. The selection is let go.
    /// </summary>
    public EditResult Resize(int width, int height, ResizeAnchor anchor = ResizeAnchor.TopLeft) =>
        _resizer.Apply($"Resize to {width} × {height}", CheckResize(width, height, anchor));

    /// <summary>
    /// What <see cref="Crop"/> would do: refused without selected tiles (<see cref="EditResult.InvalidTarget"/>) or when the box round
    /// them is under <see cref="MapSizes.MinDimension"/> tiles a side (<see cref="EditResult.InvalidValue"/>).
    /// </summary>
    public ResizeCheck CheckCrop() => _resizer.CheckCrop();

    /// <summary>Crops the map to the box round the selected tiles, as <see cref="Resize"/> cuts: one undo step.</summary>
    public EditResult Crop() => CheckCrop() is var check && check.Result == EditResult.Ok
        ? _resizer.Apply($"Crop to {check.Width} × {check.Height}", check)
        : check.Result;

    /// <summary>What <see cref="Shift"/> would do: refused (<see cref="EditResult.InvalidValue"/>) when the move is as long as the map.</summary>
    public ResizeCheck CheckShift(int dx, int dy) => _resizer.CheckShift(dx, dy);

    /// <summary>
    /// Moves everything on the map by (<paramref name="dx"/>, <paramref name="dy"/>) tiles on a map of the same size, as one undo step:
    /// what leaves on one side is cut as <see cref="Resize"/> cuts, and new ground comes in on the other.
    /// </summary>
    public EditResult Shift(int dx, int dy) => _resizer.Apply($"Shift by {dx}, {dy}", CheckShift(dx, dy));

    /// <summary>
    /// Runs <paramref name="edits"/> (calls of the editors) as one undo step named <paramref name="description"/>; inside an open stroke
    /// they join the stroke. The edits may draw from the scenario's random numbers (<c>GameState.Random</c>), which they are handed:
    /// undoing the step puts the generator back where it was, so the same edit made again gives the same result. Edits that change
    /// nothing record nothing.
    /// </summary>
    public void AsOneStep(string description, Action<Common.GameRandom> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        _context.AsOneStep(description, edits);
    }

    /// <summary>
    /// Multi-edit (EDIT-1, ADR-255): runs <paramref name="edit"/> on each of the objects as one undo step named
    /// <paramref name="description"/>: all of them, or none when one is refused (the refusal is returned; an id that is not on the map is
    /// <see cref="EditResult.InvalidTarget"/>). Inside an open stroke the edits join it. <see cref="ObjectProperty{TObject, TValue}"/>
    /// is the usual caller.
    /// </summary>
    public EditResult EditAll(string description, IReadOnlyCollection<Common.MapObjectId> objects, Func<MapObject, EditResult> edit)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(edit);
        return objects.Count == 0 ? EditResult.InvalidTarget : _context.AllOrNothing(description, objects, edit);
    }

    /// <summary>
    /// The objects with <paramref name="ids"/> when they are all on the map and of one kind (one object type: all monsters, all mines),
    /// in the order given; else null. These are what the inspector edits together.
    /// </summary>
    public IReadOnlyList<MapObject>? ObjectsOfOneKind(IEnumerable<Common.MapObjectId> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var objects = new List<MapObject>();
        foreach (var id in ids)
        {
            if (Scenario.FindObject(id) is not { } mapObject || (objects.Count > 0 && mapObject.GetType() != objects[0].GetType()))
            {
                return null;
            }

            objects.Add(mapObject);
        }

        return objects.Count == 0 ? null : objects;
    }

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
