using EternalWars.Core.Domain.Events;

namespace EternalWars.Core.Domain.Battles;

/// <summary>
/// A human player's auto combat setting (ADR-090, CMB-10), for battles against computer players and neutrals: fight by hand, ask before each
/// battle, or always see auto combat's outcome first. Accepting the outcome is quick combat; rejecting it fights the battle by hand.
/// </summary>
public enum AutoCombatMode
{
    Off,
    Ask,
    Always,
}

/// <summary>How the current battle would end with auto combat (<see cref="BattleService.Preview"/>), like the result screen.</summary>
/// <param name="Winner">The side that would win.</param>
/// <param name="Reason">How the battle would end (a computer hero may retreat).</param>
/// <param name="Casualties">What each side would lose.</param>
/// <param name="Experience">The winning hero's experience (0 when no hero wins).</param>
/// <param name="Raised">The undead the winning hero's Necromancy would raise.</param>
/// <param name="Artifacts">The defeated hero's artifacts, which the winning hero would take (best first; a full backpack loses the rest).</param>
public sealed record AutoCombatPreview(
    BattleSide Winner,
    BattleEndReason Reason,
    IReadOnlyList<BattleCasualty> Casualties,
    int Experience,
    IReadOnlyList<RaisedUndead> Raised,
    IReadOnlyList<string> Artifacts);
