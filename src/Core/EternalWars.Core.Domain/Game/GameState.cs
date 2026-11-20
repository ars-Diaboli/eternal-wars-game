using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Visibility;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// Complete snapshot of a game (or, in the editor, of a scenario before it starts).
/// Mutated only by Domain services and editor commands, hence the internal mutators.
/// </summary>
public sealed class GameState
{
    public const int MaxPlayers = 8;

    private readonly List<Player> _players;
    private readonly List<Hero> _heroes = [];
    private readonly Dictionary<HeroId, Hero> _heroById = [];
    private readonly List<Hero> _retiredHeroes = [];
    private int _highestHeroId;
    private readonly List<MapObject> _objects = [];
    private readonly Dictionary<MapObjectId, MapObject> _objectById = [];
    private readonly List<Towns.Caravan> _caravans = [];
    private int _highestCaravanId;
    private readonly Dictionary<MapPosition, (MapObject Object, FootprintCell Cell)> _objectCells = [];

    // Indexes (lookups the pathfinder makes for every tile it expands). Rebuilt or kept up to date by the mutators below; never part of the state.
    private readonly Dictionary<MapPosition, Hero> _heroAt = [];
    private long _heroAtMoves = -1; // HeroMoves.Count when _heroAt was built; -1 = rebuild
    private int[] _guards; // per tile: monster cells on its eight neighbours
    private readonly Dictionary<Type, object> _objectsOfType = []; // T -> read-only list of T, in Objects order
    private List<Towns.Town>? _townsById;
    private List<IFlaggable>? _flaggables;

    public GameState(
        AdventureMap map,
        IEnumerable<Player> players,
        IEnumerable<Hero> heroes,
        IEnumerable<MapObject>? objects = null,
        GameOptions? options = null,
        TerrainCatalog? terrains = null,
        CreatureCatalog? creatures = null,
        HeroCatalog? heroRoster = null,
        BuildingCatalog? buildings = null,
        ArtifactCatalog? artifacts = null,
        MapObjectCatalog? objectCatalog = null,
        Magic.SpellCatalog? spells = null,
        ContentCatalogs? content = null)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        // A table given on its own wins over the content's; one given neither way is the built-in table.
        ObjectCatalog = objectCatalog ?? content?.Objects ?? MapObjectCatalog.Default;
        Options = options ?? GameOptions.Default;
        Terrains = terrains ?? content?.Terrains ?? TerrainCatalog.Default;
        Creatures = creatures ?? content?.Creatures ?? CreatureCatalog.Default;
        HeroRoster = heroRoster ?? content?.Heroes ?? HeroCatalog.Default;
        Buildings = buildings ?? content?.Buildings ?? BuildingCatalog.Default;
        Artifacts = artifacts ?? content?.Artifacts ?? ArtifactCatalog.Default;
        Spells = spells ?? content?.Spells ?? Artifacts.Spells;
        Random = new GameRandom(Options.Seed);
        Fog = new FogOfWar(map, Options.FogMode);
        _guards = new int[map.Levels * map.Width * map.Height];
        _players = players.ToList();
        if (_players.Count is 0 or > MaxPlayers)
        {
            throw new ArgumentException($"A game needs 1 to {MaxPlayers} players.", nameof(players));
        }

        foreach (var player in _players)
        {
            if (Options.Colours?.GetValueOrDefault(player.Id.Value, -1) is int colour and >= 0 and < Player.ColourCount && colour != player.Colour)
            {
                if (player.Name == Player.ColourNames[player.Colour])
                {
                    player.Name = Player.ColourNames[colour];
                }

                player.Colour = colour;
            }
        }

        foreach (var hero in heroes)
        {
            AddHero(hero);
        }

