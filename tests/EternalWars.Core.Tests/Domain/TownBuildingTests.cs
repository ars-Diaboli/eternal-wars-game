using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The per-town building rows (ADR-112): growth buildings, the war lodge of Stronghold and Swamp Bestiary, level-0 rows.</summary>
public class TownBuildingTests
{
    /// <summary>A town at (0, 0) has its entrance at (3, 6); hero A stands on it.</summary>
    private static readonly string[] Rows =
    [
        "............",
        "............",
        "............",
        "............",
        "............",
        "............",
        "...A........",
        "............",
        "............",
        "...........B",
    ];

    private static readonly ResourceSet Rich = new(Gold: 100_000, Lumber: 100, Ore: 100, Crystal: 50, Mercury: 50, Sulfur: 50);

    [Fact]
    public void EveryPlayableTown_HasTwoNamedGrowthBuildings_ForTiersOneAndTwo()
    {
        foreach (var type in new[] { TownIds.Haven, TownIds.Sylvan, TownIds.Academy, TownIds.Necropolis, TownIds.Inferno, TownIds.Dungeon, TownIds.Stronghold, TownIds.SwampBestiary })
        {
            var buildings = BuildingCatalog.Default.For(type);
            Assert.Equal(new BuildingGrowth(1, 5), buildings.Get(BuiltInBuildings.Growth(1), 1).Growth);
            Assert.Equal(new BuildingGrowth(2, 2), buildings.Get(BuiltInBuildings.Growth(2), 1).Growth);
            Assert.Contains(new BuildingRequirement(BuiltInBuildings.Dwelling(1), 1), buildings.Get(BuiltInBuildings.Growth(1), 1).Requires);
        }

        Assert.NotEqual(BuildingCatalog.Default.For(TownIds.Haven).Get(BuiltInBuildings.Growth(1), 1).Name, BuildingCatalog.Default.For(TownIds.Inferno).Get(BuiltInBuildings.Growth(1), 1).Name);
        Assert.False(BuildingCatalog.Default.Contains(BuiltInBuildings.Growth(1))); // no shared row
    }

