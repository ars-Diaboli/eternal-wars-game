using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Generation;

/// <summary>
/// What stage 2 of the generator makes (ADR-064): the terrain of every tile with the walls between zones and the obstacles inside
/// them. Later stages keep working on <see cref="Map"/> and use <see cref="Walls"/> to cut the passages of the connections.
/// </summary>
public sealed class GeneratedTerrain
{
    internal GeneratedTerrain(AdventureMap map, ZoneLayout layout, IReadOnlyDictionary<(int A, int B), IReadOnlyList<MapPosition>> walls, IReadOnlyList<MapPosition> obstacles)
    {
        Map = map;
        Layout = layout;
        Walls = walls;
        Obstacles = obstacles;
    }

    public AdventureMap Map { get; }

    public ZoneLayout Layout { get; }

    /// <summary>
    /// The wall tiles (<see cref="TerrainPainter.WallTerrain"/>) between two zones, by the pair's zone indices (lower first). A tile touching three zones is in
    /// three lists. Only pairs that touch appear.
    /// </summary>
    public IReadOnlyDictionary<(int A, int B), IReadOnlyList<MapPosition>> Walls { get; }

    /// <summary>The scattered rock inside zones (not the walls).</summary>
    public IReadOnlyList<MapPosition> Obstacles { get; }
}

/// <summary>
/// Stage 2 of the generator (ADR-064): paints each zone with its terrain, raises a wall of impassable rock along every border between
/// zones, and scatters rock clumps inside the zones. Deterministic from (layout, seed): the terrain of zone <c>i</c> draws from the
/// <c>terrain</c> stream (index <c>i</c>), its obstacles from <c>obstacles</c>.
/// <list type="bullet">
/// <item>A zone has one main terrain (from its allowed list, or the default pool of its level) and, when it may use another, a few
/// patches of it.</item>
/// <item>A tile with a tile of another zone among its eight neighbours is wall: borders are two tiles thick and nothing slips through
/// diagonally. The connections stage opens the passages in them.</item>
/// <item>Obstacles are small clumps at least three tiles from the walls and from each other; a clump that would cut off part of its
/// zone is dropped. Walkable pockets the walls cut off are filled in.</item>
/// </list>
/// </summary>
public static class TerrainPainter
{
    public const TerrainType WallTerrain = TerrainType.Rock;

    /// <summary>What a zone without a terrain list is painted with, by level.</summary>
    public static IReadOnlyList<TerrainType> DefaultPool(int level) => level == 0
        ? [TerrainType.Grass, TerrainType.Dirt, TerrainType.Rough, TerrainType.Highlands, TerrainType.ForestFloor, TerrainType.Sand, TerrainType.Snow, TerrainType.Swamp, TerrainType.Wasteland]
        : [TerrainType.Subterranean, TerrainType.RockyGround, TerrainType.Lava];

    /// <summary>The patch terrains of a zone that may use any terrain: the plain neighbours of its main one.</summary>
    private static IReadOnlyList<TerrainType> PatchPool(int level, TerrainType main) =>
        (level == 0 ? new[] { TerrainType.Dirt, TerrainType.Rough, TerrainType.Grass } : [TerrainType.RockyGround, TerrainType.Subterranean]).Where(t => t != main).ToList();

    /// <summary>Per mille of a zone's tiles that start an obstacle clump, by density.</summary>
    public static int ClumpsPerMille(ObstacleDensity density) => density switch
    {
        ObstacleDensity.None => 0,
        ObstacleDensity.Sparse => 3,
        ObstacleDensity.Normal => 8,
        _ => 16,
    };

