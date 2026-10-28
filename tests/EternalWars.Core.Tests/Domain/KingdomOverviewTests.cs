using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The kingdom overview (ADR-077, ADR-084): every income source of a player, read from the income rules.</summary>
public class KingdomOverviewTests
{
    [Fact]
    public void EverySource_HasARow_AndTheDailyTotal_IsWhatTheTurnPays()
    {
        var state = Create("...........", "...........", "...........", "...........", "...........", "...........", "...........", "A.........B");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Marketplace, 2); // the resource silo
        var mine = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 8, 0);
        mine.Owner = Blue;
        Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Gold), 8, 3).Owner = Red;
        var wheel = Add<ResourceProvider>(state, BuiltInMapObjects.WaterWheel, 8, 5);
        wheel.Owner = Blue;
        var hero = state.GetHero(A);
        hero.Skills.Set(BuiltInSkills.Treasury, SkillLevel.Basic);
        hero.Artifacts.ReplaceWith(["hearth-charm"], [], state.Artifacts);
        state.RefreshArtifacts(hero);
        var session = Start(state);

        var overview = KingdomOverviews.For(state, Blue);

        Assert.Equal(session.Economy.DailyIncome(Blue), overview.DailyTotal);
        Assert.Equal([IncomeSourceKind.Town, IncomeSourceKind.Mine, IncomeSourceKind.Provider, IncomeSourceKind.Hero, IncomeSourceKind.Hero],
            overview.Rows.Select(r => r.Kind));
        Assert.Equal(TownRules.DailyIncome(state, town), overview.Rows[0].Daily);
        Assert.Contains("Resource silo", overview.Rows[0].Detail, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ResourceSet.Of(ResourceType.Ore, mine.DailyYield), overview.Rows[1].Daily);
        Assert.Equal((ResourceSet.Empty, wheel.Stored, "500 gold a week"), (overview.Rows[2].Daily, overview.Rows[2].Stored, overview.Rows[2].Detail));
        Assert.Equal(wheel.Stored, overview.StoredTotal);
        Assert.Equal(["Treasury", "Artifacts"], overview.Rows.Skip(3).Select(r => r.Detail));
        Assert.Equal(new ResourceSet(Gold: 100), overview.Rows[4].Daily);

        Assert.DoesNotContain(KingdomOverviews.For(state, Red).Rows, r => r.Object == mine.Id);
    }
}
