using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Shrines (ADR-068, ADR-120) and a hero's spell book and spells in the map format (v29).</summary>
public class ShrineAndSpellBookTests
{
    private static SpellDefinition FirstOfLevel(GameState state, int level) => ShrineRules.Candidates(state.Spells, level).First();

    // The shrine's entrance is at (3, 1) when placed at (2, 0); hero A starts below it.
    private static (GameSession Session, Shrine Shrine, Hero Hero) World(int level, string? spell, Action<Hero>? setUp = null)
    {
        var state = Create(".............", ".............", "...A.........", ".............", "C...........B");
        var shrine = Add<Shrine>(state, BuiltInMapObjects.ShrineId(level), 2, 0);
        shrine.Spell = spell;
        var hero = state.GetHero(A);
        setUp?.Invoke(hero);
        return (Start(state), shrine, hero);
    }

    [Fact]
    public void AVisitingHeroWithABookAndTheSkill_LearnsTheShrinesSpell_OnlyOnce()
    {
        var probe = Create("A..", "..B");
        var spell = FirstOfLevel(probe, 2);
        var (session, shrine, hero) = World(2, spell.Id, h =>
        {
            h.HasSpellBook = true;
            h.Skills.Set(MagicRules.MagicSkill(spell.School), SkillLevel.Advanced);
        });
        var learned = new List<SpellsLearned>();
        var empty = new List<MapObjectFoundEmpty>();
        session.Events.Subscribe<SpellsLearned>(learned.Add);
        session.Events.Subscribe<MapObjectFoundEmpty>(empty.Add);

        session.Step(A, Direction.North);

        Assert.True(hero.Spells.ContainsKey(spell.Id));
        Assert.Equal([spell.Id], Assert.Single(learned).Spells);
        session.Interactions.Visit(hero, shrine);
        Assert.Single(empty);
    }

    [Fact]
    public void AHeroWithoutABook_OrWithoutTheSkillLevel_IsRefused()
    {
        var probe = Create("A..", "..B");
        var spell = FirstOfLevel(probe, 3);
        var (session, shrine, hero) = World(3, spell.Id, h => h.HasSpellBook = false);
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);

        session.Interactions.Visit(hero, shrine);
        Assert.Contains("spell book", refused[^1].Reason, StringComparison.Ordinal);

        hero.HasSpellBook = true;
        hero.Skills.Set(MagicRules.MagicSkill(spell.School), SkillLevel.Advanced); // level 2 only
        session.Interactions.Visit(hero, shrine);
        Assert.Contains("at level 3", refused[^1].Reason, StringComparison.Ordinal);
        Assert.False(hero.Spells.ContainsKey(spell.Id));
    }

    [Fact]
    public void AnOpenShrine_DrawsASpellOfItsLevel_WhenTheGameStarts()
    {
        for (var level = 1; level <= ShrineRules.Levels; level++)
        {
            var (session, shrine, _) = World(level, null);
            Assert.NotNull(shrine.Spell);
            Assert.Equal(level, session.State.Spells.Get(shrine.Spell!).Level);
        }
    }

    [Fact]
    public void ShrinesAndSpellBooks_AreInTheMapFormat()
    {
        var state = Create(".............", ".............", "...A.........", ".............", "C...........B");
        var spell = FirstOfLevel(state, 1);
        Add<Shrine>(state, BuiltInMapObjects.ShrineId(1), 2, 0).Spell = spell.Id;
        Add<Shrine>(state, BuiltInMapObjects.ShrineId(3), 6, 0);
        var mage = state.GetHero(A);
        mage.HasSpellBook = true;
        mage.Learn(spell.Id);
        state.GetHero(C).HasSpellBook = false;

        var map = MapConverter.ToDocument(state);
        Assert.True(MapDocument.CurrentFormatVersion >= 29);
        var restored = TestFormat.Load(MapSerializer.Serialize(map));

        var shrines = restored.ObjectsOf<Shrine>().OrderBy(s => s.SpellLevel).ToList();
        Assert.Equal([spell.Id, null], shrines.Select(s => s.Spell));
        Assert.Equal([1, 3], shrines.Select(s => s.SpellLevel));
        Assert.True(restored.GetHero(A).HasSpellBook);
        Assert.Equal([spell.Id], restored.GetHero(A).Spells.Keys);
        Assert.False(restored.GetHero(C).HasSpellBook);
        Assert.Empty(restored.GetHero(C).Spells);
    }

    [Fact]
    public void AShrineWithASpellOfAnotherLevel_DoesNotLoad()
    {
        var state = Create("...........", "...A.......", "C.........B");
        Add<Shrine>(state, BuiltInMapObjects.ShrineId(1), 2, 0).Spell = FirstOfLevel(state, 2).Id;
        var map = MapConverter.ToDocument(state);

        Assert.ThrowsAny<Exception>(() => MapConverter.ToGameState(map));
    }

    [Fact]
    public void TheEditor_SetsAShrinesSpellOfItsLevel_AndAHerosBookAndSpells_Undoably()
    {
        var editor = TestEditor.Blank(10, 6);
        editor.Objects.Place(BuiltInMapObjects.ShrineId(2), new(2, 2), out var id);
        var shrine = (Shrine)editor.Scenario.FindObject(id)!;
        var levelTwo = ShrineRules.Candidates(editor.Scenario.Spells, 2).First().Id;
        var levelOne = ShrineRules.Candidates(editor.Scenario.Spells, 1).First().Id;

        Assert.Equal(EditResult.InvalidValue, editor.Contents.SetShrineSpell(id, levelOne));
        Assert.Equal(EditResult.Ok, editor.Contents.SetShrineSpell(id, levelTwo));
        Assert.Equal(levelTwo, shrine.Spell);
        editor.Undo();
        Assert.Null(shrine.Spell);
        Assert.Equal(EditResult.InvalidTarget, editor.Contents.SetShrineSpell(new MapObjectId(999), null));

        var heroState = Create("A..", "..B");
        var scenarioEditor = new MapEditorSession(heroState);
        var hero = heroState.GetHero(A);
        var hadBook = hero.HasSpellBook;
        Assert.Equal(EditResult.InvalidValue, scenarioEditor.Heroes.SetSpells(A, false, [levelOne]));
        Assert.Equal(EditResult.InvalidValue, scenarioEditor.Heroes.SetSpells(A, true, ["no.such-spell"]));
        Assert.Equal(EditResult.InvalidValue, scenarioEditor.Heroes.SetSpells(A, true, [levelOne, levelOne]));
        Assert.Equal(EditResult.Ok, scenarioEditor.Heroes.SetSpells(A, true, [levelOne, levelTwo]));
        Assert.True(hero.HasSpellBook);
        Assert.Equal(new[] { levelOne, levelTwo }.Order(StringComparer.Ordinal), hero.Spells.Keys.Order(StringComparer.Ordinal));
        scenarioEditor.Undo();
        Assert.Equal(hadBook, hero.HasSpellBook);
        Assert.Empty(hero.Spells);
    }

    [Fact]
    public void TheValidator_FlagsAHeroWithSpellsButNoBook()
    {
        var state = Create("A..", "..B");
        var hero = state.GetHero(A);
        hero.HasSpellBook = true;
        hero.Learn(FirstOfLevel(state, 1).Id);
        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Message.Contains("no spell book"));

        hero.HasSpellBook = false;
        Assert.Contains(MapValidator.Validate(state), i => i.Severity == IssueSeverity.Error && i.Message.Contains("no spell book"));
    }
}
