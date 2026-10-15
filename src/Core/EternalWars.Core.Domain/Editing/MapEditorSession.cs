using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Editing;

public enum EditResult
{
    Ok,
    OutOfBounds,

    /// <summary>Overlaps another object or a hero.</summary>
    Overlaps,

    /// <summary>The tile a hero stands on, or an object's entrance, would be impassable terrain.</summary>
    ImpassableTerrain,
    UnknownPlayer,

    /// <summary>The target does not exist or is the wrong kind of object.</summary>
    InvalidTarget,

    /// <summary>A player slot still owns heroes or mines, or it is the last slot.</summary>
    PlayerInUse,

    /// <summary>Already <see cref="GameState.MaxPlayers"/> player slots.</summary>
    TooManyPlayers,
}

/// <summary>
/// The map editor's model: a scenario (<see cref="GameState"/> that never starts) plus undoable
/// edit operations. Every change goes through an <see cref="IEditorCommand"/> and publishes events,
/// so the editor UI is a thin view exactly like the game's.
/// </summary>
public sealed class MapEditorSession
{
    private readonly IEventBus _bus;
    private List<IEditorCommand>? _stroke;
    private string _strokeDescription = "";

    public MapEditorSession(GameState scenario, MapObjectCatalog? catalog = null, IEventBus? bus = null)
    {
        Scenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        Catalog = catalog ?? MapObjectCatalog.Default;
        _bus = bus ?? new EventBus();
    }

    public GameState Scenario { get; }

    public MapObjectCatalog Catalog { get; }

    public IEventSubscriber Events => _bus;

    public UndoHistory History { get; } = new();

    // Strokes ---------------------------------------------------------------------------------

    /// <summary>Groups all following edits into a single undo step until <see cref="EndStroke"/> (mouse down … mouse up).</summary>
    public void BeginStroke(string description)
    {
        if (_stroke is not null)
        {
            throw new InvalidOperationException("A stroke is already in progress.");
        }

        _stroke = [];
        _strokeDescription = description;
    }

    public void EndStroke()
    {
        var stroke = _stroke ?? throw new InvalidOperationException("No stroke in progress.");
        _stroke = null;
        if (stroke.Count > 0)
        {
            History.Record(stroke.Count == 1 ? stroke[0] : new CompositeCommand(_strokeDescription, stroke));
            PublishHistory();
        }
    }

    public bool Undo() => ChangeHistory(History.Undo);

    public bool Redo() => ChangeHistory(History.Redo);

    // Terrain ---------------------------------------------------------------------------------

