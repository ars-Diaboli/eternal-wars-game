using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// Whether a stack answers a blow or a shot: the engine's rule, shared with the attack estimate (ADR-314). Night Terrors and Stasis stop the
/// return blow; Vigil makes it unlimited (ADR-123); a shooter with ranged retaliation fires back once a round if it could shoot the attacker now.
/// </summary>
public static class Retaliation
{
    /// <summary>
    /// Whether <paramref name="defender"/> strikes back at <paramref name="attacker"/>'s melee blow when the two stand next to each other (the
    /// engine checks that they do; the estimate assumes it, the attacker stands there to strike).
    /// </summary>
    public static bool StrikesBack(BattleUnit defender, BattleUnit attacker) =>
        defender.IsAlive && attacker.IsAlive && !defender.IsMachine &&
        !attacker.Creature.Has(CapabilityKind.NoEnemyRetaliation) &&
        !defender.Has(Magic.SpellStatus.NoRetaliation | Magic.SpellStatus.Stasis) &&
        (!defender.HasRetaliated || defender.Creature.Has(CapabilityKind.UnlimitedRetaliation) || defender.Has(Magic.SpellStatus.Vigil));

    /// <summary>First strike: the defender's return blow lands before the attacker's blow, unless the attacker has first strike too.</summary>
    public static bool AnswersFirst(BattleUnit defender, BattleUnit attacker) =>
        defender.Creature.Has(CapabilityKind.FirstStrike) && !attacker.Creature.Has(CapabilityKind.FirstStrike);

    /// <summary>Whether <paramref name="defender"/>, shot at by <paramref name="shooter"/>, fires back, and the penalty of its shot.</summary>
    public static bool ShootsBack(BattleState battle, BattleUnit defender, BattleUnit shooter, out int penalty)
    {
        penalty = 0;
        return defender.IsAlive && shooter.IsAlive &&
               defender.Creature.Has(CapabilityKind.RangedRetaliation) &&
               !shooter.Creature.Has(CapabilityKind.NoEnemyRetaliation) &&
               !defender.Has(Magic.SpellStatus.NoRetaliation | Magic.SpellStatus.Blind) &&
               (!defender.HasRetaliated || defender.Creature.Has(CapabilityKind.UnlimitedRetaliation)) &&
               BattleGrid.CanShoot(battle, defender, shooter, out penalty);
    }
}
