using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Visibility;

/// <summary>
/// Per-player visibility, two layers:
/// <list type="bullet">
/// <item><b>Explored</b>: permanent. Terrain and objects on explored tiles are known.</item>
/// <item><b>In sight</b>: live, only meaningful in <see cref="FogMode.ShroudWithRefog"/>. Other players'
/// units are visible only on tiles in sight. In <see cref="FogMode.PermanentReveal"/> it equals explored.</item>
/// </list>
/// </summary>
public sealed class FogOfWar(AdventureMap map, FogMode mode)
{
    private readonly Dictionary<PlayerId, bool[][]> _explored = [];

    // Sets rather than full-map arrays: sight is a few hundred tiles even on 256×256 maps, so updates
    // and diffs stay proportional to what observers see, not to map size.
    private readonly Dictionary<PlayerId, HashSet<MapPosition>> _inSight = [];

    /// <summary>The observers and margin each player's sight was last worked out from (<see cref="UpdateSight"/>).</summary>
    private readonly Dictionary<PlayerId, (int Margin, (MapPosition Center, int Radius)[] Observers)> _seenFrom = [];

    public FogMode Mode { get; } = mode;

    public bool IsExplored(PlayerId player, MapPosition position) =>
        map.Contains(position) && _explored.TryGetValue(player, out var levels) && levels[position.Level][Index(position)];

    public bool IsInSight(PlayerId player, MapPosition position) =>
        Mode == FogMode.PermanentReveal
            ? IsExplored(player, position)
            : _inSight.TryGetValue(player, out var sight) && sight.Contains(position);

    /// <summary>Tiles within a rounded circle: dx² + dy² ≤ r² + r (the classic "nice circle" on grids).</summary>
    public static bool IsWithinRadius(MapPosition center, MapPosition position, int radius)
    {
        if (center.Level != position.Level)
        {
            return false;
        }

        var dx = position.X - center.X;
        var dy = position.Y - center.Y;
        return (dx * dx) + (dy * dy) <= (radius * radius) + radius;
    }

    /// <summary>Marks a circle explored and returns the tiles that were not explored before.</summary>
    internal IReadOnlyList<MapPosition> Reveal(PlayerId player, MapPosition center, int radius)
    {
        var explored = ExploredLayer(player);
        var revealed = new List<MapPosition>();
        foreach (var position in Circle(center, radius))
        {
            ref var flag = ref explored[position.Level][Index(position)];
            if (!flag)
            {
                flag = true;
                revealed.Add(position);
            }
        }

        return revealed;
    }

    internal IReadOnlyList<MapPosition> RevealAll(PlayerId player)
    {
        var revealed = new List<MapPosition>();
        for (var level = 0; level < map.Levels; level++)
        {
            foreach (var position in map.Positions(level))
            {
                revealed.AddRange(Reveal(player, position, 0));
            }
        }

        return revealed;
    }

    /// <summary>
    /// Recomputes what <paramref name="player"/> has in sight, with hysteresis: a tile is in sight if an
    /// observer is within its sight radius, or if it already was in sight and an observer is still within
    /// sight radius + <paramref name="margin"/>. Returns the tiles that entered and left sight.
    /// </summary>
    /// <remarks>
    /// Only tiles within sight radius + margin of an observer that moved, came, went or changed its radius can change: any other tile keeps
    /// the observer that put it in sight (or still has none near enough), so only the circles of those observers are worked out again, against
    /// the observers that reach them. The answer is the same as working everything out afresh.
    /// </remarks>
    internal (IReadOnlyList<MapPosition> Entered, IReadOnlyList<MapPosition> Left) UpdateSight(
        PlayerId player, IReadOnlyCollection<(MapPosition Center, int Radius)> observers, int margin)
    {
        var now = observers.ToArray();
        var previous = _inSight.GetValueOrDefault(player);
        var changed = previous is not null && _seenFrom.TryGetValue(player, out var last) && last.Margin == margin ? Changed(last.Observers, now) : null;
        _seenFrom[player] = (margin, now);
        if (changed is null || Area(changed, margin) > Area(now, margin))
        {
            return UpdateAllSight(player, previous ?? [], now, margin);
        }

        if (changed.Count == 0)
        {
            return ([], []);
        }

        // Only the observers that reach a changed circle can decide its tiles.
        var near = now.Where(o => changed.Any(c => c.Center.Level == o.Center.Level
            && Math.Max(Math.Abs(c.Center.X - o.Center.X), Math.Abs(c.Center.Y - o.Center.Y)) <= c.Radius + o.Radius + (2 * margin))).ToArray();
        var entered = new List<MapPosition>();
        var left = new List<MapPosition>();
        var done = new HashSet<MapPosition>();
        foreach (var (center, radius) in changed)
        {
            foreach (var position in Circle(center, radius + margin))
            {
                if (!done.Add(position))
                {
                    continue;
                }

                var was = previous!.Contains(position);
                var inSight = false;
                foreach (var (at, reach) in near)
                {
                    if (IsWithinRadius(at, position, reach) || (was && IsWithinRadius(at, position, reach + margin)))
                    {
                        inSight = true;
                        break;
                    }
                }

                if (inSight && !was)
                {
                    previous.Add(position);
                    entered.Add(position);
                }
                else if (!inSight && was)
                {
                    previous.Remove(position);
                    left.Add(position);
                }
            }
        }

        return (entered, left);
    }

