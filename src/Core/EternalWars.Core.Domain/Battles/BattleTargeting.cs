namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// What pointing at a battlefield cell means for the active unit, HoMM-style: an enemy is shot if possible, else
/// attacked from the side the pointer is on (one of eight, ADR-204); a free cell is a move to the reachable placement that covers
/// it with its centre nearest the pointer; for the siege engine a standing siege structure is a shot at it. Shared by the battle front-ends (cursor, preview, click), so they agree
/// with each other; the rules still validate the resulting command.
/// </summary>
public static class BattleTargeting
{
    /// <summary>The command that carries out <paramref name="action"/> (from <see cref="ActionAt"/>) for <paramref name="player"/>; null for no action.</summary>
    public static Commands.GameCommand? CommandFor(Common.PlayerId player, BattleAction? action) => action switch
    {
        MoveAction move => new Commands.BattleMoveCommand(player, move.To),
        AttackAction attack => new Commands.BattleAttackCommand(player, attack.Target, attack.From),
        ShootAction shoot => new Commands.BattleShootCommand(player, shoot.Target),
        ShootStructureAction structure => new Commands.BattleShootStructureCommand(player, structure.Structure),
        AttackStructureAction hit => new Commands.BattleAttackStructureCommand(player, hit.Structure, hit.From),
        _ => null,
    };

    /// <summary>The command for pointing at <paramref name="cell"/> (<see cref="ActionAt"/>), or null when there is none.</summary>
    public static Commands.GameCommand? CommandAt(
        Common.PlayerId player, BattleState battle, CellPosition cell, float? pointerX = null, float? pointerY = null) =>
        CommandFor(player, ActionAt(battle, cell, pointerX, pointerY));

    /// <summary>
    /// The action for pointing at <paramref name="cell"/>, or null when there is none (own unit, obstacle, out of reach).
    /// <paramref name="pointerX"/> / <paramref name="pointerY"/> are the exact pointer position in cell units (the cell's
    /// centre when omitted), used to pick the side of an attack and the placement of a move.
    /// </summary>
    public static BattleAction? ActionAt(BattleState battle, CellPosition cell, float? pointerX = null, float? pointerY = null)
    {
        if (battle.Phase != BattlePhase.Fighting || battle.ActiveUnit is not { } active)
        {
            return null;
        }

        var x = pointerX ?? cell.X + 0.5f;
        var y = pointerY ?? cell.Y + 0.5f;

        // The siege engine aims at the standing structure pointed at, on the wall line or off the field (keep, towers; ADR-106).
        if (SiegeRules.IsSiegeEngine(battle, active) && battle.Siege!.Structures.FirstOrDefault(s => s.IsStanding && s.Rect.Contains(cell)) is { } structure)
        {
            return new ShootStructureAction(structure.Id);
        }

        // An attacking stack pointed at the standing gate hits it (ADR-120), from the reachable side nearest the pointer.
        if (battle.Siege is { } siege && siege.Gate is { IsStanding: true } gate && gate.Rect.Contains(cell)
            && SiegeRules.StructureAttackRefusal(battle, active, gate.Id) is null)
        {
            var spot = BestSide(BattleGrid.StructureAttackPositions(battle, active, gate), active, gate.Rect, x, y);
            return spot is { } stand ? new AttackStructureAction(gate.Id, stand) : null;
        }

        if (battle.UnitAt(cell) is { } unit)
        {
            if (unit.Side == active.Side)
            {
                return null;
            }

            if (BattleGrid.CanShoot(battle, active, unit, out _))
            {
                return new ShootAction(unit.Id);
            }

            // The side the pointer is on (ADR-204): the placement whose direction from the target is nearest the pointer's, then the
            // placement nearest the pointer, then the rules' order (closest first).
            var from = BestSide(BattleGrid.AttackPositions(battle, active, unit), active, unit.Rect, x, y);
            return from is { } placement ? new AttackAction(unit.Id, placement) : null;
        }

        var move = BattleGrid.Reachable(battle, active)
            .Where(r => r.Key != active.Position && active.RectAt(r.Key).Contains(cell))
            .OrderBy(r => Distance(active.RectAt(r.Key), x, y)).ThenBy(r => r.Value).ThenBy(r => r.Key.Y).ThenBy(r => r.Key.X)
            .Select(r => (CellPosition?)r.Key)
            .FirstOrDefault();
        return move is { } to ? new MoveAction(to) : null;
    }

