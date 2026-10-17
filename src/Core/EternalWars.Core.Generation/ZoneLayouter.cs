using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Generation;

/// <summary>How the map is divided into zones.</summary>
public enum LayoutStrategy
{
    /// <summary>
    /// Organic: zone centres are placed with a spring model (zones that are joined pull together, all repel), then every tile goes
    /// to the nearest centre, weighted so each zone gets its share of the map.
    /// </summary>
    Voronoi,

    /// <summary>Blocky: the map is cut into rectangles by recursive weighted bisection, with joined zones next to each other in the cut order.</summary>
    Rectangles,
}

/// <summary>
/// Stage 1 of the generator (ADR-064): divides the map into zones in proportion to their weights, so that zones the template
/// joins touch, every zone is one piece, and every level is covered. Deterministic from (template, size, seed, strategy);
/// draws only from the <c>layout</c> stream of <see cref="SeedStreams"/>, one per level, and uses only exact arithmetic
/// (<c>+ − × ÷ √</c> on doubles), so the same seed gives the same layout on every platform.
/// </summary>
public static class ZoneLayouter
{
    public const int MinSize = 16;

    /// <summary>How many starting points are tried before the best layout is repaired.</summary>
    private const int Attempts = 12;

    /// <summary>Voronoi tries (cheap, on a small map).</summary>
    private const int VoronoiAttempts = 16;

    /// <summary>Side of the small map Voronoi searches on.</summary>
    private const int CoarseSize = 48;

    /// <summary>Moves tried per Voronoi attempt to open connections.</summary>
    private const int RefineSteps = 300;

    private static readonly (int Dx, int Dy)[] Around = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    /// <summary>Lays out the template on a <paramref name="size"/> × <paramref name="size"/> map. Throws <see cref="ArgumentException"/> for an invalid template or size.</summary>
    public static ZoneLayout Layout(MapTemplate template, int size, ulong seed, LayoutStrategy strategy = LayoutStrategy.Voronoi)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (TemplateValidator.Validate(template).FirstOrDefault(i => i.Severity == IssueSeverity.Error) is { } error)
        {
            throw new ArgumentException($"The template is not valid: {error.Message}", nameof(template));
        }

