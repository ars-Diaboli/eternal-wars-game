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

    /// <summary>Widths offered for an effective Tactics level; none = the default only.</summary>
    public static IReadOnlyList<int> WidthOptions(SkillLevel effectiveTactics) => effectiveTactics switch
    {
        SkillLevel.Basic => [30, 32],
        SkillLevel.Advanced => [28, 30, 32],
        SkillLevel.Expert => [28, 30, 32, 34],
        _ => [BattleState.DefaultWidth],
    };

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
        var attackerExtent = DeploySide(battle, BattleSide.Attacker, attacker, ref id);
        var defenderExtent = DeploySide(battle, BattleSide.Defender, defender, ref id);
        PlaceObstacles(battle, attackerExtent + 1, battle.Width - defenderExtent - 1);
        foreach (var side in new[] { BattleSide.Attacker, BattleSide.Defender })
        {
            SupplyShots(battle, side);
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
        // War machines stand in a column of their own at the back: tent at the top, offensive machine in the middle,
        // cart at the bottom (ADR-056). The army deploys in front of them.
        var machines = all.Where(s => s.Machine is not null).ToList();
        var stacks = all.Where(s => s.Machine is null).ToList();
        var depth = 0;
        if (machines.Count > 0)
        {
            foreach (var machine in machines.OrderBy(m => MachineRow(m.Machine!.Value)))
            {
                var size = machine.Creature.Size.Cells();
                var y = machine.Machine switch
                {
                    WarMachineKind.HealingTent => 1,
                    WarMachineKind.AmmoCart => BattleState.Height - size - 1,
                    _ => (BattleState.Height - size) / 2,
                };
                var x = side == BattleSide.Attacker ? 0 : battle.Width - size;
                battle.MutableUnits.Add(new BattleUnit(id++, side, machine.Creature, 1, machine.SourceSlot, new CellPosition(x, y), machine.Machine));
            }

            depth = machines.Max(s => s.Creature.Size.Cells()) + 1;
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

    /// <summary>2–5 obstacles between the deployment zones, never touching each other (passages stay open).</summary>
    private static void PlaceObstacles(BattleState battle, int fromX, int toX)
    {
        var random = battle.Random;
        var count = random.NextInclusive(2, 5);
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
                if (battle.Obstacles.All(o => !o.Rect.Grow(2).Intersects(obstacle.Rect)))
                {
                    battle.MutableObstacles.Add(obstacle);
                    break;
                }
            }
        }
    }
}
