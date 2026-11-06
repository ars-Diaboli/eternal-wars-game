using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// A stack entering a battle; <see cref="SourceSlot"/> maps survivors back to the army (-1 for guards and war machines).
/// <see cref="Machine"/> marks a war machine (ADR-056).
/// </summary>
public sealed record BattleStackSpec(CreatureDefinition Creature, int Count, int SourceSlot, WarMachineKind? Machine = null)
{
    public static BattleStackSpec Of(WarMachineKind machine) => new(WarMachines.Definition(machine), 1, -1, machine);
}

/// <summary>Width choice, deployment and obstacles (ADR-033, ADR-038).</summary>
public static class BattleSetup
{
    /// <summary>A neutral guard is split into at most this many stacks.</summary>
    public const int MaxGuardStacks = 5;

    /// <summary>Widths offered for an effective Tactics level (ADR-069): Advanced 30 / 32, Expert 28–34; below Advanced the default only.</summary>
    public static IReadOnlyList<int> WidthOptions(SkillLevel effectiveTactics) => effectiveTactics switch
    {
        SkillLevel.Advanced => [30, 32],
        >= SkillLevel.Expert => [28, 30, 32, 34],
        _ => [BattleState.DefaultWidth],
    };

    /// <summary>Deployment zone depth in columns from the own edge for a Tactics level (ADR-072): 4 / 5 / 6; none = no deployment.</summary>
    public static int DeploymentColumns(SkillLevel tactics) => tactics switch
    {
        SkillLevel.None => 0,
        SkillLevel.Basic => 4,
        SkillLevel.Advanced => 5,
        _ => 6,
    };

    /// <summary>Why the deployer may not put the unit at <paramref name="to"/>, or null (ADR-072, ADR-073).</summary>
    public static string? DeploymentRefusal(BattleState battle, BattleUnit unit, CellPosition to)
    {
        if (battle.Phase != BattlePhase.Deploying || unit.Side != battle.Deployer)
        {
            return "Only the deploying side places its stacks before the battle.";
        }

        if (unit.IsMachine || !unit.IsAlive)
        {
            return "War machines keep their column.";
        }

        if (unit.InKeep)
        {
            return $"{unit.Creature.Name} stays in the keep.";
        }

        if (!battle.IsInDeploymentZone(unit.RectAt(to)))
        {
            return $"{unit.Creature.Name} must stand within the first {battle.DeploymentColumns} columns.";
        }

        return BattleGrid.IsFree(battle, unit, to) ? null : $"{unit.Creature.Name} does not fit at {to}.";
    }

    /// <summary>
    /// Effective Tactics = own level − enemy level; at most one side ends up above none, and that side chooses.
    /// </summary>
    public static (BattleSide? Chooser, SkillLevel Level) EffectiveTactics(SkillLevel attacker, SkillLevel defender)
    {
        var difference = (int)attacker - (int)defender;
        return difference switch
        {
            > 0 => (BattleSide.Attacker, (SkillLevel)difference),
            < 0 => (BattleSide.Defender, (SkillLevel)(-difference)),
            _ => (null, SkillLevel.None),
        };
    }

    /// <summary>HoMM3-style split of a guard into <c>min(count, 5)</c> stacks of near-equal size, largest first.</summary>
    public static IReadOnlyList<int> SplitGuard(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        var stacks = Math.Min(count, MaxGuardStacks);
        return Enumerable.Range(0, stacks).Select(i => (count / stacks) + (i < count % stacks ? 1 : 0)).ToList();
    }

    internal static void Deploy(BattleState battle, IReadOnlyList<BattleStackSpec> attacker, IReadOnlyList<BattleStackSpec> defender)
    {
        var id = 1;
        // The deployer's zone stays free of obstacles (ADR-072).
        var attackerExtent = Math.Max(DeploySide(battle, BattleSide.Attacker, attacker, ref id), battle.Deployer == BattleSide.Attacker ? battle.DeploymentColumns : 0);
        var defenderExtent = Math.Max(DeploySide(battle, BattleSide.Defender, defender, ref id), battle.Deployer == BattleSide.Defender ? battle.DeploymentColumns : 0);
        // In a siege the obstacles stay in front of the moat; nothing stands behind the walls but the defenders (ADR-106).
        var toX = battle.Siege is null ? battle.Width - defenderExtent - 1 : SiegeRules.WallX - SiegeRules.MoatColumns - 1;
        var cliffs = PlaceCliff(battle, attackerExtent + 1, toX);
        PlaceObstacles(battle, attackerExtent + 1, toX, cliffs ? (1, 3) : (2, 5));
        foreach (var side in new[] { BattleSide.Attacker, BattleSide.Defender })
        {
            SupplyShots(battle, side);
        }

        ManKeep(battle);
    }

