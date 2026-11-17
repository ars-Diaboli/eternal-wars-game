using System.Runtime.CompilerServices;
using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Footprint-aware geometry queries on a battle: where a unit can go, whom it can hit, what blocks a shot.
/// Pure functions of <see cref="BattleState"/>; used by the rules, the battle AI and views alike.
/// </summary>
/// <remarks>
/// Movement is measured as a circle (ADR-089, CMB-9): a unit reaches a placement whose path from where it stands is at most its Speed + 0.25
/// cells long (<see cref="ReachSlack"/>), the path running at any angle (the straight-line distance on an open field, the way around obstacles
/// otherwise). Lengths are in thousandths of a cell (<see cref="Unit"/>), integers, so every machine computes the same. A footprint may only
/// stand where all of its cells are inside the field and free of obstacles and other units (clearance, checked in O(1) with a prefix-sum
/// table). A diagonal step needs at least one of the two orthogonal intermediate placements to be free, so units cannot squeeze through
/// corners. Flyers and teleporters land on any free placement within the same straight-line distance.
/// <para>
/// A unit's reach is searched once and kept beside its battle until something the search reads changes (<see cref="ReachKey"/>); the
/// dictionaries handed out are shared, never changed.
/// </para>
/// </remarks>
public static class BattleGrid
{
    /// <summary>Lengths on the battlefield are in thousandths of a cell.</summary>
    public const int Unit = 1000;

    /// <summary>What a unit may move beyond its Speed (0.25 of a cell), so the reach is a round circle rather than one with spikes (CMB-9).</summary>
    public const int ReachSlack = 250;

    private static readonly (int Dx, int Dy)[] Steps = [(0, -1), (1, -1), (1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1)];

    /// <summary>The straight-line length of (<paramref name="dx"/>, <paramref name="dy"/>) cells in <see cref="Unit"/>s, rounded down.</summary>
    public static int Length(int dx, int dy)
    {
        var (ax, ay) = (Math.Abs(dx), Math.Abs(dy));
        return ax < LengthTableSide && ay < LengthTableSide ? LengthTable[(ay * LengthTableSide) + ax] : SquareRootLength(dx, dy);
    }

    /// <summary>The lengths of the offsets within a siege field's width, worked out once (the searches ask for them on every step).</summary>
    private const int LengthTableSide = BattleState.SiegeWidth + 1;

    private static readonly int[] LengthTable = [.. Enumerable.Range(0, LengthTableSide * LengthTableSide)
        .Select(i => SquareRootLength(i % LengthTableSide, i / LengthTableSide))];

    private static int SquareRootLength(int dx, int dy)
    {
        var squared = (((long)dx * dx) + ((long)dy * dy)) * Unit * Unit;
        var root = (long)Math.Sqrt(squared);
        while (root * root > squared)
        {
            root--;
        }

        while ((root + 1) * (root + 1) <= squared)
        {
            root++;
        }

        return (int)root;
    }

    /// <summary>How far a unit may move this turn, in <see cref="Unit"/>s: its Speed plus <see cref="ReachSlack"/>.</summary>
    public static int MoveLimit(BattleState battle, BattleUnit unit) => (battle.SpeedOf(unit) * Unit) + ReachSlack;

    /// <summary>
    /// Every placement the unit can move to this turn (top-left cell → path length in <see cref="Unit"/>s), including where it stands (0).
    /// Walkers: the shortest path at any angle (Theta*: a path may go straight on from an earlier placement when the line from there is clear).
    /// Flyers and teleporters: the straight-line distance.
    /// </summary>
    public static IReadOnlyDictionary<CellPosition, int> Reachable(BattleState battle, BattleUnit unit) => Search(battle, unit).Reach;

