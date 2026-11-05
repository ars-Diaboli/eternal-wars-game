using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The University and the War Academy (ADR-126, ADR-139): lessons drawn per hero on its first visit, kept, bought one by one.</summary>
public class SkillSchoolTests
{
    private static readonly ResourceSet Plenty = new(Gold: 50_000, Crystal: 50, Mercury: 50, Sulfur: 50);

    // The school's entrance is (3, 1) when placed at (2, 0); hero A starts just below it.
    private static (GameSession Session, SkillSchool School) World(string definition, Action<GameState, Hero>? setUp = null)
    {
        var state = Create(".............................", ".............................", "...A.........................", ".............................", "C...........................B");
        var school = Add<SkillSchool>(state, definition, 2, 0);
        setUp?.Invoke(state, state.GetHero(A));
        var session = Start(state);
        state.GetPlayer(Blue).Resources = Plenty;
        return (session, school);
    }

    private static void Clear(Hero hero) => hero.Skills.ReplaceWith(new Dictionary<string, SkillLevel>());

    [Fact]
    public void TheWarAcademy_OffersTwoNewGeneralGroups_AndTwoOfTheHerosGeneralSkillsALevelUp()
    {
        var hero = Create("A....").GetHero(A);
        Clear(hero);
        hero.Skills.Set(BuiltInSkills.Melee, SkillLevel.Basic);
        hero.Skills.Set(BuiltInSkills.Armor, SkillLevel.Master); // at its maximum: never offered
        hero.Skills.Set(BuiltInSkills.SchoolGroup("haven") + ".spells", SkillLevel.Basic); // a school: not the academy's
        for (var seed = 0UL; seed < 30; seed++)
        {
            var lessons = SkillSchoolRules.Draw(hero, "haven", magic: false, new GameRandom(seed));

            Assert.Equal(4, lessons.Count);
            var fresh = lessons.Where(l => l.NewGroup).ToList();
            Assert.Equal(2, fresh.Count);
            Assert.All(fresh, l =>
            {
                var group = BuiltInSkills.GroupOf(l.Skill);
                Assert.False(group.IsMagic || group.IsFaction);
                Assert.DoesNotContain(group.Id, new[] { "offense", "defense" });
                Assert.Equal(BuiltInSkills.SkillsOf(group.Id).First().Id, l.Skill); // the group's first skill
                Assert.Equal(SkillLevel.Basic, l.Level);
            });
            Assert.NotEqual(fresh[0].Skill, fresh[1].Skill);

            var raises = lessons.Where(l => !l.NewGroup).ToList();
            Assert.Equal(2, raises.Count);
            Assert.All(raises, l =>
            {
                Assert.Contains(BuiltInSkills.Get(l.Skill).Group, new[] { "offense", "defense" });
                Assert.NotEqual(BuiltInSkills.Armor, l.Skill);
                Assert.Equal(hero.Skills.Level(l.Skill) + 1, l.Level);
            });
        }
    }

    [Fact]
    public void TheUniversity_OffersOnlyMagicSchools_NeverPrimalFury_AndSkipsBannedSkills()
    {
        var hero = Create("A....").GetHero(A);
        Clear(hero);
        var bans = BuiltInSkills.Schools.Keys.Aggregate(MapBans.None, (b, town) => b.With(MapBanKind.Skill, $"{BuiltInSkills.SchoolGroup(town)}.spells", banned: true));
        for (var seed = 0UL; seed < 30; seed++)
        {
            var lessons = SkillSchoolRules.Draw(hero, "stronghold", magic: true, new GameRandom(seed), bans);

            Assert.Equal(2, lessons.Count); // nothing learned: new groups only
            Assert.All(lessons, l =>
            {
                Assert.True(BuiltInSkills.GroupOf(l.Skill).IsMagic);
                Assert.EndsWith(".attunement", l.Skill, StringComparison.Ordinal); // the spells skill is banned: the next one stands in
            });
        }
    }

    [Fact]
    public void AVisit_DrawsTheHerosLessonsOnce_AndKeepsThem()
    {
        var (session, school) = World(BuiltInMapObjects.WarAcademy, (_, hero) => Clear(hero));
        var drawn = new List<LessonsDrawn>();
        session.Events.Subscribe<LessonsDrawn>(drawn.Add);
        var hero = session.State.GetHero(A);

        session.Step(A, Direction.North);
        var lessons = Assert.Single(drawn).Lessons;
        Assert.Equal(lessons, SkillSchoolRules.LessonsOf(school, A));
        Assert.Equal(SkillSchoolRules.NewGroups, lessons.Count);

        session.Interactions.Visit(hero, school);
        Assert.Single(drawn); // walking off and on draws nothing new
        Assert.Same(lessons, SkillSchoolRules.LessonsOf(school, A));
        Assert.Empty(SkillSchoolRules.LessonsOf(school, C));
    }

