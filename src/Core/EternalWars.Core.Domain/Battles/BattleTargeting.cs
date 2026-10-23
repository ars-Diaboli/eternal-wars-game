namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// What pointing at a battlefield cell means for the active unit, HoMM-style: an enemy is shot if possible, else
/// attacked from the reachable side nearest the pointer; a free cell is a move to the reachable placement that covers
/// it with its centre nearest the pointer. Shared by the battle front-ends (cursor, preview, click), so they agree
/// with each other; the rules still validate the resulting command.
/// </summary>
public static class BattleTargeting
{
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

            // Closest first, so ties keep the rules' preferred placement.
            var from = BattleGrid.AttackPositions(battle, active, unit)
                .Select((p, i) => (Placement: p, Order: i, Distance: Distance(active.RectAt(p), x, y)))
                .OrderBy(c => c.Distance).ThenBy(c => c.Order)
                .Select(c => (CellPosition?)c.Placement)
                .FirstOrDefault();
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

    /// <summary>Squared distance from a footprint's centre to a point in cell units.</summary>
    private static float Distance(CellRect rect, float x, float y)
    {
        var dx = rect.X + (rect.Width / 2f) - x;
        var dy = rect.Y + (rect.Height / 2f) - y;
        return (dx * dx) + (dy * dy);
    }
}
