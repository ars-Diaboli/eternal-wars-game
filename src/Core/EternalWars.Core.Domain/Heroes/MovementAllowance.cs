namespace EternalWars.Core.Domain.Heroes;

/// <summary>
/// Daily adventure-map movement points, HoMM3-style: a base set by the slowest creature in the army,
/// then percentage bonuses (Logistics-type skills) and flat bonuses (boots-type artifacts).
/// Computed at the start of each of the owner's turns.
/// </summary>
public static class MovementAllowance
{
    /// <summary>Base points when the hero has no army (until armies exist in Phase 2, always).</summary>
    public const int NoArmyBase = 1500;

    /// <summary>HoMM3 table: slowest speed ≤ 3 → 1500 … ≥ 11 → 2000.</summary>
    public static int BaseForSlowestSpeed(int? slowestSpeed) => slowestSpeed switch
    {
        null or <= 3 => 1500,
        4 => 1560,
        5 => 1630,
        6 => 1700,
        7 => 1760,
        8 => 1830,
        9 => 1900,
        10 => 1960,
        _ => 2000,
    };

    public static int Daily(int? slowestSpeed, int bonusPercent = 0, int flatBonus = 0) =>
        (BaseForSlowestSpeed(slowestSpeed) * (100 + bonusPercent) / 100) + flatBonus;

    public static int Daily(Hero hero) => Daily(hero.SlowestCreatureSpeed, hero.MovementBonusPercent, hero.MovementBonusFlat);
}