    [Fact]
    public void ALesson_CostsGoldAndRareResources_RaisesTheSkill_AndIsThenShownLearned()
    {
        var (session, school) = World(BuiltInMapObjects.University, (_, hero) => Clear(hero));
        var state = session.State;
        var hero = state.GetHero(A);
        var lesson = new Lesson(BuiltInSkills.SchoolGroup("academy") + ".spells", SkillLevel.Basic, NewGroup: true);
        Assert.False(session.Execute(new TakeLessonCommand(Blue, school.Id, lesson.Skill)).IsAccepted); // nobody stands there

        session.Step(A, Direction.North);
        school.Lessons[A] = [lesson];
        var taken = new List<LessonTaken>();
        session.Events.Subscribe<LessonTaken>(taken.Add);
        session.Execute(new TakeLessonCommand(Blue, school.Id, lesson.Skill)).ThrowIfRejected();

        Assert.Equal(SkillLevel.Basic, hero.Skills.Level(lesson.Skill));
        Assert.Equal(Plenty - SkillSchoolRules.Price, state.GetPlayer(Blue).Resources);
        Assert.Equal(new ResourceSet(Gold: 1000, Crystal: 5, Mercury: 5, Sulfur: 5), Assert.Single(taken).Price);
        Assert.Equal(LessonState.Learned, SkillSchoolRules.StateOf(hero, lesson));
        Assert.Contains("already", session.Execute(new TakeLessonCommand(Blue, school.Id, lesson.Skill)).Error, StringComparison.Ordinal);
        Assert.Contains("does not offer", session.Execute(new TakeLessonCommand(Blue, school.Id, BuiltInSkills.Melee)).Error, StringComparison.Ordinal);

        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 50_000);
        school.Lessons[A] = [new Lesson(lesson.Skill, SkillLevel.Advanced, NewGroup: false)];
        Assert.Contains("Not enough", session.Execute(new TakeLessonCommand(Blue, school.Id, lesson.Skill)).Error, StringComparison.Ordinal);
    }

    [Fact]
    public void ANewGroup_PastTheSixGroupLimit_IsShownWithoutRoom_AndRefused()
    {
        var (session, school) = World(BuiltInMapObjects.University, (_, hero) =>
        {
            Clear(hero);
            foreach (var skill in new[] { BuiltInSkills.Melee, BuiltInSkills.Armor, BuiltInSkills.Treasury, BuiltInSkills.Logistics, BuiltInSkills.Tactics, BuiltInSkills.Healing })
            {
                hero.Skills.Set(skill, SkillLevel.Basic);
            }
        });
        var hero = session.State.GetHero(A);

        session.Step(A, Direction.North);
        var lessons = SkillSchoolRules.LessonsOf(school, A);
        Assert.Equal(2, lessons.Count); // still drawn
        Assert.All(lessons, l => Assert.Equal(LessonState.NoRoom, SkillSchoolRules.StateOf(hero, l)));
        Assert.Contains("at most 6", session.Execute(new TakeLessonCommand(Blue, school.Id, lessons[0].Skill)).Error, StringComparison.Ordinal);
    }

    [Fact]
    public void AHeroWithNothingToLearn_IsToldSo()
    {
        var (session, _) = World(BuiltInMapObjects.WarAcademy, (_, hero) =>
        {
            Clear(hero);
            foreach (var skill in BuiltInSkills.All.Where(s => SkillSchoolRules.Groups(magic: false).Any(g => g.Id == s.Group)))
            {
                hero.Skills.Set(skill.Id, skill.MaxLevel);
            }
        });
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);

        session.Step(A, Direction.North);

        Assert.Contains("nothing to teach", Assert.Single(refused).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheAi_GoesToTheAcademy_AndTakesItsLessons()
    {
        var state = Create(".............................", ".............................", "...A.........................", ".............................", "C...........................B");
        var school = Add<SkillSchool>(state, BuiltInMapObjects.WarAcademy, 2, 0);
        Clear(state.GetHero(A));
        state.GetHero(A).Skills.Set(BuiltInSkills.Melee, SkillLevel.Basic);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = Plenty;

        var ai = new AdventureAi(session, controlHumanSlots: true);
        ai.PlayTurn();

        Assert.Equal(0, ai.Refused);
        var lessons = SkillSchoolRules.LessonsOf(school, A);
        Assert.NotEmpty(lessons);
        Assert.Contains(lessons, l => SkillSchoolRules.StateOf(state.GetHero(A), l) == LessonState.Learned);
    }

    [Fact]
    public void TheSchools_AreInTheMapFormat_AndAGameWithALessonReplays()
    {
        var state = Create(".............", ".............", "...A.........", ".............", "C...........B");
        Add<SkillSchool>(state, BuiltInMapObjects.University, 2, 0);
        Add<SkillSchool>(state, BuiltInMapObjects.WarAcademy, 6, 0);
        var map = MapConverter.ToDocument(state);
        var json = MapSerializer.Serialize(map);
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Contains("\"skillSchool\"", json, StringComparison.Ordinal);
        Assert.Equal([true, false], restored.ObjectsOf<SkillSchool>().OrderBy(s => s.Id.Value).Select(s => s.TeachesMagic));

        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 4, Complexity: Complexity.Beginner)));
        session.Start();
        session.Step(A, Direction.North);
        var school = session.State.ObjectsOf<SkillSchool>().Single(s => s.TeachesMagic);
        var lesson = SkillSchoolRules.LessonsOf(school, A).First(l => SkillSchoolRules.StateOf(session.State.GetHero(A), l) == LessonState.Open);
        session.Execute(new TakeLessonCommand(Blue, school.Id, lesson.Skill)).ThrowIfRejected();
        var record = GameRecordSerializer.Serialize(GameReplay.Record(map, session));
        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(record));

        Assert.Contains("\"lesson\"", record, StringComparison.Ordinal);
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }
}
