using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
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

    /// <summary>A player slot still owns heroes, mines or towns, or it is the last slot.</summary>
    PlayerInUse,

    /// <summary>Already <see cref="GameState.MaxPlayers"/> player slots.</summary>
    TooManyPlayers,

    /// <summary>The creature id is not in the scenario's creature catalog.</summary>
    UnknownCreature,

    /// <summary>A number is out of range (negative stats, empty stacks, bad slot).</summary>
    InvalidValue,

    /// <summary>The line is not one of the town's, or its dwellings would break the 7-of-9 rule (ADR-030).</summary>
    DwellingRule,

    /// <summary>A town building would lack a building it requires, or one that requires it would lose it.</summary>
    RequirementMissing,
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
        var changes = Brush(center, radius)
            .Where(p => map.GetTerrain(p) != terrain)
            .Select(p => (Position: p, Previous: map.GetTerrain(p)))
            .ToList();

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

    /// <summary>Paints (or, with <see cref="RoadType.None"/>, erases) roads with a square brush. Returns tiles changed.</summary>
    public int PaintRoad(MapPosition center, RoadType road, int radius = 0)
    {
        var map = Scenario.Map;
        var changes = Brush(center, radius)
            .Where(p => map.GetRoad(p) != road)
            .Select(p => (Position: p, Previous: map.GetRoad(p)))
            .ToList();

        if (changes.Count == 0)
        {
            return 0;
        }

        var positions = changes.Select(c => c.Position).ToList();
        Run(new DelegateCommand(
            road == RoadType.None ? "Erase road" : $"Paint {road} road",
            () =>
            {
                positions.ForEach(p => map.SetRoad(p, road));
                _bus.Publish(new RoadsPainted(positions, road));
            },
            () =>
            {
                foreach (var group in changes.GroupBy(c => c.Previous))
                {
                    var restored = group.Select(c => c.Position).ToList();
                    restored.ForEach(p => map.SetRoad(p, group.Key));
                    _bus.Publish(new RoadsPainted(restored, group.Key));
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

            if (cell == FootprintCell.Visit && !Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(position)))
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

        // Whatever points at the removed object loses its link: a two-way partner, or one-way entrances.
        var dependents = mapObject switch
        {
            TwoWayGate { LinkedGate: { } partner } => new List<MapObjectId> { partner },
            OneWayGateExit => Scenario.ObjectsOf<OneWayGateEntrance>().Where(e => e.Exit == id).Select(e => e.Id).ToList(),
            _ => new List<MapObjectId>(),
        };
        var links = SnapshotLinks([id, .. dependents]);
        Run(new DelegateCommand(
            $"Remove {mapObject.Definition.Name}",
            () =>
            {
                dependents.ForEach(d => SetLink(d, null));
                RemoveObject(mapObject);
            },
            () =>
            {
                AddObject(mapObject);
                RestoreLinks(links);
            }));
        return EditResult.Ok;
    }

    /// <summary>Points a one-way entrance at an exit (or clears it with null).</summary>
    public EditResult SetGateExit(MapObjectId entranceId, MapObjectId? exitId)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == entranceId) is not OneWayGateEntrance entrance ||
            (exitId is { } e && Scenario.Objects.FirstOrDefault(o => o.Id == e) is not OneWayGateExit))
        {
            return EditResult.InvalidTarget;
        }

        var previous = entrance.Exit;
        Run(new DelegateCommand("Set gate exit", () => SetLink(entrance.Id, exitId), () => SetLink(entrance.Id, previous)));
        return EditResult.Ok;
    }

    /// <summary>Links two two-way gates both ways, unlinking whatever they were linked to before.</summary>
    public EditResult LinkGates(MapObjectId first, MapObjectId second)
    {
        if (first == second ||
            Scenario.Objects.FirstOrDefault(o => o.Id == first) is not TwoWayGate a ||
            Scenario.Objects.FirstOrDefault(o => o.Id == second) is not TwoWayGate b)
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

    // Towns -----------------------------------------------------------------------------------

    public EditResult SetTownOwner(MapObjectId id, PlayerId? owner)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Town town)
        {
            return EditResult.InvalidTarget;
        }

        if (owner is { } player && !Scenario.HasPlayer(player))
        {
            return EditResult.UnknownPlayer;
        }

        var previous = town.Owner;
        Run(new DelegateCommand("Set town owner", () => Apply(owner), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(PlayerId? value)
        {
            var before = town.Owner;
            town.Owner = value;
            _bus.Publish(new TownOwnerChanged(town.Id, before, value));
        }
    }

    public EditResult SetTownName(MapObjectId id, string name)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Town town)
        {
            return EditResult.InvalidTarget;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return EditResult.InvalidValue;
        }

        var previous = town.Name;
        Run(new DelegateCommand("Rename town", () => Apply(name.Trim()), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(string value)
        {
            town.Name = value;
            _bus.Publish(new MapObjectChanged(town.Id));
        }
    }

    /// <summary>
    /// Pre-builds a town building up to <paramref name="level"/> (0 = not built). Requirements hold as in play: a level
    /// needs what it requires, and nothing built may lose a building it requires.
    /// </summary>
    public EditResult SetTownBuilding(MapObjectId id, string building, int level)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Town town ||
            BuiltInBuildings.DwellingTier(building) is not null || !Scenario.Buildings.Contains(building))
        {
            return EditResult.InvalidTarget;
        }

        if (level < 0 || level > Scenario.Buildings.MaxLevel(building))
        {
            return EditResult.InvalidValue;
        }

        if (TownRules.UnmetRequirement(Scenario, town, building, level) is not null)
        {
            return EditResult.RequirementMissing;
        }

        var previous = town.Level(building);
        Run(new DelegateCommand($"Set {building} of {town.Name}", () => Apply(level), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(int value)
        {
            town.SetLevel(building, value);
            _bus.Publish(new MapObjectChanged(town.Id));
        }
    }

    /// <summary>Pre-builds a line's dwelling: 0 none, 1 built, 2 upgraded; the 7-of-9 rule applies (ADR-030).</summary>
    public EditResult SetTownDwelling(MapObjectId id, string line, int level)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Town town)
        {
            return EditResult.InvalidTarget;
        }

        if (TownRules.Lines(Scenario, town).FirstOrDefault(l => l.Id == line) is not { } definition ||
            TownRules.CheckDwellingChoice(Scenario, town, line, level) is not null)
        {
            return EditResult.DwellingRule;
        }

        if (level < 0 || level > (definition.UpgradesTo is null ? 1 : 2))
        {
            return EditResult.InvalidValue;
        }

        var previous = town.DwellingLevel(line);
        Run(new DelegateCommand($"Set {definition.Name} dwelling of {town.Name}", () => Apply(level), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(int value)
        {
            town.SetDwelling(line, value);
            _bus.Publish(new MapObjectChanged(town.Id));
        }
    }

    /// <summary>Puts a stack into (or, with null, clears) one of a town's garrison slots.</summary>
    public EditResult SetGarrisonSlot(MapObjectId id, int slot, CreatureStack? stack)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Town town)
        {
            return EditResult.InvalidTarget;
        }

        if (!Army.IsValidSlot(slot))
        {
            return EditResult.InvalidValue;
        }

        if (stack is not null && !Scenario.Creatures.Contains(stack.Creature))
        {
            return EditResult.UnknownCreature;
        }

        var previous = town.Garrison[slot];
        Run(new DelegateCommand($"Edit garrison of {town.Name}", () => Apply(stack), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(CreatureStack? value)
        {
            town.Garrison.Set(slot, value);
            _bus.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
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

        if (!Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(position)))
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

    public EditResult SetHeroStats(HeroId id, int attack, int defense, SkillLevel tactics)
    {
        if (Scenario.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (attack < 0 || defense < 0 || !Enum.IsDefined(tactics) || tactics > BuiltInSkills.Get(BuiltInSkills.Tactics).MaxLevel)
        {
            return EditResult.InvalidValue;
        }

        var previous = (hero.Attack, hero.Defense, hero.Tactics);
        Run(new DelegateCommand($"Edit {hero.Name}", () => Apply((attack, defense, tactics)), () => Apply(previous)));
        return EditResult.Ok;

        void Apply((int Attack, int Defense, SkillLevel Tactics) stats)
        {
            (hero.Attack, hero.Defense, hero.Tactics) = stats;
            _bus.Publish(new HeroStatsChanged(hero.Id, stats.Attack, stats.Defense, stats.Tactics));
        }
    }

    /// <summary>
    /// Makes a hero one of the roster (<see cref="GameState.HeroRoster"/>), taking over its name, class, preferred
    /// creature, specialty, stats and starting skills; null turns it back into a custom hero (identity cleared, stats kept).
    /// </summary>
    public EditResult SetHeroType(HeroId id, string? type)
    {
        if (Scenario.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (type is not null && !Scenario.HeroRoster.Contains(type))
        {
            return EditResult.InvalidValue;
        }

        var before = Snapshot(hero);
        Run(new DelegateCommand($"Make {hero.Name} {type ?? "custom"}", () => Apply(type), () => Restore(before)));
        return EditResult.Ok;

        void Apply(string? newType)
        {
            if (newType is null)
            {
                (hero.Definition, hero.Class, hero.PreferredCreature, hero.Specialty) = (null, HeroClass.Warrior, null, null);
            }
            else
            {
                hero.Apply(Scenario.HeroRoster.Get(newType));
            }

            Changed();
        }

        void Restore((string? Definition, string Name, HeroClass Class, string? Preferred, HeroSpecialty? Specialty, int Attack, int Defense,
            Dictionary<string, SkillLevel> Skills) snapshot)
        {
            (hero.Definition, hero.Name, hero.Class, hero.PreferredCreature, hero.Specialty, hero.Attack, hero.Defense) =
                (snapshot.Definition, snapshot.Name, snapshot.Class, snapshot.Preferred, snapshot.Specialty, snapshot.Attack, snapshot.Defense);
            hero.Skills.ReplaceWith(snapshot.Skills);
            Changed();
        }

        void Changed() => _bus.Publish(new HeroStatsChanged(hero.Id, hero.Attack, hero.Defense, hero.Tactics));

        static (string?, string, HeroClass, string?, HeroSpecialty?, int, int, Dictionary<string, SkillLevel>) Snapshot(Hero h) =>
            (h.Definition, h.Name, h.Class, h.PreferredCreature, h.Specialty, h.Attack, h.Defense, h.Skills.Levels.ToDictionary(s => s.Key, s => s.Value));
    }

    /// <summary>Puts a stack into (or, with null, clears) one of a hero's army slots.</summary>
    public EditResult SetArmySlot(HeroId id, int slot, CreatureStack? stack)
    {
        if (Scenario.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (!Army.IsValidSlot(slot))
        {
            return EditResult.InvalidValue;
        }

        if (stack is not null && !Scenario.Creatures.Contains(stack.Creature))
        {
            return EditResult.UnknownCreature;
        }

        var previous = hero.Army[slot];
        Run(new DelegateCommand($"Edit army of {hero.Name}", () => Apply(stack), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(CreatureStack? value)
        {
            hero.Army.Set(slot, value);
            _bus.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        }
    }

    // Guards and chests -----------------------------------------------------------------------

    /// <summary>Places a neutral monster; a null <paramref name="count"/> is rolled from its tier at game start.</summary>
    public EditResult PlaceMonster(
        MapPosition position,
        string creature,
        int? count,
        GuardDisposition disposition,
        bool neverFlees,
        out MapObjectId id)
    {
        id = default;
        if (!Scenario.Creatures.Contains(creature))
        {
            return EditResult.UnknownCreature;
        }

        if (count < 1)
        {
            return EditResult.InvalidValue;
        }

        var definition = Catalog.OfKind(MapObjectKind.Monster).FirstOrDefault();
        if (definition is null)
        {
            return EditResult.InvalidTarget;
        }

        var check = CanPlaceObject(definition, position);
        if (check != EditResult.Ok)
        {
            return check;
        }

        var monster = new MonsterGuard(Scenario.NextObjectId(), definition, position, creature, count, disposition, neverFlees);
        id = monster.Id;
        Run(new DelegateCommand($"Place {Scenario.Creatures.Get(creature).Name}", () => AddObject(monster), () => RemoveObject(monster)));
        return EditResult.Ok;
    }

    public EditResult SetMonster(MapObjectId id, string creature, int? count, GuardDisposition disposition, bool neverFlees)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not MonsterGuard monster)
        {
            return EditResult.InvalidTarget;
        }

        if (!Scenario.Creatures.Contains(creature))
        {
            return EditResult.UnknownCreature;
        }

        if (count < 1)
        {
            return EditResult.InvalidValue;
        }

        var previous = (monster.Creature, monster.Count, monster.Disposition, monster.NeverFlees);
        Run(new DelegateCommand("Edit monster", () => Apply((creature, count, disposition, neverFlees)), () => Apply(previous)));
        return EditResult.Ok;

        void Apply((string Creature, int? Count, GuardDisposition Disposition, bool NeverFlees) value)
        {
            (monster.Creature, monster.Count, monster.Disposition, monster.NeverFlees) = value;
            _bus.Publish(new MapObjectChanged(monster.Id));
        }
    }

    /// <summary>Gives a mine a guard that must be beaten before the first flagging (null removes it).</summary>
    public EditResult SetMineGuard(MapObjectId id, GuardStack? guard)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Mine mine)
        {
            return EditResult.InvalidTarget;
        }

        if (guard is not null && !Scenario.Creatures.Contains(guard.Creature))
        {
            return EditResult.UnknownCreature;
        }

        if (guard?.Count < 1)
        {
            return EditResult.InvalidValue;
        }

        var previous = mine.Guard;
        Run(new DelegateCommand("Set mine guard", () => Apply(guard), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(GuardStack? value)
        {
            mine.Guard = value;
            _bus.Publish(new MapObjectChanged(mine.Id));
        }
    }

    /// <summary>Fixes what a chest offers (null = rolled when opened).</summary>
    public EditResult SetChestValues(MapObjectId id, ChestValues? values)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not TreasureChest chest)
        {
            return EditResult.InvalidTarget;
        }

        if (values is { Gold: < 0 } or { Experience: < 0 })
        {
            return EditResult.InvalidValue;
        }

        var previous = chest.FixedValues;
        Run(new DelegateCommand("Set chest values", () => Apply(values), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(ChestValues? value)
        {
            chest.FixedValues = value;
            _bus.Publish(new MapObjectChanged(chest.Id));
        }
    }

    /// <summary>Map-wide guard rule: HoMM3 zone of control (pickups can be stolen) or fully guarded area.</summary>
    public void SetGuardMode(GuardMode mode)
    {
        var previous = Scenario.GuardMode;
        Run(new DelegateCommand($"Guard mode {mode}", () => Apply(mode), () => Apply(previous)));

        void Apply(GuardMode value)
        {
            Scenario.SetGuardMode(value);
            _bus.Publish(new GuardModeChanged(value));
        }
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
            Scenario.Objects.OfType<IFlaggable>().Any(o => o.Owner == player.Id))
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

    /// <summary>Sets a player's default team (the setup screen may still override it unless teams are locked).</summary>
    public EditResult SetPlayerTeam(PlayerId id, TeamId team)
    {
        if (!Scenario.HasPlayer(id))
        {
            return EditResult.UnknownPlayer;
        }

        var player = Scenario.GetPlayer(id);
        var previous = player.Team;
        Run(new DelegateCommand($"Set team of {player.Name}", () => Apply(team), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(TeamId value)
        {
            player.Team = value;
            _bus.Publish(new PlayerSlotsChanged());
        }
    }

    /// <summary>Locks the map's teams so the game setup cannot change them (story/scenario maps).</summary>
    public void SetTeamsLocked(bool locked)
    {
        var previous = Scenario.TeamsLocked;
        Run(new DelegateCommand(locked ? "Lock teams" : "Unlock teams", () => Apply(locked), () => Apply(previous)));

        void Apply(bool value)
        {
            Scenario.SetTeamsLocked(value);
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

    /// <summary>Sets a two-way gate's partner or a one-way entrance's exit.</summary>
    private void SetLink(MapObjectId gateId, MapObjectId? linked)
    {
        switch (Scenario.GetObject(gateId))
        {
            case TwoWayGate gate:
                gate.LinkedGate = linked;
                break;
            case OneWayGateEntrance entrance:
                entrance.Exit = linked;
                break;
            default:
                throw new InvalidOperationException($"{gateId} is not a linkable gate.");
        }

        _bus.Publish(new GateLinkChanged(gateId, linked));
    }

    private Dictionary<MapObjectId, MapObjectId?> SnapshotLinks(params MapObjectId?[] gateIds) =>
        gateIds.OfType<MapObjectId>()
            .Distinct()
            .Select(id => Scenario.Objects.FirstOrDefault(o => o.Id == id))
            .Where(o => o is TwoWayGate or OneWayGateEntrance)
            .ToDictionary(o => o!.Id, o => o!.TeleportTarget);

    private IEnumerable<MapPosition> Brush(MapPosition center, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var position = center.Offset(dx, dy);
                if (Scenario.Map.Contains(position))
                {
                    yield return position;
                }
            }
        }
    }

    private void RestoreLinks(Dictionary<MapObjectId, MapObjectId?> links)
    {
        foreach (var (gate, linked) in links)
        {
            SetLink(gate, linked);
        }
    }
}
