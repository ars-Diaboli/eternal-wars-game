using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Generation;

/// <summary>
/// What one start slot has around it on a generated map (ADR-071, RMG-8; measured by <see cref="MapFairness"/>).
/// </summary>
/// <param name="Slot">The player slot.</param>
/// <param name="Zone">The start zone's id.</param>
/// <param name="MineDistance">Walking distance in tiles from the start town to the nearest mine of each resource the map has, added up.</param>
/// <param name="TreasureValue">The value of the treasure nearer to this start town than to any other (shared when two are as near).</param>
/// <param name="GuardPower">The least guard power a hero must beat to walk from the start town to the nearest rival start town.</param>
public sealed record StartMeasure(int Slot, string Zone, int MineDistance, int TreasureValue, int GuardPower);

/// <summary>The measures of every start and what is wrong with the map (unreachable places, starts that differ too much).</summary>
public sealed record FairnessReport(IReadOnlyList<StartMeasure> Starts, IReadOnlyList<string> Problems);

/// <summary>
/// The whole-map check of the generator (ADR-071, RMG-8, ADR-117), after every stage has run:
/// <list type="bullet">
/// <item><b>Connectivity</b>: every start town can walk to every other start town, and every town, mine and treasure item can be walked to from some
/// start town. Walking is the game's: 8 ways over passable ground around blocked object cells, through guards (they are fought), pickups, border
/// guards and garrisons, by gates and portals; any other entrance ends a way.</item>
/// <item><b>Fairness</b>: start zones the template makes alike (the same size, towns, mines, piles, treasure and monsters) are compared by
/// <see cref="StartMeasure.MineDistance"/>, <see cref="StartMeasure.TreasureValue"/> and <see cref="StartMeasure.GuardPower"/>. Each must be within
/// <see cref="TolerancePercent"/> of the group's average, or within a small slack of it (<see cref="DistanceSlack"/> tiles, <see cref="TreasureSlack"/>
/// gold, <see cref="GuardSlack"/> power) so that small numbers do not fail on a tile or a pile.</item>
/// </list>
/// Each failure is a generator problem, so <see cref="MapGenerator.GenerateBest"/> tries the next sub-seed and reports the best map when none passes.
/// Distances count an orthogonal step as 1 and a diagonal one as 1.5.
/// </summary>
public static class MapFairness
{
    public const int TolerancePercent = 15;

    public const int DistanceSlack = 4;

    public const int TreasureSlack = 1500;

    public const int GuardSlack = 300;

    private const int Unreached = int.MaxValue;

    public static FairnessReport Check(MapTemplate template, GeneratedMap map, CreatureCatalog? creatures = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(map);
        creatures ??= CreatureCatalog.Default;
        var problems = new List<string>();
        var walk = new Walk(map, creatures);

        var starts = map.Towns.Where(t => t.Slot is not null).GroupBy(t => t.Slot!.Value).OrderBy(g => g.Key).Select(g => g.First()).ToList();
        var distances = starts.Select(s => walk.Distances(s.Town.VisitPosition)).ToList();

        // Connectivity: start to start, and everything to some start.
        for (var i = 0; i < starts.Count; i++)
        {
            for (var j = 0; j < starts.Count; j++)
            {
                if (i != j && walk.At(distances[i], starts[j].Town.VisitPosition) == Unreached)
                {
                    problems.Add($"The start town of slot {starts[i].Slot} cannot walk to the start town of slot {starts[j].Slot}.");
                }
            }
        }

        var places = map.Objects.Where(o => o is Town or Mine || map.TreasureValues.ContainsKey(o.Id)).ToList();
        foreach (var place in starts.Count == 0 ? [] : places.Where(p => distances.All(d => walk.At(d, p.VisitPosition) == Unreached)))
        {
            problems.Add($"The {place.Definition.Name} at {place.VisitPosition} cannot be walked to from any start town.");
        }

        // The measures.
        var resources = map.Objects.OfType<Mine>().Select(m => m.Resource).Distinct().Order().ToList();
        var treasure = new double[starts.Count];
        foreach (var item in map.Objects.Where(o => map.TreasureValues.ContainsKey(o.Id)))
        {
            var near = distances.Select(d => walk.At(d, item.VisitPosition)).ToList();
            var best = near.Min();
            if (best == Unreached)
            {
                continue;
            }

            // Shared by closeness (1 / distance²): an item half-way between two starts counts half for each, one in a start zone almost wholly for it.
            var weights = near.Select(d => d == Unreached ? 0 : 1.0 / Math.Pow(Math.Max(d, 2), 2)).ToList();
            var total = weights.Sum();
            for (var i = 0; i < starts.Count; i++)
            {
                treasure[i] += map.TreasureValues[item.Id] * weights[i] / total;
            }
        }

        var measures = new List<StartMeasure>();
        for (var i = 0; i < starts.Count; i++)
        {
            var slot = starts[i].Slot!.Value;
            var zone = template.Zones[starts[i].Zone];
            var mineDistance = 0L;
            foreach (var resource in resources)
            {
                var nearest = map.Objects.OfType<Mine>().Where(m => m.Resource == resource).Select(m => walk.At(distances[i], m.VisitPosition)).Where(d => d != Unreached).DefaultIfEmpty(0).Min();
                mineDistance += nearest;
            }

            var rivals = starts.Where((s, j) => j != i && !Allies(template, zone, template.Zones[s.Zone])).Select(s => s.Town.VisitPosition).ToList();
            var guard = rivals.Count == 0 ? 0 : walk.LeastGuard(starts[i].Town.VisitPosition, rivals);
            measures.Add(new StartMeasure(slot, zone.Id, (int)(mineDistance / 2), (int)Math.Round(treasure[i]), guard));
        }

        // Teams are compared, not players: allies share what lies between them (a player without a team is a team of one). Teams compare when their
        // start zones are alike: per team the average distance and guard power, and the treasure added up.
        var teams = measures.GroupBy(m => template.Zone(m.Zone)!.Team is { } team ? $"team {team}" : $"'{m.Zone}'").Select(g => (
            Name: g.Key,
            Signature: string.Join("+", g.Select(m => Signature(template.Zone(m.Zone)!)).Order(StringComparer.Ordinal)),
            Mines: (int)Math.Round(g.Average(m => m.MineDistance)),
            Treasure: g.Sum(m => m.TreasureValue),
            Guard: (int)Math.Round(g.Average(m => m.GuardPower)))).ToList();
        foreach (var group in teams.GroupBy(t => t.Signature).Where(g => g.Count() > 1))
        {
            var members = group.ToList();
            Compare(members, t => t.Name, t => t.Mines, DistanceSlack, "its mines are", "tiles away", problems);
            Compare(members, t => t.Name, t => t.Treasure, TreasureSlack, "the treasure nearest to it is", "worth", problems);
            Compare(members, t => t.Name, t => t.Guard, GuardSlack, "the way to its nearest rival is", "guarded by", problems);
        }

        return new FairnessReport(measures, problems);
    }

