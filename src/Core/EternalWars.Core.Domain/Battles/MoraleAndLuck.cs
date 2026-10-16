namespace EternalWars.Core.Domain.Battles;

/// <summary>What a unit's morale did this round (ADR-053).</summary>
public enum MoraleEffect
{
    None,

    /// <summary>Good morale: the unit acts again right after its action.</summary>
    ExtraAction,

    /// <summary>Good morale: +50 % initiative this round.</summary>
    Haste,

    /// <summary>Bad morale: the unit loses its turn this round.</summary>
    SkipTurn,

    /// <summary>Bad morale: −50 % initiative this round.</summary>
    Slow,
}

/// <summary>Luck of one hit, from the attacker's point of view (ADR-053).</summary>
public enum HitLuck
{
    None,

    /// <summary>+75 % damage: the attacker's good luck or the defender's bad luck.</summary>
    Lucky,

    /// <summary>−50 % damage: the defender's good luck or the attacker's bad luck.</summary>
    Unlucky,
}

/// <summary>
/// Morale and luck (ADR-053, draft numbers). A unit's value is its creature's plus its hero's, limited to ±<see cref="Limit"/>.
/// Morale is rolled at the start of every round: <see cref="ExtraActionPercent"/> per point for an extra action (good) or
/// a lost turn (bad); otherwise <see cref="InitiativePercent"/> per point for ±50 % initiative. Luck is rolled on every
/// hit by both sides with <see cref="LuckPercent"/> per point; rolls in the attacker's favour and in the defender's
/// cancel out. Nothing is rolled for a value of 0, so battles without morale or luck draw the same random numbers.
/// </summary>
public static class MoraleAndLuck
{
    public const int Limit = 5;

    public const int ExtraActionPercent = 2;

    public const int InitiativePercent = 4;

    public const int LuckPercent = 4;

    public const int LuckyDamagePercent = 175;

    public const int UnluckyDamagePercent = 50;

    public static int Clamp(int value) => Math.Clamp(value, -Limit, Limit);

    /// <summary>Initiative for this round's order after Haste (+50 %) or Slow (−50 %, at least 1).</summary>
    public static int Initiative(int initiative, MoraleEffect effect) => effect switch
    {
        MoraleEffect.Haste => initiative * 3 / 2,
        MoraleEffect.Slow => Math.Max(1, initiative / 2),
        _ => initiative,
    };

    public static int DamagePercent(HitLuck luck) => luck switch
    {
        HitLuck.Lucky => LuckyDamagePercent,
        HitLuck.Unlucky => UnluckyDamagePercent,
        _ => 100,
    };
}
