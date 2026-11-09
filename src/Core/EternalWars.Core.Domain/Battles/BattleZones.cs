using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// A field zone a spell left on the battlefield (ADR-149): its cells (clipped to the field), whose spell at which rank, the side that cast it, the
/// last round it lasts, and its amounts as cast (grown with the caster's Spell Power like damage).
/// </summary>
public sealed record BattleZone(int Id, KnownSpell Known, BattleSide Caster, CellRect Rect, int UntilRound, ZoneEffect Amounts)
{
    public string Spell => Known.Spell.Id;

    public SpellEffect Effect => Known.Effect!;

    /// <summary>What the zone does as cast; its amounts are <see cref="Amounts"/>.</summary>
    public ZoneEffect Rules => Effect.Zone!;
}

/// <summary>
/// The rules of field zones (ADR-149): where they stand, whom they block and hurt. Pure functions of <see cref="BattleState"/>; the engine
/// applies them (<c>BattleEngine</c>), the grid asks them about movement and shots.
/// </summary>
public static class BattleZones
{
    /// <summary>The cells a zone cast at the cell would cover: the spell's area or line centred on it, clipped to the field.</summary>
    public static CellRect Cells(BattleState battle, SpellEffect effect, CellPosition centre)
    {
        var rect = BattleMagic.Area(effect, centre);
        var (x, y) = (Math.Max(0, rect.X), Math.Max(0, rect.Y));
        return new CellRect(x, y, Math.Min(battle.Width, rect.Right) - x, Math.Min(BattleState.Height, rect.Bottom) - y);
    }

    /// <summary>Whether the zone keeps the walker out of its cells: a blocking zone, unless it lets the caster's own undead through.</summary>
    public static bool Blocks(BattleZone zone, BattleUnit mover) =>
        zone.Rules.BlocksMovement && !(zone.Rules.OwnUndeadPass && mover.Side == zone.Caster && mover.Creature.HasTag(CreatureTags.Undead));

    /// <summary>
    /// Whether a zone stops the shot: a <c>blocks-shots</c> zone with a cell on the line of fire that is neither the shooter's nor the target's.
    /// </summary>
    public static bool BlocksShot(BattleState battle, BattleUnit shooter, BattleUnit target)
    {
        var walls = battle.Zones.Where(z => z.Rules.BlocksShots).ToList();
        return walls.Count > 0 && BattleGrid.LineOfFireCells(shooter.Rect, target.Rect)
            .Any(c => !shooter.Rect.Contains(c) && !target.Rect.Contains(c) && walls.Any(z => z.Rect.Contains(c)));
    }

    /// <summary>Whether a <c>dims-shots</c> zone (Darkness) covers the shooter or the target: the shot deals half, once.</summary>
    public static bool DimsShot(BattleState battle, BattleUnit shooter, BattleUnit target) =>
        battle.Zones.Any(z => z.Rules.DimsShots && (z.Rect.Intersects(shooter.Rect) || z.Rect.Intersects(target.Rect)));

    /// <summary>The Defense the caster's zones give an own stack standing in them (Darkness III).</summary>
    public static int Defense(BattleState battle, BattleUnit unit) =>
        battle.Zones.Where(z => z.Caster == unit.Side && z.Effect.Modifiers.Defense != 0 && z.Rect.Intersects(unit.Rect)).Sum(z => z.Effect.Modifiers.Defense);

    /// <summary>
    /// Whether the zone's damage hits the stack: the spell works on it (filter, immunities, spell resistance, wards; war machines are spared) and it
    /// is not an own stack of a <c>spare-own</c> zone.
    /// </summary>
    public static bool Hurts(BattleState battle, BattleZone zone, BattleUnit unit) =>
        unit.IsAlive && !(zone.Effect.SpareOwn && unit.Side == zone.Caster) && BattleMagic.Affects(battle, zone.Caster, zone.Known, unit);

    /// <summary>A zone's damage to the stack: its amount as cast, less the enemy hero's Spell resistance, changed by the stack's spell-taken percent; at least 1.</summary>
    public static int Damage(BattleState battle, BattleZone zone, BattleUnit unit, int amount)
    {
        long damage = amount;
        if (unit.Side != zone.Caster)
        {
            damage = damage * (100 - battle.Participant(unit.Side).Bonus.SpellResistancePercent) / 100;
        }

        damage = damage * Math.Max(0, 100 + unit.SpellModifiers.SpellTakenPercent) / 100;
        return (int)Math.Clamp(damage, 1, int.MaxValue);
    }

    /// <summary>The health a <c>heal</c> zone gives an own creature stack in it (not war machines), up to the top creature's wounds.</summary>
    public static int Healing(BattleZone zone, BattleUnit unit) =>
        zone.Amounts.Heal > 0 && unit.IsAlive && !unit.IsMachine && unit.Side == zone.Caster && zone.Rect.Intersects(unit.Rect)
            ? Math.Min(zone.Amounts.Heal, unit.Creature.Health - unit.TopHealth)
            : 0;

    /// <summary>
    /// The damage zones deal the stack for a move along <paramref name="path"/> (its placements, from where it stood to where it stops), zone by
    /// zone in id order: <c>leave</c> when it stood in the zone; <c>enter</c> when it did not and a placement of its walk (for a flyer or teleporter,
    /// where it lands) is in the zone.
    /// </summary>
    public static IReadOnlyList<(BattleZone Zone, int Damage)> MoveDamage(BattleState battle, BattleUnit unit, IReadOnlyList<CellPosition> path)
    {
        var hits = new List<(BattleZone, int)>();
        if (path.Count < 2)
        {
            return hits;
        }

        var start = unit.RectAt(path[0]);
        var crossed = unit.MovesFreely ? path.Skip(path.Count - 1) : path.Skip(1);
        foreach (var zone in battle.Zones.OrderBy(z => z.Id))
        {
            var amount = start.Intersects(zone.Rect)
                ? zone.Amounts.Leave
                : crossed.Any(p => unit.RectAt(p).Intersects(zone.Rect)) ? zone.Amounts.Enter : 0;
            if (amount > 0 && Hurts(battle, zone, unit))
            {
                hits.Add((zone, Damage(battle, zone, unit, amount)));
            }
        }

        return hits;
    }

    /// <summary>The amounts a zone keeps as cast: each of its numbers × (10 + Spell Power) / 10 with the hero's specialty and orbs (<see cref="BattleMagic.Scaled"/>).</summary>
    public static ZoneEffect Scaled(BattleState battle, BattleSide side, KnownSpell known, int? creaturePower = null)
    {
        var rules = known.Effect!.Zone!;
        int Scale(int amount) => amount == 0 ? 0 : BattleMagic.Scaled(battle, side, known, amount, creaturePower);
        return rules with
        {
            Enter = Scale(rules.Enter),
            Start = Scale(rules.Start),
            Leave = Scale(rules.Leave),
            Beside = Scale(rules.Beside),
            Heal = Scale(rules.Heal),
        };
    }

    /// <summary>The last round a zone cast now lasts: the rest of this round plus its rounds × (10 + Spell Power) / 10, at least 1.</summary>
    public static int UntilRound(BattleState battle, BattleSide side, KnownSpell known, int? creaturePower = null) =>
        battle.Round + Math.Max(1, known.Effect!.Rounds!.Value * (10 + (creaturePower ?? BattleMagic.SpellPower(battle, side))) / 10);
}