    /// <summary>
    /// The placements the unit passes on its way to <paramref name="to"/>, from where it stands to <paramref name="to"/> (ADR-149): for a walker
    /// every placement its top-left cell goes through along the path <see cref="Reachable"/> found; for a flyer or teleporter only the two ends.
    /// Empty when it cannot get there.
    /// </summary>
    public static IReadOnlyList<CellPosition> Path(BattleState battle, BattleUnit unit, CellPosition to)
    {
        var (reach, parent) = Search(battle, unit);
        if (!reach.ContainsKey(to))
        {
            return [];
        }

        if (unit.MovesFreely || to == unit.Position)
        {
            return to == unit.Position ? [to] : [unit.Position, to];
        }

        var corners = new List<CellPosition> { to };
        while (corners[^1] != unit.Position)
        {
            corners.Add(parent[corners[^1]]);
        }

        corners.Reverse();
        var path = new List<CellPosition> { unit.Position };
        for (var i = 1; i < corners.Count; i++)
        {
            path.AddRange(LinePlacements(corners[i - 1], corners[i]));
        }

        return path;
    }

    /// <summary>The placements a footprint's top-left cell steps through going straight from one placement to another (not the first), as in <c>IsClearLine</c>.</summary>
    private static IEnumerable<CellPosition> LinePlacements(CellPosition from, CellPosition to)
    {
        int nx = Math.Abs(to.X - from.X), ny = Math.Abs(to.Y - from.Y);
        int sx = Math.Sign(to.X - from.X), sy = Math.Sign(to.Y - from.Y);
        var (x, y) = (from.X, from.Y);
        for (int ix = 0, iy = 0; ix < nx || iy < ny;)
        {
            var decision = ((1 + (2L * ix)) * ny) - ((1 + (2L * iy)) * nx);
            if (decision == 0)
            {
                (x, y, ix, iy) = (x + sx, y + sy, ix + 1, iy + 1);
            }
            else if (decision < 0)
            {
                (x, ix) = (x + sx, ix + 1);
            }
            else
            {
                (y, iy) = (y + sy, iy + 1);
            }

            yield return new CellPosition(x, y);
        }
    }

    /// <summary>The battles' searched reaches, beside them (not in them: the battle is saved and sent whole); each goes with its battle.</summary>
    private static readonly ConditionalWeakTable<BattleState, ReachCache> Reaches = [];

    /// <summary>The unit's reach and each placement's parent on its path, from the battle's cache when nothing the search reads has changed.</summary>
    private static (Dictionary<CellPosition, int> Reach, Dictionary<CellPosition, CellPosition> Parent) Search(BattleState battle, BattleUnit unit)
    {
        var cache = Reaches.GetValue(battle, _ => new ReachCache());
        var key = cache.Scratch;
        key.Clear();
        ReachKey(battle, unit, key);
        if (cache.ByUnit.TryGetValue(unit, out var known) && known.Siege == battle.Siege && key.SequenceEqual(known.Key))
        {
            return (known.Reach, known.Parent);
        }


        var reach = Search(battle, unit, out var parent);
        cache.ByUnit[unit] = new ReachEntry([.. key], battle.Siege, reach, parent);
        return (reach, parent);
    }

    /// <summary>
    /// Everything <see cref="Search(BattleState, BattleUnit, out Dictionary{CellPosition, CellPosition})"/> reads, as numbers: the field's
    /// width; the unit's place, footprint, side, move limit and whether it flies, is held or stands in the field; whether the moat stops it;
    /// the standing siege structures (the siege itself is compared by reference: its moat never changes); the obstacles; the other living
    /// units' footprints; and the zones that block it. Two equal keys give the same search, so a kept one can be handed out again.
    /// </summary>
    private static void ReachKey(BattleState battle, BattleUnit unit, List<int> key)
    {
        key.AddRange([battle.Width, unit.Position.X, unit.Position.Y, unit.Size, unit.Width, unit.Height, (int)unit.Side, MoveLimit(battle, unit)]);
        key.Add((unit.MovesFreely ? 1 : 0) | (unit.IsFixed ? 2 : 0) | (battle.Units.Contains(unit) ? 4 : 0)
                | (SiegeRules.FearsMoat(battle, unit.Side) && battle.Siege!.InMoat(unit.Rect) ? 8 : 0));
        if (battle.Siege is { } siege)
        {
            foreach (var structure in siege.Structures)
            {
                key.Add(structure.IsStanding ? structure.Id : -structure.Id - 1);
            }
        }

        key.Add(battle.Obstacles.Count);
        foreach (var obstacle in battle.Obstacles)
        {
            var rect = obstacle.Rect;
            key.AddRange([rect.X, rect.Y, rect.Width, rect.Height]);
        }

        foreach (var other in battle.Units)
        {
            if (other != unit && other.IsAlive)
            {
                key.AddRange([other.Position.X, other.Position.Y, other.Width, other.Height]);
            }
        }

        key.Add(-1);
        foreach (var zone in battle.Zones)
        {
            if (BattleZones.Blocks(zone, unit))
            {
                key.AddRange([zone.Rect.X, zone.Rect.Y, zone.Rect.Width, zone.Rect.Height]);
            }
        }
    }

