using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>
/// Daily adventure-map movement points, HoMM3-style: a base set by the slowest creature in the army,
/// then percentage bonuses (Logistics-type skills) and flat bonuses (boots-type artifacts).
/// Computed at the start of each of the owner's turns.
/// </summary>
public static class MovementAllowance
{
    /// <summary>Base points when the hero has no army.</summary>
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

    /// <summary>
    /// Uses the slowest <see cref="CreatureDefinition.MapSpeed"/> in the hero's army (null without one), raised by Troop
    /// management, then Logistics and other percentage bonuses, then flat bonuses (ADR-048).
    /// </summary>
    public static int Daily(Hero hero, CreatureCatalog creatures)
    {
        var speed = SlowestMapSpeed(hero, creatures);
        var percent = hero.MovementBonusPercent + hero.Bonus.MovementPercent + SkillEffects.WithSpecialty(hero, BuiltInSkills.Logistics, SkillEffects.MovementPercent(hero.SkillLevelOf(BuiltInSkills.Logistics)));
        return Daily(speed, percent, hero.MovementBonusFlat + hero.Bonus.MovementFlat + hero.StatusTotal(HeroStatusKind.Movement));
    }

    public static int Daily(GameState state, Hero hero) => Daily(hero, state.Creatures);

    public static int? SlowestMapSpeed(Hero hero, CreatureCatalog creatures) =>
        hero.Army.IsEmpty ? null : hero.Army.Stacks.Min(s => creatures.Get(s.Stack.Creature).MapSpeed);
}
