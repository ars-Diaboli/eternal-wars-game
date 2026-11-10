using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles.Spells;

/// <summary>
/// A buff or curse (ADR-110, ADR-123, ADR-150, ADR-152): it takes hold on each stack in its reach it works on, replacing the same spell's earlier
/// effect. Its value is a share of the value of each stack it newly takes hold on (an enemy stack for a curse, an own one for a buff) for each round
/// it lasts, more for the spells that take turns away; wards against spells are worth nothing when the enemy cannot cast. A damage spell's lasting
/// effect works and is valued the same way.
/// </summary>
internal abstract class EnchantHandler : StackByStackHandler
{
    protected sealed override void ApplyTo(CastContext cast, BattleSpellResolver resolver, BattleUnit unit) => Enchant(cast, resolver, unit, cast.Second);

    public sealed override long Value(CastContext cast) =>
        cast.Affected().Sum(u => (u.Side == cast.Side) == cast.Effect.IsHostile
            ? -LastingValue(cast.Battle, cast.Side, cast.Known, u)
            : LastingValue(cast.Battle, cast.Side, cast.Known, u));

    /// <summary>
    /// The spell's lasting effect takes hold on the stack, replacing the same spell's earlier effect; a <c>controlled</c> one (ADR-150) brings it
    /// over to the caster's side (a <c>split</c> one only the creatures within its health limit); Martyrdom (ADR-152) names its martyr.
    /// </summary>
    public static void Enchant(CastContext cast, BattleSpellResolver resolver, BattleUnit unit, int? martyr)
    {
        var (battle, side, known, creaturePower) = (cast.Battle, cast.Side, cast.Known, cast.CreaturePower);
        if (known.Effect!.Split && unit.TotalHealth > BattleMagic.HealthLimit(battle, side, known, creaturePower))
        {
            unit = SplitOff(resolver, unit, known, BattleMagic.SplitCount(battle, side, known, unit, creaturePower));
        }

        var until = BattleMagic.UntilRound(battle, side, known, unit, creaturePower);
        var turns = BattleMagic.Turns(battle, side, known, unit, creaturePower);
        var before = unit.Side;
        var modifiers = known.Effect!.Modifiers;
        if (modifiers.Burn != 0 || modifiers.Regen != 0)
        {
            // Burning and regrowth grow with Spell Power like damage, when cast (ADR-123).
            modifiers = modifiers with
            {
                Burn = modifiers.Burn == 0 ? 0 : BattleMagic.Scaled(battle, side, known, modifiers.Burn, creaturePower),
                Regen = modifiers.Regen == 0 ? 0 : BattleMagic.Scaled(battle, side, known, modifiers.Regen, creaturePower),
            };
        }

        unit.MutableSpells.RemoveAll(s => s.Spell == known.Spell.Id && !s.IsMartyr);
        unit.MutableSpells.Add(new ActiveSpell(known.Spell.Id, side, modifiers, until, turns) { Martyr = modifiers.MartyrPercent > 0 ? martyr : null });
        resolver.Events.Publish(new BattleSpellApplied(unit.Id, known.Spell.Id, until, turns));
        resolver.Wounds.SideChanged(unit, before);

        // Martyrdom III (ADR-152): the martyr stands firmer while it guards.
        if (modifiers.MartyrPercent > 0 && known.Effect.MartyrDefense > 0 && martyr is { } id && battle.FindUnit(id) is { IsAlive: true } guard)
        {
            guard.MutableSpells.RemoveAll(s => s.Spell == known.Spell.Id && s.IsMartyr);
            guard.MutableSpells.Add(new ActiveSpell(known.Spell.Id, side, new SpellModifiers(Defense: known.Effect.MartyrDefense), until) { IsMartyr = true });
            resolver.Events.Publish(new BattleSpellApplied(guard.Id, known.Spell.Id, until));
        }
    }

    /// <summary>What the lasting part of a spell is worth on the stack (0 when the stack already has this spell, or for a ward nobody can test).</summary>
    public static long LastingValue(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit)
    {
        var effect = known.Effect!;
        var m = effect.Modifiers;
        if (m.IsEmpty || unit.Spells.Any(s => s.Spell == known.Spell.Id))
        {
            return 0;
        }

        var enemyCasts = battle.Participant(side.Opponent()).Caster is not null || battle.LivingUnitsOf(side.Opponent()).Any(u => BattleMagic.CreatureSpell(battle, u) is not null);
        var wardsOnly = m == new SpellModifiers(Status: m.Status & SpellStatus.WardMind, ImmuneLevel: m.ImmuneLevel, ShroudLevel: m.ShroudLevel);
        if (wardsOnly && !enemyCasts)
        {
            return 0;
        }

        // A spell that lasts turns counts a round for each turn (ADR-150).
        var rounds = (effect.Turns ?? effect.Rounds) switch
        {
            null => BattleAi.MaxValuedRounds,
            0 => 1,
            var r => Math.Min(BattleAi.MaxValuedRounds, r.Value),
        };
        var percent = m.Has(SpellStatus.Controlled) ? BattleAi.TakenPercentPerRound
            : m.Has(SpellStatus.Blind | SpellStatus.Stasis | SpellStatus.Confused | SpellStatus.Frenzied) ? BattleAi.ControlPercentPerRound
            : BattleAi.EnchantPercentPerRound;
        var taken = effect.Split && unit.TotalHealth > BattleMagic.HealthLimit(battle, side, known)
            ? Math.Min(unit.Count, BattleMagic.SplitCount(battle, side, known, unit))
            : unit.Count;
        var value = (long)taken * unit.Creature.Value * percent * rounds / 100;
        if (m.Burn > 0)
        {
            value += BattleAi.Worth(unit, m.Burn * rounds);
        }

        if (m.DecayPercent > 0)
        {
            // Plague hurts the enemy; Pact of Ash's losses are the price on an own stack.
            value += (unit.Side == side ? -1 : 1) * BattleAi.StackValue(unit) * m.DecayPercent * rounds / 100;
        }

        return value;
    }

    /// <summary>
    /// Puppet Master III (ADR-152): <paramref name="count"/> full-health creatures leave the stack as a stack of their own on the nearest free
    /// placement, with its spells, shots and casts; they came from the same army slot and go back to it after the battle. They act from the next round.
    /// </summary>
    private static BattleUnit SplitOff(BattleSpellResolver resolver, BattleUnit unit, KnownSpell known, int count)
    {
        var battle = resolver.Battle;
        var at = BattleMagic.PlacementNear(battle, unit, unit.Creature)!.Value;
        var part = new BattleUnit(battle.NextUnitId(), unit.Owner, unit.Creature, count, unit.SourceSlot, at)
        {
            IsSummoned = unit.IsSummoned,
            IsRaised = unit.IsRaised,
            MutableCastsLeft = new Dictionary<string, int>(unit.CastsLeft, StringComparer.Ordinal),
            ShotsLeft = unit.ShotsLeft,
        };
        part.MutableSpells.AddRange(unit.Spells);
        unit.Count -= count;
        unit.InitialCount = Math.Max(unit.Count, unit.InitialCount - count);
        unit.Revived = Math.Min(unit.Revived, unit.Count);
        battle.MutableUnits.Add(part);
        resolver.Events.Publish(new BattleUnitSplit(unit.Id, unit.Count, part.Info(), known.Spell.Id));
        return part;
    }
}