    /// <summary>
    /// Deployment step (ADR-072): the top-left cell that puts the unit's footprint centre nearest the pointer (in cell units), kept
    /// inside the field. <see cref="BattleSetup.DeploymentRefusal"/> says whether it may stand there.
    /// </summary>
    public static CellPosition DeploymentPlacement(BattleState battle, BattleUnit unit, float pointerX, float pointerY) =>
        new(Math.Clamp((int)MathF.Round(pointerX - (unit.Width / 2f)), 0, battle.Width - unit.Width),
            Math.Clamp((int)MathF.Round(pointerY - (unit.Height / 2f)), 0, BattleState.Height - unit.Height));

    /// <summary>A move spell's second click (ADR-151): the top-left cell that centres the unit's footprint on the clicked cell, kept inside the field.</summary>
    public static CellPosition SpellPlacement(BattleState battle, BattleUnit unit, CellPosition cell) =>
        DeploymentPlacement(battle, unit, cell.X + 0.5f, cell.Y + 0.5f);

    /// <summary>
    /// The side of <paramref name="target"/> that a stack standing on <paramref name="stand"/> strikes from (ADR-204): the direction from the
    /// target's centre to the stand's centre, to the nearest of the eight.
    /// </summary>
    public static CompassSide SideOf(CellRect target, CellRect stand) =>
        CompassSides.Nearest(Centre(stand).X - Centre(target).X, Centre(stand).Y - Centre(target).Y);

    /// <summary>The side a melee <paramref name="action"/> (a blow at a stack or the gate) strikes from; null for other actions.</summary>
    public static CompassSide? SideOf(BattleState battle, BattleAction? action)
    {
        if (battle.ActiveUnit is not { } active)
        {
            return null;
        }

        return action switch
        {
            AttackAction attack when battle.FindUnit(attack.Target) is { } target =>
                SideOf(target.Rect, active.RectAt(attack.From ?? active.Position)),
            AttackStructureAction hit when battle.Siege?.Structures.FirstOrDefault(s => s.Id == hit.Structure) is { } structure =>
                SideOf(structure.Rect, active.RectAt(hit.From ?? active.Position)),
            _ => null,
        };
    }

    /// <summary>
    /// Of the placements a blow may come from, the one on the side the pointer is on: the pointer's direction from the target's centre picks
    /// one of eight sides (45° sectors); the placement whose own direction is nearest it wins, then the one nearest the pointer, then the
    /// list's order. A pointer at the very centre names no side, so the nearest placement wins.
    /// </summary>
    private static CellPosition? BestSide(IReadOnlyList<CellPosition> placements, BattleUnit active, CellRect target, float x, float y)
    {
        var (cx, cy) = Centre(target);
        var pointed = Math.Abs(x - cx) + Math.Abs(y - cy) < 0.01f ? (CompassSide?)null : CompassSides.Nearest(x - cx, y - cy);
        return placements
            .Select((p, i) => (Placement: p, Order: i, Gap: pointed is { } side ? AngleGap(side, active.RectAt(p), cx, cy) : 0, Distance: Distance(active.RectAt(p), x, y)))
            .OrderBy(c => c.Gap).ThenBy(c => c.Distance).ThenBy(c => c.Order)
            .Select(c => (CellPosition?)c.Placement)
            .FirstOrDefault();
    }

    /// <summary>How far, in radians, the direction from (<paramref name="cx"/>, <paramref name="cy"/>) to the stand's centre is from <paramref name="side"/>.</summary>
    private static double AngleGap(CompassSide side, CellRect stand, float cx, float cy)
    {
        var (sx, sy) = Centre(stand);
        var gap = Math.Abs(Math.Atan2(sy - cy, sx - cx) - CompassSides.Angle(side)) % (2 * Math.PI);
        return gap > Math.PI ? (2 * Math.PI) - gap : gap;
    }

    private static (float X, float Y) Centre(CellRect rect) => (rect.X + (rect.Width / 2f), rect.Y + (rect.Height / 2f));

    /// <summary>Squared distance from a footprint's centre to a point in cell units.</summary>
    private static float Distance(CellRect rect, float x, float y)
    {
        var dx = rect.X + (rect.Width / 2f) - x;
        var dy = rect.Y + (rect.Height / 2f) - y;
        return (dx * dx) + (dy * dy);
    }
}