    private static bool Allies(MapTemplate template, TemplateZone a, TemplateZone b) => a.Team is { } team && b.Team == team;

    /// <summary>What makes two start zones alike: the template gives them the same.</summary>
    private static string Signature(TemplateZone zone) =>
        $"{zone.Size}|{zone.Level}|{zone.PlayerTowns}|{zone.NeutralTowns}|{string.Join(",", zone.Mines.Where(m => m.Value > 0).OrderBy(m => m.Key).Select(m => $"{m.Key}{m.Value}"))}"
        + $"|{zone.ExtraPiles}|{string.Join(",", zone.Treasure.Select(t => $"{t.MinValue}-{t.MaxValue}x{t.Count}"))}|{zone.Monsters}";

    private static void Compare<T>(List<T> members, Func<T, string> name, Func<T, int> measure, int slack, string what, string unit, List<string> problems)
    {
        var average = members.Average(m => (double)measure(m));
        var allowed = Math.Max(average * TolerancePercent / 100, slack);
        foreach (var member in members.Where(m => Math.Abs(measure(m) - average) > allowed))
        {
            problems.Add($"Unfair start {name(member)}: {what} {unit} {measure(member)}, the alike starts {Math.Round(average)} on average (allowed ±{Math.Round(allowed)}).");
        }
    }

    /// <summary>Walking over the generated map as a hero would.</summary>
    private sealed class Walk
    {
        private static readonly (int Dx, int Dy, int Cost)[] Steps =
            [(1, 0, 2), (-1, 0, 2), (0, 1, 2), (0, -1, 2), (1, 1, 3), (1, -1, 3), (-1, 1, 3), (-1, -1, 3)];

        private readonly GeneratedMap _map;
        private readonly int _size;
        private readonly int _levels;
        private readonly bool[] _blocked;
        private readonly bool[] _stops;
        private readonly Dictionary<int, int> _teleports = [];
        private readonly Dictionary<int, List<(int Guard, int Power)>> _guarded = [];

        public Walk(GeneratedMap map, CreatureCatalog creatures)
        {
            _map = map;
            _size = map.Map.Width;
            _levels = map.Map.Levels;
            _blocked = new bool[_size * _size * _levels];
            _stops = new bool[_blocked.Length];
            for (var i = 0; i < _blocked.Length; i++)
            {
                var p = Position(i);
                _blocked[i] = map.Map.GetTerrain(p) == TerrainPainter.WallTerrain || !GeneratedMap.Terrains.IsPassable(map.Map.GetTerrain(p));
            }

            var byId = map.Objects.ToDictionary(o => o.Id);
            foreach (var mapObject in map.Objects)
            {
                foreach (var (position, cell) in mapObject.OccupiedCells())
                {
                    if (cell == FootprintCell.Blocked && map.Map.Contains(position))
                    {
                        _blocked[Index(position)] = true;
                    }
                }

                if (!mapObject.Definition.Footprint.HasVisit || !map.Map.Contains(mapObject.VisitPosition))
                {
                    continue;
                }

                var visit = Index(mapObject.VisitPosition);
                if (mapObject.TeleportTarget is { } target && byId.TryGetValue(target, out var exit))
                {
                    _teleports[visit] = Index(exit.VisitPosition);
                }
                else if (mapObject is not (MonsterGuard or ResourcePile or TreasureChest or ArtifactPickup or MapGarrison) && mapObject.EndsRoute)
                {
                    _stops[visit] = true;
                }

                var power = mapObject switch
                {
                    MonsterGuard guard => (guard.Count ?? 1) * creatures.Get(guard.Creature).Value,
                    MapGarrison garrison => garrison.Troops.Stacks.Sum(s => s.Stack.Count * creatures.Get(s.Stack.Creature).Value),
                    _ => 0,
                };
                if (power <= 0)
                {
                    continue;
                }

                var zone = mapObject is MonsterGuard monster ? monster.GuardedTiles().Append(monster.Position) : [mapObject.VisitPosition];
                foreach (var tile in zone.Where(map.Map.Contains))
                {
                    if (!_guarded.TryGetValue(Index(tile), out var list))
                    {
                        _guarded[Index(tile)] = list = [];
                    }

                    list.Add((mapObject.Id.Value, power));
                }
            }
        }

