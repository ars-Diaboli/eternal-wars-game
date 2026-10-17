using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Generation;

/// <summary>An opening cut through the wall between two zones for one passage of a connection (stage 3).</summary>
/// <param name="Connection">Index of the connection in the template.</param>
/// <param name="ZoneA">The zones the passage joins (indices in template order).</param>
/// <param name="ZoneB">The other zone.</param>
/// <param name="Tiles">Every tile opened (the way through, plus its widening for wide passages); empty for gates.</param>
/// <param name="Anchor">The tile the passage was made around: where its guard stands.</param>
public sealed record GeneratedPassage(int Connection, int ZoneA, int ZoneB, IReadOnlyList<MapPosition> Tiles, MapPosition Anchor);

/// <summary>A town the generator placed; <paramref name="Slot"/> is the player slot it starts with (null: neutral).</summary>
public sealed record GeneratedTown(int? Slot, int Zone, Town Town);

/// <summary>
/// The map while the generator works on it (ADR-064): the terrain of stage 2 plus the objects the later stages place, with what
/// is occupied, so no two things share a tile. The last stage turns it into a scenario <c>GameState</c>.
/// </summary>
public sealed class GeneratedMap
{
    private readonly List<MapObject> _objects = [];
    private readonly Dictionary<MapPosition, FootprintCell> _occupied = [];
    private readonly List<GeneratedPassage> _passages = [];
    private readonly List<string> _problems = [];
    private readonly List<GeneratedTown> _towns = [];
    private readonly Dictionary<(int A, int B), List<MapPosition>> _walls;
    private int _nextId = 1;

    private GeneratedMap(GeneratedTerrain terrain)
    {
        Map = terrain.Map;
        Layout = terrain.Layout;
        _walls = terrain.Walls.ToDictionary(w => w.Key, w => w.Value.ToList());
    }

    public static GeneratedMap From(GeneratedTerrain terrain) => new(terrain ?? throw new ArgumentNullException(nameof(terrain)));

    public AdventureMap Map { get; }

    public ZoneLayout Layout { get; }

    public IReadOnlyList<MapObject> Objects => _objects;

    public IReadOnlyList<GeneratedPassage> Passages => _passages;

    /// <summary>The towns placed, start towns first in each zone.</summary>
    public IReadOnlyList<GeneratedTown> Towns => _towns;

    /// <summary>What a stage could not do (a connection with no border to cut through, a gate with no room); the map is still usable.</summary>
    public IReadOnlyList<string> Problems => _problems;

    /// <summary>The wall tiles still standing between two zones (lower index first); opened tiles are taken out.</summary>
    public IReadOnlyDictionary<(int A, int B), List<MapPosition>> Walls => _walls;

    internal MapObjectId NextId() => new(_nextId++);

    internal void Problem(string message) => _problems.Add(message);

    internal void AddPassage(GeneratedPassage passage) => _passages.Add(passage);

    /// <summary>Whether the tile is on the map, not rock or water, and nothing stands on it.</summary>
    public bool IsFree(MapPosition position) =>
        Map.Contains(position) && Map.GetTerrain(position) != TerrainPainter.WallTerrain && !_occupied.ContainsKey(position)
        && Terrains.IsPassable(Map.GetTerrain(position));

    internal static TerrainCatalog Terrains => TerrainCatalog.Default;

    /// <summary>Whether the object's blocked and visit cells all fall on free tiles.</summary>
    public bool Fits(MapObjectDefinition definition, MapPosition topLeft) =>
        definition.Footprint.OccupiedCells.All(c => IsFree(topLeft.Offset(c.Dx, c.Dy)));

    /// <summary>Adds an object that fits (the caller checked), taking its cells.</summary>
    internal T Add<T>(T mapObject)
        where T : MapObject
    {
        _objects.Add(mapObject);
        foreach (var (position, cell) in mapObject.OccupiedCells())
        {
            _occupied[position] = cell;
        }

        return mapObject;
    }

    internal void AddTown(GeneratedTown town) => _towns.Add(town);

    /// <summary>
    /// Gives a start town another town type (players pick theirs at game setup): the town of the same size takes the old one's id,
    /// place and owner, with a fresh default start. Only start towns that are still unbuilt and ungarrisoned are meant for this.
    /// </summary>
    public Town SetTownType(Town town, string townType)
    {
        var at = _towns.FindIndex(t => t.Town == town);
        if (at < 0 || !BuiltInMapObjects.PlayableTowns.Contains(townType))
        {
            throw new ArgumentException("Not a town of this map, or not a playable town type.");
        }

        var replacement = new Town(town.Id, MapObjectCatalog.Default.Get(BuiltInMapObjects.TownId(townType)), town.Position, town.Owner, town.Name == town.Definition.Name ? null : town.Name, garrison: town.Garrison);
        _objects[_objects.IndexOf(town)] = replacement;
        _towns[at] = _towns[at] with { Town = replacement };
        return replacement;
    }

    /// <summary>Opens a wall tile: it becomes walkable ground of the given terrain and leaves the wall lists.</summary>
    internal void Open(MapPosition position, TerrainType terrain)
    {
        Map.SetTerrain(position, terrain);
        foreach (var list in _walls.Values)
        {
            list.Remove(position);
        }
    }
}