    /// <summary>Paints a square brush (radius 0 = one tile). Returns the number of tiles changed.</summary>
    public int PaintTerrain(MapPosition center, TerrainType terrain, int radius = 0)
    {
        var map = Scenario.Map;
        var changes = new List<(MapPosition Position, TerrainType Previous)>();
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var position = center.Offset(dx, dy);
                if (map.Contains(position) && map.GetTerrain(position) != terrain)
                {
                    changes.Add((position, map.GetTerrain(position)));
                }
            }
        }

        if (changes.Count == 0)
        {
            return 0;
        }

        var positions = changes.Select(c => c.Position).ToList();
        Run(new DelegateCommand(
            $"Paint {terrain}",
            () =>
            {
                positions.ForEach(p => map.SetTerrain(p, terrain));
                _bus.Publish(new TerrainPainted(positions, terrain));
            },
            () =>
            {
                foreach (var group in changes.GroupBy(c => c.Previous))
                {
                    var restored = group.Select(c => c.Position).ToList();
                    restored.ForEach(p => map.SetTerrain(p, group.Key));
                    _bus.Publish(new TerrainPainted(restored, group.Key));
                }
            }));
        return changes.Count;
    }

    // Objects ---------------------------------------------------------------------------------

    public EditResult CanPlaceObject(MapObjectDefinition definition, MapPosition topLeft)
    {
        var probe = MapObject.Create(default, definition, topLeft);
        foreach (var (position, cell) in probe.OccupiedCells())
        {
            if (!Scenario.Map.Contains(position))
            {
                return EditResult.OutOfBounds;
            }

            if (Scenario.ObjectCellAt(position) is not null || Scenario.HeroAt(position) is not null)
            {
                return EditResult.Overlaps;
            }

            if (cell == FootprintCell.Visit && !Scenario.Map.GetTerrain(position).IsPassableOnFoot())
            {
                return EditResult.ImpassableTerrain;
            }
        }

        return EditResult.Ok;
    }

    public EditResult PlaceObject(string definitionId, MapPosition topLeft, out MapObjectId id)
    {
        id = default;
        if (!Catalog.TryGet(definitionId, out var definition))
        {
            return EditResult.InvalidTarget;
        }

        var check = CanPlaceObject(definition, topLeft);
        if (check != EditResult.Ok)
        {
            return check;
        }

        var mapObject = MapObject.Create(Scenario.NextObjectId(), definition, topLeft);
        id = mapObject.Id;
        Run(new DelegateCommand($"Place {definition.Name}", () => AddObject(mapObject), () => RemoveObject(mapObject)));
        return EditResult.Ok;
    }

    public EditResult RemoveObject(MapObjectId id)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not { } mapObject)
        {
            return EditResult.InvalidTarget;
        }

        var links = SnapshotLinks(id, (mapObject as SubterraneanGate)?.LinkedGate);
        Run(new DelegateCommand(
            $"Remove {mapObject.Definition.Name}",
            () =>
            {
                if (mapObject is SubterraneanGate { LinkedGate: { } partner })
                {
                    SetLink(partner, null);
                }

                RemoveObject(mapObject);
            },
            () =>
            {
                AddObject(mapObject);
                RestoreLinks(links);
            }));
        return EditResult.Ok;
    }

    /// <summary>Links two gates both ways, unlinking whatever they were linked to before.</summary>
    public EditResult LinkGates(MapObjectId first, MapObjectId second)
    {
        if (first == second ||
            Scenario.Objects.FirstOrDefault(o => o.Id == first) is not SubterraneanGate a ||
            Scenario.Objects.FirstOrDefault(o => o.Id == second) is not SubterraneanGate b)
        {
            return EditResult.InvalidTarget;
        }

        var links = SnapshotLinks(a.Id, b.Id, a.LinkedGate, b.LinkedGate);
        Run(new DelegateCommand(
            "Link gates",
            () =>
            {
                foreach (var old in new[] { a.LinkedGate, b.LinkedGate })
                {
                    if (old is { } partner && partner != a.Id && partner != b.Id)
                    {
                        SetLink(partner, null);
                    }
                }

                SetLink(a.Id, b.Id);
                SetLink(b.Id, a.Id);
            },
            () => RestoreLinks(links)));
        return EditResult.Ok;
    }

    public EditResult SetMineOwner(MapObjectId id, PlayerId? owner)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Mine mine)
        {
            return EditResult.InvalidTarget;
        }

        if (owner is { } player && !Scenario.HasPlayer(player))
        {
            return EditResult.UnknownPlayer;
        }

        var previous = mine.Owner;
        Run(new DelegateCommand("Set mine owner", () => SetOwner(owner), () => SetOwner(previous)));
        return EditResult.Ok;

        void SetOwner(PlayerId? value)
        {
            var before = mine.Owner;
            mine.Owner = value;
            _bus.Publish(new MineOwnerChanged(mine.Id, before, value));
        }
    }

    // Heroes ----------------------------------------------------------------------------------

    public EditResult PlaceHero(PlayerId owner, MapPosition position, string name, out HeroId id)
    {
        id = default;
        if (!Scenario.HasPlayer(owner))
        {
            return EditResult.UnknownPlayer;
        }

        if (!Scenario.Map.Contains(position))
        {
            return EditResult.OutOfBounds;
        }

        if (Scenario.HeroAt(position) is not null || Scenario.ObjectCellAt(position) is not null)
        {
            return EditResult.Overlaps;
        }

        if (!Scenario.Map.GetTerrain(position).IsPassableOnFoot())
        {
            return EditResult.ImpassableTerrain;
        }

        var hero = new Hero(Scenario.NextHeroId(), name, owner, position);
        id = hero.Id;
        Run(new DelegateCommand($"Place hero {name}", () => AddHero(hero), () => RemoveHero(hero)));
        return EditResult.Ok;
    }

    public EditResult RemoveHero(HeroId id)
    {
        if (Scenario.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        Run(new DelegateCommand($"Remove hero {hero.Name}", () => RemoveHero(hero), () => AddHero(hero)));
        return EditResult.Ok;
    }

    // Map properties and player slots -----------------------------------------------------------

    public void SetMapProperties(string name, string description)
    {
        var map = Scenario.Map;
        var (oldName, oldDescription) = (map.Name, map.Description);
        Run(new DelegateCommand("Edit map properties", () => Apply(name, description), () => Apply(oldName, oldDescription)));

        void Apply(string n, string d)
        {
            (map.Name, map.Description) = (n, d);
            _bus.Publish(new MapPropertiesChanged(n, d));
        }
    }

    public EditResult AddPlayer(string name, bool isHuman, out PlayerId id)
    {
        id = default;
        if (Scenario.Players.Count >= GameState.MaxPlayers)
        {
            return EditResult.TooManyPlayers;
        }

        var player = new Player(new PlayerId(Scenario.Players.Count), name, isHuman);
        id = player.Id;
        Run(new DelegateCommand(
            $"Add player {name}",
            () =>
            {
                Scenario.AddPlayer(player);
                _bus.Publish(new PlayerSlotsChanged());
            },
            () =>
            {
                Scenario.RemovePlayer(player);
                _bus.Publish(new PlayerSlotsChanged());
            }));
        return EditResult.Ok;
    }

    /// <summary>Only the last slot can be removed (player ids are turn-order indices), and only when unused.</summary>
    public EditResult RemoveLastPlayer()
    {
        var player = Scenario.Players[^1];
        if (Scenario.Players.Count == 1 || Scenario.HeroesOf(player.Id).Any() ||
            Scenario.ObjectsOf<Mine>().Any(m => m.Owner == player.Id))
        {
            return EditResult.PlayerInUse;
        }

        Run(new DelegateCommand(
            $"Remove player {player.Name}",
            () =>
            {
                Scenario.RemovePlayer(player);
                _bus.Publish(new PlayerSlotsChanged());
            },
            () =>
            {
                Scenario.AddPlayer(player);
                _bus.Publish(new PlayerSlotsChanged());
            }));
        return EditResult.Ok;
    }

    public EditResult UpdatePlayer(PlayerId id, string name, bool isHuman)
    {
        if (!Scenario.HasPlayer(id))
        {
            return EditResult.UnknownPlayer;
        }

        var player = Scenario.GetPlayer(id);
        var (oldName, oldIsHuman) = (player.Name, player.IsHuman);
        Run(new DelegateCommand($"Edit player {oldName}", () => Apply(name, isHuman), () => Apply(oldName, oldIsHuman)));
        return EditResult.Ok;

        void Apply(string n, bool human)
        {
            (player.Name, player.IsHuman) = (n, human);
            _bus.Publish(new PlayerSlotsChanged());
        }
    }

    // Plumbing --------------------------------------------------------------------------------

    private void Run(IEditorCommand command)
    {
        command.Execute();
        if (_stroke is not null)
        {
            _stroke.Add(command);
            return;
        }

        History.Record(command);
        PublishHistory();
    }

    private bool ChangeHistory(Func<bool> change)
    {
        if (_stroke is not null)
        {
            throw new InvalidOperationException("Finish the current stroke before undo/redo.");
        }

        var changed = change();
        if (changed)
        {
            PublishHistory();
        }

        return changed;
    }

    private void PublishHistory() => _bus.Publish(new UndoHistoryChanged(History.UndoDescription, History.RedoDescription));

    private void AddObject(MapObject mapObject)
    {
        Scenario.AddObject(mapObject);
        _bus.Publish(new MapObjectPlaced(mapObject.Id, mapObject.Definition.Id, mapObject.Position));
    }

    private void RemoveObject(MapObject mapObject)
    {
        Scenario.RemoveObject(mapObject);
        _bus.Publish(new MapObjectRemoved(mapObject.Id));
    }

    private void AddHero(Hero hero)
    {
        Scenario.AddHero(hero);
        _bus.Publish(new HeroPlaced(hero.Id, hero.Owner, hero.Position));
    }

    private void RemoveHero(Hero hero)
    {
        Scenario.RemoveHero(hero);
        _bus.Publish(new HeroRemoved(hero.Id));
    }

    private void SetLink(MapObjectId gateId, MapObjectId? linked)
    {
        var gate = (SubterraneanGate)Scenario.GetObject(gateId);
        gate.LinkedGate = linked;
        _bus.Publish(new GateLinkChanged(gateId, linked));
    }

    private Dictionary<MapObjectId, MapObjectId?> SnapshotLinks(params MapObjectId?[] gateIds) =>
        gateIds.OfType<MapObjectId>()
            .Distinct()
            .Select(id => Scenario.Objects.FirstOrDefault(o => o.Id == id))
            .OfType<SubterraneanGate>()
            .ToDictionary(g => g.Id, g => g.LinkedGate);

    private void RestoreLinks(Dictionary<MapObjectId, MapObjectId?> links)
    {
        foreach (var (gate, linked) in links)
        {
            SetLink(gate, linked);
        }
    }
}
