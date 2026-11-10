using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// A dispel (Purge, Unravel, Dispel, ADR-123): it takes the spells <see cref="BattleMagic.Dispelled"/> names off each stack, even one in Stasis, and
/// needs a stack with such a spell. Worth the lasting-effect share of each spell it takes off: the enemy's count for it, the own against it.
/// </summary>
internal sealed class DispelHandler : StackByStackHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Dispel;

    public override bool PassesStasis => true;

    public override string? AimRefusal(CastContext cast, BattleUnit unit) =>
        BattleMagic.Dispelled(cast.Side, cast.Effect, unit).Count == 0 ? $"{unit.Creature.Name} has no spells {cast.Known.Spell.Name} takes off." : null;

    protected override void ApplyTo(CastContext cast, BattleSpellResolver resolver, BattleUnit unit)
    {
        var sided = unit.Side;
        foreach (var removed in BattleMagic.Dispelled(cast.Side, cast.Effect, unit))
        {
            unit.MutableSpells.Remove(removed);
            resolver.Events.Publish(new BattleSpellDispelled(unit.Id, removed.Spell, cast.Known.Spell.Id));
        }

        resolver.Wounds.SideChanged(unit, sided);
    }

    public override long Value(CastContext cast) =>
        cast.Affected().Sum(u => BattleMagic.Dispelled(cast.Side, cast.Effect, u)
            .Sum(s => (s.Caster == cast.Side ? -1 : 1) * BattleAi.StackValue(u) * BattleAi.EnchantPercentPerRound
                      * Math.Clamp(s.UntilRound - cast.Battle.Round + 1, 1, BattleAi.MaxValuedRounds) / 100));
}
