using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>A slay (Final Reckoning, ADR-123): <see cref="BattleMagic.Slain"/> creatures of each stack die outright; worth their creature value.</summary>
internal sealed class SlayHandler : StackByStackHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Slay;

    protected override void ApplyTo(CastContext cast, BattleSpellResolver resolver, BattleUnit unit) =>
        resolver.Wounds.KillCreatures(unit, cast.Known.Spell.Id, Math.Min(unit.Count, BattleMagic.Slain(cast.Effect, unit))); // the wounded top creature goes first

    public override long Value(CastContext cast) =>
        cast.Affected().Sum(u => (u.Side == cast.Side ? -1 : 1) * (long)Math.Min(u.Count, BattleMagic.Slain(cast.Effect, u)) * u.Creature.Value);
}
