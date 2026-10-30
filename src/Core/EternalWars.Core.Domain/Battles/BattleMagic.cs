using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Magic;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// A hero's spells in battle (ADR-071, ADR-072, ADR-110), rank I: at most one cast a round per side, during the turn of one of its own units,
/// which keeps its action. Damage is the spell's base × (10 + Spell Power) / 10, durations too (at least 1 round); a spell specialty adds 3 % a
/// hero level to the amount; holy spells do double damage to undead and demons; the enemy hero's Spell resistance cuts hostile spells' damage and
/// duration; Interference takes Spell Power off and adds mana to the cost. Creatures' immunities and spell resistance stop spells for good.
/// </summary>
public static class BattleMagic
{
    /// <summary>A spell specialty's effect per hero level, in percent (ADR-072, MAG-6).</summary>
    public const int SpecialtyPercentPerLevel = 3;

    /// <summary>The spells' tag for holy damage, doubled against undead and demons and lost on holy creatures.</summary>
    public const string Holy = "holy";

    /// <summary>Mana the spell costs the side's hero now: its own cost plus the enemy hero's Interference.</summary>
    public static int Cost(BattleState battle, BattleSide side, KnownSpell spell) =>
        spell.Cost + battle.Participant(side.Opponent()).Bonus.EnemySpellCostPenalty;

    /// <summary>The side's Spell Power after the enemy hero's Interference, never below 0.</summary>
    public static int SpellPower(BattleState battle, BattleSide side) =>
        Math.Max(0, (battle.Participant(side).Caster?.SpellPower ?? 0) - battle.Participant(side.Opponent()).Bonus.EnemySpellPowerPenalty);

    /// <summary>
    /// Why the side's hero cannot cast the spell now at the stack or cell, or null. A stack spell needs a living target of the right side (enemies
    /// for damage and curses, own stacks for healing and buffs) the spell works on; an area spell needs a cell on the field.
    /// </summary>
    public static string? CastRefusal(BattleState battle, BattleSide side, string spellId, int? target, CellPosition? cell)
    {
        if (battle.Participant(side).Caster is not { } caster)
        {
            return "The hero has no spell book.";
        }

        if (caster.Find(spellId) is not { } known)
        {
            return $"The hero does not know '{spellId}'.";
        }

        if (known.Spell.Effect is not { } effect)
        {
            return $"{known.Spell.Name} cannot be cast in battle.";
        }

        if (battle.HasCastThisRound(side))
        {
            return "The hero has already cast a spell this round.";
        }

        if (battle.ManaOf(side) < Cost(battle, side, known))
        {
            return $"Not enough mana for {known.Spell.Name}.";
        }

        switch (effect.Reach)
        {
            case SpellReach.Stack:
                if (target is not { } id || battle.FindUnit(id) is not { IsAlive: true } unit)
                {
                    return $"{known.Spell.Name} needs a living stack to aim at.";
                }

                if ((unit.Side != side) != effect.IsHostile)
                {
                    return effect.IsHostile ? $"{known.Spell.Name} is cast on enemy stacks." : $"{known.Spell.Name} is cast on own stacks.";
                }

                return Affects(battle, side, known, unit) ? null : $"{known.Spell.Name} has no effect on {unit.Creature.Name}.";
            case SpellReach.Area:
                return cell is { } at && at.X >= 0 && at.Y >= 0 && at.X < battle.Width && at.Y < BattleState.Height
                    ? null
                    : $"{known.Spell.Name} needs a cell on the battlefield.";
            default:
                return null;
        }
    }

    /// <summary>The square an area spell covers around its centre cell (the same size whatever the targets' sizes, ADR-072, MAG-11).</summary>
    public static CellRect Area(SpellEffect effect, CellPosition centre) =>
        new(centre.X - (effect.AreaSize / 2), centre.Y - (effect.AreaSize / 2), effect.AreaSize, effect.AreaSize);

