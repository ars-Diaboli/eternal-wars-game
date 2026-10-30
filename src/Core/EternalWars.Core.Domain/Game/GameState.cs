using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
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
    private readonly List<Hero> _retiredHeroes = [];
    private int _highestHeroId;
    private readonly List<MapObject> _objects = [];
    private readonly List<Towns.Caravan> _caravans = [];
    private int _highestCaravanId;
    private readonly Dictionary<MapPosition, (MapObject Object, FootprintCell Cell)> _objectCells = [];

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
        Magic.SpellCatalog? spells = null)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        ObjectCatalog = objectCatalog ?? MapObjectCatalog.Default;
        Options = options ?? GameOptions.Default;
        Terrains = terrains ?? TerrainCatalog.Default;
        Creatures = creatures ?? CreatureCatalog.Default;
        HeroRoster = heroRoster ?? HeroCatalog.Default;
        Buildings = buildings ?? BuildingCatalog.Default;
        Artifacts = artifacts ?? ArtifactCatalog.Default;
        Spells = spells ?? Magic.SpellCatalog.Default;
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
    internal SortedSet<string> DrawnRelics { get; } = new(StringComparer.Ordinal);

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

    /// <summary>How the game ended, or null while it is running.</summary>
    public GameOutcome? Outcome { get; internal set; }

    public bool IsOver => Outcome is not null;

    public PendingDecision? PendingDecision { get; internal set; }

    public Player CurrentPlayer => _players[CurrentPlayerIndex];

    public Player GetPlayer(PlayerId id) => _players.First(p => p.Id == id);

    public bool HasPlayer(PlayerId id) => _players.Any(p => p.Id == id);

    /// <summary>A hero on the map or in a town's garrison (ADR-112).</summary>
    public Hero GetHero(HeroId id) => _heroes.FirstOrDefault(h => h.Id == id) ?? GarrisonHeroes.First(h => h.Id == id);

    /// <summary>The player's heroes on the map (garrison heroes are not, ADR-112).</summary>
    public IEnumerable<Hero> HeroesOf(PlayerId owner) => _heroes.Where(h => h.Owner == owner);

    /// <summary>The heroes in towns' garrisons, by town id (ADR-112): off the map, not counted towards the hero limit.</summary>
    public IEnumerable<Hero> GarrisonHeroes => ObjectsOf<Towns.Town>().OrderBy(t => t.Id.Value).Select(t => t.GarrisonHero).OfType<Hero>();

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

    internal void AddHero(Hero hero)
    {
        if (_heroes.Any(h => h.Id == hero.Id))
        {
            throw new ArgumentException($"Duplicate hero id {hero.Id}.", nameof(hero));
        }

        _heroes.Add(hero);
        RefreshArtifacts(hero);
        _highestHeroId = Math.Max(_highestHeroId, hero.Id.Value);
    }

    /// <summary>Recomputes what a hero's artifacts add (after anything changed what it wears).</summary>
    internal void RefreshArtifacts(Hero hero) => hero.Bonus = ArtifactBonuses.Compute(Artifacts, hero.Artifacts);

    internal void RemoveHero(Hero hero) => _heroes.Remove(hero);

    internal void Retire(Hero hero) => _retiredHeroes.Add(hero);

    internal void Unretire(Hero hero) => _retiredHeroes.Remove(hero);

    internal void RemoveRetiredHeroesOf(PlayerId owner) => _retiredHeroes.RemoveAll(h => h.Owner == owner);

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
