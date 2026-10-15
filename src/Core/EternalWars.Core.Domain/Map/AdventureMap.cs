using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Map;

/// <summary>
/// The strategic (adventure) map: a square grid with one or two levels (surface + underground).
/// Plain data; rules live in services that publish events when they change it.
/// </summary>
public sealed class AdventureMap
{
    private readonly TerrainType[][] _terrain;

    public AdventureMap(int width, int height, int levels = 1, TerrainType fill = TerrainType.Grass)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(levels, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(levels, 2);

        Width = width;
        Height = height;
        Levels = levels;
        _terrain = new TerrainType[levels][];
        for (var level = 0; level < levels; level++)
        {
            _terrain[level] = Enumerable.Repeat(fill, width * height).ToArray();
        }
    }

    public string Name { get; set; } = "Untitled";

    public int Width { get; }

    public int Height { get; }

    public int Levels { get; }

    public bool Contains(MapPosition position) =>
        position.X >= 0 && position.X < Width &&
        position.Y >= 0 && position.Y < Height &&
        position.Level >= 0 && position.Level < Levels;

    public TerrainType GetTerrain(MapPosition position) => _terrain[position.Level][Index(position)];

    public void SetTerrain(MapPosition position, TerrainType terrain) => _terrain[position.Level][Index(position)] = terrain;

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
