using System.Globalization;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// What a click on the field would do, for the battle's status bar (ADR-314): the subject (the acting stack, or the armed spell) and the detail,
/// "Attack the Wolves: 38–45 damage, 1–2 die; they strike back". A blow, a shot or a damage spell carries its estimate
/// (<see cref="AttackEstimate"/>); a spell that cannot be cast there says why.
/// </summary>
/// <param name="Subject">The acting stack's name, or the armed spell's.</param>
/// <param name="Detail">What the click does, with its estimate or its refusal.</param>
public sealed record BattleHoverLine(string Subject, string Detail)
{
    /// <summary>
    /// The active stack's <paramref name="action"/> where the pointer is (<see cref="BattleTargeting.ActionAt"/>); null when there is none
    /// or no stack acts.
    /// </summary>
    public static BattleHoverLine? Of(BattleState battle, BattleAction? action)
    {
        if (battle.ActiveUnit is not { } active)
        {
            return null;
        }

        var detail = action switch
        {
            MoveAction => "Move here",
            AttackAction attack when battle.FindUnit(attack.Target) is { } target =>
                $"Attack the {target.Creature.Name}: {Describe(AttackEstimate.Melee(battle, active, target), target, ranged: false)}",
            ShootAction shot when battle.FindUnit(shot.Target) is { } target =>
                AttackEstimate.Shot(battle, active, target) is { } estimate
                    ? $"Shoot the {target.Creature.Name}: {Describe(estimate, target, ranged: true)}"
                    : $"Shoot the {target.Creature.Name}",
            ShootStructureAction structure when battle.Siege?.Find(structure.Structure) is { } aimed =>
                $"Fire at the {BattleNarrator.StructureName(aimed.Kind)}",
            AttackStructureAction structure when battle.Siege?.Find(structure.Structure) is { } hit =>
                $"Attack the {BattleNarrator.StructureName(hit.Kind)}",
            _ => null,
        };
        return detail is null ? null : new BattleHoverLine(active.Creature.Name, detail);
    }

    /// <summary>
    /// The armed spell <paramref name="spell"/> aimed at <paramref name="cell"/>: the hero's (<paramref name="creature"/> false) or the active
    /// creature caster's own (ADR-120). The detail is the refusal, else "Cast at the Wolves" with a damage spell's estimate, or "Cast here" at an
    /// empty cell. The target is <see cref="BattleState.SpellTargetAt"/>'s, a fallen stack included. A two-click spell (ADR-151) names its aim
    /// without checking it: its second click decides; on that <paramref name="secondAim"/> it names the second stack, with no estimate. Null
    /// when no stack acts or the spell is not known.
    /// </summary>
    public static BattleHoverLine? OfSpell(BattleState battle, CellPosition cell, string spell, bool creature, bool secondAim = false)
    {
        if (battle.ActiveUnit is not { } active)
        {
            return null;
        }

        var known = creature ? BattleMagic.CreatureSpell(battle, active) : battle.Participant(active.Side).Caster?.Find(new Common.SpellId(spell));
        if (known?.Effect is not { } effect)
        {
            return null;
        }

        var target = battle.SpellTargetAt(cell);
        if (secondAim)
        {
            return new BattleHoverLine(known.Spell.Name, target is null ? "Second aim here" : $"Second aim: the {target.Creature.Name}");
        }

        var refusal = effect.TwoClicks ? null
            : creature ? BattleMagic.CreatureCastRefusal(battle, active, target?.Id, cell)
            : BattleMagic.CastRefusal(battle, active.Side, known.Spell.Id, target?.Id, cell);
        if (refusal is not null)
        {
            return new BattleHoverLine(known.Spell.Name, refusal);
        }

        if (target is null)
        {
            return new BattleHoverLine(known.Spell.Name, "Cast here");
        }

        if (!target.IsAlive)
        {
            return new BattleHoverLine(known.Spell.Name, $"Cast at the fallen {target.Creature.Name}");
        }

        var power = creature ? BattleMagic.CreatureSpellPower(active) : (int?)null;
        var estimate = AttackEstimate.Spell(battle, active.Side, known, target.Id, cell, target, power);
        return new BattleHoverLine(
            known.Spell.Name,
            estimate is null ? $"Cast at the {target.Creature.Name}" : $"Cast at the {target.Creature.Name}: {Describe(estimate, target, ranged: false, spell: true)}");
    }

    /// <summary>
    /// "38–45 damage, 1–2 die; they strike back": the damage range, the kills (<see cref="AttackEstimate.Kills"/>) and, for a blow or a shot,
    /// whether the target answers it.
    /// </summary>
    public static string Describe(AttackEstimate estimate, BattleUnit target, bool ranged, bool spell = false)
    {
        var damage = estimate.MinDamage == estimate.MaxDamage ? Text(estimate.MinDamage) : $"{Text(estimate.MinDamage)}–{Text(estimate.MaxDamage)}";
        var kills = estimate.MinKills == target.Count ? $"all {Text(target.Count)} die"
            : estimate.MaxKills == 0 ? "none die"
            : estimate.MinKills == estimate.MaxKills ? $"{Text(estimate.MaxKills)} {(estimate.MaxKills == 1 ? "dies" : "die")}"
            : $"{Text(estimate.MinKills)}–{Text(estimate.MaxKills)} die";
        var line = $"{damage} damage, {kills}";
        if (spell)
        {
            return line;
        }

        var answer = !estimate.StrikesBack ? "no return blow" : ranged ? "they shoot back" : "they strike back";
        return $"{line}; {answer}";
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);
}