    /// <summary>
    /// In a siege with a keep (Citadel) the defenders' strongest shooting stack takes it (ADR-106 g, ADR-120): it stands behind the walls next to
    /// the keep, at the field's back edge by the gate rows, and stays there while the keep stands.
    /// </summary>
    private static void ManKeep(BattleState battle)
    {
        if (battle.Siege is not { KeepStands: true }
            || battle.LivingArmyOf(BattleSide.Defender).Where(u => u.Creature.IsRanged)
                .OrderByDescending(u => (long)u.Count * u.Creature.Value).ThenBy(u => u.Id).FirstOrDefault() is not { } shooter)
        {
            return;
        }

        var spot = Enumerable.Range(SiegeRules.WallX + 1, battle.Width - shooter.Width - SiegeRules.WallX)
            .SelectMany(x => Enumerable.Range(0, BattleState.Height - shooter.Height + 1).Select(y => new CellPosition(x, y)))
            .Where(p => p == shooter.Position || BattleGrid.IsFree(battle, shooter, p))
            .OrderBy(p => battle.Width - shooter.Width - p.X).ThenBy(p => Math.Abs(p.Y - SiegeRules.GateY)).ThenBy(p => p.Y)
            .Select(p => (CellPosition?)p)
            .FirstOrDefault();
        if (spot is { } at)
        {
            shooter.Position = at;
            shooter.InKeep = true;
        }
    }

    /// <summary>The ammo cart's extra shots at battle start; unlimited shots are checked as shots are used (ADR-056).</summary>
    private static void SupplyShots(BattleState battle, BattleSide side)
    {
        if (battle.LivingMachine(side, WarMachineKind.AmmoCart) is null ||
            WarMachines.CartShotBonusPercent(battle.Participant(side).Bonus.AmmoSupply) is not { } percent)
        {
            return;
        }

        foreach (var shooter in battle.LivingArmyOf(side).Where(u => u.Creature.IsRanged))
        {
            shooter.ShotsLeft += ((shooter.Creature.Shots * percent) + 99) / 100;
        }
    }

    /// <summary>
    /// Columns at the side's edge in slot order; each column holds as many stacks as fit in the field's height
    /// with a one-cell gap, spread evenly. Returns how many cells deep the deployment is.
    /// </summary>
    private static int DeploySide(BattleState battle, BattleSide side, IReadOnlyList<BattleStackSpec> all, ref int id)
    {
        // War machines stand in the outermost column (ADR-073): tent at the top, offensive machine in the middle, cart at the
        // bottom (ADR-056). The army deploys from the next column.
        var machines = all.Where(s => s.Machine is not null).ToList();
        var stacks = all.Where(s => s.Machine is null).ToList();
        var depth = 0;
        if (machines.Count > 0)
        {
            foreach (var machine in machines.OrderBy(m => MachineRow(m.Machine!.Value)))
            {
                var (width, height) = WarMachines.Footprint(machine.Machine!.Value);
                var y = machine.Machine switch
                {
                    WarMachineKind.HealingTent => 1,
                    WarMachineKind.AmmoCart => BattleState.Height - height - 1,
                    _ => (BattleState.Height - height) / 2,
                };
                var x = side == BattleSide.Attacker ? 0 : battle.Width - width;
                battle.MutableUnits.Add(new BattleUnit(id++, side, machine.Creature, 1, machine.SourceSlot, new CellPosition(x, y), machine.Machine));
            }

            depth = 1;
        }

        var columns = new List<List<BattleStackSpec>>();
        var current = new List<BattleStackSpec>();
        foreach (var stack in stacks)
        {
            var height = current.Sum(s => s.Creature.Size.Cells() + 1) + stack.Creature.Size.Cells();
            if (current.Count > 0 && height > BattleState.Height)
            {
                columns.Add(current);
                current = [];
            }

            current.Add(stack);
        }

        if (current.Count > 0)
        {
            columns.Add(current);
        }

        foreach (var column in columns)
        {
            var columnWidth = column.Max(s => s.Creature.Size.Cells());
            var used = column.Sum(s => s.Creature.Size.Cells());
            var gap = (BattleState.Height - used) / (column.Count + 1);
            var y = gap;
            foreach (var stack in column)
            {
                var size = stack.Creature.Size.Cells();
                var x = side == BattleSide.Attacker ? depth : battle.Width - depth - size;
                battle.MutableUnits.Add(new BattleUnit(id++, side, stack.Creature, stack.Count, stack.SourceSlot, new CellPosition(x, y)));
                y += size + gap;
            }

            depth += columnWidth + 1;
        }

        return depth;
    }

    private static int MachineRow(WarMachineKind kind) => kind switch
    {
        WarMachineKind.HealingTent => 0,
        WarMachineKind.AmmoCart => 2,
        _ => 1,
    };

    /// <summary>A battle in this many on cliff ground may get a dividing cliff (ADR-146).</summary>
    public const int CliffOneIn = 3;

