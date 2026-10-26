using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Generation;

/// <summary>
/// Stage 5b of the generator (ADR-079, MAP-26): about half the zones get a river running across them from the foot of one wall to the farthest
/// foot of another, through free ground (roads may cross it: a bridge). Its look follows the terrain where it starts. Deterministic from the seed
/// (the <c>rivers</c> stream, one per zone). Rivers block nothing; they only slow a step by <see cref="RiverRules.PenaltyPercent"/> %.
/// </summary>
public static class RiverPlacer
{
    /// <summary>A river shorter than this is not drawn.</summary>
    public const int MinLength = 8;

    private static readonly (int Dx, int Dy)[] Four = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    public static void Place(MapTemplate template, GeneratedMap map, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(map);
        var streams = new SeedStreams(seed);
        for (var zone = 0; zone < template.Zones.Count; zone++)
        {
            var random = streams.For("rivers", zone);
            if (random.Next(2) != 0)
            {
                continue;
            }

            var walls = map.Walls.Where(w => w.Key.A == zone || w.Key.B == zone).SelectMany(w => w.Value).ToHashSet();
            var tiles = map.Layout.Tiles(zone).Select(t => new MapPosition(t.X, t.Y, t.Level)).Where(map.IsFree).ToHashSet();
            var banks = tiles.Where(t => Around(t).Any(walls.Contains)).OrderBy(t => t.Level).ThenBy(t => t.Y).ThenBy(t => t.X).ToList();
            if (banks.Count < 2)
            {
                continue;
            }

            var start = banks[random.Next(banks.Count)];
            var end = banks.Where(b => b.Level == start.Level).MaxBy(b => Math.Max(Math.Abs(b.X - start.X), Math.Abs(b.Y - start.Y)));
            if (Path(tiles, start, end) is not { Count: >= MinLength } path)
            {
                continue;
            }

            var river = RiverRules.For(map.Map.GetTerrain(start));
            path.ForEach(p => map.AddRiver(p, river));
        }
    }

    /// <summary>The shortest four-way path over <paramref name="tiles"/>, or null.</summary>
    private static List<MapPosition>? Path(HashSet<MapPosition> tiles, MapPosition start, MapPosition end)
    {
        var from = new Dictionary<MapPosition, MapPosition> { [start] = start };
        var queue = new Queue<MapPosition>([start]);
        while (queue.TryDequeue(out var position))
        {
            if (position == end)
            {
                var path = new List<MapPosition> { end };
                while (path[^1] != start)
                {
                    path.Add(from[path[^1]]);
                }

                path.Reverse();
                return path;
            }

            foreach (var (dx, dy) in Four)
            {
                var next = position.Offset(dx, dy);
                if (tiles.Contains(next) && from.TryAdd(next, position))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return null;
    }

    private static IEnumerable<MapPosition> Around(MapPosition position)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                if (dx != 0 || dy != 0)
                {
                    yield return position.Offset(dx, dy);
                }
            }
        }
    }
}