    private static Dictionary<CellPosition, int> Search(BattleState battle, BattleUnit unit, out Dictionary<CellPosition, CellPosition> parent)
    {
        parent = new Dictionary<CellPosition, CellPosition> { [unit.Position] = unit.Position };
        var clearance = new Clearance(battle, unit);
        var size = unit.Size;
        var limit = MoveLimit(battle, unit);
        var speed = limit / Unit;
        var start = unit.Position;
        var result = new Dictionary<CellPosition, int> { [start] = 0 };
        if (unit.IsFixed)
        {
            return result; // the keep's stack stays in the keep, rooted stacks where they were called (ADR-120)
        }

        if (unit.MovesFreely)
        {
            for (var y = start.Y - speed; y <= start.Y + speed; y++)
            {
                for (var x = start.X - speed; x <= start.X + speed; x++)
                {
                    var p = new CellPosition(x, y);
                    var length = Length(x - start.X, y - start.Y);
                    if (length <= limit && clearance.IsFree(p, size))
                    {
                        result.TryAdd(p, length);
                    }
                }
            }

            return result;
        }

        // In a siege an attacker that walks into the moat stops there (ADR-106); one already in it moves on freely.
        var siege = battle.Siege;
        var moatStops = SiegeRules.FearsMoat(battle, unit.Side) && !siege!.InMoat(unit.Rect);
        bool StopsAt(CellPosition p) => moatStops && siege!.InMoat(unit.RectAt(p));
        Func<CellPosition, bool>? stops = moatStops ? StopsAt : null;

        // Every placement a walker reaches is inside the field (it must be free there), so the search keeps its state in arrays over the field's
        // cells. The placements are handed out in the order they were first reached, as the result has always been.
        var width = battle.Width;
        var cells = width * BattleState.Height;
        var lengths = new int[cells];
        var parents = new int[cells];
        var done = new bool[cells];
        Array.Fill(lengths, -1);
        var order = new List<int>();
        int Index(CellPosition p) => (p.Y * width) + p.X;
        CellPosition At(int index) => new(index % width, index / width);

        var origin = Index(start);
        (lengths[origin], parents[origin]) = (0, origin);
        var frontier = new PriorityQueue<int, long>();
        frontier.Enqueue(origin, Priority(0, start));
        while (frontier.TryDequeue(out var currentIndex, out var priority))
        {
            var current = At(currentIndex);
            if (priority >> 16 != lengths[currentIndex] || done[currentIndex] || StopsAt(current))
            {
                continue;
            }

            done[currentIndex] = true;
            foreach (var (dx, dy) in Steps)
            {
                var next = current.Offset(dx, dy);
                if (!clearance.IsFree(next, size) || done[Index(next)])
                {
                    continue;
                }

                if (dx != 0 && dy != 0 && !clearance.IsFree(current.Offset(dx, 0), size) && !clearance.IsFree(current.Offset(0, dy), size))
                {
                    continue;
                }

                var fromIndex = parents[currentIndex];
                var from = At(fromIndex);
                if (fromIndex == currentIndex || !IsClearLine(clearance, from, next, size, stops))
                {
                    (from, fromIndex) = (current, currentIndex);
                }

                var nextIndex = Index(next);
                var length = lengths[fromIndex] + Length(next.X - from.X, next.Y - from.Y);
                if (length > limit || (lengths[nextIndex] >= 0 && lengths[nextIndex] <= length))
                {
                    continue;
                }

                if (lengths[nextIndex] < 0)
                {
                    order.Add(nextIndex);
                }

                (lengths[nextIndex], parents[nextIndex]) = (length, fromIndex);
                frontier.Enqueue(nextIndex, Priority(length, next));
            }
        }

        foreach (var index in order)
        {
            var placement = At(index);
            result[placement] = lengths[index];
            parent[placement] = At(parents[index]);
        }

        return result;

        // Shortest first, then top to bottom, left to right (a placement's cells are within the field, below 256).
        static long Priority(int length, CellPosition p) => ((long)length << 16) | ((long)p.Y << 8) | (uint)p.X;
    }

