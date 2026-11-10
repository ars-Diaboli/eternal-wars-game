using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// A field zone (ADR-149): it covers its cells around the aimed cell, clipped to the field, from now to the last round it lasts
/// (<see cref="BattleZones"/> works it). Worth one round of what it does to the stacks standing in or next to it now, enemies for, own against: its
/// damage as a round starts, the tearing of a stack that walks out, its healing of own stacks. Blocking and darkening are not valued (proposal), so
/// the AI casts the walls and Darkness only for their damage.
/// </summary>
internal sealed class ZoneHandler : SpellEffectHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Zone;

    public override void Apply(CastContext cast, BattleSpellResolver resolver)
    {
        var (battle, side, known) = (cast.Battle, cast.Side, cast.Known);
        var zone = new BattleZone(
            ++battle.ZonesMade, known, side, BattleZones.Cells(battle, known.Effect!, cast.Cell!.Value), BattleZones.UntilRound(battle, side, known, cast.CreaturePower),
            BattleZones.Scaled(battle, side, known, cast.CreaturePower));
        battle.MutableZones.Add(zone);
        resolver.Events.Publish(new BattleZoneCreated(zone.Id, zone.Spell, side, zone.Rect, zone.UntilRound));
    }

    public override long Value(CastContext cast)
    {
        if (cast.Cell is not { } cell)
        {
            return 0;
        }

        var (battle, side, known) = (cast.Battle, cast.Side, cast.Known);
        var zone = new BattleZone(0, known, side, BattleZones.Cells(battle, known.Effect!, cell), battle.Round, BattleZones.Scaled(battle, side, known, cast.CreaturePower));
        long value = 0;
        foreach (var unit in battle.LivingUnits)
        {
            var inside = zone.Rect.Intersects(unit.Rect);
            var amount = inside ? zone.Amounts.Start + zone.Amounts.Leave : unit.Rect.IsAdjacentTo(zone.Rect) ? zone.Amounts.Beside : 0;
            if (amount > 0 && BattleZones.Hurts(battle, zone, unit))
            {
                value += (unit.Side == side ? -1 : 1) * BattleAi.Worth(unit, BattleZones.Damage(battle, zone, unit, amount));
            }

            value += BattleAi.Worth(unit, BattleZones.Healing(zone, unit));
        }

        return value;
    }
}
