using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// A summon (ADR-106, ADR-120): what <see cref="BattleMagic.SummonPlacement"/> calls joins an own summoned stack of its creature or stands on the
/// free placement nearest the own edge; it needs room. The creatures act from the next round, vanish when the battle ends and are worth no
/// experience. Worth the creature value it calls.
/// </summary>
internal sealed class SummonHandler : SpellEffectHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Summon;

    public override string? CastRefusal(CastContext cast) =>
        BattleMagic.SummonPlacement(cast.Battle, cast.Side, cast.Known, cast.CreaturePower) is null
            ? $"There is no room to call {cast.Known.Spell.Name}'s creatures."
            : null;

    public override void Apply(CastContext cast, BattleSpellResolver resolver)
    {
        var battle = cast.Battle;
        if (BattleMagic.SummonPlacement(battle, cast.Side, cast.Known, cast.CreaturePower) is not { } placement)
        {
            return;
        }

        var (creature, count, joins, at) = placement;
        var unit = joins;
        if (unit is not null)
        {
            unit.Count += count;
        }
        else
        {
            unit = new BattleUnit(battle.NextUnitId(), cast.Side, creature, count, -1, at)
            {
                IsSummoned = true,
                IsRooted = cast.Effect.Rooted,
            };
            battle.MutableUnits.Add(unit);
        }

        resolver.Events.Publish(new BattleUnitSummoned(
            new BattleUnitInfo(unit.Id, unit.Side, unit.Creature.Id, unit.Count, unit.Position, unit.Width, unit.Height), cast.Known.Spell.Id, count));
    }

    public override long Value(CastContext cast) =>
        BattleMagic.SummonPlacement(cast.Battle, cast.Side, cast.Known, cast.CreaturePower) is { } summon ? (long)summon.Count * summon.Creature.Value : 0;
}
