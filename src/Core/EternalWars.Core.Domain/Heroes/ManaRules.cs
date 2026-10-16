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
/// Mana rules (ADR-063, draft numbers): the pool is 10 × Knowledge plus attunement bonuses; every day a hero regains
/// ⌈Knowledge / 10⌉ plus attunement bonuses; a spell never costs less than a quarter of its base cost, rounded up.
/// Casting does not exist yet (Phase 5); these are the numbers it will use.
/// </summary>
public static class ManaRules
{
    public const int ManaPerKnowledge = 10;

    /// <summary>The cheapest a spell gets, in percent of its base cost.</summary>
    public const int MinimumCostPercent = 25;

    /// <summary>The attunement skill of a town's school (<c>magic.haven.attunement</c>).</summary>
    public static string AttunementSkill(string town) => $"{BuiltInSkills.SchoolGroup(town)}.attunement";

    /// <summary>Academy/Inferno: pool and daily mana. Haven/Necropolis: daily mana and cheaper spells. Sylvan/Dungeon: both kinds of mana.</summary>
    public static AttunementBonus Attunement(string town, SkillLevel level)
    {
        var n = (int)level;
        if (n <= 0)
        {
            return default;
        }

        return town switch
        {
            TownIds.Academy or TownIds.Inferno => new(MaxMana: new[] { 0, 12, 25, 37, 50 }[n], DailyMana: n),
            TownIds.Haven or TownIds.Necropolis => new(DailyMana: 3 * n, CostReduction: n),
            TownIds.Sylvan or TownIds.Dungeon => new(MaxMana: 5 * n, DailyMana: 2 * n),
            _ => default,
        };
    }

    /// <summary>The sum of the hero's attunement skills.</summary>
    public static AttunementBonus Bonus(Hero hero) =>
        BuiltInSkills.Schools.Keys.Aggregate(default(AttunementBonus), (total, town) => total + Attunement(town, hero.SkillLevelOf(AttunementSkill(town))));

    public static int MaxMana(Hero hero) => (ManaPerKnowledge * hero.TotalKnowledge) + Bonus(hero).MaxMana;

    /// <summary>Mana regained each day: ⌈Knowledge / 10⌉ plus attunement.</summary>
    public static int DailyMana(Hero hero) => (hero.TotalKnowledge + 9) / 10 + Bonus(hero).DailyMana;

    /// <summary>Mana cost of a spell for this hero: attunement takes it down, but never below 25 % of the base, rounded up.</summary>
    public static int SpellCost(Hero hero, int baseCost)
    {
        var floor = (baseCost * MinimumCostPercent + 99) / 100;
        return Math.Max(floor, baseCost - Bonus(hero).CostReduction);
    }
}
