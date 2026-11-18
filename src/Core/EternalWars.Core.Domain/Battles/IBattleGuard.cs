namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// A map object whose troops a hero fights (<see cref="BattleParticipant.Guard"/> of the defending side): a monster, a mine's or a dwelling's
/// guard, a creature bank, a guarded Pandora's Box, a town's garrison without a hero, a map garrison. It says what the end of the battle does to
/// it, and changes nothing itself: <see cref="BattleAftermath"/> asks it once the battle is over and <see cref="BattleGuardService"/> applies the
/// answer (ADR-216).
/// </summary>
internal interface IBattleGuard
{
    /// <summary>The guard won, or its attacker fled or surrendered: what it keeps.</summary>
    GuardOutcome Survived();

    /// <summary>The guard lost: it leaves the map, falls to the winner or pays out.</summary>
    GuardOutcome Defeated();
}
