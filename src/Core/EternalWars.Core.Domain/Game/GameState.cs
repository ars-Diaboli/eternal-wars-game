using EternalWars.Core.Domain.Common;
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
        GameOptions? options = null)
    {
        Map = map ?? throw new ArgumentNullException(nameof(map));
        Options = options ?? GameOptions.Default;
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

    public FogOfWar Fog { get; }

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

    public MapObject GetObject(MapObjectId id) => _objects.First(o => o.Id == id);

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