    /// <summary>
    /// The stacks in the spell's reach, by id: the target; every stack with a cell in the area (both sides); every stack of the enemy side for a
    /// hostile spell or of the own side otherwise; every stack on the field.
    /// </summary>
    public static IReadOnlyList<BattleUnit> Reach(BattleState battle, BattleSide side, SpellEffect effect, int? target, CellPosition? cell)
    {
        IEnumerable<BattleUnit> units = effect.Reach switch
        {
            SpellReach.Stack => battle.FindUnit(target!.Value) is { IsAlive: true } unit ? [unit] : [],
            SpellReach.Area => battle.LivingUnits.Where(u => u.Rect.Intersects(Area(effect, cell!.Value))),
            SpellReach.Side => battle.LivingUnitsOf(effect.IsHostile ? side.Opponent() : side),
            _ => battle.LivingUnits,
        };
        return units.OrderBy(u => u.Id).ToList();
    }

    /// <summary>
    /// Whether the spell works on the stack: war machines only take damage; the spell's filter (living, undead, undead and demons, shooters);
    /// creature immunities to the spell's tags; creature spell resistance against hostile spells; holy creatures ignore holy spells.
    /// </summary>
    public static bool Affects(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit)
    {
        var spell = known.Spell;
        var effect = spell.Effect!;
        var creature = unit.Creature;
        if (unit.IsMachine)
        {
            return effect.Kind == SpellEffectKind.Damage && effect.Filter is SpellFilter.None;
        }

        var fits = effect.Filter switch
        {
            SpellFilter.Living => !creature.HasTag(CreatureTags.Undead) && !creature.HasTag(CreatureTags.Construct),
            SpellFilter.Undead => creature.HasTag(CreatureTags.Undead),
            SpellFilter.UndeadOrDemon => creature.HasTag(CreatureTags.Undead) || creature.HasTag(CreatureTags.Demon),
            SpellFilter.Shooter => creature.Shots > 0,
            _ => true,
        };
        if (!fits || creature.IsImmuneTo(spell.Tags) || (spell.HasTag(Holy) && creature.HasTag(CreatureTags.Holy)))
        {
            return false;
        }

        return !(effect.IsHostile && unit.Side != side && creature.ResistsSpell(spell.Level, known.Rank));
    }

    /// <summary>
    /// The spell's damage to the stack: base × (10 + Spell Power) / 10, the specialty's +3 % a level, ×2 for holy damage to undead and demons,
    /// less the enemy hero's Spell resistance, changed by the stack's spell-taken percent (Arcane Ward); at least 1.
    /// </summary>
    public static int Damage(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit)
    {
        var spell = known.Spell;
        long damage = Amount(battle, side, known);
        if (spell.HasTag(Holy) && (unit.Creature.HasTag(CreatureTags.Undead) || unit.Creature.HasTag(CreatureTags.Demon)))
        {
            damage *= 2;
        }

        if (unit.Side != side)
        {
            damage = damage * (100 - battle.Participant(unit.Side).Bonus.SpellResistancePercent) / 100;
        }

        damage = damage * Math.Max(0, 100 + unit.SpellModifiers.SpellTakenPercent) / 100;
        return (int)Math.Clamp(damage, 1, int.MaxValue);
    }

    /// <summary>A damage or healing spell's amount from the side's hero: base × (10 + Spell Power) / 10, with its specialty.</summary>
    public static int Amount(BattleState battle, BattleSide side, KnownSpell known)
    {
        var amount = (long)known.Spell.Effect!.Amount * (10 + SpellPower(battle, side)) / 10;
        if (battle.Participant(side).Caster is { } caster && caster.Specialty == known.Spell.Id)
        {
            amount = amount * (100 + (SpecialtyPercentPerLevel * caster.Level)) / 100;
        }

        return (int)Math.Min(int.MaxValue, amount);
    }

    /// <summary>
    /// The last round a buff or curse lasts on the stack: the rest of this round plus its rounds × (10 + Spell Power) / 10, cut by the enemy hero's
    /// Spell resistance for a hostile spell (rounded up), at least 1; a spell for the battle lasts to its end.
    /// </summary>
    public static int UntilRound(BattleState battle, BattleSide side, KnownSpell known, BattleUnit unit)
    {
        if (known.Spell.Effect!.Rounds is not { } rounds)
        {
            return BattleEngine.MaxRounds;
        }

        var duration = Math.Max(1, rounds * (10 + SpellPower(battle, side)) / 10);
        if (known.Spell.Effect.IsHostile && unit.Side != side)
        {
            var resistance = battle.Participant(unit.Side).Bonus.SpellResistancePercent;
            duration = Math.Max(1, ((duration * (100 - resistance)) + 99) / 100);
        }

        return battle.Round + duration;
    }
}
