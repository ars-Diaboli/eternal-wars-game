using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Map;

/// <summary>
/// The strategic (adventure) map: a square grid with one or two levels (surface + underground),
/// a terrain layer, a road layer, a river layer (ADR-079) and the ground's height (ADR-181, ADR-182).
/// Plain data; rules live in services that publish events when they change it.
/// </summary>
/// <remarks>
/// The ground height is looks only: no rule reads it. It is kept per tile <b>corner</b>, a grid one larger than the map
/// each way, in eighths of a cell width (<see cref="ReliefLimits.StepsPerCell"/>); corner (cx, cy) is the top-left corner
/// of tile (cx, cy). <see cref="ReliefShaper"/> fills it from terrain and levels it under objects.
/// </remarks>
public sealed class AdventureMap
{
    private readonly TerrainType[][] _terrain;
    private readonly RoadType[][] _roads;
    private readonly RiverType[][] _rivers;
    private readonly sbyte[][] _ground;

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
        _ground = new sbyte[levels][];
        for (var level = 0; level < levels; level++)
        {
            _terrain[level] = Enumerable.Repeat(fill, width * height).ToArray();
            _roads[level] = new RoadType[width * height];
            _rivers[level] = new RiverType[width * height];
            _ground[level] = new sbyte[(width + 1) * (height + 1)];
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

    /// <summary>Whether corner (<paramref name="cx"/>, <paramref name="cy"/>) exists: 0 … Width and 0 … Height.</summary>
    public bool ContainsCorner(int level, int cx, int cy) =>
        cx >= 0 && cx <= Width && cy >= 0 && cy <= Height && level >= 0 && level < Levels;

    /// <summary>The ground's height at a tile corner, in eighths of a cell width.</summary>
    public int GetCorner(int level, int cx, int cy) => _ground[level][CornerIndex(level, cx, cy)];

    /// <summary>Sets a corner's height (eighths); clamped to <see cref="ReliefLimits.StoredMin"/> … <see cref="ReliefLimits.StoredMax"/>.</summary>
    public void SetCorner(int level, int cx, int cy, int eighths) =>
        _ground[level][CornerIndex(level, cx, cy)] = (sbyte)Math.Clamp(eighths, ReliefLimits.StoredMin, ReliefLimits.StoredMax);

    /// <summary>Whether any corner is off 0; a map whose ground was never shaped has none.</summary>
    public bool HasGroundHeights => _ground.Any(level => level.Any(h => h != 0));

    /// <summary>
    /// The ground's height in cell widths at a point given in tile units, where tile (x, y)'s centre is (x, y) and its
    /// corners are half a tile away (as the 3D view places tiles). Bilinear over the tile's four corners; points outside
    /// the map take the nearest edge.
    /// </summary>
    public float GroundHeightAt(int level, float x, float y)
    {
        var u = Math.Clamp(x + 0.5f, 0f, Width);
        var v = Math.Clamp(y + 0.5f, 0f, Height);
        var cx = Math.Min((int)MathF.Floor(u), Width - 1);
        var cy = Math.Min((int)MathF.Floor(v), Height - 1);
        var fx = u - cx;
        var fy = v - cy;
        var top = Lerp(GetCorner(level, cx, cy), GetCorner(level, cx + 1, cy), fx);
        var bottom = Lerp(GetCorner(level, cx, cy + 1), GetCorner(level, cx + 1, cy + 1), fx);
        return Lerp(top, bottom, fy) / ReliefLimits.StepsPerCell;
    }

    /// <summary>The ground's height in cell widths at a tile's centre (the mean of its corners).</summary>
    public float GroundHeightAt(MapPosition position) => GroundHeightAt(position.Level, position.X, position.Y);

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

    private int CornerIndex(int level, int cx, int cy)
    {
        if (!ContainsCorner(level, cx, cy))
        {
            throw new ArgumentOutOfRangeException(nameof(cx), (level, cx, cy), "Corner is outside the map.");
        }

        return (cy * (Width + 1)) + cx;
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}