        if (size is < MinSize or > MapSizes.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, $"The map size goes from {MinSize} to {MapSizes.MaxDimension}.");
        }

        var streams = new SeedStreams(seed);
        var zones = template.Zones;
        var index = zones.Select((z, i) => (z.Id, i)).ToDictionary(p => p.Id, p => p.i, StringComparer.Ordinal);
        var edges = template.Connections.Select(c => (A: index[c.From], B: index[c.To])).ToList();
        var cells = new int[template.Levels][];
        var unmet = new List<(string, string)>();

        for (var level = 0; level < template.Levels; level++)
        {
            var members = Enumerable.Range(0, zones.Count).Where(i => zones[i].Level == level).ToList();
            var levelEdges = edges.Where(e => zones[e.A].Level == level && zones[e.B].Level == level).ToList();
            var random = streams.For("layout", level);
            var weights = new double[zones.Count];
            members.ForEach(m => weights[m] = zones[m].Size);

            // Many tries from different starting points; the first that makes every connection touch wins, else the best one is repaired.
            // Voronoi searches on a small map (cheap) and paints the winner at full size, where the contacts scale up with it.
            var grid = new int[size * size];
            var bestOpen = int.MaxValue;
            var coarse = Math.Min(size, CoarseSize);
            for (var attempt = 0; attempt < (strategy == LayoutStrategy.Voronoi ? VoronoiAttempts : Attempts) && bestOpen > 0; attempt++)
            {
                var tried = new int[size * size];
                if (strategy == LayoutStrategy.Voronoi)
                {
                    var (solution, coarseOpen) = Refine(coarse, members, levelEdges, weights, Solve(coarse, zones.Count, members, weights, levelEdges, random, layered: attempt % 2 == 0), random);
                    if (coarseOpen > 0 && bestOpen != int.MaxValue)
                    {
                        continue; // not worth painting at full size once something is known
                    }

                    Paint(tried, size, members, weights, solution, (double)size / coarse, balance: size > coarse ? 6 : 0);
                }
                else
                {
                    Rectangles(tried, size, members, weights, levelEdges, random);
                }

                MakeContiguous(tried, size);
                var open = CountOpen(tried, size, levelEdges);
                if (open > 0 && strategy == LayoutStrategy.Voronoi)
                {
                    Repair(tried, size, levelEdges); // a thin contact of the small map can be lost when painting at full size
                    open = CountOpen(tried, size, levelEdges);
                }

                if (open < bestOpen)
                {
                    (grid, bestOpen) = (tried, open);
                }
            }

            Repair(grid, size, levelEdges);
            unmet.AddRange(levelEdges.Where(e => !Touch(grid, size, e.A, e.B)).Select(e => (zones[e.A].Id, zones[e.B].Id)));
            cells[level] = grid;
        }

        return new ZoneLayout(size, zones.Select(z => z.Id).ToList(), cells, unmet);
    }

    // Voronoi ---------------------------------------------------------------------------------

    /// <summary>
    /// Where the zone centres are and how much each weighs in the power diagram, on a map of the size they were solved for. The
    /// arrays are indexed by zone (template order); only the zones of the level are used.
    /// </summary>
    private sealed record VoronoiSolution(double[] X, double[] Y, double[] Power);

    private static VoronoiSolution Solve(int size, int zoneCount, List<int> members, double[] weights, List<(int A, int B)> edges, GameRandom random, bool layered)
    {
        var x = new double[zoneCount];
        var y = new double[zoneCount];
        var power = new double[zoneCount];
        if (members.Count == 1)
        {
            (x[members[0]], y[members[0]], power[members[0]]) = (size / 2.0, size / 2.0, 1);
            return new(x, y, power);
        }

        var total = members.Sum(m => weights[m]);
        var target = new double[zoneCount];
        var radius = new double[zoneCount];
        foreach (var m in members)
        {
            target[m] = (double)size * size * weights[m] / total;
            radius[m] = Math.Sqrt(target[m] / Math.PI);
            x[m] = (size * 0.1) + (random.Next(10000) / 10000.0 * size * 0.8);
            y[m] = (size * 0.1) + (random.Next(10000) / 10000.0 * size * 0.8);
        }

        if (layered)
        {
            Layered(members, edges, size, random, x, y);
        }

        // Springs: joined zones want their centres about 0.85 of the sum of their radii apart, every pair at least 0.75 of it.
        for (var step = 0; step < 150; step++)
        {
            for (var i = 0; i < members.Count; i++)
            {
                for (var j = i + 1; j < members.Count; j++)
                {
                    Push(members[i], members[j], 0.75 * (radius[members[i]] + radius[members[j]]), 0.5, repel: true);
                }
            }

            foreach (var (a, b) in edges)
            {
                Push(a, b, 0.85 * (radius[a] + radius[b]), 0.25, repel: false);
            }

            foreach (var m in members)
            {
                x[m] = Math.Clamp(x[m], 1, size - 2);
                y[m] = Math.Clamp(y[m], 1, size - 2);
            }
        }

        void Push(int a, int b, double wanted, double strength, bool repel)
        {
            var dx = x[b] - x[a];
            var dy = y[b] - y[a];
            var distance = Math.Sqrt((dx * dx) + (dy * dy));
            if (distance < 1e-6)
            {
                (dx, dy, distance) = ((random.Next(2001) / 1000.0) - 1, (random.Next(2001) / 1000.0) - 1, 1);
            }

            var error = repel ? wanted - distance : distance - wanted; // positive: they need to move
            if (error <= 0)
            {
                return;
            }

            var move = error * strength / Math.Max(distance, 1e-6);
            var sign = repel ? 1 : -1; // repelling moves a away from b (against dx), attracting towards it
            x[a] -= sign * dx * move * 0.5;
            y[a] -= sign * dy * move * 0.5;
            x[b] += sign * dx * move * 0.5;
            y[b] += sign * dy * move * 0.5;
        }

        // Power diagram: a tile goes to the zone with the least (distance² − weight); weights grow for zones below their share.
        foreach (var m in members)
        {
            power[m] = radius[m] * radius[m];
        }

        Balance(new int[size * size], size, members, x, y, power, target, 8, 0.1);
        return new VoronoiSolution(x, y, power);
    }

    /// <summary>A few rounds of: paint, then grow the weight of zones below their share and (by <paramref name="drift"/>) move centres towards the middle of their cells.</summary>
    private static void Balance(int[] grid, int size, List<int> members, double[] x, double[] y, double[] power, double[] target, int rounds, double drift)
    {
        var count = new int[x.Length];
        var sumX = new double[x.Length];
        var sumY = new double[x.Length];
        for (var round = 0; round < rounds; round++)
        {
            Assign(grid, size, members, x, y, power);
            Array.Clear(count);
            Array.Clear(sumX);
            Array.Clear(sumY);
            for (var i = 0; i < grid.Length; i++)
            {
                count[grid[i]]++;
                sumX[grid[i]] += (i % size) + 0.5;
                sumY[grid[i]] += (i / size) + 0.5;
            }

            foreach (var m in members)
            {
                power[m] += (target[m] - count[m]) / Math.PI * 0.3;
                if (count[m] > 0 && drift > 0)
                {
                    x[m] += ((sumX[m] / count[m]) - x[m]) * drift;
                    y[m] += ((sumY[m] / count[m]) - y[m]) * drift;
                }
            }
        }
    }

    /// <summary>
    /// Local search on the small map: move one zone centre (or swap two) and keep the change when no more connections are open than
    /// before, until none is open or the tries run out. The objective is the real one (contacts of the painted, balanced diagram),
    /// which the spring model only approximates.
    /// </summary>
    private static (VoronoiSolution Solution, int Open) Refine(int size, List<int> members, List<(int A, int B)> edges, double[] weights, VoronoiSolution start, GameRandom random)
    {
        var grid = new int[size * size];
        var total = members.Sum(m => weights[m]);
        var target = new double[weights.Length];
        foreach (var m in members)
        {
            target[m] = (double)size * size * weights[m] / total;
        }

        int Evaluate(VoronoiSolution candidate)
        {
            var (x, y, power) = ((double[])candidate.X.Clone(), (double[])candidate.Y.Clone(), (double[])candidate.Power.Clone());
            Balance(grid, size, members, x, y, power, target, 5, 0);
            Assign(grid, size, members, x, y, power);
            MakeContiguous(grid, size);

            // Open connections first; a layout where a zone ended far from its share (the balance cannot always fix a bad move) counts against it too.
            var area = new int[weights.Length];
            foreach (var zone in grid)
            {
                area[zone]++;
            }

            return (CountOpen(grid, size, edges) * 100) + members.Count(m => area[m] < 0.5 * target[m] || area[m] > 1.6 * target[m]);
        }

        var best = start;
        var open = Evaluate(best);
        for (var step = 0; step < RefineSteps && open > 0; step++)
        {
            var (x, y) = ((double[])best.X.Clone(), (double[])best.Y.Clone());
            if (members.Count > 1 && random.Next(4) == 0)
            {
                var i = random.Next(members.Count);
                var j = (i + 1 + random.Next(members.Count - 1)) % members.Count;
                (x[members[i]], x[members[j]]) = (x[members[j]], x[members[i]]);
                (y[members[i]], y[members[j]]) = (y[members[j]], y[members[i]]);
            }
            else
            {
                var m = members[random.Next(members.Count)];
                var reach = size * 0.3;
                x[m] = Math.Clamp(x[m] + ((random.Next(2001) - 1000) / 1000.0 * reach), 1, size - 2);
                y[m] = Math.Clamp(y[m] + ((random.Next(2001) - 1000) / 1000.0 * reach), 1, size - 2);
            }

            var candidate = new VoronoiSolution(x, y, (double[])best.Power.Clone());
            var candidateOpen = Evaluate(candidate);
            if (candidateOpen <= open)
            {
                (best, open) = (candidate, candidateOpen);
            }
        }

        return (best, open / 100);
    }

    /// <summary>
    /// Paints the power diagram of a solution (scaled to another map size, and re-balanced there by <paramref name="balance"/> rounds
    /// so every zone gets its share of the bigger map) into the grid; a zone that lost every tile gets the one nearest its centre.
    /// </summary>
    private static void Paint(int[] grid, int size, List<int> members, double[] weights, VoronoiSolution solution, double scale, int balance)
    {
        var x = solution.X.Select(v => v * scale).ToArray();
        var y = solution.Y.Select(v => v * scale).ToArray();
        var power = solution.Power.Select(v => v * scale * scale).ToArray();
        if (balance > 0)
        {
            var total = members.Sum(m => weights[m]);
            var target = new double[weights.Length];
            members.ForEach(m => target[m] = (double)size * size * weights[m] / total);
            Balance(grid, size, members, x, y, power, target, balance, 0);
        }

        Assign(grid, size, members, x, y, power);
        foreach (var m in members.Where(m => !grid.Contains(m)))
        {
            grid[(Math.Clamp((int)y[m], 0, size - 1) * size) + Math.Clamp((int)x[m], 0, size - 1)] = m;
        }
    }

    /// <summary>
    /// A starting embedding that fits hubs: the best connected zone goes in the middle, the zones one connection away around it on
    /// a square ring, those two away on a wider ring, and so on, each ring in graph order from a random angle. Squares instead
    /// of circles keep it to exact arithmetic.
    /// </summary>
    private static void Layered(List<int> members, List<(int A, int B)> edges, int size, GameRandom random, double[] x, double[] y)
    {
        int Degree(int m) => edges.Count(e => e.A == m || e.B == m);
        var top = members.Max(Degree);
        var ties = members.Where(m => Degree(m) == top).ToList();
        var centre = ties[random.Next(ties.Count)];
        var depth = new Dictionary<int, int> { [centre] = 0 };
        var order = new List<int> { centre };
        for (var head = 0; head < order.Count; head++)
        {
            foreach (var next in edges.Where(e => e.A == order[head] || e.B == order[head]).Select(e => e.A == order[head] ? e.B : e.A).Distinct().OrderBy(n => n))
            {
                if (depth.TryAdd(next, depth[order[head]] + 1))
                {
                    order.Add(next);
                }
            }
        }

        var deepest = depth.Values.Max();
        foreach (var m in members.Where(m => !depth.ContainsKey(m)))
        {
            depth[m] = deepest + 1; // not joined to the rest on this level (the connection is a gate): outermost
            order.Add(m);
        }

        x[centre] = size / 2.0;
        y[centre] = size / 2.0;
        foreach (var ring in order.Where(m => m != centre).GroupBy(m => depth[m]))
        {
            var half = Math.Min(0.42, 0.24 * ring.Key) * size;
            var nodes = ring.ToList();
            var offset = random.Next(10000) / 10000.0;
            for (var i = 0; i < nodes.Count; i++)
            {
                var (px, py) = SquarePoint(((double)i / nodes.Count) + offset, half);
                x[nodes[i]] = (size / 2.0) + px;
                y[nodes[i]] = (size / 2.0) + py;
            }
        }
    }

    /// <summary>The point at fraction <paramref name="t"/> (wraps around) along the perimeter of a square of half-width <paramref name="half"/> around the origin.</summary>
    private static (double X, double Y) SquarePoint(double t, double half)
    {
        var u = (t - Math.Floor(t)) * 4;
        var side = (int)u;
        var f = u - side;
        return side switch
        {
            0 => ((-half) + (2 * half * f), -half),
            1 => (half, (-half) + (2 * half * f)),
            2 => (half - (2 * half * f), half),
            _ => (-half, half - (2 * half * f)),
        };
    }

    private static void Assign(int[] grid, int size, List<int> members, double[] x, double[] y, double[] power)
    {
        for (var i = 0; i < grid.Length; i++)
        {
            var px = (i % size) + 0.5;
            var py = (i / size) + 0.5;
            var best = members[0];
            var bestCost = double.MaxValue;
            foreach (var m in members)
            {
                var dx = px - x[m];
                var dy = py - y[m];
                var cost = (dx * dx) + (dy * dy) - power[m];
                if (cost < bestCost)
                {
                    (best, bestCost) = (m, cost);
                }
            }

            grid[i] = best;
        }
    }

    // Rectangles ------------------------------------------------------------------------------

    private static void Rectangles(int[] grid, int size, List<int> members, double[] weights, List<(int A, int B)> edges, GameRandom random)
    {
        // Cut order: a walk over the zone graph from a random zone, neighbours in random order, so joined zones end up next to each other.
        var order = new List<int>();
        var seen = new HashSet<int>();
        var queue = new Queue<int>();
        while (order.Count < members.Count)
        {
            var start = members.Where(m => !seen.Contains(m)).ElementAt(order.Count == 0 ? random.Next(members.Count) : 0);
            seen.Add(start);
            queue.Enqueue(start);
            while (queue.TryDequeue(out var current))
            {
                order.Add(current);
                var next = edges.Where(e => e.A == current || e.B == current).Select(e => e.A == current ? e.B : e.A).Where(n => !seen.Contains(n)).Distinct().ToList();
                for (var i = next.Count - 1; i > 0; i--)
                {
                    var j = random.Next(i + 1);
                    (next[i], next[j]) = (next[j], next[i]);
                }

                foreach (var n in next)
                {
                    seen.Add(n);
                    queue.Enqueue(n);
                }
            }
        }

        Split(order, 0, 0, size, size);

        void Split(List<int> zones, int x0, int y0, int w, int h)
        {
            if (zones.Count == 1 || (w < 2 && h < 2))
            {
                for (var y = y0; y < y0 + h; y++)
                {
                    for (var x = x0; x < x0 + w; x++)
                    {
                        grid[(y * size) + x] = zones[0];
                    }
                }

                return;
            }

            var sum = zones.Sum(z => weights[z]);
            var cut = 1;
            var best = double.MaxValue;
            var running = 0.0;
            for (var k = 1; k < zones.Count; k++)
            {
                running += weights[zones[k - 1]];
                var distance = Math.Abs(running - (sum / 2));
                if (distance < best)
                {
                    (cut, best) = (k, distance);
                }
            }

            var left = zones.Take(cut).ToList();
            var right = zones.Skip(cut).ToList();
            var share = left.Sum(z => weights[z]) / sum;
            if ((w >= h && w >= 2) || h < 2)
            {
                var wl = Math.Clamp((int)Math.Round(w * share), 1, w - 1);
                Split(left, x0, y0, wl, h);
                Split(right, x0 + wl, y0, w - wl, h);
            }
            else
            {
                var hl = Math.Clamp((int)Math.Round(h * share), 1, h - 1);
                Split(left, x0, y0, w, hl);
                Split(right, x0, y0 + hl, w, h - hl);
            }
        }
    }

    // Shared clean-up -------------------------------------------------------------------------

    /// <summary>Makes the connections that do not touch touch, by <see cref="Join"/>, a few rounds (it nearly always takes one).</summary>
    private static void Repair(int[] grid, int size, List<(int A, int B)> edges)
    {
        // Joining one pair must not cut the contact of another: the tiles that make a contact are locked once it exists.
        var locked = new bool[grid.Length];
        foreach (var (a, b) in edges)
        {
            LockContact(grid, size, a, b, locked);
        }

        for (var round = 0; round < 4; round++)
        {
            var any = false;
            foreach (var (a, b) in edges.Where(e => !Touch(grid, size, e.A, e.B)).ToList())
            {
                any = true;
                Join(grid, size, a, b, locked);
                LockContact(grid, size, a, b, locked);
            }

            MakeContiguous(grid, size);
            if (!any)
            {
                break;
            }
        }
    }

    /// <summary>Every zone becomes one connected piece: the smaller pieces go to the neighbour zone they border most.</summary>
    private static void MakeContiguous(int[] grid, int size)
    {
        for (var pass = 0; pass < 8; pass++)
        {
            var label = new int[grid.Length];
            Array.Fill(label, -1);
            var pieces = new List<(int Zone, List<int> Tiles)>();
            for (var start = 0; start < grid.Length; start++)
            {
                if (label[start] >= 0)
                {
                    continue;
                }

                var tiles = new List<int> { start };
                label[start] = pieces.Count;
                for (var head = 0; head < tiles.Count; head++)
                {
                    var (tx, ty) = (tiles[head] % size, tiles[head] / size);
                    foreach (var (dx, dy) in Around)
                    {
                        var (nx, ny) = (tx + dx, ty + dy);
                        if (nx < 0 || ny < 0 || nx >= size || ny >= size)
                        {
                            continue;
                        }

                        var next = (ny * size) + nx;
                        if (label[next] < 0 && grid[next] == grid[start])
                        {
                            label[next] = pieces.Count;
                            tiles.Add(next);
                        }
                    }
                }

                pieces.Add((grid[start], tiles));
            }

            var changed = false;
            foreach (var group in pieces.GroupBy(p => p.Zone).Where(g => g.Count() > 1))
            {
                var ordered = group.OrderByDescending(p => p.Tiles.Count).ThenBy(p => p.Tiles[0]).ToList();
                foreach (var (zone, tiles) in ordered.Skip(1))
                {
                    var borders = new Dictionary<int, int>();
                    foreach (var tile in tiles)
                    {
                        foreach (var next in Neighbours(tile, size).Where(n => grid[n] != zone))
                        {
                            borders[grid[next]] = borders.GetValueOrDefault(grid[next]) + 1;
                        }
                    }

                    if (borders.Count > 0)
                    {
                        var receiver = borders.OrderByDescending(b => b.Value).ThenBy(b => b.Key).First().Key;
                        tiles.ForEach(t => grid[t] = receiver);
                        changed = true;
                    }
                }
            }

            if (!changed)
            {
                return;
            }
        }
    }

    /// <summary>Whether two zones have tiles side by side.</summary>
    private static bool Touch(int[] grid, int size, int a, int b)
    {
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var here = grid[(y * size) + x];
                if (here != a && here != b)
                {
                    continue;
                }

                var other = here == a ? b : a;
                if ((x + 1 < size && grid[(y * size) + x + 1] == other) || (y + 1 < size && grid[((y + 1) * size) + x] == other))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>How many of the connections have no contact: one pass over the grid, noting which zone pairs sit side by side.</summary>
    private static int CountOpen(int[] grid, int size, List<(int A, int B)> edges)
    {
        var contact = new HashSet<(int, int)>();
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var here = grid[(y * size) + x];
                if (x + 1 < size && grid[(y * size) + x + 1] is var right && right != here)
                {
                    contact.Add(here < right ? (here, right) : (right, here));
                }

                if (y + 1 < size && grid[((y + 1) * size) + x] is var below && below != here)
                {
                    contact.Add(here < below ? (here, below) : (below, here));
                }
            }
        }

        return edges.Count(e => !contact.Contains(e.A < e.B ? (e.A, e.B) : (e.B, e.A)));
    }

    /// <summary>Locks one pair of side-by-side tiles of two zones (the first in reading order), if they touch.</summary>
    private static void LockContact(int[] grid, int size, int a, int b, bool[] locked)
    {
        for (var i = 0; i < grid.Length; i++)
        {
            if (grid[i] == a && Neighbours(i, size).FirstOrDefault(n => grid[n] == b, -1) is var other and >= 0)
            {
                locked[i] = true;
                locked[other] = true;
                return;
            }
        }
    }

    /// <summary>
    /// Makes two zones touch: the shortest way from one to the other, through tiles that other zones can spare without falling
    /// apart (<see cref="IsSimple"/>) and that no contact depends on, is handed half to each. Rarely needed after the springs.
    /// </summary>
    private static void Join(int[] grid, int size, int a, int b, bool[] locked)
    {
        var area = new Dictionary<int, int>();
        foreach (var zone in grid)
        {
            area[zone] = area.GetValueOrDefault(zone) + 1;
        }

        var blocked = new HashSet<int>();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            if (FindWay(grid, size, a, b, locked, area, blocked) is not { } path)
            {
                return;
            }

            if (path.Count == 0)
            {
                return; // they touch already
            }

            var toA = (path.Count + 1) / 2;
            var before = path.Select(t => grid[t]).ToList();
            var done = 0;
            for (; done < path.Count; done++)
            {
                if (!IsSimple(grid, size, path[done]))
                {
                    blocked.Add(path[done]);
                    break;
                }

                grid[path[done]] = done < toA ? a : b;
            }

            if (done == path.Count)
            {
                path.ForEach(t => locked[t] = true);
                return;
            }

            for (var i = 0; i < done; i++)
            {
                grid[path[i]] = before[i]; // that way would have cut a zone in two: undo and look for another
            }
        }
    }

    /// <summary>The tiles strictly between the zones along the shortest way (4-connected), or null if there is none; empty when they touch.</summary>
    private static List<int>? FindWay(int[] grid, int size, int a, int b, bool[] locked, Dictionary<int, int> area, HashSet<int> blocked)
    {
        var parent = new int[grid.Length];
        Array.Fill(parent, -2);
        var queue = new Queue<int>();
        for (var i = 0; i < grid.Length; i++)
        {
            if (grid[i] == a)
            {
                parent[i] = -1;
                queue.Enqueue(i);
            }
        }

        while (queue.TryDequeue(out var current))
        {
            foreach (var next in Neighbours(current, size))
            {
                if (parent[next] != -2)
                {
                    continue;
                }

                if (grid[next] == b)
                {
                    var path = new List<int>();
                    for (var tile = current; parent[tile] != -1; tile = parent[tile])
                    {
                        path.Add(tile);
                    }

                    path.Reverse(); // from just outside a, to just before b
                    return path;
                }

                if (grid[next] != a && !locked[next] && !blocked.Contains(next) && area[grid[next]] > 1 && IsSimple(grid, size, next))
                {
                    parent[next] = current;
                    queue.Enqueue(next);
                }
            }
        }

        return null;
    }

    private static readonly (int Dx, int Dy)[] Ring = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];

    /// <summary>
    /// Whether taking a tile out of its zone leaves the zone's tiles around it in one piece: its same-zone neighbours in the 3×3
    /// ring form a single run (so the zone stays connected locally). Conservative: a tile that fails may still be harmless.
    /// </summary>
    private static bool IsSimple(int[] grid, int size, int tile)
    {
        var x = tile % size;
        var y = tile / size;
        var zone = grid[tile];
        bool Same(int i)
        {
            var (nx, ny) = (x + Ring[i].Dx, y + Ring[i].Dy);
            return nx >= 0 && nx < size && ny >= 0 && ny < size && grid[(ny * size) + nx] == zone;
        }

        var runs = 0;
        var any = false;
        for (var i = 0; i < Ring.Length; i++)
        {
            any |= Same(i);
            if (Same(i) && !Same((i + 7) % 8))
            {
                runs++;
            }
        }

        return !any || runs == 1;
    }

    private static IEnumerable<int> Neighbours(int tile, int size)
    {
        var x = tile % size;
        var y = tile / size;
        foreach (var (dx, dy) in Around)
        {
            if (x + dx >= 0 && x + dx < size && y + dy >= 0 && y + dy < size)
            {
                yield return ((y + dy) * size) + x + dx;
            }
        }
    }
}
