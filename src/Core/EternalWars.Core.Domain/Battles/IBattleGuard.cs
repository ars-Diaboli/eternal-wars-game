using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// A map object whose troops a hero fights (<see cref="BattleParticipant.Guard"/> of the defending side): a monster, a mine's or a dwelling's
/// guard, a creature bank, a guarded Pandora's Box, a town's garrison without a hero, a map garrison. It says what the end of the battle does to
/// it; <see cref="BattleAftermath"/> asks it once the battle is over.
/// </summary>
internal interface IBattleGuard
{
    /// <summary>The guard won: it keeps what lived (<see cref="BattleGuardOutcome.Survivors"/> or its troops slot by slot).</summary>
    void KeepSurvivors(BattleGuardOutcome outcome);

    /// <summary>The guard lost to <paramref name="winner"/> (the attacking hero): it leaves the map, falls to the winner or pays out.</summary>
    void Defeated(BattleGuardOutcome outcome, Hero? winner);
}