    /// <summary>
    /// Placements from which <paramref name="unit"/> can melee <paramref name="target"/> this turn, closest first
    /// (then top-to-bottom, left-to-right, so the order is deterministic).
    /// </summary>
    public static IReadOnlyList<CellPosition> AttackPositions(BattleState battle, BattleUnit unit, BattleUnit target) =>
        CanMelee(unit, target) ? AttackPositions(Reachable(battle, unit), unit, target) : [];

    /// <summary>
    /// <see cref="AttackPositions(BattleState, BattleUnit, BattleUnit)"/> from the unit's <paramref name="reach"/> (<see cref="Reachable"/>), for
    /// weighing many targets with one search.
    /// </summary>
    public static IReadOnlyList<CellPosition> AttackPositions(IReadOnlyDictionary<CellPosition, int> reach, BattleUnit unit, BattleUnit target)
    {
        if (!CanMelee(unit, target))
        {
            return [];
        }

        var rect = target.Rect;
        List<(CellPosition At, int Length)>? spots = null;
        foreach (var (at, length) in reach)
        {
            if (unit.RectAt(at).IsAdjacentTo(rect))
            {
                (spots ??= []).Add((at, length));
            }
        }

        if (spots is null)
        {
            return [];
        }

        // Closest first, then top-to-bottom, left-to-right: placements are distinct, so the order is total.
        spots.Sort((a, b) => a.Length != b.Length ? a.Length.CompareTo(b.Length) : a.At.Y != b.At.Y ? a.At.Y.CompareTo(b.At.Y) : a.At.X.CompareTo(b.At.X));
        return spots.ConvertAll(s => s.At);
    }

    private static bool CanMelee(BattleUnit unit, BattleUnit target) =>
        !(unit.IsMachine || target.InKeep || unit.Has(Magic.SpellStatus.NoAttack) || !CanBeAttacked(unit, target, melee: true));

    /// <summary>Placements from which the unit can hit the structure in melee this turn (the gate, ADR-120), closest first.</summary>
    public static IReadOnlyList<CellPosition> StructureAttackPositions(BattleState battle, BattleUnit unit, SiegeStructure structure) =>
        unit.IsMachine || !structure.IsStanding ? [] : Reachable(battle, unit)
            .Where(r => unit.RectAt(r.Key).IsAdjacentTo(structure.Rect))
            .OrderBy(r => r.Value).ThenBy(r => r.Key.Y).ThenBy(r => r.Key.X)
            .Select(r => r.Key)
            .ToList();

    /// <summary>Whether the unit's footprint fits at <paramref name="topLeft"/>: inside the field, no obstacle, no other living unit.</summary>
    public static bool IsFree(BattleState battle, BattleUnit unit, CellPosition topLeft) => new Clearance(battle, unit).IsFree(topLeft, unit.Size);

    /// <summary>
    /// <see cref="IsFree"/> for many placements of one unit: the field is read once, when this is called, so the battle must not change while
    /// the answer is used.
    /// </summary>
    public static Func<CellPosition, bool> FreePlacements(BattleState battle, BattleUnit unit)
    {
        var clearance = new Clearance(battle, unit);
        var size = unit.Size;
        return topLeft => clearance.IsFree(topLeft, size);
    }

    public static IEnumerable<BattleUnit> AdjacentEnemies(BattleState battle, BattleUnit unit) =>
        battle.LivingUnits.Where(u => IsFoe(unit, u) && u.Rect.IsAdjacentTo(unit.Rect));

    /// <summary>Whether the unit fights the other: a stack of the other side, or for a frenzied stack (ADR-150) every other stack.</summary>
    public static bool IsFoe(BattleUnit unit, BattleUnit other) => other != unit && (other.Side != unit.Side || unit.Has(Magic.SpellStatus.Frenzied));

    /// <summary>Engagement hook: other living units of <paramref name="side"/> touching <paramref name="target"/>.</summary>
    public static int EngagingUnits(BattleState battle, BattleUnit target, BattleSide side, BattleUnit? except = null) =>
        battle.LivingUnits.Count(u => u.Side == side && u != except && u.Rect.IsAdjacentTo(target.Rect));

