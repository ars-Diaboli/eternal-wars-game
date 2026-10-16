using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Primary stat growth (ADR-055): one point per level, HoMM3-style odds by class.</summary>
public class StatGrowthTests
{
    [Theory]
    [InlineData(HeroClass.Warrior, 2)]
    [InlineData(HeroClass.Warrior, 10)]
    [InlineData(HeroClass.Mage, 9)]
    [InlineData(HeroClass.Mage, 25)]
    public void Odds_SumToAHundred_AndRollsFollowThem(HeroClass heroClass, int level)
    {
        var odds = StatGrowth.Odds(heroClass, level);
        Assert.Equal(100, odds.Sum());

        var random = new GameRandom(5);
        var counts = new int[4];
        const int rolls = 10_000;
        for (var i = 0; i < rolls; i++)
        {
            counts[(int)StatGrowth.Roll(heroClass, level, random)]++;
        }

        for (var stat = 0; stat < 4; stat++)
        {
            Assert.InRange(100.0 * counts[stat] / rolls, odds[stat] - 2, odds[stat] + 2);
        }
    }

    [Fact]
    public void Warriors_LeanToAttackAndDefense_Mages_ToSpellPowerAndKnowledge_LaterLevelsEvenOut()
    {
        int Might(HeroClass c, int level) => StatGrowth.Odds(c, level)[0] + StatGrowth.Odds(c, level)[1];

        Assert.True(Might(HeroClass.Warrior, 2) > 50);
        Assert.True(Might(HeroClass.Mage, 2) < 50);
        Assert.True(Might(HeroClass.Warrior, 10) < Might(HeroClass.Warrior, 9));
        Assert.True(Might(HeroClass.Mage, 10) > Might(HeroClass.Mage, 9));
    }

    [Fact]
    public void LevelUp_ShowsTheStatInThePrompt_AndRaisesItByOne()
    {
        var state = Create("A..", "..B");
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 1, 0);
        chest.FixedValues = new ChestValues(0, 1000); // level 2
        var session = Start(state);
        var hero = state.GetHero(A);
        var levelUps = new List<HeroLeveledUp>();
        session.Events.Subscribe<HeroLeveledUp>(levelUps.Add);

        session.MoveTo(A, chest.VisitPosition);
        session.ResolveChest(TreasureChestReward.Experience);
        var decision = Assert.IsType<LevelUpDecision>(state.PendingDecision);
        var before = hero.Stat(decision.Stat);
        var total = Total(hero);

        session.Execute(new ChooseSkillCommand(Blue, decision.Options[0])).ThrowIfRejected();

        Assert.Equal(before + 1, hero.Stat(decision.Stat));
        Assert.Equal(total + 1, Total(hero));
        Assert.Equal(new HeroLeveledUp(A, 2, decision.Options[0], SkillLevel.Basic, decision.Stat, before + 1), Assert.Single(levelUps));
    }

    [Fact]
    public void ComputerHeroes_GainAStatPerLevel_Too()
    {
        var hero = new Hero(A, "Bot", Blue, new MapPosition(0, 0), DefaultArmy());
        var state = new GameState(new AdventureMap(4, 1), [new Player(Blue, "Blue", isHuman: false)], [hero]);
        var session = Start(state);

        hero.Experience = ExperienceTable.For(6);
        session.Progression.OfferNext();

        Assert.Equal(5, Total(hero)); // five level-ups from level 1
    }

    [Fact]
    public void RosterStats_AndMapStats_LoadAndRoundTrip()
    {
        var mage = HeroCatalog.Default.Get("haven.ansel");
        Assert.Equal((0, 0, 3, 2), (mage.Attack, mage.Defense, mage.SpellPower, mage.Knowledge));
        Assert.All(HeroCatalog.Default.All, h => Assert.Equal(5, h.Attack + h.Defense + h.SpellPower + h.Knowledge));

        var editor = new MapEditorSession(BlankScenario.Create(12, 10));
        editor.PlaceHero(new PlayerId(0), new MapPosition(2, 2), "Orrin", out var id);
        Assert.Equal(EditResult.Ok, editor.SetHeroStats(id, 1, 2, SkillLevel.None, 3, 4));
        Assert.Equal(EditResult.InvalidValue, editor.SetHeroStats(id, 1, 2, SkillLevel.None, -1, 0));

        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario))));
        var hero = restored.GetHero(id);
        Assert.Equal((1, 2, 3, 4), (hero.Attack, hero.Defense, hero.SpellPower, hero.Knowledge));

        editor.Undo();
        Assert.Equal((0, 0), (editor.Scenario.GetHero(id).SpellPower, editor.Scenario.GetHero(id).Knowledge));
        Assert.Equal(EditResult.Ok, editor.SetHeroStats(id, 5, 5, SkillLevel.None)); // omitted: kept
        Assert.Equal((0, 0), (editor.Scenario.GetHero(id).SpellPower, editor.Scenario.GetHero(id).Knowledge));
    }

    [Fact]
    public void RosterHeroOnAMap_KeepsTheRostersSpellPowerAndKnowledge_WhenTheMapOmitsThem()
    {
        var document = MapConverter.ToDocument(BlankScenario.Create(12, 10)) with
        {
            Heroes = [new HeroPlacementDocument(1, "Ansel", 0, 2, 2, Type: "haven.ansel")],
        };

        var hero = MapConverter.ToGameState(document).GetHero(new HeroId(1));

        Assert.Equal((3, 2), (hero.SpellPower, hero.Knowledge));
    }

    private static int Total(Hero hero) => hero.Attack + hero.Defense + hero.SpellPower + hero.Knowledge;
}