    private static readonly (int Dx, int Dy)[] Four = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static GeneratedTerrain Paint(MapTemplate template, ZoneLayout layout, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(layout);
        if (layout.ZoneIds.Count != template.Zones.Count || layout.Levels != template.Levels)
        {
            throw new ArgumentException("The layout was not made from this template.", nameof(layout));
        }

        var size = layout.Size;
        var streams = new SeedStreams(seed);
        var map = new AdventureMap(size, size, layout.Levels) { Name = template.Name, Description = template.Description };
        var walls = new Dictionary<(int, int), List<MapPosition>>();
        var obstacles = new List<MapPosition>();

        for (var level = 0; level < layout.Levels; level++)
        {
            var tiles = Enumerable.Range(0, template.Zones.Count).ToDictionary(z => z, _ => new List<int>());
            for (var i = 0; i < size * size; i++)
            {
                tiles[layout.ZoneAt(i % size, i / size, level)].Add(i);
            }

            var rock = new bool[size * size];
            foreach (var (zone, zoneTiles) in tiles.Where(t => t.Value.Count > 0))
            {
                PaintZone(map, layout, template.Zones[zone], zone, zoneTiles, level, streams.For("terrain", zone));
            }

            RaiseWalls(map, layout, level, rock, walls);
            foreach (var (zone, zoneTiles) in tiles.Where(t => t.Value.Count > 0))
            {
                FillPockets(map, layout, zoneTiles, level, rock);
                PlaceObstacles(map, layout, template.Zones[zone], zoneTiles, level, rock, streams.For("obstacles", zone), obstacles);
            }
        }

        return new GeneratedTerrain(map, layout, walls.ToDictionary(w => w.Key, w => (IReadOnlyList<MapPosition>)w.Value), obstacles);
    }

    private static void PaintZone(AdventureMap map, ZoneLayout layout, TemplateZone zone, int index, List<int> tiles, int level, GameRandom random)
    {
        var size = layout.Size;
        var allowed = zone.Terrains is { Count: > 0 } list ? list : DefaultPool(level);
        var main = allowed[random.Next(allowed.Count)];
        foreach (var tile in tiles)
        {
            map.SetTerrain(new MapPosition(tile % size, tile / size, level), main);
        }

        var patches = zone.Terrains is { Count: > 0 } ? allowed.Where(t => t != main).ToList() : [.. PatchPool(level, main)];
        if (patches.Count == 0 || tiles.Count < 40)
        {
            return;
        }

        var inZone = tiles.ToHashSet();
        var count = Math.Min(4, 1 + (tiles.Count / 1500));
        for (var i = 0; i < count; i++)
        {
            var share = 3 + random.Next(6); // percent of the zone
            Grow(map, layout, inZone, tiles[random.Next(tiles.Count)], Math.Max(6, tiles.Count * share / 100 / count), level, patches[random.Next(patches.Count)], random);
        }
    }

    /// <summary>Paints a blob of at most <paramref name="count"/> tiles of the zone, growing from a tile by repeatedly taking a random tile next to it.</summary>
    private static void Grow(AdventureMap map, ZoneLayout layout, HashSet<int> inZone, int start, int count, int level, TerrainType terrain, GameRandom random)
    {
        var size = layout.Size;
        var taken = new HashSet<int> { start };
        var frontier = new List<int> { start };
        while (taken.Count < count && frontier.Count > 0)
        {
            var at = random.Next(frontier.Count);
            var current = frontier[at];
            var open = Four.Select(d => (X: (current % size) + d.Dx, Y: (current / size) + d.Dy))
                .Where(p => p.X >= 0 && p.Y >= 0 && p.X < size && p.Y < size)
                .Select(p => (p.Y * size) + p.X)
                .Where(t => inZone.Contains(t) && !taken.Contains(t)).ToList();
            if (open.Count == 0)
            {
                frontier.RemoveAt(at);
                continue;
            }

            var next = open[random.Next(open.Count)];
            taken.Add(next);
            frontier.Add(next);
        }

        foreach (var tile in taken)
        {
            map.SetTerrain(new MapPosition(tile % size, tile / size, level), terrain);
        }
    }

