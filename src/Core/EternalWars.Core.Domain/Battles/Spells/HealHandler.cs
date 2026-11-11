using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// Healing: the spell's amount back to each stack's top creature; with <c>cure</c> (Heal II, ADR-177) the enemy's spells come off it first, and with
/// <c>revive% N</c> (Heal III) the health beyond the top creature's wounds revives its dead for good, up to N % of the creatures it lost. Worth the
/// creature value of the health it gives the aimed stack and of the enemy's spells it takes off.
/// </summary>
internal sealed class HealHandler : StackByStackHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Heal;

    protected override void ApplyTo(CastContext cast, BattleSpellResolver resolver, BattleUnit unit)
    {
        if (cast.Effect.Cures)
        {
            DispelHandler.TakeOff(resolver, unit, Cured(cast, unit), cast.Known.Spell.Id);
        }

        var amount = BattleMagic.Amount(cast.Battle, cast.Side, cast.Known, cast.CreaturePower);
        var wounds = unit.Creature.Health - unit.TopHealth;
        resolver.Wounds.HealTop(unit, amount);
        if (Revives(cast.Effect, unit, amount - wounds) is > 0 and var revived)
        {
            resolver.Wounds.Revive(unit, cast.Known.Spell.Id, revived, lasting: true);
        }
    }

    public override long Value(CastContext cast)
    {
        if (cast.Target is not { } id || cast.Battle.FindUnit(id) is not { } wounded)
        {
            return 0;
        }

        var amount = BattleMagic.Amount(cast.Battle, cast.Side, cast.Known, cast.CreaturePower);
        var wounds = wounded.Creature.Health - wounded.TopHealth;
        var cured = cast.Effect.Cures ? DispelHandler.RemovalValue(cast, wounded, Cured(cast, wounded)) : 0;
        return BattleAi.Worth(wounded, Math.Min(amount, wounds) + Revives(cast.Effect, wounded, amount - wounds)) + cured;
    }

    /// <summary>The enemy's spells on the stack, which a curing heal takes off.</summary>
    private static IReadOnlyList<ActiveSpell> Cured(CastContext cast, BattleUnit unit) => [.. unit.Spells.Where(s => s.Caster != cast.Side)];

    /// <summary>
    /// The health a <c>revive%</c> heal's <paramref name="left"/> over brings back as the stack's dead: at most the creatures its percent of those lost
    /// allows (rounded down), never a fraction of a creature beyond them.
    /// </summary>
    public static int Revives(SpellEffect effect, BattleUnit unit, int left)
    {
        if (effect.RevivePercent == 0 || left <= 0 || unit.IsMachine)
        {
            return 0;
        }

        var creatures = (unit.InitialCount - unit.Count) * effect.RevivePercent / 100;
        return (int)Math.Min(left, (long)creatures * unit.Creature.Health);
    }
}
