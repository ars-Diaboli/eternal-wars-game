using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Core.Domain.Battles;

/// <summary>Everything that modifies one hit, besides the attacker/defender stats.</summary>
/// <param name="BaseRoll">One roll in the creature's damage range, multiplied by the stack size (ADR-033).</param>
/// <param name="IsRanged">A shot (the ranged penalty applies) rather than a melee strike.</param>
/// <param name="PenaltyEighths">The shot's ranged penalty: distance and cover, less Marksmanship (<see cref="BattleGrid.RangedPenaltyEighths"/>).</param>
/// <param name="Luck">Luck of this hit: +75 % or −50 % (ADR-053).</param>
public sealed record HitModifiers(int BaseRoll, bool IsRanged, int PenaltyEighths = 0, HitLuck Luck = HitLuck.None);

/// <summary>
/// HoMM3 damage baseline in integer maths (ADR-033): base roll × stack size; +5 % per point of Attack above
/// Defense (cap +300 %), −2.5 % per point of Defense above Attack (cap −70 %); then capability bonuses, the
/// ranged penalty (distance and cover, ADR-070), the <c>MeleePenalty</c> halving; hero skills (Melee, Archery, Armor) and the preferred
/// creature's +Attack/+Defense (ADR-048); native terrain's +1 Attack/+1 Defense (ADR-068); spells (ADR-110); luck (ADR-053); Ballistics for war machines (ADR-056).
/// Never less than 1.
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// The Attack a stack strikes with: the creature's, its hero's, rage, the preferred creature's bonus, native terrain and spells (the
    /// information window shows it, ADR-154).
    /// </summary>
    public static int AttackOf(BattleState battle, BattleUnit unit) =>
        unit.Creature.Attack + battle.Participant(unit.Side).Attack + unit.RageAttack
        + (battle.Participant(unit.Side).Bonus.Prefers(unit.Creature) ? Heroes.SkillEffects.PreferredAttack : 0)
        + (battle.IsNative(unit) ? BattleState.NativeAttack : 0) + unit.SpellModifiers.Attack;

    /// <summary>The Defense a stack is struck against: as <see cref="AttackOf"/>, with zones and the defend bonus.</summary>
    public static int DefenseOf(BattleState battle, BattleUnit unit)
    {
        var defense = unit.Creature.Defense + battle.Participant(unit.Side).Defense
                      + (battle.Participant(unit.Side).Bonus.Prefers(unit.Creature) ? Heroes.SkillEffects.PreferredDefense : 0)
                      + (battle.IsNative(unit) ? BattleState.NativeDefense : 0) + unit.SpellModifiers.Defense
                      + (battle.Zones.Count > 0 ? BattleZones.Defense(battle, unit) : 0);
        return unit.IsDefending ? defense + DefendBonus(defense) : defense;
    }

    public static int Damage(BattleState battle, BattleUnit attacker, BattleUnit defender, HitModifiers hit)
    {
        var attackerBonus = battle.Participant(attacker.Side).Bonus;
        var defenderBonus = battle.Participant(defender.Side).Bonus;
        var attack = AttackOf(battle, attacker);
        var defense = DefenseOf(battle, defender);

        long damage = (long)hit.BaseRoll * attacker.Count;
        if (attack > defense)
        {
            damage = damage * (100 + Math.Min(300, 5 * (attack - defense))) / 100;
        }
        else if (defense > attack)
        {
            damage = damage * (1000 - Math.Min(700, 25 * (defense - attack))) / 1000;
        }

        var bonusPercent = 0;
        if (attacker.Creature.Has(CapabilityKind.PackHunter))
        {
            bonusPercent += attacker.Creature.CapabilityValue(CapabilityKind.PackHunter) *
                            BattleGrid.EngagingUnits(battle, defender, attacker.Side, except: attacker);
        }

        if (attacker.Creature.Has(CapabilityKind.Relentless))
        {
            bonusPercent += attacker.Creature.CapabilityValue(CapabilityKind.Relentless) * defender.HitsThisRound;
        }

        damage = damage * (100 + bonusPercent) / 100;
        damage = damage * (100 + (hit.IsRanged ? attackerBonus.RangedPercent : attackerBonus.MeleePercent)) / 100;
        damage = damage * (100 - defenderBonus.DamageTakenReductionPercent) / 100;
        if (hit.IsRanged)
        {
            damage = damage * (100 - defenderBonus.RangedDamageTakenReductionPercent) / 100;
        }

        damage = damage * (100 + Spells(attacker.SpellModifiers, defender.SpellModifiers, hit.IsRanged)) / 100;
        damage = damage * MoraleAndLuck.DamagePercent(hit.Luck) / 100;
        if (attacker.IsMachine)
        {
            damage = damage * (100 + WarMachines.DamageBonusPercent(attackerBonus.Ballistics)) / 100;
        }

        if (hit.IsRanged)
        {
            damage = damage * (8 - hit.PenaltyEighths) / 8;
            if (battle.Zones.Count > 0 && BattleZones.DimsShot(battle, attacker, defender))
            {
                damage /= 2; // Darkness (ADR-149): a shot into or out of it
            }
        }
        else if (attacker.Creature.Has(CapabilityKind.MeleePenalty))
        {
            damage /= 2;
        }

        return (int)Math.Clamp(damage, 1, int.MaxValue);
    }

    /// <summary>
    /// Spells (ADR-110): the attacker's melee or ranged percent plus the defender's damage-taken percents, and an attack out of hiding's
    /// <c>ambush%</c> (Unseen III, ADR-177); at least −100.
    /// </summary>
    private static int Spells(Magic.SpellModifiers attacker, Magic.SpellModifiers defender, bool isRanged) =>
        Math.Max(-100, (isRanged ? attacker.RangedPercent + defender.RangedTakenPercent : attacker.MeleePercent + defender.MeleeTakenPercent) + defender.DamageTakenPercent
                       + (attacker.Has(Magic.SpellStatus.Unseen) ? attacker.AmbushPercent : 0));

    /// <summary>Defend: +20 % Defense, at least +1, until the unit's next turn.</summary>
    public static int DefendBonus(int defense) => Math.Max(1, defense / 5);

    /// <summary>Bless rolls the creature's maximum damage, Curse its minimum (ADR-110); otherwise <paramref name="roll"/>.</summary>
    public static int Roll(BattleUnit attacker, int roll) => Math.Sign(attacker.SpellModifiers.Roll) switch
    {
        > 0 => attacker.Creature.MaxDamage,
        < 0 => attacker.Creature.MinDamage,
        _ => roll,
    };

    /// <summary>Damage expected from an average roll; the AI uses it to compare options.</summary>
    public static int Expected(BattleState battle, BattleUnit attacker, BattleUnit defender, bool isRanged, int penaltyEighths = 0)
    {
        var average = Roll(attacker, (attacker.Creature.MinDamage + attacker.Creature.MaxDamage) / 2);
        return Damage(battle, attacker, defender, new HitModifiers(Math.Max(1, average), isRanged, penaltyEighths));
    }
}
