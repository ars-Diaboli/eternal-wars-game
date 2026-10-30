namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Simple, deterministic battle AI for neutrals, computer players and quick combat. Greedy per turn:
/// shoot the most valuable target, else melee the most valuable reachable target, else close in on the
/// nearest enemy, else defend. Value = creature value of the hit points it expects to remove.
/// </summary>
internal static class BattleAi
{
    public static void Act(BattleState battle, BattleEngine engine)
    {
        var unit = battle.ActiveUnit ?? throw new InvalidOperationException("No unit is active.");
        var enemies = battle.LivingUnitsOf(unit.Side.Opponent()).ToList();

        // The siege engine breaks the fortifications first (ADR-106); with nothing left standing it shoots troops if Ballistics lets it.
        if (SiegeRules.IsSiegeEngine(battle, unit))
        {
            if (battle.Siege!.AutoTarget() is { } structure && engine.ShootStructure(structure.Id) is null)
            {
                return;
            }

            if (SiegeRules.FiresByItself(battle, unit))
            {
                engine.Defend();
                return;
            }
        }

        // A Stronghold hero's warcries are free: use the strongest one it still has (once a round, ADR-067).
        foreach (var warcry in new[] { Warcry.Terror, Warcry.Intimidate, Warcry.Rally })
        {
            if (engine.UseWarcry(unit.Side, warcry) is null)
            {
                break;
            }
        }

        if (BattleGrid.CanShootNow(battle, unit))
        {
            var shot = enemies
                .Select(e => (Target: e, CanShoot: BattleGrid.CanShoot(battle, unit, e, out var penalty), Penalty: penalty))
                .Where(s => s.CanShoot)
                .OrderByDescending(s => Score(battle, unit, s.Target, isRanged: true, s.Penalty))
                .ThenBy(s => s.Target.Id)
                .FirstOrDefault();
            if (shot.Target is not null && engine.Shoot(shot.Target.Id) is null)
            {
                return;
            }
        }

        var melee = enemies
            .Select(e => (Target: e, Positions: BattleGrid.AttackPositions(battle, unit, e)))
            .Where(m => m.Positions.Count > 0)
            .OrderByDescending(m => Score(battle, unit, m.Target, isRanged: false))
            .ThenBy(m => m.Target.Id)
            .FirstOrDefault();
        if (melee.Target is not null && engine.Attack(melee.Target.Id, melee.Positions[0]) is null)
        {
            return;
        }

        // In a siege the defenders hold the walls: they fight what reaches them but do not go out (ADR-106).
        var holds = battle.Siege is not null && unit.Side == BattleSide.Defender;
        if (enemies.Count > 0 && !holds && Advance(battle, unit, enemies) is { } destination && engine.Move(destination) is null)
        {
            return;
        }

        engine.Defend();
    }

    private static long Score(BattleState battle, BattleUnit unit, BattleUnit target, bool isRanged, int penalty = 0)
    {
        var damage = Math.Min(DamageCalculator.Expected(battle, unit, target, isRanged, penalty), target.TotalHealth);
        return (long)damage * target.Creature.Value / target.Creature.Health;
    }

    /// <summary>The reachable placement closest to the nearest enemy, if it is closer than standing still.</summary>
    private static CellPosition? Advance(BattleState battle, BattleUnit unit, List<BattleUnit> enemies)
    {
        if (battle.Siege is not null && !unit.Creature.MovesFreely)
        {
            return SiegeAdvance(battle, unit, enemies);
        }

        var nearest = enemies.OrderBy(e => unit.Rect.GapTo(e.Rect)).ThenBy(e => e.Id).First();
        var best = BattleGrid.Reachable(battle, unit)
            .Select(r => (Position: r.Key, Steps: r.Value, Gap: unit.RectAt(r.Key).GapTo(nearest.Rect)))
            .OrderBy(r => r.Gap).ThenBy(r => r.Steps).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
            .First();
        return best.Gap < unit.Rect.GapTo(nearest.Rect) ? best.Position : null;
    }

    /// <summary>
    /// A walking attacker in a siege (ADR-106): along the way round the walls to the nearest enemy (through a breach or the broken gate); while
    /// the walls hold, it waits where it is rather than walk into the moat.
    /// </summary>
    private static CellPosition? SiegeAdvance(BattleState battle, BattleUnit unit, List<BattleUnit> enemies)
    {
        var steps = BattleGrid.ApproachSteps(battle, unit, enemies.Where(e => !e.IsMachine || enemies.All(x => x.IsMachine)));
        if (!steps.TryGetValue(unit.Position, out var now))
        {
            // No way in yet: close up to the moat, not into it.
            var nearest = enemies.OrderBy(e => unit.Rect.GapTo(e.Rect)).ThenBy(e => e.Id).First();
            var fearsMoat = SiegeRules.FearsMoat(battle, unit.Side);
            var closer = BattleGrid.Reachable(battle, unit)
                .Where(r => !fearsMoat || !battle.Siege!.InMoat(unit.RectAt(r.Key)))
                .Select(r => (Position: r.Key, Length: r.Value, Gap: unit.RectAt(r.Key).GapTo(nearest.Rect)))
                .OrderBy(r => r.Gap).ThenBy(r => r.Length).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
                .Select(r => ((CellPosition Position, int Gap)?)(r.Position, r.Gap))
                .FirstOrDefault();
            return closer is { } c && c.Gap < unit.Rect.GapTo(nearest.Rect) ? c.Position : null;
        }

        var best = BattleGrid.Reachable(battle, unit)
            .Where(r => steps.ContainsKey(r.Key))
            .Select(r => (Position: r.Key, Length: r.Value, Steps: steps[r.Key]))
            .OrderBy(r => r.Steps).ThenBy(r => r.Length).ThenBy(r => r.Position.Y).ThenBy(r => r.Position.X)
            .First();
        return best.Steps < now ? best.Position : null;
    }
}
