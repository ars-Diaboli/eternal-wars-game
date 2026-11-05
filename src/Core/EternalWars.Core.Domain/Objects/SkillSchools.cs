using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// One lesson a University or War Academy offers a hero (ADR-126, ADR-139): <see cref="Skill"/> raised to <see cref="Level"/>. A
/// <see cref="NewGroup"/> lesson is the first skill of a group the hero had not learned when the offers were drawn, at Basic.
/// </summary>
public sealed record Lesson(string Skill, SkillLevel Level, bool NewGroup);

/// <summary>Whether a hero can take a lesson now.</summary>
public enum LessonState
{
    /// <summary>The hero can take it (if the player can pay).</summary>
    Open,

    /// <summary>The skill is at the lesson's level or above: taken here, or learned elsewhere since.</summary>
    Learned,

    /// <summary>The skill would start a seventh group (<see cref="HeroSkills.MaxGroups"/>).</summary>
    NoRoom,
}

/// <summary>
/// A University (the magic schools) or a War Academy (the general groups, offense … war machines) on the adventure map (ADR-126, ADR-139). On a
/// hero's first visit it draws that hero's lessons (<see cref="SkillSchoolRules.Draw"/>) and keeps them: walking off and on draws nothing new.
/// The hero on its entrance takes any number of them, each for <see cref="SkillSchoolRules.Price"/>.
/// </summary>
public sealed class SkillSchool(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.SkillSchool)
{
    /// <summary>The University teaches the magic schools; the War Academy the general groups.</summary>
    public bool TeachesMagic => Definition.Id == BuiltInMapObjects.University;

    /// <summary>The lessons drawn for each hero that visited, in the order shown (new groups first).</summary>
    public Dictionary<HeroId, IReadOnlyList<Lesson>> Lessons { get; } = [];
}

public static class SkillSchoolRules
{
    /// <summary>New groups offered per hero.</summary>
    public const int NewGroups = 2;

    /// <summary>Skills of the hero's learned groups offered one level up.</summary>
    public const int Raises = 2;

    /// <summary>What one lesson costs (ADR-126).</summary>
    public static ResourceSet Price { get; } = new(Gold: 1000, Crystal: 5, Mercury: 5, Sulfur: 5);

    /// <summary>The groups a school teaches: the magic schools, or the general groups (never the faction groups).</summary>
    public static IEnumerable<SkillGroup> Groups(bool magic) =>
        BuiltInSkills.Groups.Where(g => !g.IsFaction && g.IsMagic == magic);

    /// <summary>
    /// A hero's lessons (ADR-139): up to <see cref="NewGroups"/> groups it has not learned (each group's first skill not banned, at Basic) and up to
    /// <see cref="Raises"/> skills of its learned groups below their maximum, one level up; each drawn with the level-up weights. New groups are drawn
    /// even when the hero has no room for them (shown as such). Banned skills are left out (ADR-126).
    /// </summary>
    public static IReadOnlyList<Lesson> Draw(Hero hero, string? heroTown, bool magic, GameRandom random, MapBans? bans = null)
    {
        var groups = Groups(magic).ToList();
        var learned = hero.Skills.Groups.ToHashSet(StringComparer.Ordinal);
        var newGroups = groups.Where(g => !learned.Contains(g.Id))
            .Select(g => BuiltInSkills.SkillsOf(g.Id).FirstOrDefault(s => bans?.BansSkill(s.Id) != true))
            .OfType<SkillDefinition>()
            .ToList();
        var raises = groups.Where(g => learned.Contains(g.Id))
            .SelectMany(g => BuiltInSkills.SkillsOf(g.Id))
            .Where(s => hero.Skills.Level(s.Id) < s.MaxLevel && bans?.BansSkill(s.Id) != true)
            .ToList();

        return
        [
            .. Pick(newGroups, NewGroups, hero, heroTown, random).Select(s => new Lesson(s.Id, SkillLevel.Basic, NewGroup: true)),
            .. Pick(raises, Raises, hero, heroTown, random).Select(s => new Lesson(s.Id, hero.Skills.Level(s.Id) + 1, NewGroup: false)),
        ];
    }

    /// <summary>Whether the hero can take the lesson now.</summary>
    public static LessonState StateOf(Hero hero, Lesson lesson) =>
        hero.Skills.Level(lesson.Skill) >= lesson.Level ? LessonState.Learned
        : hero.Skills.CanRaise(lesson.Skill) && hero.Skills.Level(lesson.Skill) == lesson.Level - 1 ? LessonState.Open
        : LessonState.NoRoom;

    /// <summary>The lessons drawn for the hero at this school; empty before its first visit.</summary>
    public static IReadOnlyList<Lesson> LessonsOf(SkillSchool school, HeroId hero) => school.Lessons.GetValueOrDefault(hero) ?? [];

    private static IEnumerable<SkillDefinition> Pick(List<SkillDefinition> candidates, int count, Hero hero, string? heroTown, GameRandom random)
    {
        var pool = candidates.Select(s => (Skill: s, Weight: SkillOffers.Weight(hero, heroTown, s))).ToList();
        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var roll = random.Next(pool.Sum(p => p.Weight));
            var index = 0;
            while (roll >= pool[index].Weight)
            {
                roll -= pool[index].Weight;
                index++;
            }

            yield return pool[index].Skill;
            pool.RemoveAt(index);
        }
    }
}
