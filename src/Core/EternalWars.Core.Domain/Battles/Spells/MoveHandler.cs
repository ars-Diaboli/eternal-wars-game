using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// A move (Teleport, Shadow Step, ADR-151): the stack lands on the cell picked with the second click (its footprint's top-left), over everything
/// between. The AI moves only its active melee stack next to an enemy it cannot reach this turn; the move is worth the blow it can strike from there.
/// </summary>
internal sealed class MoveHandler : SpellEffectHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Move;

    /// <summary>
    /// Why the move cannot take the stack to the cell, or null: a rooted stack and the keep's stack stay put; the footprint must fit there (inside the
    /// field, clear of obstacles and other stacks) somewhere else than it stands, within the spell's range (a straight line from where it stands,
    /// like a flyer's reach), and for <c>shadow</c> not next to an enemy stack.
    /// </summary>
    public override string? AffectedRefusal(CastContext cast, BattleUnit unit)
    {
        if (unit.IsRooted || unit.InKeep)
        {
            return $"{unit.Creature.Name} cannot be moved.";
        }

        if (cast.Cell is not { } at)
        {
            return $"{cast.Known.Spell.Name} needs the cell {unit.Creature.Name} goes to.";
        }

        return CanLand(cast.Battle, cast.Side, cast.Effect, unit, at) ? null : $"{unit.Creature.Name} cannot be moved to {at}.";
    }

    /// <summary>
    /// The stack lands on the placement. A field zone it leaves or lands in, and the moat, hurt it as they would a flyer's. With rank III's
    /// <c>again</c> a stack that had already taken its turn this round gets one more, after the waiting stacks (it may not wait then);
    /// <c>again-attack</c> makes it a turn without moving.
    /// </summary>
    public override void Apply(CastContext cast, BattleSpellResolver resolver)
    {
        var battle = cast.Battle;
        var unit = battle.GetUnit(cast.Target!.Value);
        var to = cast.Cell!.Value;
        var from = unit.Position;
        var hits = battle.Zones.Count > 0 ? BattleZones.MoveDamage(battle, unit, [from, to]) : [];
        unit.Position = to;
        resolver.Events.Publish(new BattleUnitTeleported(unit.Id, from, to, cast.Known.Spell.Id));
        resolver.Wounds.Landed(unit, from, hits);

        var again = cast.Effect.Again;
        if (again == SpellTurnAgain.None || !unit.IsAlive || unit == battle.ActiveUnit
            || battle.Queue.Contains(unit.Id) || battle.Waiting.Contains(unit.Id))
        {
            return;
        }

        battle.MutableWaiting.Add(unit.Id);
        battle.WaitedThisRound.Add(unit.Id);
        unit.StaysPut = again == SpellTurnAgain.Attack;
        resolver.Events.Publish(new BattleUnitTurnRegained(unit.Id, cast.Known.Spell.Id, unit.StaysPut));
    }

    public override long Value(CastContext cast) =>
        cast.Target is { } moved && cast.Battle.FindUnit(moved) is { } mover && cast.Cell is { } to
            ? cast.Battle.LivingUnitsOf(cast.Side.Opponent()).Where(e => mover.RectAt(to).IsAdjacentTo(e.Rect))
                .Select(e => BattleAi.Score(cast.Battle, mover, e, isRanged: false)).DefaultIfEmpty(0).Max()
            : 0;

    /// <summary>
    /// Where the AI moves a stack (proposal): only the active stack, when it fights in melee (it cannot shoot now) and cannot reach an enemy this
    /// turn, to each placement next to an enemy stack it may land on. So Shadow Step I, which never lands next to an enemy, is not cast by the AI.
    /// </summary>
    public override IEnumerable<(int? Target, CellPosition? Cell)> Aims(BattleState battle, BattleSide side, SpellEffect effect)
    {
        if (battle.ActiveUnit is not { IsMachine: false } unit || unit.Side != side || unit.IsFixed || unit.Has(SpellStatus.NoAttack)
            || BattleGrid.CanShootNow(battle, unit))
        {
            yield break;
        }

        var enemies = battle.LivingUnitsOf(side.Opponent()).Where(e => BattleGrid.CanBeAttacked(unit, e, melee: true) && !e.InKeep).OrderBy(e => e.Id).ToList();
        if (enemies.Any(e => BattleGrid.AttackPositions(battle, unit, e).Count > 0))
        {
            yield break;
        }

        var seen = new HashSet<CellPosition>();
        foreach (var enemy in enemies)
        {
            for (var y = enemy.Position.Y - unit.Height; y <= enemy.Rect.Bottom; y++)
            {
                for (var x = enemy.Position.X - unit.Width; x <= enemy.Rect.Right; x++)
                {
                    var at = new CellPosition(x, y);
                    if (unit.RectAt(at).IsAdjacentTo(enemy.Rect) && seen.Add(at) && CanLand(battle, side, effect, unit, at))
                    {
                        yield return (unit.Id, at);
                    }
                }
            }
        }
    }

    /// <summary>Whether the move may take the stack to the placement (<see cref="AffectedRefusal"/>).</summary>
    private static bool CanLand(BattleState battle, BattleSide side, SpellEffect effect, BattleUnit unit, CellPosition at) =>
        at != unit.Position
        && (effect.MoveRange == 0
            || BattleGrid.Length(at.X - unit.Position.X, at.Y - unit.Position.Y) <= (effect.MoveRange * BattleGrid.Unit) + BattleGrid.ReachSlack)
        && BattleGrid.IsFree(battle, unit, at)
        && !(effect.Shadow && battle.LivingUnits.Any(u => u.Side != side && u.Rect.IsAdjacentTo(unit.RectAt(at))));
}
