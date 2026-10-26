using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Map;

/// <summary>
/// The strategic (adventure) map: a square grid with one or two levels (surface + underground),
/// a terrain layer, a road layer and a river layer (ADR-079).
/// Plain data; rules live in services that publish events when they change it.
/// </summary>
public sealed class AdventureMap
{
    private readonly TerrainType[][] _terrain;
    private readonly RoadType[][] _roads;
    private readonly RiverType[][] _rivers;

    public AdventureMap(int width, int height, int levels = 1, TerrainType fill = TerrainType.Grass)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(width, MapSizes.MaxDimension);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(height, MapSizes.MaxDimension);
        ArgumentOutOfRangeException.ThrowIfLessThan(levels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(levels, 2);

        Width = width;
        Height = height;
        Levels = levels;
        _terrain = new TerrainType[levels][];
        _roads = new RoadType[levels][];
        _rivers = new RiverType[levels][];
        for (var level = 0; level < levels; level++)
        {
            _terrain[level] = Enumerable.Repeat(fill, width * height).ToArray();
            _roads[level] = new RoadType[width * height];
            _rivers[level] = new RiverType[width * height];
        }
    }

    public string Name { get; set; } = "Untitled";

    public string Description { get; set; } = "";

    public int Width { get; }

    public int Height { get; }

    public int Levels { get; }

    public bool Contains(MapPosition position) =>
        position.X >= 0 && position.X < Width &&
        position.Y >= 0 && position.Y < Height &&
        position.Level >= 0 && position.Level < Levels;

    public TerrainType GetTerrain(MapPosition position) => _terrain[position.Level][Index(position)];

    public void SetTerrain(MapPosition position, TerrainType terrain) => _terrain[position.Level][Index(position)] = terrain;

    public RoadType GetRoad(MapPosition position) => _roads[position.Level][Index(position)];

    public void SetRoad(MapPosition position, RoadType road) => _roads[position.Level][Index(position)] = road;

    public bool HasRoads => _roads.Any(level => level.Any(r => r != RoadType.None));

    public RiverType GetRiver(MapPosition position) => _rivers[position.Level][Index(position)];

    public void SetRiver(MapPosition position, RiverType river) => _rivers[position.Level][Index(position)] = river;

    public bool HasRivers => _rivers.Any(level => level.Any(r => r != RiverType.None));

    public IEnumerable<MapPosition> Positions(int level)
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                yield return new MapPosition(x, y, level);
            }
        }
    }

    private int Index(MapPosition position)
    {
        if (!Contains(position))
        {
            throw new ArgumentOutOfRangeException(nameof(position), position, "Position is outside the map.");
        }

        return (position.Y * Width) + position.X;
    }
}
