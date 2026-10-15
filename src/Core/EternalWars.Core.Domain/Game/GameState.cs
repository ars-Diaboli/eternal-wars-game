using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
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
    private readonly List<MapObject> _objects = [];
    private readonly Dictionary<MapPosition, (MapObject Object, FootprintCell Cell)> _objectCells = [];

    public GameState(
        AdventureMap map,
        IEnumerable<Player> players,
        IEnumerable<Hero> heroes,
        IEnumerable<MapObject>? objects = null,
        GameOptions? options = null,
        TerrainCatalog? terrains = null,
        CreatureCatalog? creatures = null)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        Options = options ?? GameOptions.Default;
        Terrains = terrains ?? TerrainCatalog.Default;
        Creatures = creatures ?? CreatureCatalog.Default;
        Random = new GameRandom(Options.Seed);
        Fog = new FogOfWar(map, Options.FogMode);
        _players = players.ToList();
        if (_players.Count is 0 or > MaxPlayers)
        {
            throw new ArgumentException($"A game needs 1 to {MaxPlayers} players.", nameof(players));
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

    public AdventureMap Map { get; }

    public GameOptions Options { get; }

    /// <summary>Terrain movement rules in effect for this game.</summary>
    public TerrainCatalog Terrains { get; }

    /// <summary>Creature definitions in effect for this game.</summary>
    public CreatureCatalog Creatures { get; }

    /// <summary>The game RNG; only Domain services draw from it, in command order, so games replay exactly.</summary>
    internal GameRandom Random { get; }

    /// <summary>Current state of the game RNG (saves, replay checks).</summary>
    public ulong RandomState => Random.State;

    /// <summary>Map setting: how neutral monsters guard their surroundings.</summary>
    public GuardMode GuardMode { get; internal set; }

    /// <summary>The battle being fought, if any (a <see cref="Decisions.BattleDecision"/> is pending meanwhile).</summary>
    public BattleState? Battle { get; internal set; }

    public FogOfWar Fog { get; }

    /// <summary>Per-player last known state of objects (refog mode).</summary>
    public ObjectMemory Memory { get; } = new();

    /// <summary>Set by the map author: teams from the map may not be changed at game setup.</summary>
    public bool TeamsLocked { get; private set; }

    public IReadOnlyList<Player> Players => _players;

    public IReadOnlyList<Hero> Heroes => _heroes;

    public IReadOnlyList<MapObject> Objects => _objects;

    public GameDate Date { get; internal set; } = GameDate.First;

    public int CurrentPlayerIndex { get; internal set; }

    public bool IsStarted { get; internal set; }

    public PendingDecision? PendingDecision { get; internal set; }

    public Player CurrentPlayer => _players[CurrentPlayerIndex];

    public Player GetPlayer(PlayerId id) => _players.First(p => p.Id == id);

    public bool HasPlayer(PlayerId id) => _players.Any(p => p.Id == id);

    public Hero GetHero(HeroId id) => _heroes.First(h => h.Id == id);

    public IEnumerable<Hero> HeroesOf(PlayerId owner) => _heroes.Where(h => h.Owner == owner);

    public Hero? HeroAt(MapPosition position) => _heroes.FirstOrDefault(h => h.Position == position);

    /// <summary>Whether <paramref name="viewer"/> currently sees <paramref name="hero"/> (own heroes always).</summary>
    public bool CanSee(PlayerId viewer, Hero hero) => hero.Owner == viewer || Fog.IsInSight(viewer, hero.Position);

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

        foreach (var (player, team) in teams)
        {
            GetPlayer(player).Team = team;
        }
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

    /// <summary>Scenario setting (map loading, editor): how neutral monsters guard their surroundings.</summary>
    public void SetGuardMode(GuardMode mode)
    {
        if (IsStarted)
        {
            throw new InvalidOperationException("The guard mode can only be changed before the game starts.");
        }

        GuardMode = mode;
    }

    /// <summary>
    /// Monsters guarding <paramref name="position"/>: those on one of its eight neighbours (the monster's own tile
    /// is its entrance, not a guarded tile). Lowest id first.
    /// </summary>
    public IEnumerable<MonsterGuard> GuardsOf(MapPosition position) =>
        DirectionExtensions.All
            .Select(d => ObjectCellAt(position.Step(d)))
            .Select(c => c?.Object)
            .OfType<MonsterGuard>()
            .OrderBy(m => m.Id.Value);

    public bool IsGuarded(MapPosition position) => GuardsOf(position).Any();

    public MapObject GetObject(MapObjectId id) => _objects.First(o => o.Id == id);

    public MapObject? FindObject(MapObjectId id) => _objects.FirstOrDefault(o => o.Id == id);

    /// <summary>Where a gate delivers heroes, or null if it is unlinked or its link dangles.</summary>
    public MapPosition? TeleportDestination(MapObject gate) =>
        gate.TeleportTarget is { } target && FindObject(target) is { } landing ? landing.VisitPosition : null;

    public IEnumerable<T> ObjectsOf<T>()
        where T : MapObject => _objects.OfType<T>();

    /// <summary>The object occupying <paramref name="position"/> (blocked or visit cell), if any.</summary>
    public (MapObject Object, FootprintCell Cell)? ObjectCellAt(MapPosition position) =>
        _objectCells.TryGetValue(position, out var cell) ? cell : null;

    public HeroId NextHeroId() => new(_heroes.Count == 0 ? 1 : _heroes.Max(h => h.Id.Value) + 1);

    public MapObjectId NextObjectId() => new(_objects.Count == 0 ? 1 : _objects.Max(o => o.Id.Value) + 1);

    internal void AddPlayer(Player player) => _players.Add(player);

    internal void RemovePlayer(Player player) => _players.Remove(player);

    internal void AddHero(Hero hero)
    {
        if (_heroes.Any(h => h.Id == hero.Id))
        {
            throw new ArgumentException($"Duplicate hero id {hero.Id}.", nameof(hero));
        }

        _heroes.Add(hero);
    }

    internal void RemoveHero(Hero hero) => _heroes.Remove(hero);

    internal void AddObject(MapObject mapObject)
    {
        if (_objects.Any(o => o.Id == mapObject.Id))
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

        _objects.Add(mapObject);
        foreach (var (position, cell) in mapObject.OccupiedCells())
        {
            _objectCells[position] = (mapObject, cell);
        }
    }

    internal void RemoveObject(MapObject mapObject)
    {
        if (!_objects.Remove(mapObject))
        {
            return;
        }

        foreach (var (position, _) in mapObject.OccupiedCells())
        {
            _objectCells.Remove(position);
        }
    }
}
