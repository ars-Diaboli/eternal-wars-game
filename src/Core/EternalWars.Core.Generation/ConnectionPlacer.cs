using EternalWars.Core.Domain.Armies;
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
/// <item><b>Portal</b> (ADR-112): like a gate, but a pair of portals anywhere in the two zones, on any levels.</item>
/// </list>
/// A normal connection's guard (ADR-112, <see cref="MapTemplate.GuardOf"/>) is a neutral stack, a neutral garrison in the way (where the wall closes
/// beside it; else a stack), or a border guard or gate with the Keymaster's Tent of its colour in the first zone. Connections with a chance below
/// 100 % are rolled first (<see cref="Present"/>).
/// What cannot be done (a connection whose zones never touched, a gate with no room) is listed in <see cref="GeneratedMap.Problems"/>.
/// Draws from the <c>connections</c> stream, one per connection, and the <c>presence</c> stream.
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
        var present = Present(template, seed);
        var keymasters = new HashSet<ObjectColour>();
        foreach (var c in Enumerable.Range(0, template.Connections.Count).Where(c => present[c]).OrderBy(Border).ThenBy(c => c).ToList())
        {
            var connection = template.Connections[c];
            var (a, b) = (index[connection.From], index[connection.To]);
            var random = streams.For("connections", c);
            if (connection.Kind is ConnectionKind.Gate or ConnectionKind.Portal)
            {
                for (var i = 0; i < connection.Passages; i++)
                {
                    PlaceGates(map, template, connection, c, a, b, random, creatures);
                }
            }
            else
            {
                var guard = template.GuardOf(connection);
                var border = guard is ConnectionGuard.BorderGuard or ConnectionGuard.BorderGate;
                var colour = connection.Colour ?? (border ? (ObjectColour)random.Next(4) : default); // only a border post draws, so other maps stay the same
                Cut(map, connection, guard, colour, c, a, b, random, creatures);
                if (border && map.Passages.Any(p => p.Connection == c) && keymasters.Add(colour))
                {
                    PlaceKeymaster(map, template, connection, colour, a, b, random, creatures);
                }
            }
        }
    }

    /// <summary>
    /// Which connections the map has (RMG-13): each is rolled against its <see cref="TemplateConnection.Chance"/> on the <c>presence</c> stream; then,
    /// in template order, every dropped connection that joins two parts the others leave apart is made after all, so the zones stay connected.
    /// </summary>
    public static bool[] Present(MapTemplate template, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(template);
        var streams = new SeedStreams(seed);
        var connections = template.Connections;
        var present = connections.Select((c, i) => c.Chance >= 100 || streams.For("presence", i).Next(100) < c.Chance).ToArray();
        var index = template.Zones.Select((z, i) => (z.Id, i)).ToDictionary(p => p.Id, p => p.i);
        var parent = Enumerable.Range(0, template.Zones.Count).ToArray();
        int Root(int z) => parent[z] == z ? z : parent[z] = Root(parent[z]);
        bool Join(TemplateConnection c)
        {
            var (x, y) = (Root(index[c.From]), Root(index[c.To]));
            parent[x] = y;
            return x != y;
        }

        for (var i = 0; i < connections.Count; i++)
        {
            if (present[i])
            {
                Join(connections[i]);
            }
        }

        for (var i = 0; i < connections.Count; i++)
        {
            if (!present[i] && Join(connections[i]))
            {
                present[i] = true;
            }
        }

        return present;
    }

    // Passages through walls ------------------------------------------------------------------

    private static void Cut(GeneratedMap map, TemplateConnection connection, ConnectionGuard guard, ObjectColour colour, int number, int a, int b, GameRandom random, CreatureCatalog creatures)
    {
        var made = new List<MapPosition>();
        foreach (var strict in new[] { true, false })
        {
            if (made.Count < connection.Passages)
            {
                CutPassages(map, connection, guard, colour, number, a, b, random, creatures, strict, made);
            }
        }

        if (made.Count < connection.Passages)
        {
            map.Problem($"Connection {connection.From} - {connection.To}: room for {made.Count} of {connection.Passages} passages.");
        }
    }

    /// <summary>Opens passages until the connection has all of them; <paramref name="strict"/> refuses wall tiles that touch ground of a third zone (a leak), the second round allows them.</summary>
    private static void CutPassages(GeneratedMap map, TemplateConnection connection, ConnectionGuard guard, ObjectColour colour, int number, int a, int b, GameRandom random, CreatureCatalog creatures, bool strict, List<MapPosition> anchors)
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
            var garrison = guard == ConnectionGuard.Garrison ? GarrisonFor(map, candidate, way, toA, a, b) : null;
            if (guard == ConnectionGuard.Garrison && garrison is null && strict)
            {
                continue; // first round: only where the garrison closes the way; the second takes any place and guards it with monsters
            }

            var opened = Carve(map, way, toA, toB, connection.Kind == ConnectionKind.Wide ? WideRadius : 0, inWall, a, b, level);
            anchors.Add(candidate);
            map.AddPassage(new GeneratedPassage(number, a, b, opened, garrison?.At ?? candidate));
            if (connection.Kind != ConnectionKind.Normal)
            {
                continue;
            }

            switch (guard)
            {
                case ConnectionGuard.Garrison when garrison is { } placed:
                    PlaceGarrison(map, placed.Definition, placed.At, connection.GuardValue, random, creatures);
                    break;
                case ConnectionGuard.BorderGuard or ConnectionGuard.BorderGate:
                    var post = MapObjectCatalog.Default.Get(BuiltInMapObjects.ColouredId(guard == ConnectionGuard.BorderGate ? BuiltInMapObjects.BorderGate : BuiltInMapObjects.BorderGuard, colour));
                    map.Add(new BorderPost(map.NextId(), post, candidate));
                    break;
                default:
                    if (connection.GuardValue > 0)
                    {
                        Guard(map, candidate, connection.GuardValue, random, creatures);
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// A garrison that closes a one-tile way: on a tile of the way (the anchor first) whose neighbours left and right (a horizontal garrison, passed
    /// north–south) or above and below (a vertical one) are standing wall, where the two zones' ground and the way no longer meet once it stands
    /// there (a flood over the two zones). Null when no tile of the way does.
    /// </summary>
    private static (MapObjectDefinition Definition, MapPosition At)? GarrisonFor(GeneratedMap map, MapPosition anchor, List<MapPosition> way, List<MapPosition> toA, int a, int b)
    {
        var onWay = way.ToHashSet();
        bool Wall(MapPosition t) => map.Map.Contains(t) && map.Map.GetTerrain(t) == TerrainPainter.WallTerrain && !onWay.Contains(t) && !map.IsOccupied(t);
        var level = anchor.Level;
        var starts = Four.Select(d => toA[^1].Offset(d.Dx, d.Dy)).Where(n => map.IsFree(n) && map.Layout.ZoneAt(n.X, n.Y, level) == a).ToList();
        if (starts.Count == 0)
        {
            return null;
        }

        var start = starts[0];

        bool Closes(HashSet<MapPosition> blocked)
        {
            var seen = new HashSet<MapPosition> { start };
            var queue = new Queue<MapPosition>([start]);
            while (queue.TryDequeue(out var p))
            {
                foreach (var d in DirectionExtensions.All)
                {
                    var next = p.Step(d);
                    if (blocked.Contains(next) || !map.Map.Contains(next) || seen.Contains(next))
                    {
                        continue;
                    }

                    var zone = map.Layout.ZoneAt(next.X, next.Y, level);
                    var ground = map.IsFree(next) && zone is var z && (z == a || z == b);
                    if (!ground && !(onWay.Contains(next) && !map.IsOccupied(next)))
                    {
                        continue;
                    }

                    if (ground && zone == b && !onWay.Contains(next))
                    {
                        return false;
                    }

                    seen.Add(next);
                    queue.Enqueue(next);
                }
            }

            return true;
        }

        foreach (var tile in way.OrderBy(t => t == anchor ? 0 : 1))
        {
            foreach (var (id, dx, dy) in new[] { (BuiltInMapObjects.Garrison, 1, 0), (BuiltInMapObjects.VerticalGarrison, 0, 1) })
            {
                var (first, second) = (tile.Offset(-dx, -dy), tile.Offset(dx, dy));
                if (Wall(first) && Wall(second) && Closes([tile, first, second]))
                {
                    return (MapObjectCatalog.Default.Get(id), tile);
                }
            }
        }

        return null;
    }

    /// <summary>A neutral garrison with its entrance on the anchor (its blocked ends in the wall), holding troops worth the guard value.</summary>
    private static void PlaceGarrison(GeneratedMap map, MapObjectDefinition definition, MapPosition anchor, int value, GameRandom random, CreatureCatalog creatures)
    {
        var visit = definition.Footprint.VisitOffset;
        var troops = new Army();
        if (value > 0)
        {
            var (creature, count) = GuardPicker.Pick(creatures, value, random);
            troops = Army.Of(new CreatureStack(creature.Id, count));
        }

        map.Add(new MapGarrison(map.NextId(), definition, anchor.Offset(-visit.Dx, -visit.Dy), troops: troops));
    }

    /// <summary>The Keymaster's Tent of a border post's colour, in the connection's first zone, with a guard of the connection's value next to it.</summary>
    private static void PlaceKeymaster(GeneratedMap map, MapTemplate template, TemplateConnection connection, ObjectColour colour, int a, int b, GameRandom random, CreatureCatalog creatures)
    {
        var definition = MapObjectCatalog.Default.Get(BuiltInMapObjects.ColouredId(BuiltInMapObjects.KeymasterTent, colour));
        if (Spot(map, definition, a, template.Zones[a].Level, [], random) is not { } at)
        {
            map.Problem($"Connection {connection.From} - {connection.To}: no room for the Keymaster's Tent.");
            return;
        }

        var tent = map.Add(new KeymasterTent(map.NextId(), definition, at));
        if (connection.GuardValue > 0)
        {
            var front = Four.Select(d => tent.VisitPosition.Offset(d.Dx, d.Dy)).Where(map.IsFree).ToList();
            if (front.Count > 0)
            {
                Guard(map, front[random.Next(front.Count)], connection.GuardValue, random, creatures);
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
        // Passage guards block the way: plain HoMM3 zone of control (ADR-065); Area is for treasure guards.
        map.Add(new MonsterGuard(map.NextId(), MapObjectCatalog.Default.Get(BuiltInMapObjects.Monster), at, creature.Id, count, GuardDisposition.Aggressive, mode: GuardMode.Classic));
    }

    // Gates -----------------------------------------------------------------------------------

    private static void PlaceGates(GeneratedMap map, MapTemplate template, TemplateConnection connection, int number, int a, int b, GameRandom random, CreatureCatalog creatures)
    {
        var sameLevel = template.Zones[a].Level == template.Zones[b].Level;
        var portal = connection.Kind == ConnectionKind.Portal;
        var definition = MapObjectCatalog.Default.Get(sameLevel || portal ? BuiltInMapObjects.TwoWayMonolith : BuiltInMapObjects.SubterraneanGate);
        // A gate stands near the border it crosses; a portal anywhere in its zones.
        var near = !portal && map.Walls.TryGetValue(a < b ? (a, b) : (b, a), out var wall) ? wall : [];
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
