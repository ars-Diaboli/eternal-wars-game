using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Generation;

/// <summary>
/// Stage 5 of the generator (ADR-064): roads, user rules (RMG-4). Every zone has a <b>hub</b>, its first town's entrance or, without a
/// town, the free tile nearest its middle. A road joins:
/// <list type="bullet">
/// <item>every other town of the zone to the hub;</item>
/// <item>every passage of a connection to the hubs of both zones, so the zones are joined by road;</item>
/// <item>with <see cref="TemplateRoads.Gates"/>, every gate of a zone that has a town to the hub;</item>
/// <item>for each resource in <see cref="TemplateRoads.Mines"/>, the mine of that resource closest to the hub, in zones with a town.</item>
/// </list>
/// Roads take the cheapest way over walkable ground (8 ways; existing road is cheaper, so roads merge) and never cross an object's
/// body. Runs after towns and mines and before treasure, which then keeps off the road (<see cref="GeneratedMap.Fits"/>). Deterministic, no random draws.
/// </summary>
public static class RoadPlacer
{
    private const int Straight = 10;
    private const int Diagonal = 14;
    private const int OnRoad = 4;

    public static void Place(MapTemplate template, GeneratedMap map)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(map);
        if (map.Layout.ZoneIds.Count != template.Zones.Count)
        {
            throw new ArgumentException("The map was not made from this template.", nameof(map));
        }

        var roads = template.Roads;
        if (!roads.Enabled || roads.Type == RoadType.None)
        {
            return;
        }

        var blocked = map.Objects.SelectMany(o => o.OccupiedCells()).Where(c => c.Cell == FootprintCell.Blocked).Select(c => c.Position).ToHashSet();
        var hubs = new MapPosition?[template.Zones.Count];
        for (var zone = 0; zone < template.Zones.Count; zone++)
        {
            hubs[zone] = Hub(map, zone, blocked);
        }

        foreach (var zone in Enumerable.Range(0, template.Zones.Count))
        {
            if (hubs[zone] is not { } hub)
            {
                continue;
            }

            var towns = map.Towns.Where(t => t.Zone == zone).ToList();
            foreach (var town in towns.Skip(1))
            {
                Lay(map, roads.Type, blocked, town.Town.VisitPosition, hub, $"town in zone '{template.Zones[zone].Id}'");
            }

            if (towns.Count == 0)
            {
                continue;
            }

            if (roads.Gates)
            {
                foreach (var gate in map.Objects.OfType<TwoWayGate>().Where(g => ZoneOf(map, g.VisitPosition) == zone))
                {
                    Lay(map, roads.Type, blocked, gate.VisitPosition, hub, $"gate in zone '{template.Zones[zone].Id}'");
                }
            }

            foreach (var resource in roads.Mines)
            {
                var closest = map.Objects.OfType<Mine>().Where(m => m.Resource == resource && ZoneOf(map, m.VisitPosition) == zone)
                    .OrderBy(m => Distance(m.VisitPosition, hub)).ThenBy(m => m.Id.Value).FirstOrDefault();
                if (closest is not null)
                {
                    Lay(map, roads.Type, blocked, closest.VisitPosition, hub, $"{resource} mine in zone '{template.Zones[zone].Id}'");
                }
            }
        }

        foreach (var passage in map.Passages.Where(p => p.Tiles.Count > 0))
        {
            foreach (var zone in new[] { passage.ZoneA, passage.ZoneB })
            {
                if (hubs[zone] is { } hub)
                {
                    Lay(map, roads.Type, blocked, passage.Anchor, hub, $"passage of connection {passage.Connection + 1}");
                }
            }
        }
    }

    private static int ZoneOf(GeneratedMap map, MapPosition p) => map.Layout.ZoneAt(p.X, p.Y, p.Level);

    private static int Distance(MapPosition a, MapPosition b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>The zone's first town's entrance, else the walkable tile of the zone nearest its middle; null for a zone with no ground.</summary>
    private static MapPosition? Hub(GeneratedMap map, int zone, HashSet<MapPosition> blocked)
    {
        if (map.Towns.FirstOrDefault(t => t.Zone == zone) is { } town)
        {
            return town.Town.VisitPosition;
        }

        var tiles = map.Layout.Tiles(zone).Select(t => new MapPosition(t.X, t.Y, t.Level)).Where(t => Walkable(map, blocked, t)).ToList();
        if (tiles.Count == 0)
        {
            return null;
        }

        var cx = tiles.Average(t => t.X);
        var cy = tiles.Average(t => t.Y);
        return tiles.OrderBy(t => ((t.X - cx) * (t.X - cx)) + ((t.Y - cy) * (t.Y - cy))).ThenBy(t => t.Y).ThenBy(t => t.X).First();
    }

    private static bool Walkable(GeneratedMap map, HashSet<MapPosition> blocked, MapPosition p) =>
        map.Map.Contains(p) && map.Map.GetTerrain(p) != TerrainPainter.WallTerrain && GeneratedMap.Terrains.IsPassable(map.Map.GetTerrain(p)) && !blocked.Contains(p);

    /// <summary>Paints a road along the cheapest way from <paramref name="from"/> to <paramref name="to"/> (both included), or lists a problem.</summary>
    private static void Lay(GeneratedMap map, RoadType type, HashSet<MapPosition> blocked, MapPosition from, MapPosition to, string what)
    {
        if (Way(map, blocked, from, to) is not { } way)
        {
            map.Problem($"No road could reach the {what} at {from}.");
            return;
        }

        foreach (var tile in way)
        {
            map.AddRoad(tile, type);
        }
    }

    /// <summary>Dijkstra over walkable ground on one level; diagonals cost more, road tiles less. Ties break by position, so it is deterministic.</summary>
    private static List<MapPosition>? Way(GeneratedMap map, HashSet<MapPosition> blocked, MapPosition from, MapPosition to)
    {
        if (from.Level != to.Level || !Walkable(map, blocked, from) || !Walkable(map, blocked, to))
        {
            return null;
        }

        var cost = new Dictionary<MapPosition, int> { [from] = 0 };
        var parent = new Dictionary<MapPosition, MapPosition>();
        var queue = new PriorityQueue<MapPosition, (int Cost, int Y, int X)>();
        queue.Enqueue(from, (0, from.Y, from.X));
        while (queue.TryDequeue(out var current, out var priority))
        {
            if (priority.Cost > cost[current])
            {
                continue;
            }

            if (current == to)
            {
                var way = new List<MapPosition> { to };
                while (parent.TryGetValue(way[^1], out var previous))
                {
                    way.Add(previous);
                }

                return way;
            }

            foreach (var direction in DirectionExtensions.All)
            {
                var next = current.Step(direction);
                if (!Walkable(map, blocked, next))
                {
                    continue;
                }

                var diagonal = next.X != current.X && next.Y != current.Y;
                var step = map.Map.GetRoad(next) != RoadType.None ? OnRoad : diagonal ? Diagonal : Straight;
                var total = priority.Cost + step;
                if (!cost.TryGetValue(next, out var known) || total < known)
                {
                    cost[next] = total;
                    parent[next] = current;
                    queue.Enqueue(next, (total, next.Y, next.X));
                }
            }
        }

        return null;
    }
}