    /// <summary>Whether a shooter may fire at all: ammunition left and no enemy adjacent.</summary>
    public static bool CanShootNow(BattleState battle, BattleUnit unit) =>
        unit.Creature.IsRanged && unit.ShotsLeft > 0 && !unit.Has(Magic.SpellStatus.NoShoot | Magic.SpellStatus.NoAttack) && !AdjacentEnemies(battle, unit).Any();

    /// <summary>
    /// Whether spells let the attacker hit the target (ADR-123): not one in Stasis, not an enemy that is Unseen, not one under Sanctuary in melee.
    /// </summary>
    public static bool CanBeAttacked(BattleUnit attacker, BattleUnit target, bool melee) =>
        !target.Has(Magic.SpellStatus.Stasis)
        && !(target.Side != attacker.Side && target.Has(Magic.SpellStatus.Unseen))
        && !(melee && target.Has(Magic.SpellStatus.Sanctuary));

    /// <summary>Whether <paramref name="unit"/> can shoot <paramref name="target"/>; <paramref name="penaltyEighths"/> is the shot's ranged penalty (<see cref="RangedPenaltyEighths"/>).</summary>
    public static bool CanShoot(BattleState battle, BattleUnit unit, BattleUnit target, out int penaltyEighths)
    {
        penaltyEighths = 0;
        if (!target.IsAlive || !IsFoe(unit, target) || !CanShootNow(battle, unit) || !CanBeAttacked(unit, target, melee: false))
        {
            return false;
        }

        // The siege engine fires at troops only from Advanced Ballistics (ADR-063).
        if (SiegeRules.IsSiegeEngine(battle, unit) && !WarMachines.CanTargetTroops(battle.Participant(unit.Side).Bonus.Ballistics))
        {
            return false;
        }

        // Sanctuary III (ADR-177): an enemy shooter needs a clear line to a screened stack.
        if (target.Has(Magic.SpellStatus.Screened) && target.Side != unit.Side && !IsClearShot(battle, unit, target))
        {
            return false;
        }

        penaltyEighths = RangedPenaltyEighths(battle, unit, target);
        return penaltyEighths <= BattleState.MaxRangedPenaltyEighths && !BattleZones.BlocksShot(battle, unit, target);
    }

    /// <summary>
    /// Whether the line of fire from the shooter to the target crosses no other living stack, obstacle or standing siege structure on the field
    /// (<see cref="LineOfFireCells"/>; Sanctuary III, ADR-177).
    /// </summary>
    public static bool IsClearShot(BattleState battle, BattleUnit shooter, BattleUnit target) =>
        LineOfFireCells(shooter.Rect, target.Rect).All(c =>
            shooter.Rect.Contains(c) || target.Rect.Contains(c)
            || (battle.UnitAt(c) is null && !battle.Obstacles.Any(o => o.Rect.Contains(c))
                && !(battle.Siege?.Structures.Any(s => s.IsStanding && s.OnField && s.Rect.Contains(c)) ?? false)));

    /// <summary>
    /// A shot's ranged penalty in eighths (ADR-070, ADR-072, CMB-6): the distance penalty and the cover add up, the shooter's Marksmanship is
    /// taken off (not below 0); above 7/8 the target cannot be shot. War machines have no distance penalty.
    /// </summary>
    public static int RangedPenaltyEighths(BattleState battle, BattleUnit shooter, BattleUnit target)
    {
        if (shooter.Has(Magic.SpellStatus.TrueShot))
        {
            return 0; // Hunter's Sight (ADR-123): no distance penalty, no cover
        }

        var distance = shooter.IsMachine ? 0 : DistancePenaltyEighths(shooter.Rect.DistanceTo(target.Rect));
        return Math.Max(0, distance + CoverEighths(battle, shooter, target) - battle.Participant(shooter.Side).Bonus.MarksmanshipEighths);
    }

    /// <summary>
    /// The distance penalty: 1/8 per whole 10 cells between the footprints (0–9 none, 10–19 1/8, …), the distance measured like movement, as a
    /// straight line (<see cref="CellRect.DistanceTo"/>, in <see cref="Unit"/>s; CMB-9).
    /// </summary>
    public static int DistancePenaltyEighths(int distanceBetween) => Math.Max(0, distanceBetween) / (BattleState.DistancePenaltyCells * Unit);

