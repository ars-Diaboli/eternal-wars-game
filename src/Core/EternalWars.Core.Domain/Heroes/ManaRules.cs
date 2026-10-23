using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>What a school attunement skill adds at one level (ADR-063).</summary>
/// <param name="MaxMana">Added to the mana pool.</param>
/// <param name="DailyMana">Added to the mana regained every day.</param>
/// <param name="CostReduction">Taken off the mana cost of every spell.</param>
public readonly record struct AttunementBonus(int MaxMana = 0, int DailyMana = 0, int CostReduction = 0)
{
    public static AttunementBonus operator +(AttunementBonus a, AttunementBonus b) =>
        new(a.MaxMana + b.MaxMana, a.DailyMana + b.DailyMana, a.CostReduction + b.CostReduction);
}

/// <summary>
/// Mana rules (ADR-063, ADR-069, draft numbers): the pool is 10 × Knowledge plus attunement bonuses plus 10 × ⌈level / 10⌉; every day a
/// hero regains ⌈Knowledge / 10⌉ plus attunement bonuses; a spell never costs less than a quarter of its base cost, rounded up.
/// Casting does not exist yet (Phase 5); these are the numbers it will use.
/// </summary>
public static class ManaRules
{
    public const int ManaPerKnowledge = 10;

    /// <summary>The pool grows by this much for every started ten hero levels (ADR-069; the maximum only, ADR-072, MAG-8).</summary>
    public const int ManaPerTenLevels = 10;

    /// <summary>The cheapest a spell gets, in percent of its base cost.</summary>
    public const int MinimumCostPercent = 25;

    /// <summary>The attunement skill of a town's school (<c>magic.haven.attunement</c>).</summary>
    public static string AttunementSkill(string town) => $"{BuiltInSkills.SchoolGroup(town)}.attunement";

    /// <summary>
    /// ADR-069's table: Arcana / Chaos +10 pool and +1 daily mana a level; Light +2 daily mana and −1 spell cost; Death +2 daily mana (and
    /// Necromancy, <see cref="Battles.NecromancyRules"/>); Nature / Shadow +5 pool and +2 daily mana.
    /// </summary>
    public static AttunementBonus Attunement(string town, SkillLevel level)
    {
        var n = (int)level;
        if (n <= 0)
        {
            return default;
        }

        return town switch
        {
            TownIds.Academy or TownIds.Inferno => new(MaxMana: 10 * n, DailyMana: n),
            TownIds.Haven => new(DailyMana: 2 * n, CostReduction: n),
            TownIds.Necropolis => new(DailyMana: 2 * n),
            TownIds.Sylvan or TownIds.Dungeon => new(MaxMana: 5 * n, DailyMana: 2 * n),
            _ => default,
        };
    }

    /// <summary>The sum of the hero's attunement skills.</summary>
    public static AttunementBonus Bonus(Hero hero) =>
        BuiltInSkills.Schools.Keys.Aggregate(default(AttunementBonus), (total, town) => total + Attunement(town, hero.SkillLevelOf(AttunementSkill(town))));

    public static int MaxMana(Hero hero) => (ManaPerKnowledge * hero.TotalKnowledge) + Bonus(hero).MaxMana + LevelMana(hero.Level);

    /// <summary>Mana by level: +10 at levels 1–10, +20 at 11–20, and so on (every hero has at least 10).</summary>
    public static int LevelMana(int level) => ManaPerTenLevels * ((Math.Max(1, level) + 9) / 10);

    /// <summary>Mana regained each day: ⌈Knowledge / 10⌉ plus attunement.</summary>
    public static int DailyMana(Hero hero) => (hero.TotalKnowledge + 9) / 10 + Bonus(hero).DailyMana;

    /// <summary>Mana cost of a spell for this hero: attunement takes it down, but never below 25 % of the base, rounded up.</summary>
    public static int SpellCost(Hero hero, int baseCost)
    {
        var floor = (baseCost * MinimumCostPercent + 99) / 100;
        return Math.Max(floor, baseCost - Bonus(hero).CostReduction);
    }
}
