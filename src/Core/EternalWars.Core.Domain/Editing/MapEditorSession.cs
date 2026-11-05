using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
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

    /// <summary>The player would have more heroes than it may (ADR-068).</summary>
    HeroLimit,

    /// <summary>Map dwellings sell base creatures only (ADR-073); the creature is an upgrade.</summary>
    UpgradedCreature,

    /// <summary>A colour already has <see cref="MagiWindow.MaxEyes"/> Eyes of the Magi (ADR-070).</summary>
    TooManyEyes,
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

    /// <summary>Paints (or, with <see cref="RiverType.None"/>, erases) rivers with a square brush (ADR-079). Returns tiles changed.</summary>
    public int PaintRiver(MapPosition center, RiverType river, int radius = 0)
    {
        var map = Scenario.Map;
        var changes = Brush(center, radius)
            .Where(p => map.GetRiver(p) != river)
            .Select(p => (Position: p, Previous: map.GetRiver(p)))
            .ToList();

        if (changes.Count == 0)
        {
            return 0;
        }

        var positions = changes.Select(c => c.Position).ToList();
        Run(new DelegateCommand(
            river == RiverType.None ? "Erase river" : $"Paint {river} river",
            () =>
            {
                positions.ForEach(p => map.SetRiver(p, river));
                _bus.Publish(new RiversPainted(positions, river));
            },
            () =>
            {
                foreach (var group in changes.GroupBy(c => c.Previous))
                {
                    var restored = group.Select(c => c.Position).ToList();
                    restored.ForEach(p => map.SetRiver(p, group.Key));
                    _bus.Publish(new RiversPainted(restored, group.Key));
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

        if (definition.Kind == MapObjectKind.MagiEye && Scenario.Objects.OfType<MagiEye>().Count(e => e.Colour == definition.Colour) >= MagiWindow.MaxEyes)
        {
            return EditResult.TooManyEyes;
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

    /// <summary>Flags a resource provider for a player from the start (null = neutral; ADR-079).</summary>
    public EditResult SetProviderOwner(MapObjectId id, PlayerId? owner)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not ResourceProvider provider)
        {
            return EditResult.InvalidTarget;
        }

        if (owner is { } player && !Scenario.HasPlayer(player))
        {
            return EditResult.UnknownPlayer;
        }

        var previous = provider.Owner;
        Run(new DelegateCommand("Set provider owner", () => SetOwner(owner), () => SetOwner(previous)));
        return EditResult.Ok;

        void SetOwner(PlayerId? value)
        {
            var before = provider.Owner;
            provider.Owner = value;
            provider.WasClaimed = value is not null;
            _bus.Publish(new ProviderOwnerChanged(provider.Id, before, value));
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
    /// Gives a town another playable town type, e.g. after generating a map: a town of the new type takes its id, place, owner, garrison and,
    /// unless it kept the old type's default name, its name. Buildings the new type also has stay (at most its top level); each dwelling, and the
    /// creatures waiting in it, moves to the new type's line at the same place in the same tier. Fixed guild spells (v30) are of the old school and
    /// are dropped (undo brings them back). Refused when the result lacks a requirement.
    /// </summary>
    public EditResult SetTownType(MapObjectId id, string townType)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Town town || !BuiltInMapObjects.PlayableTowns.Contains(townType) ||
            !Catalog.TryGet(BuiltInMapObjects.TownId(townType), out var definition))
        {
            return EditResult.InvalidTarget;
        }

        if (townType == town.TownType)
        {
            return EditResult.Ok;
        }

        var replacement = TownRules.Retyped(Scenario, town, definition);
        replacement.IsRandom = town.IsRandom; // a random town's type only shows it in the editor
        if (TownRules.UnmetRequirement(Scenario, replacement) is not null)
        {
            return EditResult.RequirementMissing;
        }

        Run(new DelegateCommand($"Make {town.Name} a {townType} town", () => Swap(town, replacement), () => Swap(replacement, town)));
        return EditResult.Ok;

        void Swap(Town from, Town to)
        {
            RemoveObject(from);
            AddObject(to);
        }
    }

    /// <summary>
    /// Makes a town random or fixed (ADR-126): a random town's faction is drawn when the game starts, an owned one taking its owner's chosen faction.
    /// Making it random drops its fixed guild spells, which belong to one school (undo brings them back).
    /// </summary>
    public EditResult SetTownRandom(MapObjectId id, bool random)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Town town)
        {
            return EditResult.InvalidTarget;
        }

        if (town.IsRandom == random)
        {
            return EditResult.Ok;
        }

        var spells = town.FixedGuildSpells;
        Run(new DelegateCommand(random ? $"Make {town.Name} a random town" : $"Make {town.Name} a {town.TownType} town", () => Apply(random, random ? [] : spells), () => Apply(!random, spells)));
        return EditResult.Ok;

        void Apply(bool value, IReadOnlyList<string> guild)
        {
            town.IsRandom = value;
            town.FixedGuildSpells = guild;
            _bus.Publish(new MapObjectChanged(town.Id));
        }
    }

    /// <summary>
    /// Makes a monster random (ADR-126): <paramref name="tier"/> 0 = any tier, 1–7 = that tier, null = the creature as placed. A tier shows the first
    /// creature of that tier the draw could pick, so the editor has something to show; the creature is drawn when the game starts.
    /// </summary>
    public EditResult SetMonsterRandomTier(MapObjectId id, int? tier)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not MonsterGuard monster)
        {
            return EditResult.InvalidTarget;
        }

        if (tier is < 0 or > Creatures.CreatureDefinition.MaxTier)
        {
            return EditResult.InvalidValue;
        }

        var shown = tier is > 0 and var t ? PlaceholderRules.MonsterCandidates(Scenario.Creatures, t).FirstOrDefault()?.Id ?? monster.Creature : monster.Creature;
        var (oldTier, oldCreature) = (monster.RandomTier, monster.Creature);
        if (oldTier == tier && oldCreature == shown)
        {
            return EditResult.Ok;
        }

        Run(new DelegateCommand(tier is null ? "Make the monster fixed" : "Make the monster random", () => Apply(tier, shown), () => Apply(oldTier, oldCreature)));
        return EditResult.Ok;

        void Apply(int? value, string creature)
        {
            monster.RandomTier = value;
            monster.Creature = creature;
            _bus.Publish(new MapObjectChanged(monster.Id));
        }
    }

    /// <summary>
    /// Pre-builds a town building up to <paramref name="level"/> (0 = not built). Requirements hold as in play: a level
    /// needs what it requires, and nothing built may lose a building it requires.
    /// </summary>
    public EditResult SetTownBuilding(MapObjectId id, string building, int level)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Town town ||
            BuiltInBuildings.DwellingTier(building) is not null || !TownRules.Buildings(Scenario, town).Contains(building))
        {
            return EditResult.InvalidTarget;
        }

        if (level < 0 || level > TownRules.Buildings(Scenario, town).MaxLevel(building))
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

    /// <summary>
    /// Pre-builds a line's dwelling: 0 none, 1 built, 2 upgraded; the 7-of-9 rule (ADR-030) and the dwelling's
    /// requirements (the fort, ADR-066) apply.
    /// </summary>
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

        if (TownRules.UnmetDwellingRequirement(Scenario, town, definition, level) is not null)
        {
            return EditResult.RequirementMissing;
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

    /// <summary>Puts a stack into (or, with null, clears) one of the garrison slots of a town or a map garrison (ADR-070).</summary>
    public EditResult SetGarrisonSlot(MapObjectId id, int slot, CreatureStack? stack)
    {
        var (troops, name) = Scenario.Objects.FirstOrDefault(o => o.Id == id) switch
        {
            Town t => (t.Garrison, t.Name),
            MapGarrison g => (g.Troops, g.Definition.Name.ToLowerInvariant()),
            _ => (null, ""),
        };
        if (troops is null)
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

        var previous = troops[slot];
        Run(new DelegateCommand($"Edit garrison of {name}", () => Apply(stack), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(CreatureStack? value)
        {
            troops.Set(slot, value);
            _bus.Publish(new GarrisonChanged(id, troops.Slots.ToList()));
        }
    }

    /// <summary>Flags a map garrison for a player from the start (null = neutral; ADR-070).</summary>
    public EditResult SetMapGarrisonOwner(MapObjectId id, PlayerId? owner)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not MapGarrison garrison)
        {
            return EditResult.InvalidTarget;
        }

        if (owner is { } player && !Scenario.HasPlayer(player))
        {
            return EditResult.UnknownPlayer;
        }

        var previous = garrison.Owner;
        Run(new DelegateCommand("Set garrison owner", () => SetOwner(owner), () => SetOwner(previous)));
        return EditResult.Ok;

        void SetOwner(PlayerId? value)
        {
            var before = garrison.Owner;
            garrison.Owner = value;
            _bus.Publish(new GarrisonOwnerChanged(garrison.Id, before, value));
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

        if (Scenario.HeroAt(position) is not null || (Scenario.ObjectCellAt(position) is { } cell && !MapValidator.IsOwnTownEntrance(cell, owner)))
        {
            return EditResult.Overlaps;
        }

        if (!Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(position)))
        {
            return EditResult.ImpassableTerrain;
        }

        if (Scenario.HeroLimitReached(owner))
        {
            return EditResult.HeroLimit;
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

    /// <summary>Sets primary stats and Tactics; Spell Power and Knowledge stay as they are when omitted.</summary>
    public EditResult SetHeroStats(HeroId id, int attack, int defense, SkillLevel tactics, int? spellPower = null, int? knowledge = null)
    {
        if (Scenario.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (attack < 0 || defense < 0 || spellPower < 0 || knowledge < 0 || !Enum.IsDefined(tactics) || tactics > BuiltInSkills.Get(BuiltInSkills.Tactics).MaxLevel)
        {
            return EditResult.InvalidValue;
        }

        var previous = (hero.Attack, hero.Defense, hero.Tactics, hero.SpellPower, hero.Knowledge);
        var next = (attack, defense, tactics, spellPower ?? hero.SpellPower, knowledge ?? hero.Knowledge);
        Run(new DelegateCommand($"Edit {hero.Name}", () => Apply(next), () => Apply(previous)));
        return EditResult.Ok;

        void Apply((int Attack, int Defense, SkillLevel Tactics, int SpellPower, int Knowledge) stats)
        {
            (hero.Attack, hero.Defense, hero.Tactics, hero.SpellPower, hero.Knowledge) = stats;
            _bus.Publish(new HeroStatsChanged(hero.Id, stats.Attack, stats.Defense, stats.Tactics));
        }
    }

    /// <summary>Sets which war machines a hero owns (ADR-056).</summary>
    public EditResult SetHeroWarMachines(HeroId id, IReadOnlySet<Battles.WarMachineSlot> machines)
    {
        if (Scenario.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (machines.Any(m => !Enum.IsDefined(m)))
        {
            return EditResult.InvalidValue;
        }

        var previous = hero.WarMachines.ToList();
        var next = machines.ToList();
        Run(new DelegateCommand($"Edit {hero.Name}'s war machines", () => Apply(next), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(List<Battles.WarMachineSlot> slots)
        {
            hero.SetWarMachines(slots);
            _bus.Publish(new HeroStatsChanged(hero.Id, hero.Attack, hero.Defense, hero.Tactics));
        }
    }

    /// <summary>
    /// Sets what a hero wears (ids; each goes to the slot its definition names) and carries (ADR-061). Refused when an
    /// artifact is unknown, does not fit its slots, or the backpack would overflow.
    /// </summary>
    public EditResult SetHeroArtifacts(HeroId id, IReadOnlyList<string> worn, IReadOnlyList<string> backpack)
    {
        if (Scenario.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        var previous = (Worn: hero.Artifacts.AllWorn.Select(w => w.Id).ToList(), Backpack: hero.Artifacts.Backpack.ToList());
        var next = (Worn: worn.ToList(), Backpack: backpack.ToList());
        try
        {
            new Artifacts.HeroArtifacts().ReplaceWith(next.Worn, next.Backpack, Scenario.Artifacts); // validates without touching the hero
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException)
        {
            return EditResult.InvalidValue;
        }

        Run(new DelegateCommand($"Edit {hero.Name}'s artifacts", () => Apply(next), () => Apply(previous)));
        return EditResult.Ok;

        void Apply((List<string> Worn, List<string> Backpack) set)
        {
            hero.Artifacts.ReplaceWith(set.Worn, set.Backpack, Scenario.Artifacts);
            Scenario.RefreshArtifacts(hero);
            _bus.Publish(new HeroArtifactsChanged(hero.Id));
        }
    }

    /// <summary>Fixes which artifact a pickup holds (null = rolled from its tier when the game starts).</summary>
    public EditResult SetArtifactPickup(MapObjectId id, string? artifact)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not ArtifactPickup pickup)
        {
            return EditResult.InvalidTarget;
        }

        if (artifact is not null && !Scenario.Artifacts.Contains(artifact))
        {
            return EditResult.InvalidValue;
        }

        var previous = pickup.Artifact;
        Run(new DelegateCommand("Set artifact", () => Apply(artifact), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(string? value)
        {
            pickup.Artifact = value;
            _bus.Publish(new MapObjectChanged(pickup.Id));
        }
    }

    /// <summary>Sets a hero's morale and luck (−5…+5, ADR-053).</summary>
    public EditResult SetHeroMoraleAndLuck(HeroId id, int morale, int luck)
    {
        if (Scenario.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (Math.Abs(morale) > Battles.MoraleAndLuck.Limit || Math.Abs(luck) > Battles.MoraleAndLuck.Limit)
        {
            return EditResult.InvalidValue;
        }

        var previous = (hero.Morale, hero.Luck);
        Run(new DelegateCommand($"Edit {hero.Name}'s morale and luck", () => Apply((morale, luck)), () => Apply(previous)));
        return EditResult.Ok;

        void Apply((int Morale, int Luck) values)
        {
            (hero.Morale, hero.Luck) = values;
            _bus.Publish(new HeroStatsChanged(hero.Id, hero.Attack, hero.Defense, hero.Tactics));
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
            int SpellPower, int Knowledge, Dictionary<string, SkillLevel> Skills) snapshot)
        {
            (hero.Definition, hero.Name, hero.Class, hero.PreferredCreature, hero.Specialty, hero.Attack, hero.Defense, hero.SpellPower, hero.Knowledge) =
                (snapshot.Definition, snapshot.Name, snapshot.Class, snapshot.Preferred, snapshot.Specialty, snapshot.Attack, snapshot.Defense,
                 snapshot.SpellPower, snapshot.Knowledge);
            hero.Skills.ReplaceWith(snapshot.Skills);
            Changed();
        }

        void Changed() => _bus.Publish(new HeroStatsChanged(hero.Id, hero.Attack, hero.Defense, hero.Tactics));

        static (string?, string, HeroClass, string?, HeroSpecialty?, int, int, int, int, Dictionary<string, SkillLevel>) Snapshot(Hero h) =>
            (h.Definition, h.Name, h.Class, h.PreferredCreature, h.Specialty, h.Attack, h.Defense, h.SpellPower, h.Knowledge,
             h.Skills.Levels.ToDictionary(s => s.Key, s => s.Value));
    }

    /// <summary>
    /// Makes a hero a placeholder or a hero of its own again (ADR-126): a placeholder becomes a random roster hero of its owner's faction when the
    /// game starts, keeping its place, army and artifacts; what the editor sets on its type, stats, skills and spells is replaced then.
    /// </summary>
    public EditResult SetHeroPlaceholder(HeroId id, bool placeholder)
    {
        if (Scenario.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if (hero.IsPlaceholder == placeholder)
        {
            return EditResult.Ok;
        }

        Run(new DelegateCommand(placeholder ? $"Make {hero.Name} a hero placeholder" : $"Make {hero.Name} a hero of its own", () => Apply(placeholder), () => Apply(!placeholder)));
        return EditResult.Ok;

        void Apply(bool value)
        {
            hero.IsPlaceholder = value;
            _bus.Publish(new HeroStatsChanged(hero.Id, hero.Attack, hero.Defense, hero.Tactics));
        }
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

    /// <summary>
    /// Edits a map dwelling (ADR-054): its creature, its owner and the creatures waiting in it (null = one week's growth when
    /// the game starts). Map dwellings sell base creatures only (ADR-073).
    /// </summary>
    public EditResult SetDwelling(MapObjectId id, string creature, PlayerId? owner, int? available)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not CreatureDwelling dwelling)
        {
            return EditResult.InvalidTarget;
        }

        if (!Scenario.Creatures.Contains(creature))
        {
            return EditResult.UnknownCreature;
        }

        if (Scenario.Creatures.IsUpgrade(creature))
        {
            return EditResult.UpgradedCreature;
        }

        if (owner is { } player && !Scenario.HasPlayer(player))
        {
            return EditResult.UnknownPlayer;
        }

        if (available < 0)
        {
            return EditResult.InvalidValue;
        }

        var previous = (dwelling.Creature, dwelling.Owner, dwelling.Available);
        Run(new DelegateCommand("Edit dwelling", () => Apply((creature, owner, available)), () => Apply(previous)));
        return EditResult.Ok;

        void Apply((string Creature, PlayerId? Owner, int? Available) value)
        {
            var before = dwelling.Owner;
            (dwelling.Creature, dwelling.Owner, dwelling.Available) = value;
            _bus.Publish(new MapObjectChanged(dwelling.Id));
            if (before != value.Owner)
            {
                _bus.Publish(new DwellingOwnerChanged(dwelling.Id, before, value.Owner));
            }
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

    /// <summary>The message a sign post shows (ADR-068).</summary>
    public EditResult SetSignMessage(MapObjectId id, string message)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not SignPost sign)
        {
            return EditResult.InvalidTarget;
        }

        var previous = sign.Message;
        Run(new DelegateCommand("Set sign message", () => Apply(message), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(string value)
        {
            sign.Message = value;
            _bus.Publish(new MapObjectChanged(sign.Id));
        }
    }

    /// <summary>
    /// What a Pandora's Box holds, its guard (empty for none, at most seven stacks) and its message (ADR-100). Contents that are not gains are
    /// refused; an empty box is allowed while editing and is a validator error.
    /// </summary>
    public EditResult SetPandorasBox(MapObjectId id, BoxContents contents, IReadOnlyList<CreatureStack> guard, string? message)
    {
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(guard);
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not PandorasBox box)
        {
            return EditResult.InvalidTarget;
        }

        if (contents.Problem() is not null || guard.Count > Army.SlotCount || guard.Any(s => s.Count < 1 || !Scenario.Creatures.Contains(s.Creature))
            || contents.Creatures.Any(s => !Scenario.Creatures.Contains(s.Creature)))
        {
            return EditResult.InvalidValue;
        }

        message = string.IsNullOrEmpty(message) ? null : message;
        var previous = (box.Contents, box.Guard, box.Message);
        Run(new DelegateCommand("Set Pandora's box", () => Apply(contents, guard, message), () => Apply(previous.Contents, previous.Guard, previous.Message)));
        return EditResult.Ok;

        void Apply(BoxContents value, IReadOnlyList<CreatureStack> stacks, string? text)
        {
            (box.Contents, box.Guard, box.Message) = (value, stacks, text);
            _bus.Publish(new MapObjectChanged(box.Id));
        }
    }

    /// <summary>The hero event a Pandora's Box runs for its opener (MAP-35), or null for none; an unknown name is the validator's error.</summary>
    public EditResult SetPandorasBoxEvent(MapObjectId id, string? @event)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not PandorasBox box)
        {
            return EditResult.InvalidTarget;
        }

        @event = string.IsNullOrWhiteSpace(@event) ? null : @event.Trim();
        var previous = box.Event;
        Run(new DelegateCommand("Set Pandora's box event", () => Apply(@event), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(string? name)
        {
            box.Event = name;
            _bus.Publish(new MapObjectChanged(box.Id));
        }
    }

    /// <summary>The quest a Seer's Hut, Quest Guard or Quest Gate runs (EVT-5), or null for none; an unknown name is refused when the game loads.</summary>
    public EditResult SetQuestSiteQuest(MapObjectId id, string? quest)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not QuestSite site)
        {
            return EditResult.InvalidTarget;
        }

        quest = string.IsNullOrWhiteSpace(quest) ? null : quest.Trim();
        var previous = site.Quest;
        Run(new DelegateCommand("Set quest", () => Apply(quest), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(string? name)
        {
            site.Quest = name;
            _bus.Publish(new MapObjectChanged(site.Id));
        }
    }

    /// <summary>
    /// A sphinx's riddle (ADR-079, MAP-27): the author's question with its answer and two wrong ones, or null for one drawn from <c>riddles.csv</c>
    /// when the game starts. Refused when a text is empty or two answers are the same (ignoring case and spaces at the ends).
    /// </summary>
    public EditResult SetSphinxRiddle(MapObjectId id, Riddle? riddle)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Sphinx sphinx)
        {
            return EditResult.InvalidTarget;
        }

        if (riddle is not null)
        {
            riddle = new Riddle(riddle.Question.Trim(), riddle.Answer.Trim(), riddle.Wrong1.Trim(), riddle.Wrong2.Trim());
            string[] answers = [riddle.Answer, riddle.Wrong1, riddle.Wrong2];
            if (riddle.Question.Length == 0 || answers.Any(a => a.Length == 0) || answers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != answers.Length)
            {
                return EditResult.InvalidValue;
            }
        }

        var previous = sphinx.Riddle;
        Run(new DelegateCommand("Set riddle", () => Apply(riddle), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(Riddle? value)
        {
            sphinx.Riddle = value;
            _bus.Publish(new MapObjectChanged(sphinx.Id));
        }
    }

    /// <summary>
    /// A witch hut's two skills (ADR-079, MAP-34): two different known skills of any group, or none for two rolled when the game starts. Old skill ids
    /// are stored as their successors (ADR-072).
    /// </summary>
    public EditResult SetWitchHutSkills(MapObjectId id, IReadOnlyList<string> skills)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not WitchHut hut)
        {
            return EditResult.InvalidTarget;
        }

        if ((skills.Count != 0 && skills.Count != WitchHut.SkillCount) || skills.Any(s => !BuiltInSkills.Contains(s)))
        {
            return EditResult.InvalidValue;
        }

        var resolved = skills.Select(BuiltInSkills.Resolve).ToList();
        if (resolved.Distinct(StringComparer.Ordinal).Count() != resolved.Count)
        {
            return EditResult.InvalidValue;
        }

        var previous = hut.Skills;
        Run(new DelegateCommand("Set witch hut skills", () => Apply(resolved), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(IReadOnlyList<string> value)
        {
            hut.Skills = value;
            _bus.Publish(new MapObjectChanged(hut.Id));
        }
    }

    /// <summary>A shrine's spell (ADR-120): a spell of the shrine's level, or null for one drawn when the game starts.</summary>
    public EditResult SetShrineSpell(MapObjectId id, string? spell)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Shrine shrine)
        {
            return EditResult.InvalidTarget;
        }

        if (ShrineRules.SpellRefusal(Scenario.Spells, shrine.SpellLevel, spell) is not null)
        {
            return EditResult.InvalidValue;
        }

        var previous = shrine.Spell;
        Run(new DelegateCommand("Set shrine spell", () => Apply(spell), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(string? value)
        {
            shrine.Spell = value;
            _bus.Publish(new MapObjectChanged(shrine.Id));
        }
    }

    /// <summary>
    /// The spells a town's mage guild is fixed to teach (map v30): spells of the town's school, at most 3 / 3 / 2 / 2 per level, each once
    /// (<see cref="Magic.MagicRules.GuildSpellsProblem"/>); the game-start draw fills the rest. Empty = everything drawn. The order is kept as given.
    /// </summary>
    public EditResult SetTownGuildSpells(MapObjectId id, IReadOnlyList<string> spells)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Town town)
        {
            return EditResult.InvalidTarget;
        }

        if (Magic.MagicRules.GuildSpellsProblem(Scenario.Spells, town.TownType, spells) is not null)
        {
            return EditResult.InvalidValue;
        }

        var previous = town.FixedGuildSpells;
        IReadOnlyList<string> next = [.. spells];
        Run(new DelegateCommand($"Set guild spells of {town.Name}", () => Apply(next), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(IReadOnlyList<string> value)
        {
            town.FixedGuildSpells = value;
            _bus.Publish(new MapObjectChanged(town.Id));
        }
    }

    /// <summary>A hero's spell book and the spells in it (ADR-120): known spell ids, none without a book.</summary>
    public EditResult SetHeroSpells(HeroId id, bool spellBook, IReadOnlyList<string> spells)
    {
        if (Scenario.Heroes.FirstOrDefault(h => h.Id == id) is not { } hero)
        {
            return EditResult.InvalidTarget;
        }

        if ((!spellBook && spells.Count > 0) || spells.Any(s => !Scenario.Spells.Contains(s)) || spells.Distinct(StringComparer.Ordinal).Count() != spells.Count)
        {
            return EditResult.InvalidValue;
        }

        var previous = (Book: hero.HasSpellBook, Spells: hero.Spells.Keys.ToList());
        var next = (Book: spellBook, Spells: spells.ToList());
        Run(new DelegateCommand($"Edit {hero.Name}'s spells", () => Apply(next), () => Apply(previous)));
        return EditResult.Ok;

        void Apply((bool Book, List<string> Spells) set)
        {
            hero.SetSpellBook(set.Book, set.Spells);
            _bus.Publish(new HeroSpellsChanged(hero.Id));
        }
    }

    /// <summary>Who sits in a prison and at what level (ADR-068); null = drawn when the game starts.</summary>
    public EditResult SetPrison(MapObjectId id, string? hero, int? level)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not Prison prison)
        {
            return EditResult.InvalidTarget;
        }

        if (hero is not null && !Scenario.HeroRoster.Contains(hero))
        {
            return EditResult.InvalidValue;
        }

        if (level is < 1 or > 75)
        {
            return EditResult.InvalidValue;
        }

        var previous = (prison.Hero, prison.Level);
        Run(new DelegateCommand("Set prisoner", () => Apply(hero, level), () => Apply(previous.Hero, previous.Level)));
        return EditResult.Ok;

        void Apply(string? who, int? at)
        {
            (prison.Hero, prison.Level) = (who, at);
            _bus.Publish(new MapObjectChanged(prison.Id));
        }
    }

    /// <summary>
    /// How one monster stack guards its surroundings (ADR-065): Classic (HoMM3 zone of control, pickups can be taken
    /// from outside) or Area (treasure guard: touching anything in the 3×3 area starts the fight).
    /// </summary>
    public EditResult SetMonsterGuardMode(MapObjectId id, GuardMode mode)
    {
        if (Scenario.Objects.FirstOrDefault(o => o.Id == id) is not MonsterGuard monster)
        {
            return EditResult.InvalidTarget;
        }

        if (!Enum.IsDefined(mode))
        {
            return EditResult.InvalidValue;
        }

        var previous = monster.Mode;
        Run(new DelegateCommand($"Guard mode {mode}", () => Apply(mode), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(GuardMode value)
        {
            monster.Mode = value;
            _bus.Publish(new MapObjectChanged(monster.Id));
        }
    }

    /// <summary>
    /// Sets how the scenario is won and lost (ADR-057) and the map's hero limit (ADR-068); problems with the conditions are reported by
    /// <see cref="MapValidator"/>. Refused when the limit is out of range or below the heroes a player (without a limit of its own) already has.
    /// </summary>
    public EditResult SetScenarioRules(ScenarioRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (rules.MaxHeroes is < 1 or > ScenarioRules.HeroLimit || rules.WeekEvents.Problem() is not null)
        {
            return EditResult.InvalidValue;
        }

        if (Scenario.Players.Any(p => p.MaxHeroes is null && Scenario.HeroesOf(p.Id).Count() > rules.MaxHeroes))
        {
            return EditResult.HeroLimit;
        }

        var previous = Scenario.Rules;
        var what = previous with { Bans = rules.Bans } == rules ? "Edit the bans" : "Edit victory and defeat conditions";
        Run(new DelegateCommand(what, () => Apply(rules), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(ScenarioRules value)
        {
            Scenario.SetRules(value);
            _bus.Publish(new ScenarioRulesChanged());
        }
    }

    // Map events (EVT-6) -------------------------------------------------------------------------

    /// <summary>A name no event of the map has: <paramref name="stem"/>, else "stem 2", "stem 3", ….</summary>
    public string UnusedEventName(string stem)
    {
        stem = string.IsNullOrWhiteSpace(stem) ? "event" : stem.Trim();
        var name = stem;
        for (var i = 2; Scenario.MapEvents.Set.Find(name) is not null; i++)
        {
            name = $"{stem} {i}";
        }

        return name;
    }

    /// <summary>Adds an event at the end of the list (events run in list order). Refused when its name is empty or taken.</summary>
    public EditResult AddMapEvent(MapEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var set = Scenario.MapEvents.Set;
        if (string.IsNullOrWhiteSpace(definition.Name) || set.Find(definition.Name) is not null)
        {
            return EditResult.InvalidValue;
        }

        ChangeEvents($"Add event {definition.Name}", set with { Events = [.. set.Events, definition] });
        return EditResult.Ok;
    }

    /// <summary>
    /// Replaces the event <paramref name="name"/> (its tree, kind, towns, stages or name). A new name must be free; the cells, Pandora's boxes,
    /// quest objects and events that named the old one name the new one.
    /// </summary>
    public EditResult ReplaceMapEvent(string name, MapEventDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var set = Scenario.MapEvents.Set;
        if (set.Find(name) is null)
        {
            return EditResult.InvalidTarget;
        }

        var renamed = definition.Name != name;
        if (string.IsNullOrWhiteSpace(definition.Name) || (renamed && set.Find(definition.Name) is not null))
        {
            return EditResult.InvalidValue;
        }

        var events = set.Events.Select(e => e.Name == name ? definition : e);
        if (!renamed)
        {
            ChangeEvents($"Edit event {name}", set with { Events = [.. events] });
            return EditResult.Ok;
        }

        var to = definition.Name;
        EventAction Rename(EventAction action) => action switch
        {
            RunEventAction run when run.Event == name => run with { Event = to },
            EnableEventAction enable when enable.Event == name => enable with { Event = to },
            _ => action,
        };
        var next = set with
        {
            Events = [.. events.Select(e => MapEventTreeEdit.Rewrite(e, Rename))],
            Cells = [.. set.Cells.Select(c => c.Event == name ? c with { Event = to } : c)],
        };
        ChangeEvents($"Rename event {name} to {to}", next, [.. References(name).Select(o => (o, (string?)to))]);
        return EditResult.Ok;
    }

    /// <summary>
    /// Removes the event and its cells; the Pandora's boxes and quest objects that named it name none. Events that still run or enable it are the
    /// validator's errors.
    /// </summary>
    public EditResult RemoveMapEvent(string name)
    {
        var set = Scenario.MapEvents.Set;
        if (set.Find(name) is null)
        {
            return EditResult.InvalidTarget;
        }

        var next = set with { Events = [.. set.Events.Where(e => e.Name != name)], Cells = [.. set.Cells.Where(c => c.Event != name)] };
        ChangeEvents($"Remove event {name}", next, [.. References(name).Select(o => (o, (string?)null))]);
        return EditResult.Ok;
    }

    /// <summary>Moves the event up (negative) or down the list, which is the order events of one moment run in (EVT-2 e).</summary>
    public EditResult MoveMapEvent(string name, int offset)
    {
        var set = Scenario.MapEvents.Set;
        var from = set.Events.ToList().FindIndex(e => e.Name == name);
        if (from < 0)
        {
            return EditResult.InvalidTarget;
        }

        var to = Math.Clamp(from + offset, 0, set.Events.Count - 1);
        if (to != from)
        {
            var events = set.Events.ToList();
            var moved = events[from];
            events.RemoveAt(from);
            events.Insert(to, moved);
            ChangeEvents($"Move event {name}", set with { Events = events });
        }

        return EditResult.Ok;
    }

    /// <summary>Declares a map variable, or changes its starting value (EVT-3 c). Refused for an empty name.</summary>
    public EditResult SetMapVariable(string name, int value)
    {
        var set = Scenario.MapEvents.Set;
        if (string.IsNullOrWhiteSpace(name))
        {
            return EditResult.InvalidValue;
        }

        name = name.Trim();
        if (set.Variables.TryGetValue(name, out var old) && old == value)
        {
            return EditResult.Ok;
        }

        var variables = new Dictionary<string, int>(set.Variables, StringComparer.Ordinal) { [name] = value };
        ChangeEvents($"Set variable {name}", set with { Variables = variables });
        return EditResult.Ok;
    }

    /// <summary>Removes a map variable; events that still use it are the validator's errors.</summary>
    public EditResult RemoveMapVariable(string name)
    {
        var set = Scenario.MapEvents.Set;
        if (!set.Variables.ContainsKey(name))
        {
            return EditResult.InvalidTarget;
        }

        ChangeEvents($"Remove variable {name}", set with
        {
            Variables = set.Variables.Where(v => v.Key != name).ToDictionary(StringComparer.Ordinal),
            KeptVariables = [.. set.KeptVariables.Where(v => v != name)],
            TakenVariables = [.. set.TakenVariables.Where(v => v != name)],
        });
        return EditResult.Ok;
    }

    /// <summary>
    /// Marks a map variable as a campaign variable (EVT-1 f, FLOW-1): <paramref name="kept"/> "kept for the next map", <paramref name="taken"/>
    /// "taken from the last map" (both, or neither for an ordinary variable). Refused for a variable that is not declared.
    /// </summary>
    public EditResult SetCampaignVariable(string name, bool kept, bool taken)
    {
        var set = Scenario.MapEvents.Set;
        if (!set.Variables.ContainsKey(name))
        {
            return EditResult.InvalidTarget;
        }

        if (set.IsKept(name) == kept && set.IsTaken(name) == taken)
        {
            return EditResult.Ok;
        }

        static IReadOnlyList<string> Mark(IReadOnlyList<string> names, string name, bool on) =>
            [.. names.Where(v => v != name).Concat(on ? [name] : []).Order(StringComparer.Ordinal)];
        ChangeEvents($"Campaign variable {name}", set with { KeptVariables = Mark(set.KeptVariables, name, kept), TakenVariables = Mark(set.TakenVariables, name, taken) });
        return EditResult.Ok;
    }

    /// <summary>
    /// Puts an invisible cell of the hero event <paramref name="event"/> at the position (EVT-2), replacing any cell there, or removes the cell there
    /// (null). Refused off the map, on impassable ground or under an object, and for a name that is not a hero event.
    /// </summary>
    public EditResult SetEventCell(MapPosition position, string? @event)
    {
        var set = Scenario.MapEvents.Set;
        var old = set.Cells.FirstOrDefault(c => c.Position == position);
        if (@event is null)
        {
            if (old is null)
            {
                return EditResult.InvalidTarget;
            }

            ChangeEvents("Remove event cell", set with { Cells = [.. set.Cells.Where(c => c != old)] });
            return EditResult.Ok;
        }

        if (!Scenario.Map.Contains(position))
        {
            return EditResult.OutOfBounds;
        }

        if (!Scenario.Terrains.IsPassable(Scenario.Map.GetTerrain(position)))
        {
            return EditResult.ImpassableTerrain;
        }

        if (Scenario.ObjectCellAt(position) is not null)
        {
            return EditResult.Overlaps;
        }

        if (set.Find(@event) is not { Kind: MapEventKind.Hero })
        {
            return EditResult.InvalidTarget;
        }

        if (old?.Event == @event)
        {
            return EditResult.Ok;
        }

        var cell = new EventCell(position.X, position.Y, position.Level, @event);
        ChangeEvents($"Place event cell of {@event}", set with { Cells = [.. set.Cells.Where(c => c != old), cell] });
        return EditResult.Ok;
    }

    /// <summary>The Pandora's boxes and quest objects that name the event.</summary>
    private IEnumerable<MapObject> References(string name) =>
        Scenario.Objects.Where(o => o is PandorasBox { Event: var e } && e == name || o is QuestSite { Quest: var q } && q == name);

    /// <summary>One undoable change of the map events, with the event names some objects get (a rename or removal).</summary>
    private void ChangeEvents(string description, MapEventSet next, IReadOnlyList<(MapObject Object, string? Name)>? names = null)
    {
        var previous = Scenario.MapEvents.Set;
        var after = names ?? [];
        var before = after.Select(n => (n.Object, Name: n.Object is PandorasBox box ? box.Event : ((QuestSite)n.Object).Quest)).ToList();
        Run(new DelegateCommand(description, () => Apply(next, after), () => Apply(previous, before)));

        void Apply(MapEventSet set, IReadOnlyList<(MapObject Object, string? Name)> objects)
        {
            foreach (var (mapObject, name) in objects)
            {
                switch (mapObject)
                {
                    case PandorasBox box:
                        box.Event = name;
                        break;
                    case QuestSite site:
                        site.Quest = name;
                        break;
                }

                _bus.Publish(new MapObjectChanged(mapObject.Id));
            }

            Scenario.SetMapEvents(set, check: false);
            _bus.Publish(new MapEventsChanged());
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

    /// <summary>
    /// Gives a player its own hero limit (campaign-like maps, ADR-068), or null for the map's. Refused below the heroes it already has.
    /// </summary>
    public EditResult SetPlayerHeroLimit(PlayerId id, int? limit)
    {
        if (!Scenario.HasPlayer(id))
        {
            return EditResult.UnknownPlayer;
        }

        if (limit is < 1 or > ScenarioRules.HeroLimit)
        {
            return EditResult.InvalidValue;
        }

        if (Scenario.HeroesOf(id).Count() > (limit ?? Scenario.Rules.MaxHeroes))
        {
            return EditResult.HeroLimit;
        }

        var player = Scenario.GetPlayer(id);
        var previous = player.MaxHeroes;
        Run(new DelegateCommand($"Set hero limit of {player.Name}", () => Apply(limit), () => Apply(previous)));
        return EditResult.Ok;

        void Apply(int? value)
        {
            Scenario.SetHeroLimit(id, value);
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

    /// <summary>
    /// Sets the team counts a game of the map may have (ADR-070, ADR-117), 2 to 8 each, null for no limit; overrides what a template gave a
    /// generated map. Refused (false) for counts out of range or the fewest above the most.
    /// </summary>
    public bool SetTeamCounts(int? min, int? max)
    {
        if (min is < 2 or > GameState.MaxPlayers || max is < 2 or > GameState.MaxPlayers || (min is { } low && max is { } high && low > high))
        {
            return false;
        }

        var previous = (Scenario.MinTeams, Scenario.MaxTeams);
        if (previous == (min, max))
        {
            return true;
        }

        Run(new DelegateCommand("Set team counts", () => Apply(min, max), () => Apply(previous.MinTeams, previous.MaxTeams)));
        return true;

        void Apply(int? fewest, int? most)
        {
            Scenario.SetTeamCounts(fewest, most);
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