    [Fact]
    public void GrowthBuilding_AddsAFlatFiveToEveryTierOneDwelling_AfterTheFortBonus()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Fort, 2); // Citadel +50 %
        town.SetDwelling(BuiltInCreatures.Footman, 1);
        town.SetDwelling(BuiltInCreatures.Pikeman, 1);
        var footman = state.Creatures.Get(BuiltInCreatures.Footman);
        var pikeman = state.Creatures.Get(BuiltInCreatures.Pikeman);
        var zealot = state.Creatures.Get(state.Creatures.BaseCreaturesOf(TownIds.Haven).First(c => c.Tier == 2).Id);
        var (f, p, z) = (TownRules.WeeklyGrowth(state, town, footman), TownRules.WeeklyGrowth(state, town, pikeman), TownRules.WeeklyGrowth(state, town, zealot));

        town.SetLevel(BuiltInBuildings.Growth(1), 1);
        Assert.Equal((f + 5, p + 5, z), (TownRules.WeeklyGrowth(state, town, footman), TownRules.WeeklyGrowth(state, town, pikeman), TownRules.WeeklyGrowth(state, town, zealot)));
        Assert.Equal(footman.WeeklyGrowth + (footman.WeeklyGrowth / 2) + 5, TownRules.WeeklyGrowth(state, town, footman));

        town.SetLevel(BuiltInBuildings.Growth(2), 1);
        Assert.Equal(z + 2, TownRules.WeeklyGrowth(state, town, zealot));
    }

    [Fact]
    public void GrowthBuilding_NeedsADwellingOfItsTier_AndIsBuiltLikeAnyOther()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Inferno, 0, 0, Blue);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = Rich;
        var imp = state.Creatures.BaseCreaturesOf(TownIds.Inferno).First(c => c.Tier == 1);

        Assert.Contains("Tier 1 dwelling", session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Growth(1))).Error);

        town.SetDwelling(imp.Id, 1);
        var before = state.GetPlayer(Blue).Resources;
        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Growth(1))).ThrowIfRejected();

        Assert.Equal(before - new ResourceSet(Gold: 1000, Lumber: 5), state.GetPlayer(Blue).Resources);
        Assert.Equal(1, town.Level(BuiltInBuildings.Growth(1)));
    }

    [Theory]
    [InlineData(TownIds.Stronghold, "War Lodge")]
    [InlineData(TownIds.SwampBestiary, "Totem Circle")]
    public void PrimalFuryTowns_HaveAWarLodge_InsteadOfAMageGuild(string type, string name)
    {
        var buildings = BuildingCatalog.Default.For(type);

        Assert.False(buildings.Contains(BuiltInBuildings.MageGuild));
        Assert.Equal(3, buildings.MaxLevel(BuiltInBuildings.WarLodge));
        Assert.Equal(name, buildings.Get(BuiltInBuildings.WarLodge, 1).Name);
        Assert.Contains(new BuildingRequirement(BuiltInBuildings.WarLodge, 1), buildings.Get(BuiltInBuildings.TownHall, 2).Requires);
        Assert.True(BuildingCatalog.Default.For(TownIds.Haven).Contains(BuiltInBuildings.MageGuild));
        Assert.False(BuildingCatalog.Default.For(TownIds.Haven).Contains(BuiltInBuildings.WarLodge));
    }

    [Fact]
    public void TheWarLodge_TeachesTheOwnersHeroInTheTown_TheWarcriesUpToItsLevel()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Stronghold, 0, 0, Blue);
        var hero = state.GetHero(A);
        hero.Skills.Set(BuiltInSkills.Warcry, SkillLevel.Expert);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = Rich;
        var learned = Record<WarcriesLearned>(session);

        Assert.StartsWith("Unknown town building", session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.MageGuild)).Error);
        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.WarLodge)).ThrowIfRejected();

        Assert.Equal(1, hero.WarcriesLearned);
        Assert.Equal(new WarcriesLearned(hero.Id, town.Id, 1), Assert.Single(learned));
        Assert.Equal(SkillLevel.Basic, Core.Domain.Battles.HeroBattleBonuses.For(hero, state.Creatures).Warcry);
    }

    [Fact]
    public void TheAi_BuildsTheGrowthBuildings_OnceItsDwellingsStand()
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        foreach (var line in state.Creatures.BaseCreaturesOf(TownIds.Haven).Where(c => c.Tier <= 2))
        {
            town.SetDwelling(line.Id, 2);
        }

        foreach (var line in state.Creatures.BaseCreaturesOf(TownIds.Haven).Where(c => c.Tier >= 3).GroupBy(c => c.Tier).Select(g => g.First()))
        {
            town.SetDwelling(line.Id, 2);
        }

        foreach (var (building, level) in new[] { (BuiltInBuildings.Marketplace, 2), (BuiltInBuildings.Blacksmith, 1), (BuiltInBuildings.MageGuild, 4), (BuiltInBuildings.TownHall, 2) })
        {
            town.SetLevel(building, level);
        }

        var session = Start(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);
        for (var day = 0; day < 4; day++)
        {
            state.GetPlayer(Blue).Resources = Rich;
            ai.PlayTurn();
            session.EndTurn();
        }

        Assert.Equal((1, 1), (town.Level(BuiltInBuildings.Growth(1)), town.Level(BuiltInBuildings.Growth(2))));
    }

    [Fact]
    public void ATownsLevelZeroRow_TakesASharedBuildingAway_AndAGrowthTextMustNameADwelling()
    {
        const string header = "id,town,level,name,gold,resources,requires,income,growth,notes\n";
        var dwellings = string.Concat(Enumerable.Range(1, 5).Select(t => $"dwelling-{t},,1,D{t},1,,,,,\ndwelling-{t},,2,U{t},1,,,,,\n"));
        var catalog = new BuildingCatalog(BuildingTable.Parse(header + "blacksmith,,1,Smithy,1,,,,,\nblacksmith,inferno,0,(none),,,,,,\n" + dwellings));

        Assert.True(catalog.For(TownIds.Haven).Contains(BuiltInBuildings.Blacksmith));
        Assert.False(catalog.For(TownIds.Inferno).Contains(BuiltInBuildings.Blacksmith));
        Assert.Throws<FormatException>(() => BuildingTable.Parse(header + "growth-1,haven,1,Farm,1,,,,fort 5,\n"));
    }

    private static List<T> Record<T>(GameSession session)
        where T : IEvent
    {
        var list = new List<T>();
        session.Events.Subscribe<T>(list.Add);
        return list;
    }
}
