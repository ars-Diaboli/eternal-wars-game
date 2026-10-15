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
    internal (IReadOnlyList<MapPosition> Entered, IReadOnlyList<MapPosition> Left) UpdateSight(
        PlayerId player, IReadOnlyCollection<(MapPosition Center, int Radius)> observers, int margin)
    {
        var previous = _inSight.GetValueOrDefault(player) ?? [];
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