    /// <summary>A tile with another zone among its eight neighbours becomes wall; the walls are listed by the pairs of zones they stand between.</summary>
    private static void RaiseWalls(AdventureMap map, ZoneLayout layout, int level, bool[] rock, Dictionary<(int, int), List<MapPosition>> walls)
    {
        var size = layout.Size;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var zone = layout.ZoneAt(x, y, level);
                var others = new SortedSet<int>();
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if ((dx != 0 || dy != 0) && x + dx >= 0 && y + dy >= 0 && x + dx < size && y + dy < size
                            && layout.ZoneAt(x + dx, y + dy, level) is var other && other != zone)
                        {
                            others.Add(other);
                        }
                    }
                }

                if (others.Count == 0)
                {
                    continue;
                }

                var position = new MapPosition(x, y, level);
                map.SetTerrain(position, WallTerrain);
                rock[(y * size) + x] = true;
                foreach (var other in others)
                {
                    var key = zone < other ? (zone, other) : (other, zone);
                    if (!walls.TryGetValue(key, out var list))
                    {
                        walls[key] = list = [];
                    }

                    list.Add(position);
                }
            }
        }
    }

    /// <summary>Walls can cut a thin part of a zone off from the rest; what is not in the zone's biggest walkable piece becomes rock too.</summary>
    private static void FillPockets(AdventureMap map, ZoneLayout layout, List<int> tiles, int level, bool[] rock)
    {
        var pieces = WalkablePieces(layout.Size, tiles, rock);
        foreach (var piece in pieces.OrderByDescending(p => p.Count).Skip(1))
        {
            foreach (var tile in piece)
            {
                rock[tile] = true;
                map.SetTerrain(new MapPosition(tile % layout.Size, tile / layout.Size, level), WallTerrain);
            }
        }
    }

    private static List<List<int>> WalkablePieces(int size, List<int> tiles, bool[] rock)
    {
        var inZone = tiles.Where(t => !rock[t]).ToHashSet();
        var pieces = new List<List<int>>();
        foreach (var start in tiles.Where(inZone.Contains))
        {
            if (!inZone.Remove(start))
            {
                continue;
            }

            var piece = new List<int> { start };
            for (var head = 0; head < piece.Count; head++)
            {
                foreach (var (dx, dy) in Four)
                {
                    var (x, y) = ((piece[head] % size) + dx, (piece[head] / size) + dy);
                    if (x >= 0 && y >= 0 && x < size && y < size && inZone.Remove((y * size) + x))
                    {
                        piece.Add((y * size) + x);
                    }
                }
            }

            pieces.Add(piece);
        }

        return pieces;
    }

    private static void PlaceObstacles(AdventureMap map, ZoneLayout layout, TemplateZone zone, List<int> tiles, int level, bool[] rock, GameRandom random, List<MapPosition> placed)
    {
        var size = layout.Size;
        var clumps = tiles.Count * ClumpsPerMille(zone.Obstacles) / 1000;
        if (clumps == 0)
        {
            return;
        }

        // Candidates keep three tiles from the walls (two rounds of growing the rock by one tile, eight ways).
        var near = (bool[])rock.Clone();
        for (var round = 0; round < 2; round++)
        {
            var grown = (bool[])near.Clone();
            foreach (var tile in tiles)
            {
                if (!near[tile] && Around(tile, size).Any(n => near[n]))
                {
                    grown[tile] = true;
                }
            }

            near = grown;
        }

        var candidates = tiles.Where(t => !near[t]).ToList();
        var blocked = new HashSet<int>();
        for (var i = 0; i < clumps && candidates.Count > 0; i++)
        {
            var start = candidates[random.Next(candidates.Count)];
            if (blocked.Contains(start))
            {
                continue;
            }

            var clump = new List<int> { start };
            var target = 2 + random.Next(5);
            for (var attempts = 0; clump.Count < target && attempts < 20; attempts++)
            {
                var from = clump[random.Next(clump.Count)];
                var next = Four.Select(d => (X: (from % size) + d.Dx, Y: (from / size) + d.Dy)).Where(p => p.X >= 0 && p.Y >= 0 && p.X < size && p.Y < size)
                    .Select(p => (p.Y * size) + p.X).Where(t => !near[t] && !clump.Contains(t) && !blocked.Contains(t) && layout.ZoneAt(t % size, t / size, level) == layout.ZoneAt(start % size, start / size, level)).ToList();
                if (next.Count > 0)
                {
                    clump.Add(next[random.Next(next.Count)]);
                }
            }

            clump.ForEach(t => rock[t] = true);
            if (WalkablePieces(size, tiles, rock).Count > 1)
            {
                clump.ForEach(t => rock[t] = false); // it would cut off a part of the zone
                continue;
            }

            foreach (var tile in clump)
            {
                map.SetTerrain(new MapPosition(tile % size, tile / size, level), WallTerrain);
                placed.Add(new MapPosition(tile % size, tile / size, level));
                blocked.Add(tile);
                foreach (var around in Around(tile, size))
                {
                    blocked.Add(around);
                }
            }
        }
    }

    private static IEnumerable<int> Around(int tile, int size)
    {
        var (x, y) = (tile % size, tile / size);
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if ((dx != 0 || dy != 0) && x + dx >= 0 && y + dy >= 0 && x + dx < size && y + dy < size)
                {
                    yield return ((y + dy) * size) + x + dx;
                }
            }
        }
    }
}
