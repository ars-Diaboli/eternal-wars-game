using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>Experience needed per hero level (HoMM3's table, extended by +20 % per level after 12).</summary>
public static class ExperienceTable
{
    private static readonly int[] Thresholds = Build();

    /// <summary>Total experience needed to reach <paramref name="level"/> (level 1 needs 0).</summary>
    public static int For(int level) => level <= 1 ? 0 : Thresholds[Math.Min(level, Thresholds.Length - 1)];

    /// <summary>The level a hero with this much experience has reached.</summary>
    public static int LevelFor(int experience)
    {
        var level = 1;
        while (level + 1 < Thresholds.Length && experience >= Thresholds[level + 1])
        {
            level++;
        }

        return level;
    }

    private static int[] Build()
    {
        // Index = level. HoMM3: 1000, 2000, 3200, 4600, 6200, 8000, 10000, 12200, 14700, 17500, 20600.
        var thresholds = new List<int> { 0, 0, 1000, 2000, 3200, 4600, 6200, 8000, 10000, 12200, 14700, 17500, 20600 };
        while (thresholds.Count <= 75)
        {
            var last = thresholds[^1];
            var step = thresholds[^1] - thresholds[^2];
            thresholds.Add(Math.Min(int.MaxValue / 2, last + (step * 6 / 5)));
        }

        return [.. thresholds];
    }
}

/// <summary>
/// The skills a hero is offered at a level-up (ADR-048): up to <see cref="OfferCount"/> different skills it may raise,
/// drawn from the game RNG with weights by class: warriors lean to the general groups, mages to their own school.
/// Rival schools (<see cref="BuiltInSkills.Rivals"/>) appear only with <see cref="RivalSchoolChance"/> per level-up.
/// </summary>
public static class SkillOffers
{
    public const int OfferCount = 3;

    /// <summary>Percentage chance that one offer of a level-up is a rival town's school.</summary>
    public const int RivalSchoolChance = 5;

    public static IReadOnlyList<string> Generate(Hero hero, string? heroTown, GameRandom random)
    {
        var rivals = heroTown is not null && BuiltInSkills.Rivals.TryGetValue(heroTown, out var r) ? r : [];
        var candidates = BuiltInSkills.All.Where(s => hero.Skills.CanRaise(s.Id) && IsOffered(s, heroTown)).ToList();
        var rivalSkills = candidates.Where(s => IsSchoolOf(s, rivals)).ToList();
        var pool = candidates.Except(rivalSkills).Select(s => (s.Id, Weight: Weight(hero, heroTown, s))).ToList();

        var offers = new List<string>();
        if (rivalSkills.Count > 0 && random.Next(100) < RivalSchoolChance)
        {
            offers.Add(rivalSkills[random.Next(rivalSkills.Count)].Id);
        }

        while (offers.Count < OfferCount && pool.Count > 0)
        {
            var total = pool.Sum(p => p.Weight);
            var roll = random.Next(total);
            var index = 0;
            while (roll >= pool[index].Weight)
            {
                roll -= pool[index].Weight;
                index++;
            }

            offers.Add(pool[index].Id);
            pool.RemoveAt(index);
        }

        return offers;
    }

    /// <summary>
    /// Weights: skills already being learned ×3; own school 6 (mage) / 2 (warrior); the own faction group (Primal Fury) 6 for both, so
    /// warriors learn it soon (ADR-072, HERO-7); other schools 2 / 1; general groups 2 / 4.
    /// </summary>
    private static int Weight(Hero hero, string? heroTown, SkillDefinition skill)
    {
        var group = BuiltInSkills.GetGroup(skill.Group);
        var mage = hero.Class == HeroClass.Mage;
        var weight = group.IsFaction ? 6
            : group.IsMagic ? group.BelongsTo(heroTown) ? (mage ? 6 : 2) : (mage ? 2 : 1)
            : mage ? 2 : 4;
        return hero.Skills.Groups.Contains(skill.Group) ? weight * 3 : weight;
    }

    /// <summary>Faction ability groups are offered only to heroes of their own towns.</summary>
    private static bool IsOffered(SkillDefinition skill, string? heroTown) =>
        BuiltInSkills.GetGroup(skill.Group) is not { IsFaction: true } group || group.BelongsTo(heroTown);

    private static bool IsSchoolOf(SkillDefinition skill, IReadOnlyList<string> towns) =>
        BuiltInSkills.GetGroup(skill.Group).School is { } school && towns.Contains(school);
}
