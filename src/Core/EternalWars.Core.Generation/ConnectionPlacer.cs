using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Generation;

/// <summary>
/// Stage 3 of the generator (ADR-064): opens the connections of the template through the walls between zones and guards them.
/// Every connection makes <see cref="TemplateConnection.Passages"/> passages (one or more, set per connection in the template),
/// each at least <see cref="Spacing"/> tiles from the others of the same connection; extra contact between zones stays walled.
/// <list type="bullet">
/// <item><b>Normal</b>: a one-tile-wide way through the wall, with a neutral stack on it when the connection has a guard value
/// (<see cref="GuardPicker"/>).</item>
/// <item><b>Wide</b>: the same way, widened to five tiles, never guarded.</item>
/// <item><b>Gate</b>: for each passage a pair of linked two-way gates, one in each zone (monoliths on one level, subterranean
/// gates across levels), each with a guard of the full value next to its entrance.</item>
/// </list>
/// What cannot be done (a connection whose zones never touched, a gate with no room) is listed in <see cref="GeneratedMap.Problems"/>.
/// Draws from the <c>connections</c> stream, one per connection.
/// </summary>
public static class ConnectionPlacer
{
    private static readonly (int Dx, int Dy)[] Four = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Wide passages open every wall tile within this many tiles of the way through.</summary>
    public const int WideRadius = 2;

    /// <summary>The least distance between two passages of one connection.</summary>
    public static int Spacing(int mapSize) => Math.Max(6, mapSize / 12);

    public static void Place(MapTemplate template, GeneratedMap map, ulong seed, CreatureCatalog? creatures = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(map);
        creatures ??= CreatureCatalog.Default;
        if (map.Layout.ZoneIds.Count != template.Zones.Count)
        {
            throw new ArgumentException("The map was not made from this template.", nameof(map));
        }

        var streams = new SeedStreams(seed);
        var index = template.Zones.Select((z, i) => (z.Id, i)).ToDictionary(p => p.Id, p => p.i);

        // The thinnest borders first: a wide passage elsewhere could otherwise open the few wall tiles a short border has.
        int Border(int c) => map.Walls.TryGetValue(index[template.Connections[c].From] < index[template.Connections[c].To]
            ? (index[template.Connections[c].From], index[template.Connections[c].To]) : (index[template.Connections[c].To], index[template.Connections[c].From]), out var w) ? w.Count : 0;
        foreach (var c in Enumerable.Range(0, template.Connections.Count).OrderBy(Border).ThenBy(c => c).ToList())
        {
            var connection = template.Connections[c];
            var (a, b) = (index[connection.From], index[connection.To]);
            var random = streams.For("connections", c);
            if (connection.Kind == ConnectionKind.Gate)
            {
                for (var i = 0; i < connection.Passages; i++)
                {
                    PlaceGates(map, template, connection, c, a, b, random, creatures);
                }
            }
            else
            {
                Cut(map, connection, c, a, b, random, creatures);
            }
        }
    }

    // Passages through walls ------------------------------------------------------------------

    private static void Cut(GeneratedMap map, TemplateConnection connection, int number, int a, int b, GameRandom random, CreatureCatalog creatures)
    {
        var made = new List<MapPosition>();
        foreach (var strict in new[] { true, false })
        {
            if (made.Count < connection.Passages)
            {
                CutPassages(map, connection, number, a, b, random, creatures, strict, made);
            }
        }

        if (made.Count < connection.Passages)
        {
            map.Problem($"Connection {connection.From} - {connection.To}: room for {made.Count} of {connection.Passages} passages.");
        }
    }