    /// <summary>A gap in the cliff is at least this many cells, so that a large creature gets through; other obstacles keep this many columns off it.</summary>
    public const int CliffGap = 4;

    /// <summary>The ground whose battles may have a dividing cliff (ADR-146): highlands, rough, rocky and subterranean.</summary>
    public static bool IsCliffGround(Map.TerrainType terrain) =>
        terrain is Map.TerrainType.Highlands or Map.TerrainType.Rough or Map.TerrainType.RockyGround or Map.TerrainType.Subterranean;

    /// <summary>
    /// A dividing cliff (ADR-146) in the middle of the open ground, one battle in <see cref="CliffOneIn"/> on cliff ground, never in a siege or a
    /// creature bank: one or two pieces of 6–12 cells running down the field, at most two columns apart, that leave at least two gaps of
    /// <see cref="CliffGap"/> cells or more. Returns whether a cliff was placed.
    /// </summary>
    private static bool PlaceCliff(BattleState battle, int fromX, int toX)
    {
        const int Shift = 2;
        var band = BattleObstacle.CliffThickness + Shift;
        if (battle.Siege is not null || !battle.CliffsAllowed || !IsCliffGround(battle.Terrain) || toX - fromX < band
            || battle.Random.Next(CliffOneIn) != 0)
        {
            return false;
        }

        var random = battle.Random;
        var x = Math.Clamp(((fromX + toX - band) / 2) + random.NextInclusive(-1, 1), fromX, toX - band);
        var pieces = new List<(int Y, int Length)>();
        if (random.Next(2) == 0)
        {
            // One piece with a gap of at least CliffGap above and below.
            var length = random.NextInclusive(6, 12);
            pieces.Add((random.NextInclusive(CliffGap, BattleState.Height - length - CliffGap), length));
        }
        else
        {
            // Two pieces, short enough together to leave the gap between them and one more; the rest of the free rows go above and below.
            var first = random.NextInclusive(6, BattleState.Height - (2 * CliffGap) - 6);
            var second = random.NextInclusive(6, BattleState.Height - (2 * CliffGap) - first);
            var free = BattleState.Height - first - second;
            var between = random.NextInclusive(CliffGap, free - CliffGap);
            var outer = free - between;
            var above = random.Next(2) == 0 ? random.NextInclusive(CliffGap, outer) : random.NextInclusive(0, outer - CliffGap);
            pieces.Add((above, first));
            pieces.Add((above + first + between, second));
        }

        foreach (var (y, length) in pieces)
        {
            var at = new CellPosition(x + random.NextInclusive(0, Shift), y);
            battle.MutableObstacles.Add(new BattleObstacle(battle.Obstacles.Count + 1, at, ObstacleClass.Cliff, length));
        }

        return true;
    }

    /// <summary>The open rows above, between and below the pieces (sorted by row), or null when two pieces overlap or touch.</summary>
    internal static IReadOnlyList<int>? CliffGaps(IReadOnlyList<(int Y, int Length)> pieces)
    {
        var gaps = new List<int>();
        var row = 0;
        foreach (var (y, length) in pieces)
        {
            if (y < row || (gaps.Count > 0 && y == row))
            {
                return null;
            }

            gaps.Add(y - row);
            row = y + length;
        }

        gaps.Add(BattleState.Height - row);
        return gaps;
    }

    /// <summary>
    /// Obstacles between the deployment zones, never touching each other (passages stay open) and at least <see cref="CliffGap"/> columns off a
    /// cliff, so they never close its gaps.
    /// </summary>
    private static void PlaceObstacles(BattleState battle, int fromX, int toX, (int Min, int Max) range)
    {
        var random = battle.Random;
        var count = random.NextInclusive(range.Min, range.Max);
        for (var i = 0; i < count; i++)
        {
            var cover = random.Next(3) == 0 ? ObstacleClass.Big : ObstacleClass.Small;
            var size = cover.Cells();
            if (toX - fromX < size)
            {
                return;
            }

            for (var attempt = 0; attempt < 20; attempt++)
            {
                var obstacle = new BattleObstacle(
                    battle.Obstacles.Count + 1,
                    new CellPosition(random.NextInclusive(fromX, toX - size), random.NextInclusive(1, BattleState.Height - size - 1)),
                    cover);
                if (battle.Obstacles.All(o => !KeepOut(o).Intersects(obstacle.Rect)))
                {
                    battle.MutableObstacles.Add(obstacle);
                    break;
                }
            }
        }

        static CellRect KeepOut(BattleObstacle o) => o.Class == ObstacleClass.Cliff
            ? new CellRect(o.Rect.X - CliffGap, 0, o.Rect.Width + (2 * CliffGap), BattleState.Height)
            : o.Rect.Grow(2);
    }
}
