using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The second catalogue's buffs, mana sites, power-ups and the Mage's Chest (ADR-069, ADR-070).</summary>
public class SecondCatalogueTests
{
    // A 2×2 site at (2, 0) has its entrance at (3, 1); hero A starts just below.
    private static (GameSession Session, T Site) World<T>(string definition)
        where T : MapObject
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        var site = Add<T>(state, definition, 2, 0);
        return (Start(state), site);
    }

    private static void Visit(GameSession session, MapObject site, HeroId hero, int? day = null)
    {
        if (day is { } d)
        {
            session.State.Date = new GameDate(d);
        }

        session.Interactions.Visit(session.State.GetHero(hero), site);
    }

    [Theory]
    [InlineData(BuiltInMapObjects.CloverField, 1, 0, 0)]
    [InlineData(BuiltInMapObjects.Rainbow, 2, 0, 0)]
    [InlineData(BuiltInMapObjects.RallyFlag, 1, 1, BuffRules.RallyFlagMovement)]
    public void TheNewBuffs_GiveLuckMoraleAndMovement_ForTheNextBattle(string definition, int luck, int morale, int movement)
    {
        var (session, site) = World<BuffSite>(definition);
        var hero = session.State.GetHero(A);
        var points = hero.MovementPoints;

        Visit(session, site, A);
        Visit(session, site, A); // once a day

        Assert.Equal((luck, morale, points + movement), (hero.TotalLuck, hero.TotalMorale, hero.MovementPoints));
        Assert.All(hero.Statuses, s => Assert.Equal(StatusExpiry.NextBattle, s.Expires));
    }

    [Fact]
    public void TheManaVortex_DoublesTheMaximum_UntilSpent_OnceAWeekPerHero()
    {
        var (session, vortex) = World<BuffSite>(BuiltInMapObjects.ManaVortex);
        var hero = session.State.GetHero(A);
        hero.Knowledge = 3;
        var max = hero.MaxMana;
        Assert.True(max > 0);

        Visit(session, vortex, A);
        Assert.Equal(2 * max, hero.Mana);

        hero.Mana -= max / 2;
        Visit(session, vortex, A, day: 3); // the same week: nothing
        Assert.Equal(2 * max - (max / 2), hero.Mana);
        Visit(session, vortex, A, day: 8);
        Assert.Equal(2 * max, hero.Mana);
    }

    [Fact]
    public void TheFountainOfMagic_GivesHalfTheMaximumBack_UpToIt()
    {
        var (session, fountain) = World<BuffSite>(BuiltInMapObjects.FountainOfMagic);
        var hero = session.State.GetHero(A);
        hero.Knowledge = 4;
        var max = hero.MaxMana;
        hero.Mana = 0;

        Visit(session, fountain, A);
        Assert.Equal(max / 2, hero.Mana);
        Visit(session, fountain, A, day: 2); // weekly per hero
        Assert.Equal(max / 2, hero.Mana);
        Visit(session, fountain, A, day: 8);
        Assert.Equal(max, hero.Mana); // not above the maximum

        Assert.Equal(2 * max, BuffRules.ManaAfter(BuffRule.FountainOfMagic, 2 * max, max)); // never lowers a vortex's mana
    }

    [Theory]
    [InlineData(BuiltInMapObjects.TrainingGrounds, PrimaryStat.Attack)]
    [InlineData(BuiltInMapObjects.MarlettoTower, PrimaryStat.Defense)]
    [InlineData(BuiltInMapObjects.StarAxis, PrimaryStat.SpellPower)]
    [InlineData(BuiltInMapObjects.GardenOfRevelation, PrimaryStat.Knowledge)]
    public void TheOneStatSites_RaiseTheirStatByOne_OncePerHero(string definition, PrimaryStat stat)
    {
        var (session, site) = World<TrainingSite>(definition);
        var hero = session.State.GetHero(A);
        var before = hero.Stat(stat);

        session.Step(A, Direction.North);
        Visit(session, site, A, day: 20);

        Assert.Null(session.State.PendingDecision);
        Assert.Equal(before + 1, hero.Stat(stat));
    }

    [Fact]
    public void ADreamTeacher_GivesFiveHundred_PlusFiveHundredForEveryOtherOneTheHeroLearntAt()
    {
        var (session, first) = World<TrainingSite>(BuiltInMapObjects.DreamTeacher);
        var second = Add<TrainingSite>(session.State, BuiltInMapObjects.DreamTeacher, 5, 0);
        var third = Add<TrainingSite>(session.State, BuiltInMapObjects.DreamTeacher, 5, 3);
        var hero = session.State.GetHero(A);

        Visit(session, first, A);
        Assert.Equal(500, hero.Experience);
        Visit(session, second, A);
        Assert.Equal(500 + 1000, hero.Experience);
        Visit(session, second, A); // once per hero
        Visit(session, third, A);
        Assert.Equal(500 + 1000 + 1500, hero.Experience);

        Visit(session, third, C); // another hero starts at 500
        Assert.Equal(500, session.State.GetHero(C).Experience);
    }

    [Fact]
    public void TheMagesChest_FollowsItsOdds_AndVanishes()
    {
        var rolls = Enumerable.Range(1, 2000).Select(seed => LootRules.Roll(LootRule.MagesChest, new GameRandom((ulong)seed))).ToList();

        Assert.InRange(rolls.Count(r => r.Artifact is null), 50, 160); // 5 %
        Assert.InRange(rolls.Count(r => r.Artifact == ArtifactTier.Major), 50, 160); // 5 %
        Assert.InRange(rolls.Count(r => r.Artifact == ArtifactTier.Minor && r.Resources.IsEmpty), 1080, 1320); // 60 %
        var withGold = rolls.Where(r => r.Artifact == ArtifactTier.Minor && !r.Resources.IsEmpty).ToList();
        Assert.InRange(withGold.Count, 500, 700); // 30 %
        Assert.All(withGold, r => Assert.Equal(r.Resources, new ResourceSet(Gold: r.Resources.Gold)));
        Assert.All(withGold, r => Assert.True(r.Resources.Gold is >= 500 and <= 1000 && r.Resources.Gold % 100 == 0));
        Assert.Equal(1000, withGold.Max(r => r.Resources.Gold));

        var state = Create("...", ".A.");
        var chest = Add<LootSite>(state, BuiltInMapObjects.MagesChest, 1, 0);
        var session = Start(state);
        var found = new List<LootFound>();
        session.Events.Subscribe<LootFound>(found.Add);
        session.Step(A, Direction.North);
        Assert.Single(found);
        Assert.Null(state.FindObject(chest.Id));
    }

    [Fact]
    public void PortalsAreCalledPortals_TheIdsStay()
    {
        Assert.Equal("Portal", MapObjectCatalog.Default.Get(BuiltInMapObjects.TwoWayMonolith).Name);
        Assert.Equal("One-way portal", MapObjectCatalog.Default.Get(BuiltInMapObjects.OneWayMonolithEntrance).Name);
        Assert.Equal("monolith.two-way", BuiltInMapObjects.TwoWayMonolith);
    }
}