    /// <summary>
    /// Sum of cover from every unit (either side) and obstacle the line of fire passes through, each counted once
    /// (ADR-035). The line runs from the centre of the shooter's footprint to the centre of the target's. In a siege an attacker's shot across a
    /// standing wall section or the gate takes the wall's 1/2 once (ADR-106; the defenders shoot out freely, assumed).
    /// </summary>
    public static int CoverEighths(BattleState battle, BattleUnit shooter, BattleUnit target)
    {
        var units = new HashSet<BattleUnit>();
        var obstacles = new HashSet<BattleObstacle>();
        var wall = false;
        var walls = shooter.Side == BattleSide.Attacker && battle.Siege is { } siege
            ? siege.Structures.Where(s => s.IsStanding && s.OnField).ToList()
            : [];
        foreach (var cell in LineOfFireCells(shooter.Rect, target.Rect))
        {
            wall |= walls.Any(s => s.Rect.Contains(cell));
            if (battle.UnitAt(cell) is { } unit && unit != shooter && unit != target)
            {
                units.Add(unit);
            }

            foreach (var obstacle in battle.Obstacles.Where(o => o.Rect.Contains(cell)))
            {
                obstacles.Add(obstacle);
            }
        }

        // The keep's stack is behind the keep's walls (ADR-120): the same 1/2 as a wall, once.
        wall |= target.InKeep;
        return units.Sum(u => u.Creature.Size.CoverEighths()) + obstacles.Sum(o => o.Class.CoverEighths()) + (wall ? ObstacleClass.Wall.CoverEighths() : 0);
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

    /// <summary>
    /// Whether the footprint can slide straight from <paramref name="from"/> to <paramref name="to"/>: every placement its top-left cell passes
    /// through is free. Where the line crosses a grid corner exactly, one of the two placements beside the corner is enough (as for a diagonal
    /// step). A placement where the mover would have to stop (<paramref name="stops"/>: the moat) ends the line unless it is <paramref name="to"/>.
    /// </summary>
    private static bool IsClearLine(Clearance clearance, CellPosition from, CellPosition to, int size, Func<CellPosition, bool>? stops = null)
    {
        int nx = Math.Abs(to.X - from.X), ny = Math.Abs(to.Y - from.Y);
        int sx = Math.Sign(to.X - from.X), sy = Math.Sign(to.Y - from.Y);
        var (x, y) = (from.X, from.Y);
        for (int ix = 0, iy = 0; ix < nx || iy < ny;)
        {
            var decision = ((1 + (2L * ix)) * ny) - ((1 + (2L * iy)) * nx);
            if (decision == 0)
            {
                if (!Passable(clearance, new CellPosition(x + sx, y), to, size, stops) && !Passable(clearance, new CellPosition(x, y + sy), to, size, stops))
                {
                    return false;
                }

                x += sx;
                y += sy;
                ix++;
                iy++;
            }
            else if (decision < 0)
            {
                x += sx;
                ix++;
            }
            else
            {
                y += sy;
                iy++;
            }

            if (!Passable(clearance, new CellPosition(x, y), to, size, stops))
            {
                return false;
            }
        }

        return true;
    }

    private static bool Passable(Clearance clearance, CellPosition p, CellPosition to, int size, Func<CellPosition, bool>? stops) =>
        clearance.IsFree(p, size) && (stops is null || p == to || !stops(p));

    /// <summary>
    /// Steps (8-way, one per placement) from each placement to the nearest one next to any of <paramref name="targets"/>, around obstacles and a
    /// siege's walls but through units (they move): the battle AI's way to a breach (ADR-106). Placements that cannot get there are missing.
    /// </summary>
    public static IReadOnlyDictionary<CellPosition, int> ApproachSteps(BattleState battle, BattleUnit unit, IEnumerable<BattleUnit> targets)
    {
        var clearance = new Clearance(battle, unit, ignoreUnits: true);
        var size = unit.Size;
        var rects = targets.Select(t => t.Rect).ToList();
        var steps = new Dictionary<CellPosition, int>();
        var frontier = new Queue<CellPosition>();
        for (var y = 0; y <= BattleState.Height - size; y++)
        {
            for (var x = 0; x <= battle.Width - size; x++)
            {
                var p = new CellPosition(x, y);
                if (clearance.IsFree(p, size) && rects.Any(r => unit.RectAt(p).IsAdjacentTo(r)))
                {
                    steps[p] = 0;
                    frontier.Enqueue(p);
                }
            }
        }

        while (frontier.TryDequeue(out var current))
        {
            foreach (var (dx, dy) in Steps)
            {
                var next = current.Offset(dx, dy);
                if (steps.ContainsKey(next) || !clearance.IsFree(next, size)
                    || (dx != 0 && dy != 0 && !clearance.IsFree(current.Offset(dx, 0), size) && !clearance.IsFree(current.Offset(0, dy), size)))
                {
                    continue;
                }

                steps[next] = steps[current] + 1;
                frontier.Enqueue(next);
            }
        }

        return steps;
    }

    private sealed class ReachCache
    {
        public Dictionary<BattleUnit, ReachEntry> ByUnit { get; } = new(ReferenceEqualityComparer.Instance);

        /// <summary>The key being built, so a hit allocates nothing.</summary>
        public List<int> Scratch { get; } = [];
    }

    private sealed record ReachEntry(int[] Key, SiegeState? Siege, Dictionary<CellPosition, int> Reach, Dictionary<CellPosition, CellPosition> Parent);

    /// <summary>
    /// Free-cell lookup for placing one unit's footprint: obstacles, a siege's standing walls (the gate only for attackers) and every other living
    /// unit block.
    /// </summary>
    private sealed class Clearance
    {
        private readonly int[] _blockedPrefix; // (x, y) at y * (width + 1) + x: the blocked cells left of x and above y
        private readonly int _stride;
        private readonly int _width;

        public Clearance(BattleState battle, BattleUnit mover, bool ignoreUnits = false)
        {
            (_width, _stride) = (battle.Width, battle.Width + 1);
            var blocked = new bool[battle.Width, BattleState.Height];
            var walls = battle.Siege?.Structures
                .Where(s => s.IsStanding && s.OnField && (s.Kind == SiegeStructureKind.Wall || mover.Side == BattleSide.Attacker))
                .Select(s => s.Rect) ?? [];
            // Blocking zones (ADR-149) keep walkers out, but a stack already standing in one may step out over its own cells.
            var own = battle.Units.Contains(mover) ? mover.Rect : default;
            foreach (var rect in battle.Obstacles.Select(o => o.Rect).Concat(walls)
                         .Concat(ignoreUnits ? [] : battle.LivingUnits.Where(u => u != mover).Select(u => u.Rect)))
            {
                foreach (var cell in rect.Cells().Where(c => c.X >= 0 && c.Y >= 0 && c.X < battle.Width && c.Y < BattleState.Height))
                {
                    blocked[cell.X, cell.Y] = true;
                }
            }

            foreach (var zone in battle.Zones.Where(z => BattleZones.Blocks(z, mover)))
            {
                foreach (var cell in zone.Rect.Cells().Where(c => !own.Contains(c)))
                {
                    blocked[cell.X, cell.Y] = true;
                }
            }

            var p = _blockedPrefix = new int[_stride * (BattleState.Height + 1)];
            for (var x = 0; x < battle.Width; x++)
            {
                for (var y = 0; y < BattleState.Height; y++)
                {
                    p[((y + 1) * _stride) + x + 1] = (blocked[x, y] ? 1 : 0) + p[((y + 1) * _stride) + x] + p[(y * _stride) + x + 1] - p[(y * _stride) + x];
                }
            }
        }

        public bool IsFree(CellPosition topLeft, int size)
        {
            var (left, top, right, bottom) = (topLeft.X, topLeft.Y, topLeft.X + size, topLeft.Y + size);
            if (left < 0 || top < 0 || right > _width || bottom > BattleState.Height)
            {
                return false; // outside the field (BattleState.IsInside)
            }

            var p = _blockedPrefix;
            return p[(bottom * _stride) + right] - p[(bottom * _stride) + left] - p[(top * _stride) + right] + p[(top * _stride) + left] == 0;
        }
    }
}
