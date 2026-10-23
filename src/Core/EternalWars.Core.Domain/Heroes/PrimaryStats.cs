using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>A hero's four primary stats (ADR-055). Spell Power and Knowledge take effect with magic (Phase 5).</summary>
public enum PrimaryStat
{
    Attack,
    Defense,
    SpellPower,
    Knowledge,
}

/// <summary>
/// HoMM3-style primary stat growth (ADR-055, draft odds): every level-up raises one primary stat by 1, drawn from the
/// game RNG with odds by class, one table for levels 2–9 and another from level 10 on. Warriors lean to Attack and
/// Defense, mages to Spell Power and Knowledge; later levels even out. Mages of the Primal Fury towns have no spells and use even odds
/// (ADR-073, MAG-10).
/// </summary>
public static class StatGrowth
{
    /// <summary>From this level on the second table applies.</summary>
    public const int LateLevel = 10;

    /// <summary>Percent odds in <see cref="PrimaryStat"/> order (Attack, Defense, Spell Power, Knowledge); each row sums to 100.</summary>
    public static IReadOnlyList<int> Odds(HeroClass heroClass, int level, string? town = null) =>
        (heroClass, level >= LateLevel, BuiltInSkills.IsPrimalFuryTown(town)) switch
        {
            (HeroClass.Mage, _, true) => [25, 25, 25, 25],
            (HeroClass.Warrior, false, _) => [35, 45, 10, 10],
            (HeroClass.Warrior, true, _) => [30, 30, 20, 20],
            (HeroClass.Mage, false, _) => [10, 10, 40, 40],
            _ => [20, 20, 30, 30],
        };

    /// <summary>The stat raised on reaching <paramref name="level"/>; <paramref name="town"/> is the hero's own town.</summary>
    public static PrimaryStat Roll(HeroClass heroClass, int level, GameRandom random, string? town = null)
    {
        var odds = Odds(heroClass, level, town);
        var roll = random.Next(100);
        for (var i = 0; i < odds.Count; i++)
        {
            if (roll < odds[i])
            {
                return (PrimaryStat)i;
            }

            roll -= odds[i];
        }

        return PrimaryStat.Knowledge;
    }
}
