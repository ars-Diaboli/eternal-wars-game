using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// What one blow, shot or damage spell would do to a stack (ADR-314), for the status bar: the lowest and highest damage, the fewest and most
/// creatures those kill, and whether the target returns the blow. It is the blow at the attacker's count now: a first strike answered before it
/// or a wall crossed on the way can make it weaker; a martyr's share of the damage (ADR-123) is not taken off.
/// </summary>
/// <param name="MinDamage">The damage of the lowest roll (a damage spell's is fixed: both ends are the same).</param>
/// <param name="MaxDamage">The damage of the highest roll.</param>
/// <param name="MinKills">The creatures <paramref name="MinDamage"/> kills.</param>
/// <param name="MaxKills">The creatures <paramref name="MaxDamage"/> kills.</param>
/// <param name="StrikesBack">Whether the target strikes or shoots back.</param>
public sealed record AttackEstimate(int MinDamage, int MaxDamage, int MinKills, int MaxKills, bool StrikesBack)
{
    /// <summary>
    /// A melee blow of <paramref name="attacker"/> at <paramref name="target"/>, struck from a cell next to it. The target strikes back by the
    /// engine's rule (<see cref="Retaliation.StrikesBack"/>), unless it has no first strike to answer before the blow and the weakest blow surely
    /// kills it or it is a copy the blow makes vanish (ADR-152).
    /// </summary>
    public static AttackEstimate Melee(BattleState battle, BattleUnit attacker, BattleUnit target)
    {
        var (min, max) = Range(battle, attacker, target, isRanged: false, penaltyEighths: 0);
        return Of(target, min, max, Retaliation.StrikesBack(target, attacker) && (Retaliation.AnswersFirst(target, attacker) || Outlives(target, min)));
    }

    /// <summary>
    /// A shot of <paramref name="shooter"/> at <paramref name="target"/> with its range and cover penalty; null when it cannot shoot it. The target
    /// shoots back by the engine's rule (<see cref="Retaliation.ShootsBack"/>) unless the weakest shot surely kills it.
    /// </summary>
    public static AttackEstimate? Shot(BattleState battle, BattleUnit shooter, BattleUnit target)
    {
        if (!BattleGrid.CanShoot(battle, shooter, target, out var penalty))
        {
            return null;
        }

        var (min, max) = Range(battle, shooter, target, isRanged: true, penalty);
        return Of(target, min, max, Retaliation.ShootsBack(battle, target, shooter, out _) && Outlives(target, min));
    }

    /// <summary>
    /// A damage spell of <paramref name="side"/> aimed at <paramref name="aim"/> or <paramref name="cell"/>, as it would hit <paramref name="unit"/>
    /// (its splash and chain hits included, <see cref="BattleMagic.DamagePlan"/>); null when the spell is not a damage spell or misses the stack.
    /// Nothing strikes back at a spell.
    /// </summary>
    public static AttackEstimate? Spell(
        BattleState battle, BattleSide side, KnownSpell known, int? aim, CellPosition? cell, BattleUnit unit, int? creaturePower = null)
    {
        if (known.Effect is not { Kind: Magic.SpellEffectKind.Damage })
        {
            return null;
        }

        foreach (var (hit, damage) in BattleMagic.DamagePlan(battle, side, known, aim, cell, creaturePower))
        {
            if (hit == unit)
            {
                return Of(unit, damage, damage, strikesBack: false);
            }
        }

        return null;
    }

    /// <summary>The creatures <paramref name="damage"/> would kill in <paramref name="target"/>: its top creature takes it first (as the engine's wounds do).</summary>
    public static int Kills(BattleUnit target, int damage)
    {
        var remaining = (long)target.TotalHealth - damage;
        if (remaining <= 0)
        {
            return target.Count;
        }

        var health = target.Creature.Health;
        return target.Count - (int)((remaining + health - 1) / health);
    }

    /// <summary>
    /// The damage of the creature's lowest and highest roll, without luck (<see cref="DamageCalculator.Damage"/>); Bless and Curse fix the roll
    /// (<see cref="DamageCalculator.Roll"/>).
    /// </summary>
    private static (int Min, int Max) Range(BattleState battle, BattleUnit attacker, BattleUnit target, bool isRanged, int penaltyEighths)
    {
        int At(int roll) => DamageCalculator.Damage(battle, attacker, target, new HitModifiers(DamageCalculator.Roll(attacker, roll), isRanged, penaltyEighths));
        return (At(attacker.Creature.MinDamage), At(attacker.Creature.MaxDamage));
    }

    /// <summary>Whether the target is still there after the weakest blow: not all killed, and not a copy on its last hit.</summary>
    private static bool Outlives(BattleUnit target, int minDamage) => Kills(target, minDamage) < target.Count && !(target.IsCopy && target.CopyHits <= 1);

    private static AttackEstimate Of(BattleUnit target, int min, int max, bool strikesBack) =>
        new(min, max, Kills(target, min), Kills(target, max), strikesBack);
}