    private (IReadOnlyList<MapPosition> Entered, IReadOnlyList<MapPosition> Left) UpdateAllSight(
        PlayerId player, HashSet<MapPosition> previous, (MapPosition Center, int Radius)[] observers, int margin)
    {
        var current = new HashSet<MapPosition>();
        foreach (var (center, radius) in observers)
        {
            foreach (var position in Circle(center, radius + margin))
            {
                if (previous.Contains(position) || IsWithinRadius(center, position, radius))
                {
                    current.Add(position);
                }
            }
        }

        _inSight[player] = current;
        return (current.Where(p => !previous.Contains(p)).ToList(), previous.Where(p => !current.Contains(p)).ToList());
    }

    /// <summary>The observers in one list and not in the other (each as often as it is missing there), from both lists.</summary>
    private static List<(MapPosition Center, int Radius)> Changed((MapPosition Center, int Radius)[] before, (MapPosition Center, int Radius)[] after)
    {
        var matched = new bool[before.Length];
        var changed = new List<(MapPosition Center, int Radius)>();
        foreach (var observer in after)
        {
            var at = -1;
            for (var i = 0; i < before.Length && at < 0; i++)
            {
                if (!matched[i] && before[i] == observer)
                {
                    at = i;
                }
            }

            if (at < 0)
            {
                changed.Add(observer);
            }
            else
            {
                matched[at] = true;
            }
        }

        for (var i = 0; i < before.Length; i++)
        {
            if (!matched[i])
            {
                changed.Add(before[i]);
            }
        }

        return changed;
    }

    /// <summary>About how many tiles the observers' circles of sight radius + margin cover: their squares, overlaps counted each time.</summary>
    private static long Area(IEnumerable<(MapPosition Center, int Radius)> observers, int margin) =>
        observers.Sum(o => (long)((2 * (o.Radius + margin)) + 1) * ((2 * (o.Radius + margin)) + 1));

    private IEnumerable<MapPosition> Circle(MapPosition center, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var position = center.Offset(dx, dy);
                if (map.Contains(position) && IsWithinRadius(center, position, radius))
                {
                    yield return position;
                }
            }
        }
    }

    /// <summary>Every tile <paramref name="player"/> has explored (a LAN host sends them to the player's computer, ADR-193).</summary>
    internal IEnumerable<MapPosition> ExploredOf(PlayerId player)
    {
        if (!_explored.TryGetValue(player, out var levels))
        {
            yield break;
        }

        for (var level = 0; level < map.Levels; level++)
        {
            for (var index = 0; index < levels[level].Length; index++)
            {
                if (levels[level][index])
                {
                    yield return new MapPosition(index % map.Width, index / map.Width, level);
                }
            }
        }
    }

    /// <summary>The tiles <paramref name="player"/> has in sight (meaningful under refog; see <see cref="IsInSight"/>).</summary>
    internal IReadOnlyCollection<MapPosition> SightOf(PlayerId player) => _inSight.GetValueOrDefault(player) ?? [];

    /// <summary>A LAN client (ADR-193): tiles the host says the player has explored.</summary>
    internal void MarkExplored(PlayerId player, IEnumerable<MapPosition> positions)
    {
        var explored = ExploredLayer(player);
        foreach (var position in positions.Where(map.Contains))
        {
            explored[position.Level][Index(position)] = true;
        }
    }

    /// <summary>A LAN client (ADR-193): the tiles the host says the player has in sight now.</summary>
    internal void SetSight(PlayerId player, IEnumerable<MapPosition> positions)
    {
        _inSight[player] = [.. positions];
        _seenFrom.Remove(player);
    }

    private bool[][] ExploredLayer(PlayerId player)
    {
        if (!_explored.TryGetValue(player, out var levels))
        {
            levels = new bool[map.Levels][];
            for (var level = 0; level < map.Levels; level++)
            {
                levels[level] = new bool[map.Width * map.Height];
            }

            _explored[player] = levels;
        }

        return levels;
    }

    private int Index(MapPosition position) => (position.Y * map.Width) + position.X;
}
