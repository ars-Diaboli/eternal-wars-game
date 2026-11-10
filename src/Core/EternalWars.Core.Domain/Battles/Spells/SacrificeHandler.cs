using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// A sacrifice (Blood Price, ADR-152): the own stack loses <see cref="BattleMagic.Sacrificed"/> creatures (no shield takes it) and the hero gains the
/// mana their health buys; not on a copy. The AI does not trade creatures for mana (worth 0, proposal).
/// </summary>
internal sealed class SacrificeHandler : StackByStackHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Sacrifice;

    public override string? AffectedRefusal(CastContext cast, BattleUnit unit) =>
        unit.IsCopy ? $"{cast.Known.Spell.Name} does not work on a copy." : null;

    protected override void ApplyTo(CastContext cast, BattleSpellResolver resolver, BattleUnit unit)
    {
        var (battle, side, spellId) = (cast.Battle, cast.Side, cast.Known.Spell.Id);
        var (health, mana) = BattleMagic.SacrificeYield(cast.Effect, unit);
        resolver.Wounds.SpellDamage(unit, spellId, health, shieldable: false);
        if (mana > 0)
        {
            battle.ManaLeft[side] = battle.ManaOf(side) + mana;
            resolver.Events.Publish(new BattleManaGained(side, spellId, mana, battle.ManaOf(side)));
        }
    }

    public override long Value(CastContext cast) => 0;
}
