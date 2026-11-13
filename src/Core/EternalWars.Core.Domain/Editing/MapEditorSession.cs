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

    public MapEditorSession(GameState scenario, MapObjectCatalog? catalog = null, IEventBus? bus = null)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        _context = new EditorContext(scenario, catalog ?? MapObjectCatalog.Default, bus ?? new EventBus());
        Heights = new HeightEditor(_context);
        Terrain = new TerrainEditor(_context, Heights);
        Objects = new ObjectEditor(_context, Heights);
        Contents = new ObjectContentsEditor(_context);
        Monsters = new MonsterEditor(_context, Objects);
        Towns = new TownEditor(_context);
        Heroes = new HeroEditor(_context);
        MapEvents = new MapEventEditor(_context);
        Players = new PlayerEditor(_context);
        Map = new MapPropertiesEditor(_context);
    }

    public GameState Scenario => _context.Scenario;

    public MapObjectCatalog Catalog => _context.Catalog;

    public IEventSubscriber Events => _context.Bus;

    public UndoHistory History => _context.History;

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
}