    /// <summary>Opens passages until the connection has all of them; <paramref name="strict"/> refuses wall tiles that touch ground of a third zone (a leak), the second round allows them.</summary>
    private static void CutPassages(GeneratedMap map, TemplateConnection connection, int number, int a, int b, GameRandom random, CreatureCatalog creatures, bool strict, List<MapPosition> anchors)
    {
        if (!map.Walls.TryGetValue(a < b ? (a, b) : (b, a), out var wall) || wall.Count == 0)
        {
            if (strict)
            {
                map.Problem($"Connection {connection.From} - {connection.To}: the zones have no wall between them to open.");
            }

            return;
        }

        var level = wall[0].Level;
        var size = map.Layout.Size;

        // Only wall tiles that touch no ground but the two zones' own may be opened, so a passage never leaks into a third zone.
        bool Ground(MapPosition n, out int zone)
        {
            zone = map.Map.Contains(n) ? map.Layout.ZoneAt(n.X, n.Y, n.Level) : -1;
            return map.Map.Contains(n) && map.Map.GetTerrain(n) != TerrainPainter.WallTerrain;
        }

        bool CanOpen(MapPosition tile)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (strict && Ground(tile.Offset(dx, dy), out var zone) && zone != a && zone != b)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        var inWall = map.Walls.Where(w => w.Key.A == a || w.Key.B == a || w.Key.A == b || w.Key.B == b).SelectMany(w => w.Value)
            .Where(t => t.Level == level && map.Map.GetTerrain(t) == TerrainPainter.WallTerrain && CanOpen(t)).ToHashSet();
        bool Touches(MapPosition tile, int zone) => Four.Any(d => tile.Offset(d.Dx, d.Dy) is var n && Ground(n, out var z) && z == zone);

        var accessA = inWall.Where(t => Touches(t, a)).ToHashSet();
        var accessB = inWall.Where(t => Touches(t, b)).ToHashSet();
        var candidates = wall.Where(inWall.Contains).OrderBy(t => t.Y).ThenBy(t => t.X).ToList();
        for (var i = candidates.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        foreach (var candidate in candidates)
        {
            if (anchors.Count >= connection.Passages)
            {
                break;
            }

            if (anchors.Any(p => Math.Max(Math.Abs(p.X - candidate.X), Math.Abs(p.Y - candidate.Y)) < Spacing(size)))
            {
                continue;
            }

            if (Route(inWall, candidate, accessA) is not { } toA || Route(inWall, candidate, accessB) is not { } toB)
            {
                continue;
            }

            var way = toA.Concat(toB).Distinct().ToList();
            var opened = Carve(map, way, toA, toB, connection.Kind == ConnectionKind.Wide ? WideRadius : 0, inWall, a, b, level);
            anchors.Add(candidate);
            map.AddPassage(new GeneratedPassage(number, a, b, opened, candidate));
            if (connection.Kind == ConnectionKind.Normal && connection.GuardValue > 0)
            {
                Guard(map, candidate, connection.GuardValue, random, creatures);
            }
        }
    }

    /// <summary>The shortest 4-connected way from a tile to any of the targets through wall tiles (the first tile is the start), or null.</summary>
    private static List<MapPosition>? Route(HashSet<MapPosition> inWall, MapPosition start, HashSet<MapPosition> targets)
    {
        var parent = new Dictionary<MapPosition, MapPosition?> { [start] = null };
        var queue = new Queue<MapPosition>([start]);
        while (queue.TryDequeue(out var current))
        {
            if (targets.Contains(current))
            {
                var path = new List<MapPosition>();
                for (MapPosition? tile = current; tile is { } t; tile = parent[t])
                {
                    path.Add(t);
                }

                path.Reverse(); // start ... target
                return path;
            }

            foreach (var (dx, dy) in Four)
            {
                var next = current.Offset(dx, dy);
                if (inWall.Contains(next) && parent.TryAdd(next, current))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return null;
    }

    /// <summary>Opens the way (each tile takes the terrain of the ground it leads out to) and, for a radius, every wall tile near it.</summary>
    private static List<MapPosition> Carve(GeneratedMap map, List<MapPosition> way, List<MapPosition> toA, List<MapPosition> toB, int radius, HashSet<MapPosition> inWall, int a, int b, int level)
    {
        var opened = new List<MapPosition>();
        foreach (var (route, zone) in new[] { (toA, a), (toB, b) })
        {
            // From the ground end inwards: the first tile copies the terrain of the walkable neighbour of its zone, the rest the tile before.
            var end = route[^1];
            var ground = Four.Select(d => end.Offset(d.Dx, d.Dy)).First(n => map.Map.Contains(n) && map.Map.GetTerrain(n) != TerrainPainter.WallTerrain
                && map.Layout.ZoneAt(n.X, n.Y, level) == zone);
            var terrain = map.Map.GetTerrain(ground);
            for (var i = route.Count - 1; i >= 0; i--)
            {
                if (inWall.Contains(route[i]) && map.Map.GetTerrain(route[i]) == TerrainPainter.WallTerrain)
                {
                    map.Open(route[i], terrain);
                    opened.Add(route[i]);
                }
            }
        }

        if (radius > 0)
        {
            foreach (var tile in inWall.Where(t => map.Map.GetTerrain(t) == TerrainPainter.WallTerrain).ToList())
            {
                foreach (var w in way)
                {
                    if (Math.Max(Math.Abs(w.X - tile.X), Math.Abs(w.Y - tile.Y)) <= radius)
                    {
                        map.Open(tile, map.Map.GetTerrain(w));
                        opened.Add(tile);
                        break;
                    }
                }
            }
        }

        return opened.Distinct().ToList();
    }

    private static void Guard(GeneratedMap map, MapPosition at, int value, GameRandom random, CreatureCatalog creatures)
    {
        if (!map.IsFree(at))
        {
            map.Problem($"A guard of {value} power found no free tile at {at}.");
            return;
        }

        var (creature, count) = GuardPicker.Pick(creatures, value, random);
        map.Add(new MonsterGuard(map.NextId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.Monster), at, creature.Id, count, GuardDisposition.Aggressive));
    }

    // Gates -----------------------------------------------------------------------------------

    private static void PlaceGates(GeneratedMap map, MapTemplate template, TemplateConnection connection, int number, int a, int b, GameRandom random, CreatureCatalog creatures)
    {
        var sameLevel = template.Zones[a].Level == template.Zones[b].Level;
        var definition = MapObjectCatalog.Default.Get(sameLevel ? BuiltInMapObjects.TwoWayMonolith : BuiltInMapObjects.SubterraneanGate);
        var near = map.Walls.TryGetValue(a < b ? (a, b) : (b, a), out var wall) ? wall : [];
        if (Spot(map, definition, a, template.Zones[a].Level, near, random) is not { } first
            || Spot(map, definition, b, template.Zones[b].Level, near, random, avoid: first) is not { } second)
        {
            map.Problem($"Connection {connection.From} - {connection.To}: no room for a gate in each zone.");
            return;
        }

        var firstId = map.NextId();
        var secondId = map.NextId();
        var gates = new[]
        {
            map.Add(new TwoWayGate(firstId, definition, first, secondId)),
            map.Add(new TwoWayGate(secondId, definition, second, firstId)),
        };
        map.AddPassage(new GeneratedPassage(number, a, b, [], gates[0].VisitPosition));
        if (connection.GuardValue > 0)
        {
            foreach (var gate in gates)
            {
                var front = Four.Select(d => gate.VisitPosition.Offset(d.Dx, d.Dy)).Where(map.IsFree).ToList();
                if (front.Count > 0)
                {
                    Guard(map, front[random.Next(front.Count)], connection.GuardValue, random, creatures);
                }
                else
                {
                    map.Problem($"A gate of {connection.From} - {connection.To} has no free tile in front of it for a guard.");
                }
            }
        }
    }

    /// <summary>A place for the object in a zone: free, with free ground around it, preferably near the zone's border with the other zone.</summary>
    private static MapPosition? Spot(GeneratedMap map, MapObjectDefinition definition, int zone, int level, List<MapPosition> border, GameRandom random, MapPosition? avoid = null)
    {
        var size = map.Layout.Size;
        var tiles = map.Layout.Tiles(zone).Where(t => t.Level == level).Select(t => new MapPosition(t.X, t.Y, t.Level)).Where(t => map.Fits(definition, t)
            && Margin(map, definition, t) && (avoid is not { } other || other.Level != level || Math.Max(Math.Abs(other.X - t.X), Math.Abs(other.Y - t.Y)) > 6)).ToList();
        if (tiles.Count == 0)
        {
            return null;
        }

        var close = border.Count == 0 ? tiles : tiles.Where(t => border.Any(w => w.Level == level && Math.Max(Math.Abs(w.X - t.X), Math.Abs(w.Y - t.Y)) <= size / 6)).ToList();
        var pool = close.Count > 0 ? close : tiles;
        return pool[random.Next(pool.Count)];
    }

    /// <summary>Every tile around the object's bounding box is free ground (so it cannot pinch a corridor shut), and the one below its entrance too.</summary>
    internal static bool Margin(GeneratedMap map, MapObjectDefinition definition, MapPosition topLeft)
    {
        for (var dy = -1; dy <= definition.Footprint.Height; dy++)
        {
            for (var dx = -1; dx <= definition.Footprint.Width; dx++)
            {
                var tile = topLeft.Offset(dx, dy);
                if (!map.IsFree(tile))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
