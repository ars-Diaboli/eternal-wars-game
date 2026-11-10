using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>Healing: the spell's amount back to each stack's top creature; worth the creature value of the health it gives the aimed stack.</summary>
internal sealed class HealHandler : StackByStackHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Heal;

    protected override void ApplyTo(CastContext cast, BattleSpellResolver resolver, BattleUnit unit) =>
        resolver.Wounds.HealTop(unit, BattleMagic.Amount(cast.Battle, cast.Side, cast.Known, cast.CreaturePower));

    public override long Value(CastContext cast) =>
        cast.Target is { } id && cast.Battle.FindUnit(id) is { } wounded
            ? BattleAi.Worth(wounded, Math.Min(BattleMagic.Amount(cast.Battle, cast.Side, cast.Known, cast.CreaturePower), wounded.Creature.Health - wounded.TopHealth))
            : 0;
}
