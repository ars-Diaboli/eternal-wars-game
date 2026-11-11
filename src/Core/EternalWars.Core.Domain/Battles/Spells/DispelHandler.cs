using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// A dispel (Purge, Unravel, Dispel, ADR-123): it takes the spells <see cref="BattleMagic.Dispelled"/> names off each stack, even one in Stasis, and
/// needs a stack with such a spell (or, for one that heals, a wounded one). ADR-177: <c>heal N</c> also heals each stack (Purge II), <c>sap-power N</c>
/// takes Spell Power from the enemy hero for the battle (Unravel III), <c>refund% N</c> gives back part of the mana when only the enemy's spells
/// came off (Dispel III). Worth the lasting-effect share of each spell it takes off (the enemy's count for it, the own against it) and the health
/// it gives.
/// </summary>
internal sealed class DispelHandler : StackByStackHandler
{
    public override SpellEffectKind Kind => SpellEffectKind.Dispel;

    public override bool PassesStasis => true;

    public override string? AimRefusal(CastContext cast, BattleUnit unit) =>
        BattleMagic.Dispelled(cast.Side, cast.Effect, unit).Count == 0 && Heals(cast, unit) == 0
            ? $"{unit.Creature.Name} has no spells {cast.Known.Spell.Name} takes off."
            : null;

    public override void Apply(CastContext cast, BattleSpellResolver resolver)
    {
        var (battle, side, effect) = (cast.Battle, cast.Side, cast.Effect);
        var removed = cast.Affected().SelectMany(u => BattleMagic.Dispelled(side, effect, u)).ToList();
        base.Apply(cast, resolver);

        var enemy = side.Opponent();
        if (effect.SapPower > 0 && battle.Participant(enemy).Caster is not null)
        {
            battle.SappedPower[enemy] = battle.SappedPower.GetValueOrDefault(enemy) + effect.SapPower;
            resolver.Events.Publish(new BattleSpellPowerSapped(enemy, cast.Known.Spell.Id, effect.SapPower));
        }

        // Dispel III: half the mana back when it took off only the enemy's spells (a creature caster pays none).
        if (effect.RefundPercent > 0 && cast.CreaturePower is null && removed.Count > 0 && removed.All(s => s.Caster != side)
            && BattleMagic.Cost(battle, side, cast.Known) * effect.RefundPercent / 100 is > 0 and var refund)
        {
            battle.ManaLeft[side] = battle.ManaOf(side) + refund;
            resolver.Events.Publish(new BattleManaGained(side, cast.Known.Spell.Id, refund, battle.ManaOf(side)));
        }
    }

    protected override void ApplyTo(CastContext cast, BattleSpellResolver resolver, BattleUnit unit)
    {
        TakeOff(resolver, unit, BattleMagic.Dispelled(cast.Side, cast.Effect, unit), cast.Known.Spell.Id);
        if (Heals(cast, unit) is > 0 and var health)
        {
            resolver.Wounds.HealTop(unit, health);
        }
    }

    public override long Value(CastContext cast) =>
        cast.Affected().Sum(u => RemovalValue(cast, u, BattleMagic.Dispelled(cast.Side, cast.Effect, u)) + BattleAi.Worth(u, Heals(cast, u)));

    /// <summary>Takes the spells off the stack, reporting each, and lets the stack go back to its side when a control spell came off.</summary>
    public static void TakeOff(BattleSpellResolver resolver, BattleUnit unit, IReadOnlyList<ActiveSpell> spells, string by)
    {
        var sided = unit.Side;
        foreach (var removed in spells)
        {
            unit.MutableSpells.Remove(removed);
            resolver.Events.Publish(new BattleSpellDispelled(unit.Id, removed.Spell, by));
        }

        resolver.Wounds.SideChanged(unit, sided);
    }

    /// <summary>What taking the spells off the stack is worth to the caster: the enemy's count for it, its own side's against it.</summary>
    public static long RemovalValue(CastContext cast, BattleUnit unit, IEnumerable<ActiveSpell> spells) =>
        spells.Sum(s => (s.Caster == cast.Side ? -1 : 1) * BattleAi.StackValue(unit) * BattleAi.EnchantPercentPerRound
                        * Math.Clamp(s.UntilRound - cast.Battle.Round + 1, 1, BattleAi.MaxValuedRounds) / 100);

    /// <summary>The health a healing dispel gives an own creature stack (Purge II): its amount, up to the top creature's wounds.</summary>
    private static int Heals(CastContext cast, BattleUnit unit) =>
        cast.Effect.Amount > 0 && unit.Side == cast.Side && !unit.IsMachine
            ? Math.Min(BattleMagic.Amount(cast.Battle, cast.Side, cast.Known, cast.CreaturePower), unit.Creature.Health - unit.TopHealth)
            : 0;
}
