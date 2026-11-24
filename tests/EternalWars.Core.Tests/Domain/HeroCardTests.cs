using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The selected hero's card on the adventure map (ADR-309): stats, the three bars, morale, luck and the seven slots.</summary>
public class HeroCardTests
{
    [Fact]
    public void TheCard_ShowsTheStatsWithBonuses_TheBars_AndSevenSlots()
    {
        var state = Create("A...................", "...................B");
        Start(state);
        var hero = state.GetHero(A);
        hero.Morale = 2;
        hero.Luck = -1;
        hero.Bonus = ArtifactBonuses.None with { Attack = 3 };
        hero.MovementPoints = hero.MaxMovementPoints / 2;
        hero.Army.Set(3, new CreatureStack(BuiltInCreatures.Archer, 4));

        var card = HeroCard.Of(hero);

        Assert.Equal((hero.Name, hero.Level, hero.PortraitHero), (card.Name, card.Level, card.Portrait));
        Assert.Equal((hero.Attack + 3, hero.Defense, hero.SpellPower, hero.Knowledge), (card.Attack, card.Defense, card.SpellPower, card.Knowledge));
        Assert.Equal(new Gauge(hero.MaxMovementPoints / 2, hero.MaxMovementPoints), card.Movement);
        Assert.Equal(new Gauge(hero.Mana, hero.MaxMana), card.Mana);
        Assert.Equal((2, -1), (card.Morale, card.Luck));
        Assert.Equal(Army.SlotCount, card.Slots.Count);
        Assert.Equal([0, 3], card.Slots.Select((s, i) => (s, i)).Where(x => x.s is not null).Select(x => x.i));
        Assert.Null(card.Slots[6]);
    }

    [Fact]
    public void TheSlots_AreASnapshot()
    {
        var state = Create("A...................", "...................B");
        Start(state);
        var hero = state.GetHero(A);
        var card = HeroCard.Of(hero);

        hero.Army.Set(5, new CreatureStack(BuiltInCreatures.Archer, 4));

        Assert.Null(card.Slots[5]);
    }

    [Theory]
    [InlineData(0, 0, 1000)] // level 1: 0 of the 1000 to level 2
    [InlineData(1500, 500, 1000)] // half-way from level 2 (1000) to level 3 (2000)
    [InlineData(1999, 999, 1000)]
    [InlineData(2000, 0, 1200)] // level 3: 2000 to 3200
    public void TheExperienceBar_FillsWithinTheLevel(int experience, int value, int max)
    {
        var state = Create("A...................", "...................B");
        Start(state);
        var hero = state.GetHero(A);
        hero.Experience = experience;
        hero.Level = ExperienceTable.LevelFor(experience);

        var bar = HeroCard.ExperienceOf(hero);

        Assert.Equal(new Gauge(value, max), bar);
        Assert.Equal(TrainingRules.ExperienceToNextLevel(hero), max - value);
    }

    [Fact]
    public void TheExperienceBar_IsFull_AtTheLastLevel()
    {
        var state = Create("A...................", "...................B");
        Start(state);
        var hero = state.GetHero(A);
        hero.Experience = int.MaxValue / 2;
        hero.Level = ExperienceTable.LevelFor(hero.Experience);

        Assert.Equal(1.0, HeroCard.ExperienceOf(hero).Fraction);
    }

    [Fact]
    public void TheExperienceBar_IsFull_WhileLevelUpsWait()
    {
        var state = Create("A...................", "...................B");
        Start(state);
        var hero = state.GetHero(A);
        hero.Experience = 2500; // level 3 by experience, the choices for levels 2 and 3 not yet made

        Assert.Equal((1, 2), (hero.Level, hero.PendingLevelUps));
        Assert.Equal(new Gauge(1000, 1000), HeroCard.ExperienceOf(hero));
    }

    [Theory]
    [InlineData(5, 10, 0.5)]
    [InlineData(15, 10, 1.0)] // extra movement or a Mana Vortex fills the bar
    [InlineData(0, 10, 0.0)]
    [InlineData(3, 0, 0.0)]
    public void AGauge_IsAFractionFromZeroToOne(int value, int max, double fraction) =>
        Assert.Equal(fraction, new Gauge(value, max).Fraction);
}
