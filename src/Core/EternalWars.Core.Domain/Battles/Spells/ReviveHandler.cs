using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// A revive (Resurrection, ADR-123): the spell's amount back to each stack, its top creature first, then its dead up to its count at
/// the start of the battle (for good when <c>lasting</c>); it needs a stack with something missing, and <c>targets N</c> takes only such stacks.
/// Worth the creature value of the health it gives back.
/// </summary>
internal sealed class ReviveHandler : StackByStackHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Revive;

    public override string? AimRefusal(CastContext cast, BattleUnit unit) =>
        BattleMagic.Missing(unit) == 0 ? $"{unit.Creature.Name} has nothing to revive." : null;

    public override bool SpreadsTo(BattleUnit unit) => BattleMagic.Missing(unit) > 0;

    protected override void ApplyTo(CastContext cast, BattleSpellResolver resolver, BattleUnit unit) =>
        resolver.Wounds.Revive(unit, cast.Known.Spell.Id, BattleMagic.Amount(cast.Battle, cast.Side, cast.Known, cast.CreaturePower), cast.Effect.Lasting);

    public override long Value(CastContext cast) =>
        cast.Affected().Sum(u => (long)Math.Min(BattleMagic.Missing(u), BattleMagic.Amount(cast.Battle, cast.Side, cast.Known, cast.CreaturePower)) * u.Creature.Value / u.Creature.Health);
}