        foreach (var mapObject in objects ?? [])
        {
            AddObject(mapObject);
        }
    }

    /// <summary>The game's content tables, to build another game of the same content (a replay).</summary>
    public ContentCatalogs Content => new(Creatures, HeroRoster, Buildings, Artifacts, ObjectCatalog, Terrains);

    public AdventureMap Map { get; }

    public GameOptions Options { get; }

    /// <summary>Terrain movement rules in effect for this game.</summary>
    public TerrainCatalog Terrains { get; }

    /// <summary>Creature definitions in effect for this game.</summary>
    public CreatureCatalog Creatures { get; }

    /// <summary>Hero roster (<see cref="HeroDefinition"/>s) map heroes may be made from (ADR-048).</summary>
    public HeroCatalog HeroRoster { get; }

    /// <summary>Town building levels, costs and requirements in effect for this game (ADR-046).</summary>
    public BuildingCatalog Buildings { get; }

    /// <summary>Map object definitions in effect for this game (objects that turn into others, e.g. a cleared abandoned mine, ADR-068).</summary>
    public MapObjectCatalog ObjectCatalog { get; }

    /// <summary>Artifacts in effect for this game (ADR-061).</summary>
    public ArtifactCatalog Artifacts { get; }

    /// <summary>Spells in effect for this game (ADR-110).</summary>
    public Magic.SpellCatalog Spells { get; }

    /// <summary>The game RNG; only Domain services draw from it, in command order, so games replay exactly.</summary>
    internal GameRandom Random { get; }

    /// <summary>Relics handed out as rewards since the pool was last refilled (<c>ArtifactService.Draw</c>, ADR-068).</summary>
    internal SortedSet<Common.ArtifactId> DrawnRelics { get; } = [];

    /// <summary>Current state of the game RNG (saves, replay checks).</summary>
    public ulong RandomState => Random.State;

    /// <summary>The battle being fought, if any (a <see cref="Decisions.BattleDecision"/> is pending meanwhile).</summary>
    public BattleState? Battle { get; internal set; }

    public FogOfWar Fog { get; }

    /// <summary>Per-player last known state of objects (refog mode).</summary>
    public ObjectMemory Memory { get; } = new();

    /// <summary>Set by the map author: teams from the map may not be changed at game setup.</summary>
    public bool TeamsLocked { get; private set; }

    public IReadOnlyList<Player> Players => _players;

    public IReadOnlyList<Hero> Heroes => _heroes;

    /// <summary>
    /// Heroes that retreated or surrendered (ADR-081): off the map, waiting in their player's taverns as they were, oldest first. They do not
    /// count towards the hero limit.
    /// </summary>
    public IReadOnlyList<Hero> RetiredHeroes => _retiredHeroes;

    /// <summary>
    /// The carried heroes this campaign map had no place for (ADR-241): they wait for a later map. Set when the game starts, from the options and
    /// the map, so a replay sets it the same; not part of the digest.
    /// </summary>
    internal IReadOnlyList<Campaigns.CarriedHero> CarriedWaiting { get; set; } = [];

    /// <summary>
    /// A map event's end of the game (<see cref="MapEvents.EndGameAction"/>, ADR-242), waiting for the game flow to settle it after the command; no
    /// event runs meanwhile. Never outlives the command.
    /// </summary>
    internal ScriptedEnd? ScriptedEnd { get; set; }

    public IEnumerable<Hero> RetiredHeroesOf(PlayerId owner) => _retiredHeroes.Where(h => h.Owner == owner);

    public IReadOnlyList<MapObject> Objects => _objects;

    /// <summary>Caravans on their way (ADR-054): virtual, they have no place on the map. Created by commands only, so replays rebuild them.</summary>
    public IReadOnlyList<Towns.Caravan> Caravans => _caravans;

    public GameDate Date { get; internal set; } = GameDate.First;

    /// <summary>This week's event (ADR-073), announced when the week starts.</summary>
    public Turns.WeekEvent WeekEvent { get; internal set; } = Turns.WeekEvent.None;

    /// <summary>
    /// Next week's event, rolled a week ahead so the Astrologer's Tower can tell it (ADR-077, MAP-32: no re-roll). Front-ends show it only when a
    /// tower is consulted (<see cref="Events.AstrologerConsulted"/>).
    /// </summary>
    public Turns.WeekEvent NextWeekEvent { get; internal set; } = Turns.WeekEvent.None;

    public int CurrentPlayerIndex { get; internal set; }

    public bool IsStarted { get; internal set; }

    /// <summary>How the scenario is won and lost (ADR-057); set by the map, changed before the game starts only.</summary>
    public ScenarioRules Rules { get; private set; } = ScenarioRules.Default;

    /// <summary>What the scenario rules remember from the start (ADR-213); set when the game starts, kept by <see cref="GameFlowService"/>.</summary>
    public ScenarioTracking Tracking { get; internal set; } = ScenarioTracking.None;

    /// <summary>How the game ended, or null while it is running.</summary>
    public GameOutcome? Outcome { get; internal set; }

    public bool IsOver => Outcome is not null;

    /// <summary>The arena map's battle (ADR-097), chosen at the start of day 2; null before, and on every other map.</summary>
    public ArenaBattle? Arena { get; internal set; }

    public PendingDecision? PendingDecision
    {
        get => _pendingDecision;
        internal set
        {
            _pendingDecision = value;
            DecisionPlayer = value is null ? null : HeroOwner(value.Hero) ?? CurrentPlayer.Id;
        }
    }

    /// <summary>Whose question <see cref="PendingDecision"/> is: its hero's owner, else the player whose turn asked it; null without one.</summary>
    public PlayerId? DecisionPlayer { get; private set; }

    private PendingDecision? _pendingDecision;

    private PlayerId? HeroOwner(HeroId hero) => (_heroById.GetValueOrDefault(hero) ?? GarrisonHero(hero))?.Owner;

    // The simultaneous days (MP-2, ADR-197) ---------------------------------------------------------------

    private readonly List<PlayerId> _openTurns = [];

    /// <summary>
    /// The people whose turns are open at once on a simultaneous day (MP-2, ADR-197), in player order; empty on a day played turn by turn and
    /// once every person has ended the turn (the computer players then play theirs one by one).
    /// </summary>
    public IReadOnlyList<PlayerId> OpenTurns => _openTurns;

    internal List<PlayerId> OpenTurnList => _openTurns;

    /// <summary>Whether several people's turns are open at once (<see cref="OpenTurns"/>).</summary>
    public bool IsSimultaneous => _openTurns.Count > 0;

    /// <summary>Whether <paramref name="player"/> may act on the map now: its turn is open (a simultaneous day) or it is the current player.</summary>
    public bool IsTurnOf(PlayerId player) => IsSimultaneous ? _openTurns.Contains(player) : CurrentPlayer.Id == player;

    /// <summary>The players who may act on the map now: those with an open turn, the current player first; else the current player.</summary>
    public IEnumerable<PlayerId> PlayersToAct => IsSimultaneous ? _openTurns.OrderBy(p => p == CurrentPlayer.Id ? 0 : 1) : [CurrentPlayer.Id];

    /// <summary>The players who ended their turn today on a simultaneous day; they do not play again until the next day.</summary>
    internal HashSet<PlayerId> DoneToday { get; } = [];

    /// <summary>The players whose turn began today (its income, movement and questions), so a turn begins once a day.</summary>
    internal HashSet<PlayerId> BegunToday { get; } = [];

    /// <summary>A conflict had a simultaneous day played again turn by turn: every day after it is too (MP-2).</summary>
    public bool SimultaneousEnded { get; internal set; }

    /// <summary>
    /// Why today's simultaneous play must stop (a conflict, MP-2): the LAN host plays the day again turn by turn; until then every command is
    /// refused. Null otherwise.
    /// </summary>
    public string? SimultaneousConflict { get; internal set; }

    /// <summary>The player's place in the turn order.</summary>
    internal int IndexOf(PlayerId player) => _players.FindIndex(p => p.Id == player);

    public Player CurrentPlayer => _players[CurrentPlayerIndex];

    /// <summary>The player; an unknown id refuses the command that names it (<see cref="CommandRefusedException"/>).</summary>
    public Player GetPlayer(PlayerId id) => _players.FirstOrDefault(p => p.Id == id) ?? throw new CommandRefusedException($"Unknown player {id}.");

    public bool HasPlayer(PlayerId id) => _players.Any(p => p.Id == id);

    /// <summary>A hero on the map or in a town's garrison (ADR-112); an unknown id refuses the command that names it.</summary>
    public Hero GetHero(HeroId id) => _heroById.GetValueOrDefault(id) ?? GarrisonHero(id) ?? throw new CommandRefusedException($"Unknown hero {id}.");

    private Hero? GarrisonHero(HeroId id)
    {
        foreach (var town in TownsById)
        {
            if (town.GarrisonHero is { } hero && hero.Id == id)
            {
                return hero;
            }
        }

        return null;
    }

    /// <summary>The player's heroes on the map (garrison heroes are not, ADR-112).</summary>
    public IEnumerable<Hero> HeroesOf(PlayerId owner) => _heroes.Where(h => h.Owner == owner);

    /// <summary>The heroes in towns' garrisons, by town id (ADR-112): off the map, not counted towards the hero limit.</summary>
    public IEnumerable<Hero> GarrisonHeroes => TownsById.Select(t => t.GarrisonHero).OfType<Hero>();

    /// <summary>The towns, lowest id first (cached until a town is added or removed).</summary>
    private List<Towns.Town> TownsById => _townsById ??= [.. ObjectsOf<Towns.Town>().OrderBy(t => t.Id.Value)];

    /// <summary>The objects that can fly a flag (towns, mines, dwellings, providers, garrisons), in map order (cached until one is added or removed).</summary>
    internal IReadOnlyList<IFlaggable> Flaggables => _flaggables ??= [.. _objects.OfType<IFlaggable>()];

    /// <summary>Every hero of the player: on the map, then in garrisons (income, mana, movement and statuses count them all, ADR-112).</summary>
    public IEnumerable<Hero> AllHeroesOf(PlayerId owner) => HeroesOf(owner).Concat(GarrisonHeroes.Where(h => h.Owner == owner));

    /// <summary>The town whose garrison holds the hero, or null (ADR-112).</summary>
    public Towns.Town? GarrisonOf(HeroId hero) => ObjectsOf<Towns.Town>().FirstOrDefault(t => t.GarrisonHero?.Id == hero);

    /// <summary>How many heroes the player may have: its own limit, else the map's (<see cref="ScenarioRules.MaxHeroes"/>, ADR-068).</summary>
    public int HeroLimit(PlayerId owner) => GetPlayer(owner).MaxHeroes ?? Rules.MaxHeroes;

    /// <summary>Whether the player already has as many heroes as it may (<see cref="HeroLimit"/>).</summary>
    public bool HeroLimitReached(PlayerId owner) => HeroesOf(owner).Count() >= HeroLimit(owner);

    /// <summary>Scenario setting (map loading, editor): a player's own hero limit, 1 to 8, or null for the map's (ADR-068).</summary>
    public void SetHeroLimit(PlayerId owner, int? limit)
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("Hero limits can only be changed before the game starts.");
        }

        if (limit is < 1 or > ScenarioRules.HeroLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, $"A player may have 1 to {ScenarioRules.HeroLimit} heroes.");
        }

        GetPlayer(owner).MaxHeroes = limit;
    }

    /// <summary>How many times this game's heroes have moved (any <see cref="Hero.Position"/> set since they were added).</summary>
    internal HeroMoveCounter HeroMoves { get; } = new();

    /// <summary>The hero on the tile (the first in <see cref="Heroes"/> should two share it), or null.</summary>
    public Hero? HeroAt(MapPosition position)
    {
        if (_heroAtMoves != HeroMoves.Count)
        {
            // One of this game's heroes moved, or one joined or left: index the heroes again (the first hero on a tile wins).
            var moves = HeroMoves.Count;
            _heroAt.Clear();
            foreach (var hero in _heroes)
            {
                _heroAt.TryAdd(hero.Position, hero);
            }

            _heroAtMoves = moves;
        }

        return _heroAt.GetValueOrDefault(position);
    }

    /// <summary>Whether <paramref name="viewer"/> currently sees <paramref name="hero"/> (own heroes always; Veil III hides a hero from afar).</summary>
    public bool CanSee(PlayerId viewer, Hero hero) =>
        hero.Owner == viewer || (Fog.IsInSight(viewer, hero.Position) && !Magic.AdventureMagic.HidesFrom(this, hero, viewer));

    /// <summary>Different players on the same team.</summary>
    public bool AreAllies(PlayerId a, PlayerId b) => a != b && GetPlayer(a).Team == GetPlayer(b).Team;

    public bool AreEnemies(PlayerId a, PlayerId b) => GetPlayer(a).Team != GetPlayer(b).Team;

    /// <summary>
    /// Game setup: overrides the map's default teams. Refused once the game started or when the map locks
    /// its teams (story/scenario maps).
    /// </summary>
    public void AssignTeams(IReadOnlyDictionary<PlayerId, TeamId> teams)
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("Teams can only be changed before the game starts.");
        }

        if (TeamsLocked)
        {
            throw new InvalidOperationException("This map locks its teams.");
        }

        var after = _players.Select(p => teams.TryGetValue(p.Id, out var team) ? team : p.Team).Distinct().Count();
        if (TeamCountProblem(after) is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        foreach (var (player, team) in teams)
        {
            GetPlayer(player).Team = team;
        }
    }

    /// <summary>
    /// Game setup (ADR-159): who plays each player, a person (true) or the computer (false), in place of the map's defaults; players not named keep
    /// theirs. Refused once the game started, for an unknown player, and when nobody would be a person.
    /// </summary>
    public void AssignControllers(IReadOnlyDictionary<PlayerId, bool> humans)
    {
        ArgumentNullException.ThrowIfNull(humans);
        if (IsStarted)
        {
            throw new InvalidOperationException("Who plays a player can only be changed before the game starts.");
        }

        if (humans.Keys.Where(p => !HasPlayer(p)).Select(p => (PlayerId?)p).FirstOrDefault() is { } unknown)
        {
            throw new InvalidOperationException($"Unknown player {unknown}.");
        }

        if (!_players.Any(p => humans.TryGetValue(p.Id, out var human) ? human : p.IsHuman))
        {
            throw new InvalidOperationException("At least one player must be played by a person.");
        }

        foreach (var (player, human) in humans)
        {
            GetPlayer(player).IsHuman = human;
        }
    }

    /// <summary>The fewest teams a game of this map may have (ADR-070, ADR-117); null = no limit.</summary>
    public int? MinTeams { get; private set; }

    /// <summary>The most teams a game of this map may have; null = no limit (as many as players, 8 = free for all with eight players).</summary>
    public int? MaxTeams { get; private set; }

    /// <summary>Why <paramref name="teams"/> teams are not allowed on this map, or null when they are.</summary>
    public string? TeamCountProblem(int teams) =>
        teams < (MinTeams ?? 1) || teams > (MaxTeams ?? MaxPlayers)
            ? $"This map is played by {(MinTeams == MaxTeams ? $"{MinTeams}" : $"{MinTeams ?? 1} to {MaxTeams ?? MaxPlayers}")} teams, not {teams}."
            : null;

    /// <summary>Scenario setting (map loading, editor, generator): the team counts allowed at setup, 2 to 8 each, or null for no limit.</summary>
    public void SetTeamCounts(int? min, int? max)
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("The team counts can only be changed before the game starts.");
        }

        if (min is < 2 or > MaxPlayers || max is < 2 or > MaxPlayers || (min is { } low && max is { } high && low > high))
        {
            throw new ArgumentOutOfRangeException(nameof(min), $"Team counts go from 2 to {MaxPlayers}, the fewest not above the most.");
        }

        (MinTeams, MaxTeams) = (min, max);
    }

    /// <summary>Scenario setting (map loading, editor): whether game setup may change the teams.</summary>
    public void SetTeamsLocked(bool locked)
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("Teams can only be locked before the game starts.");
        }

        TeamsLocked = locked;
    }

    /// <summary>The map events (ADR-115): the author's events and what changed while the game ran.</summary>
    public MapEventState MapEvents { get; } = new();

    /// <summary>Where the Grail lies, whether it was found, and the holes dug (ADR-140).</summary>
    public Objects.GrailState Grail { get; } = new();

    /// <summary>
    /// Buries the Grail at <paramref name="position"/> (the map's author, ADR-140), or takes it away (null); before the game starts only. A position
    /// off the map is refused.
    /// </summary>
    public void SetGrail(MapPosition? position)
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("The Grail is buried before the game starts.");
        }

        if (position is { } at && !Map.Contains(at))
        {
            throw new ArgumentOutOfRangeException(nameof(position), $"{at} is not on the map.");
        }

        Grail.Position = position;
    }

    /// <summary>
    /// Scenario setting (map loading, editor): the map events; refused when they do not fit the map (<see cref="MapEventRules"/>). The editor
    /// passes <paramref name="check"/> false: events being written may not fit yet, and its validator shows the problems.
    /// </summary>
    public void SetMapEvents(MapEventSet set, bool check = true)
    {
        ArgumentNullException.ThrowIfNull(set);
        if (IsStarted)
        {
            throw new InvalidOperationException("The map events can only be changed before the game starts.");
        }

        if (check && MapEventRules.Problems(this, set) is [var first, ..])
        {
            throw new ArgumentException(first, nameof(set));
        }

        MapEvents.Load(set, Options.CampaignVariables);
    }

    /// <summary>Scenario setting (map loading, editor): the victory and defeat conditions.</summary>
    public void SetRules(ScenarioRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (IsStarted)
        {
            throw new InvalidOperationException("The rules can only be changed before the game starts.");
        }

        if (rules.MaxHeroes is < 1 or > ScenarioRules.HeroLimit)
        {
            throw new ArgumentOutOfRangeException(nameof(rules), rules.MaxHeroes, $"A player may have 1 to {ScenarioRules.HeroLimit} heroes.");
        }

        Rules = rules;
    }

    /// <summary>
    /// Monsters guarding <paramref name="position"/>: those on one of its eight neighbours (the monster's own tile
    /// is its entrance, not a guarded tile). Lowest id first.
    /// </summary>
    public IEnumerable<MonsterGuard> GuardsOf(MapPosition position) =>
        !IsGuarded(position)
            ? []
            : DirectionExtensions.All
                .Select(d => ObjectCellAt(position.Step(d)))
                .Select(c => c?.Object)
                .OfType<MonsterGuard>()
                .OrderBy(m => m.Id.Value);

    /// <summary>Whether a monster guards <paramref name="position"/> (<see cref="GuardsOf"/> is not empty); a lookup, it allocates nothing.</summary>
    public bool IsGuarded(MapPosition position) => Map.Contains(position) && _guards[TileIndex(position)] > 0;

    private int TileIndex(MapPosition position) => (((position.Level * Map.Height) + position.Y) * Map.Width) + position.X;

    /// <summary>Counts a monster's cells on (+1) or off (-1) the tiles around them.</summary>
    private void CountGuards(MapObject mapObject, int delta)
    {
        if (mapObject is not MonsterGuard)
        {
            return;
        }

        foreach (var (cell, _) in mapObject.OccupiedCells())
        {
            foreach (var direction in DirectionExtensions.All)
            {
                if (cell.Step(direction) is var tile && Map.Contains(tile))
                {
                    _guards[TileIndex(tile)] += delta;
                }
            }
        }
    }

    /// <summary>Forgets the cached object lists the object belongs to (it was added or removed).</summary>
    private void ForgetObjectLists(MapObject mapObject)
    {
        foreach (var type in _objectsOfType.Keys.Where(t => t.IsInstanceOfType(mapObject)).ToList())
        {
            _objectsOfType.Remove(type);
        }

        if (mapObject is Towns.Town)
        {
            _townsById = null;
        }

        if (mapObject is IFlaggable)
        {
            _flaggables = null;
        }
    }

    /// <summary>The map object; an unknown id refuses the command that names it (<see cref="CommandRefusedException"/>).</summary>
    public MapObject GetObject(MapObjectId id) => FindObject(id) ?? throw new CommandRefusedException($"Unknown map object {id}.");

    public MapObject? FindObject(MapObjectId id) => _objectById.GetValueOrDefault(id);

    /// <summary>Where a gate delivers heroes, or null if it is unlinked or its link dangles.</summary>
    public MapPosition? TeleportDestination(MapObject gate) =>
        gate.TeleportTarget is { } target && FindObject(target) is { } landing ? landing.VisitPosition : null;

    /// <summary>Whether any object has a <see cref="MapObject.TeleportTarget"/> (only gates have one; links may change, so it is checked each time).</summary>
    public bool HasTeleporters
    {
        get
        {
            foreach (var gate in ObjectsOf<TwoWayGate>())
            {
                if (gate.TeleportTarget is not null)
                {
                    return true;
                }
            }

            foreach (var gate in ObjectsOf<OneWayGateEntrance>())
            {
                if (gate.TeleportTarget is not null)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The objects of type <typeparamref name="T"/> (subtypes included), in <see cref="Objects"/> order; cached until one is added or removed.</summary>
    public IEnumerable<T> ObjectsOf<T>()
        where T : MapObject
    {
        if (!_objectsOfType.TryGetValue(typeof(T), out var list))
        {
            list = _objects.OfType<T>().ToList().AsReadOnly();
            _objectsOfType[typeof(T)] = list;
        }

        return (IReadOnlyList<T>)list;
    }

    /// <summary>The object occupying <paramref name="position"/> (blocked or visit cell), if any.</summary>
    public (MapObject Object, FootprintCell Cell)? ObjectCellAt(MapPosition position) =>
        _objectCells.TryGetValue(position, out var cell) ? cell : null;

    /// <summary>An id never used before in this game, also not by a hero that has since left it (hired heroes).</summary>
    internal HeroId NextUnusedHeroId() => new(Math.Max(_highestHeroId, _heroes.Count == 0 ? 0 : _heroes.Max(h => h.Id.Value)) + 1);

    public HeroId NextHeroId() => new(_heroes.Count == 0 ? 1 : _heroes.Max(h => h.Id.Value) + 1);

    public MapObjectId NextObjectId() => new(_objects.Count == 0 ? 1 : _objects.Max(o => o.Id.Value) + 1);

    internal int NextCaravanId() => ++_highestCaravanId;

    internal void AddCaravan(Towns.Caravan caravan) => _caravans.Add(caravan);

    internal void ReplaceCaravan(Towns.Caravan old, Towns.Caravan next) => _caravans[_caravans.IndexOf(old)] = next;

    internal void RemoveCaravan(Towns.Caravan caravan) => _caravans.Remove(caravan);

    internal void RemoveCaravansOf(PlayerId owner) => _caravans.RemoveAll(c => c.Owner == owner);

    internal void AddPlayer(Player player) => _players.Add(player);

    internal void RemovePlayer(Player player) => _players.Remove(player);

    /// <summary>Adds the hero at the end of <see cref="Heroes"/>, or at <paramref name="index"/> (an undone removal puts it back where it was).</summary>
    internal void AddHero(Hero hero, int? index = null)
    {
        if (!_heroById.TryAdd(hero.Id, hero))
        {
            throw new ArgumentException($"Duplicate hero id {hero.Id}.", nameof(hero));
        }

        _heroes.Insert(index ?? _heroes.Count, hero);
        hero.MoveCounter = HeroMoves;
        _heroAtMoves = -1;
        RefreshArtifacts(hero);
        _highestHeroId = Math.Max(_highestHeroId, hero.Id.Value);
    }

    /// <summary>
    /// The editor's resize (EDIT-8) and its undo, before the game starts: the map becomes a copy of <paramref name="contents"/> and every
    /// object and hero (garrison heroes too) moves by (<paramref name="dx"/>, <paramref name="dy"/>) with it. All of them must fit on the
    /// new map (the editor removes the others first); the order of the objects stays. The Grail and the map events are the caller's.
    /// </summary>
    internal void Reframe(AdventureMap contents, int dx, int dy)
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("The map is resized before the game starts.");
        }

        if (_objects.SelectMany(o => o.OccupiedCells()).Any(c => !InFrame(c.Position)) || _heroes.Any(h => !InFrame(h.Position)))
        {
            throw new ArgumentException("An object or a hero would be off the resized map.", nameof(contents));
        }

        Map.TakeFrom(contents);
        Fog.Forget();
        _guards = new int[Map.Levels * Map.Width * Map.Height];
        _objectCells.Clear();
        foreach (var mapObject in _objects)
        {
            mapObject.Position = mapObject.Position.Offset(dx, dy);
            foreach (var (position, cell) in mapObject.OccupiedCells())
            {
                _objectCells[position] = (mapObject, cell);
            }

            CountGuards(mapObject, +1);
        }

        foreach (var hero in _heroes.Concat(GarrisonHeroes))
        {
            hero.Position = hero.Position.Offset(dx, dy);
        }

        _heroAtMoves = -1;

        bool InFrame(MapPosition position) =>
            position.X + dx >= 0 && position.X + dx < contents.Width && position.Y + dy >= 0 && position.Y + dy < contents.Height;
    }

    /// <summary>Rebuilds the tile → hero lookup on its next use: a hero's place was written past <see cref="Hero.Position"/>'s setter.</summary>
    internal void ForgetHeroPlaces() => _heroAtMoves = -1;

    /// <summary>Recomputes what a hero's artifacts add (after anything changed what it wears).</summary>
    internal void RefreshArtifacts(Hero hero) =>
        hero.Bonus = ArtifactBonuses.Compute(Artifacts, hero.Artifacts) with { HomeTown = ProgressionService.TownOf(this, hero) };

    /// <summary>Removes the hero; returns where it was in <see cref="Heroes"/>, or -1 when it was not there.</summary>
    internal int RemoveHero(Hero hero)
    {
        var index = _heroes.IndexOf(hero);
        if (index >= 0)
        {
            _heroes.RemoveAt(index);
            _heroById.Remove(hero.Id);
            _heroAtMoves = -1;
        }

        return index;
    }

    internal void Retire(Hero hero) => _retiredHeroes.Add(hero);

    internal void Unretire(Hero hero) => _retiredHeroes.Remove(hero);

    internal void RemoveRetiredHeroesOf(PlayerId owner) => _retiredHeroes.RemoveAll(h => h.Owner == owner);

    /// <summary>Adds the object at the end of <see cref="Objects"/>, or at <paramref name="index"/> (an undone removal puts it back where it was).</summary>
    internal void AddObject(MapObject mapObject, int? index = null)
    {
        if (_objectById.ContainsKey(mapObject.Id))
        {
            throw new ArgumentException($"Duplicate object id {mapObject.Id}.", nameof(mapObject));
        }

        foreach (var (position, _) in mapObject.OccupiedCells())
        {
            if (!Map.Contains(position))
            {
                throw new ArgumentException($"{mapObject} does not fit on the map.", nameof(mapObject));
            }

            if (_objectCells.ContainsKey(position))
            {
                throw new ArgumentException($"{mapObject} overlaps another object at {position}.", nameof(mapObject));
            }
        }

        _objects.Insert(index ?? _objects.Count, mapObject);
        _objectById.Add(mapObject.Id, mapObject);
        foreach (var (position, cell) in mapObject.OccupiedCells())
        {
            _objectCells[position] = (mapObject, cell);
        }

        CountGuards(mapObject, +1);
        ForgetObjectLists(mapObject);
    }

    /// <summary>Removes the object; returns where it was in <see cref="Objects"/>, or -1 when it was not there.</summary>
    internal int RemoveObject(MapObject mapObject)
    {
        var index = _objects.IndexOf(mapObject);
        if (index < 0)
        {
            return index;
        }

        _objects.RemoveAt(index);
        _objectById.Remove(mapObject.Id);

        foreach (var (position, _) in mapObject.OccupiedCells())
        {
            _objectCells.Remove(position);
        }

        CountGuards(mapObject, -1);
        ForgetObjectLists(mapObject);
        return index;
    }
}
