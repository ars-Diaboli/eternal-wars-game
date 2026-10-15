using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Footprint-aware geometry queries on a battle: where a unit can go, whom it can hit, what blocks a shot.
/// Pure functions of <see cref="BattleState"/>; used by the rules, the battle AI and views alike.
/// </summary>
/// <remarks>
/// Movement is 8-way, one cell per step, up to the creature's Speed. A footprint may only stand where all of
/// its cells are inside the field and free of obstacles and other units (clearance, checked in O(1) with a
/// prefix-sum table). A diagonal step needs at least one of the two orthogonal intermediate placements to be
/// free, so units cannot squeeze through corners. Flyers may land on any free placement within their speed.
/// </remarks>
public static class BattleGrid
{
    private static readonly (int Dx, int Dy)[] Steps = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];

    /// <summary>Every placement the unit can move to this turn (top-left cell → steps), including where it stands (0).</summary>
    public static IReadOnlyDictionary<CellPosition, int> Reachable(BattleState battle, BattleUnit unit)
    {
        var clearance = new Clearance(battle, unit);
        var size = unit.Size;
        var speed = unit.Creature.Speed;
        var result = new Dictionary<CellPosition, int> { [unit.Position] = 0 };

        if (unit.Creature.IsFlying)
        {
            for (var y = unit.Position.Y - speed; y <= unit.Position.Y + speed; y++)
            {
                for (var x = unit.Position.X - speed; x <= unit.Position.X + speed; x++)
                {
                    var p = new CellPosition(x, y);
                    if (clearance.IsFree(p, size))
                    {
                        result.TryAdd(p, Math.Max(Math.Abs(x - unit.Position.X), Math.Abs(y - unit.Position.Y)));
                    }
                }
            }

            return result;
        }

        var frontier = new Queue<CellPosition>();
        frontier.Enqueue(unit.Position);
        while (frontier.TryDequeue(out var current))
        {
            var steps = result[current];
            if (steps == speed)
            {
                continue;
            }

            foreach (var (dx, dy) in Steps)
            {
                var next = current.Offset(dx, dy);
                if (result.ContainsKey(next) || !clearance.IsFree(next, size))
                {
                    continue;
                }

                if (dx != 0 && dy != 0 && !clearance.IsFree(current.Offset(dx, 0), size) && !clearance.IsFree(current.Offset(0, dy), size))
                {
                    continue;
                }

                result[next] = steps + 1;
                frontier.Enqueue(next);
            }
        }

        return result;
    }

    /// <summary>
    /// Placements from which <paramref name="unit"/> can melee <paramref name="target"/> this turn, closest first
    /// (then top-to-bottom, left-to-right, so the order is deterministic).
    /// </summary>
    public static IReadOnlyList<CellPosition> AttackPositions(BattleState battle, BattleUnit unit, BattleUnit target) =>
        Reachable(battle, unit)
            .Where(r => new CellRect(r.Key, unit.Size).IsAdjacentTo(target.Rect))
            .OrderBy(r => r.Value).ThenBy(r => r.Key.Y).ThenBy(r => r.Key.X)
            .Select(r => r.Key)
            .ToList();

    public static IEnumerable<BattleUnit> AdjacentEnemies(BattleState battle, BattleUnit unit) =>
        battle.LivingUnits.Where(u => u.Side != unit.Side && u.Rect.IsAdjacentTo(unit.Rect));

    /// <summary>Engagement hook: other living units of <paramref name="side"/> touching <paramref name="target"/>.</summary>
    public static int EngagingUnits(BattleState battle, BattleUnit target, BattleSide side, BattleUnit? except = null) =>
        battle.LivingUnits.Count(u => u.Side == side && u != except && u.Rect.IsAdjacentTo(target.Rect));

    /// <summary>Whether a shooter may fire at all: ammunition left and no enemy adjacent.</summary>
    public static bool CanShootNow(BattleState battle, BattleUnit unit) =>
        unit.Creature.IsRanged && unit.ShotsLeft > 0 && !AdjacentEnemies(battle, unit).Any();

    /// <summary>Whether <paramref name="unit"/> can shoot <paramref name="target"/>; <paramref name="coverEighths"/> is the cover on the line of fire.</summary>
    public static bool CanShoot(BattleState battle, BattleUnit unit, BattleUnit target, out int coverEighths)
    {
        coverEighths = 0;
        if (!target.IsAlive || target.Side == unit.Side || !CanShootNow(battle, unit))
        {
            return false;
        }

        coverEighths = CoverEighths(battle, unit, target);
        return coverEighths <= BattleState.MaxCoverEighths;
    }

    /// <summary>
    /// Sum of cover from every unit (either side) and obstacle the line of fire passes through, each counted once
    /// (ADR-035). The line runs from the centre of the shooter's footprint to the centre of the target's.
    /// </summary>
    public static int CoverEighths(BattleState battle, BattleUnit shooter, BattleUnit target)
    {
        var units = new HashSet<BattleUnit>();
        var obstacles = new HashSet<BattleObstacle>();
        foreach (var cell in LineOfFireCells(shooter.Rect, target.Rect))
        {
            if (battle.UnitAt(cell) is { } unit && unit != shooter && unit != target)
            {
                units.Add(unit);
            }

            foreach (var obstacle in battle.Obstacles.Where(o => o.Rect.Contains(cell)))
            {
                obstacles.Add(obstacle);
            }
        }

        return units.Sum(u => u.Creature.Size.CoverEighths()) + obstacles.Sum(o => o.Class.CoverEighths());
    }

    /// <summary>
    /// Supercover of the segment between the two footprint centres: every cell the segment passes through or
    /// touches (a line through a grid corner also counts the two cells beside the corner). Integer maths only.
    /// </summary>
    public static IEnumerable<CellPosition> LineOfFireCells(CellRect from, CellRect to)
    {
        var (ax, ay) = from.Center2;
        var (bx, by) = to.Center2;
        var (minX, maxX) = (Math.Min(ax, bx), Math.Max(ax, bx));
        var (minY, maxY) = (Math.Min(ay, by), Math.Max(ay, by));

        for (var cy = (minY / 2) - 1; cy <= (maxY / 2) + 1; cy++)
        {
            for (var cx = (minX / 2) - 1; cx <= (maxX / 2) + 1; cx++)
            {
                // Closed square [2cx, 2cx+2] × [2cy, 2cy+2] in doubled coordinates.
                if (2 * cx > maxX || (2 * cx) + 2 < minX || 2 * cy > maxY || (2 * cy) + 2 < minY)
                {
                    continue;
                }

                var positive = 0;
                var negative = 0;
                foreach (var (px, py) in new[] { (2 * cx, 2 * cy), ((2 * cx) + 2, 2 * cy), (2 * cx, (2 * cy) + 2), ((2 * cx) + 2, (2 * cy) + 2) })
                {
                    var side = ((long)(bx - ax) * (py - ay)) - ((long)(by - ay) * (px - ax));
                    if (side > 0)
                    {
                        positive++;
                    }
                    else if (side < 0)
                    {
                        negative++;
                    }
                }

                if (positive < 4 && negative < 4)
                {
                    yield return new CellPosition(cx, cy);
                }
            }
        }
    }

    /// <summary>Free-cell lookup for placing one unit's footprint: obstacles and every other living unit block.</summary>
    private sealed class Clearance
    {
        private readonly int[,] _blockedPrefix;
        private readonly BattleState _battle;

        public Clearance(BattleState battle, BattleUnit mover)
        {
            _battle = battle;
            var blocked = new bool[battle.Width, BattleState.Height];
            foreach (var rect in battle.Obstacles.Select(o => o.Rect)
                         .Concat(battle.LivingUnits.Where(u => u != mover).Select(u => u.Rect)))
            {
                foreach (var cell in rect.Cells().Where(c => c.X >= 0 && c.Y >= 0 && c.X < battle.Width && c.Y < BattleState.Height))
                {
                    blocked[cell.X, cell.Y] = true;
                }
            }

            _blockedPrefix = new int[battle.Width + 1, BattleState.Height + 1];
            for (var x = 0; x < battle.Width; x++)
            {
                for (var y = 0; y < BattleState.Height; y++)
                {
                    _blockedPrefix[x + 1, y + 1] = (blocked[x, y] ? 1 : 0) + _blockedPrefix[x, y + 1] + _blockedPrefix[x + 1, y] - _blockedPrefix[x, y];
                }
            }
        }

        public bool IsFree(CellPosition topLeft, int size)
        {
            var rect = new CellRect(topLeft, size);
            if (!_battle.IsInside(rect))
            {
                return false;
            }

            var p = _blockedPrefix;
            return p[rect.Right, rect.Bottom] - p[rect.X, rect.Bottom] - p[rect.Right, rect.Y] + p[rect.X, rect.Y] == 0;
        }
    }
}
