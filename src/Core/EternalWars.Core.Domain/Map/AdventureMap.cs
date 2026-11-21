using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Map;

/// <summary>
/// The strategic (adventure) map: a grid (square or not) with one or two levels (surface + underground),
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
    private TerrainType[][] _terrain;
    private RoadType[][] _roads;
    private RiverType[][] _rivers;
    private sbyte[][] _ground;

    /// <summary>Each tile's start zone: the player's number, or -1 for none (MP-4).</summary>
    private sbyte[][] _startZones;

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
        _startZones = new sbyte[levels][];
        for (var level = 0; level < levels; level++)
        {
            _terrain[level] = Enumerable.Repeat(fill, width * height).ToArray();
            _roads[level] = new RoadType[width * height];
            _rivers[level] = new RiverType[width * height];
            _ground[level] = new sbyte[(width + 1) * (height + 1)];
            _startZones[level] = Enumerable.Repeat((sbyte)-1, width * height).ToArray();
        }
    }

    public string Name { get; set; } = "Untitled";

    public string Description { get; set; } = "";

    /// <summary>The map's width in tiles; only the editor's resize changes it (<see cref="TakeFrom"/>).</summary>
    public int Width { get; private set; }

    /// <inheritdoc cref="Width"/>
    public int Height { get; private set; }

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

    /// <summary>
    /// The heights of every corner of a level, row by row (<see cref="Width"/> + 1 to a row): read at the speed of a memory copy, for the
    /// editor's undo of a reshape on a large map (ADR-278).
    /// </summary>
    public ReadOnlySpan<sbyte> Corners(int level) => _ground[level];

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

    /// <summary>
    /// The player whose start zone the tile is in (MP-4, ADR-197); null for none. A player's start zone is where its day stays its own in a
    /// simultaneous first week (MP-2): another player's hero stepping into it ends the simultaneous days.
    /// </summary>
    public PlayerId? GetStartZone(MapPosition position) => _startZones[position.Level][Index(position)] is >= 0 and var owner ? new PlayerId(owner) : null;

    public void SetStartZone(MapPosition position, PlayerId? owner)
    {
        if (owner is { Value: < 0 or > sbyte.MaxValue } outside)
        {
            throw new ArgumentOutOfRangeException(nameof(owner), outside, "Not a player's number.");
        }

        _startZones[position.Level][Index(position)] = (sbyte)(owner?.Value ?? -1);
    }

    /// <summary>Whether any tile is in a start zone.</summary>
    public bool HasStartZones => _startZones.Any(level => level.Any(z => z >= 0));

    /// <summary>The players that have a start zone, in number order.</summary>
    public IReadOnlyList<PlayerId> StartZoneOwners =>
        [.. _startZones.SelectMany(level => level).Where(z => z >= 0).Distinct().Order().Select(z => new PlayerId(z))];

    /// <summary>The tiles of <paramref name="owner"/>'s start zone, on every level.</summary>
    public IEnumerable<MapPosition> StartZoneTiles(PlayerId owner) =>
        Enumerable.Range(0, Levels).SelectMany(Positions).Where(p => GetStartZone(p) == owner);

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

    /// <summary>
    /// A new map of <paramref name="width"/> × <paramref name="height"/> tiles with this one's content moved by (<paramref name="dx"/>,
    /// <paramref name="dy"/>): tile (x, y) of this map is tile (x + dx, y + dy) of the new one, and what falls outside is cut (EDIT-8).
    /// Tiles the old map does not cover are grass on the surface and subterranean below, without road, river or start zone; corners
    /// it does not cover are at height 0 (the caller shapes them). The name and the description come along.
    /// </summary>
    public AdventureMap Reframed(int width, int height, int dx, int dy)
    {
        var result = new AdventureMap(width, height, Levels) { Name = Name, Description = Description };
        for (var level = 0; level < Levels; level++)
        {
            if (level > 0)
            {
                Array.Fill(result._terrain[level], TerrainType.Subterranean);
            }

            for (var y = Math.Max(0, dy); y < Math.Min(height, Height + dy); y++)
            {
                for (var x = Math.Max(0, dx); x < Math.Min(width, Width + dx); x++)
                {
                    var (to, from) = ((y * width) + x, ((y - dy) * Width) + x - dx);
                    result._terrain[level][to] = _terrain[level][from];
                    result._roads[level][to] = _roads[level][from];
                    result._rivers[level][to] = _rivers[level][from];
                    result._startZones[level][to] = _startZones[level][from];
                }
            }

            for (var cy = Math.Max(0, dy); cy <= Math.Min(height, Height + dy); cy++)
            {
                for (var cx = Math.Max(0, dx); cx <= Math.Min(width, Width + dx); cx++)
                {
                    result._ground[level][(cy * (width + 1)) + cx] = _ground[level][((cy - dy) * (Width + 1)) + cx - dx];
                }
            }
        }

        return result;
    }

    /// <summary>
    /// This map becomes a copy of <paramref name="other"/>: its size and every layer (the editor's resize and its undo; whoever holds
    /// the map keeps the same object). The name and the description stay. The levels must be the same.
    /// </summary>
    internal void TakeFrom(AdventureMap other)
    {
        if (other.Levels != Levels)
        {
            throw new ArgumentException("A resize keeps the map's levels.", nameof(other));
        }

        (Width, Height) = (other.Width, other.Height);
        _terrain = [.. other._terrain.Select(l => (TerrainType[])l.Clone())];
        _roads = [.. other._roads.Select(l => (RoadType[])l.Clone())];
        _rivers = [.. other._rivers.Select(l => (RiverType[])l.Clone())];
        _ground = [.. other._ground.Select(l => (sbyte[])l.Clone())];
        _startZones = [.. other._startZones.Select(l => (sbyte[])l.Clone())];
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