        /// <summary>The cost to reach a tile, walking in or landing on it from a gate.</summary>
        public int At(int[] distances, MapPosition position) =>
            _map.Map.Contains(position) ? Math.Min(distances[Index(position) * 2], distances[(Index(position) * 2) + 1]) : Unreached;

        /// <summary>Walking cost (2 a straight step, 3 a diagonal one) from the start to every tile; <see cref="Unreached"/> where it cannot go.</summary>
        public int[] Distances(MapPosition from)
        {
            // A node is a tile and how it was entered: walked onto (even) or landed on from a gate (odd), which lets the hero walk off a gate.
            var cost = new int[_blocked.Length * 2];
            Array.Fill(cost, Unreached);
            var open = new PriorityQueue<int, int>();
            var start = (Index(from) * 2) + 1;
            cost[start] = 0;
            open.Enqueue(start, 0);
            while (open.TryDequeue(out var current, out var at))
            {
                if (at > cost[current])
                {
                    continue;
                }

                foreach (var (next, step) in Next(current))
                {
                    if (at + step < cost[next])
                    {
                        cost[next] = at + step;
                        open.Enqueue(next, at + step);
                    }
                }
            }

            return cost;
        }

        /// <summary>
        /// The least guard power on a way from the start to one of the targets (a guard counts when the way steps into the tiles it guards from
        /// outside them), ties broken by distance.
        /// </summary>
        public int LeastGuard(MapPosition from, IReadOnlyList<MapPosition> targets)
        {
            var goals = targets.Where(_map.Map.Contains).Select(Index).ToHashSet();
            var best = new (long Guard, int Distance)[_blocked.Length * 2];
            Array.Fill(best, (long.MaxValue, int.MaxValue));
            var open = new PriorityQueue<int, (long, int)>();
            var start = (Index(from) * 2) + 1;
            best[start] = (0, 0);
            open.Enqueue(start, (0, 0));
            while (open.TryDequeue(out var current, out var at))
            {
                if (at.CompareTo(best[current]) > 0)
                {
                    continue;
                }

                if (goals.Contains(current / 2))
                {
                    return (int)Math.Min(int.MaxValue, at.Item1);
                }

                var here = _guarded.GetValueOrDefault(current / 2);
                foreach (var (next, step) in Next(current))
                {
                    var entered = _guarded.TryGetValue(next / 2, out var guards)
                        ? guards.Where(g => here is null || here.All(h => h.Guard != g.Guard)).Sum(g => (long)g.Power)
                        : 0;
                    var candidate = (at.Item1 + entered, at.Item2 + step);
                    if (candidate.CompareTo(best[next]) < 0)
                    {
                        best[next] = candidate;
                        open.Enqueue(next, candidate);
                    }
                }
            }

            return 0; // no way at all: the connectivity check reports it
        }

        /// <summary>
        /// The nodes one step on: walked onto a gate, only its exit (landed on); walked onto another entrance that ends a route, none; else the
        /// passable tiles around.
        /// </summary>
        private IEnumerable<(int Next, int Cost)> Next(int node)
        {
            var (current, landed) = (node / 2, node % 2 == 1);
            if (!landed && _teleports.TryGetValue(current, out var exit))
            {
                yield return ((exit * 2) + 1, 2);
                yield break;
            }

            if (!landed && _stops[current])
            {
                yield break;
            }

            var p = Position(current);
            foreach (var (dx, dy, cost) in Steps)
            {
                var (x, y) = (p.X + dx, p.Y + dy);
                if (x < 0 || y < 0 || x >= _size || y >= _size)
                {
                    continue;
                }

                var next = Index(new MapPosition(x, y, p.Level));
                if (!_blocked[next])
                {
                    yield return (next * 2, cost);
                }
            }
        }

        private int Index(MapPosition p) => (((p.Level * _size) + p.Y) * _size) + p.X;

        private MapPosition Position(int index) => new(index % _size, index / _size % _size, index / (_size * _size));
    }
}
