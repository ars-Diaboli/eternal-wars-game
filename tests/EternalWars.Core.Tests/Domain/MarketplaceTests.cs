using EternalWars.Core.Events;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Marketplace trading and the map marketplace (ADR-067).</summary>
public class MarketplaceTests
{
    private static readonly string[] Rows =
    [
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "....................",
        "...A................",
        "....................",
        "...................B",
    ];

    [Theory]
    [InlineData(ResourceType.Lumber, 2, 2500, 25)] // X = 1
    [InlineData(ResourceType.Ore, 4, 1667, 37)] // X = 2: 5000 / 3 rounded up, 12.5 * 3 = 37.5
    [InlineData(ResourceType.Lumber, 3, 2000, 31)] // one governed town, X = 1.5: factor 2.5
    [InlineData(ResourceType.Crystal, 2, 5000, 50)]
    [InlineData(ResourceType.Sulfur, 8, 2000, 125)] // X = 4: factor 5
    [InlineData(ResourceType.Mercury, 18, 1000, 250)] // X = 9: factor 10
    [InlineData(ResourceType.Lumber, 40, 500, 125)] // more towns do not help beyond 9
    public void Rates_FollowTheFormula(ResourceType resource, int halves, int buy, int sell)
    {
        Assert.Equal(new MarketRate(buy, sell), MarketplaceRules.RateAtHalves(resource, halves));
    }

    [Fact]
    public void MapMarketplace_TradesAsIfFourTowns()
    {
        Assert.Equal(new MarketRate(1000, 62), MarketplaceRules.MapRate(ResourceType.Lumber));
        Assert.Equal(new MarketRate(2000, 125), MarketplaceRules.MapRate(ResourceType.Crystal));
    }

    [Fact]
    public void Gold_HasNoRate() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => MarketplaceRules.RateAtHalves(ResourceType.Gold, 2));

    [Fact]
    public void WithoutAMarketplaceTown_NothingCanBeTraded()
    {
        var (session, _) = Setup(marketplace: false);

        Assert.False(session.Execute(Buy(ResourceType.Lumber, 1)).IsAccepted);
        Assert.False(session.Execute(Sell(ResourceType.Lumber, 1)).IsAccepted);
        Assert.False(MarketplaceRules.CanTrade(session.State, Blue));
    }

    [Fact]
    public void ABuiltMarketplace_AllowsBuyingAtTheSingleTownRate()
    {
        var (session, _) = Setup();
        var trades = new List<ResourcesTraded>();
        session.Events.Subscribe<ResourcesTraded>(trades.Add);
        var blue = session.State.GetPlayer(Blue);
        var before = blue.Resources;

        session.Execute(Buy(ResourceType.Lumber, 3)).ThrowIfRejected();

        Assert.Equal(before + new ResourceSet(Gold: -7500, Lumber: 3), blue.Resources);
        Assert.Equal(new ResourcesTraded(Blue, ResourceType.Lumber, TradeDirection.Buy, 3, 7500, null), Assert.Single(trades));
    }

    [Fact]
    public void Selling_GivesTheSellRate()
    {
        var (session, _) = Setup();
        var blue = session.State.GetPlayer(Blue);
        var before = blue.Resources;

        session.Execute(Sell(ResourceType.Crystal, 10)).ThrowIfRejected();

        Assert.Equal(before + new ResourceSet(Gold: 500, Crystal: -10), blue.Resources);
    }

    [Fact]
    public void ThereIsNoTradeCap()
    {
        var (session, _) = Setup();
        session.State.GetPlayer(Blue).Resources = new ResourceSet(Gold: 1_000_000, Lumber: 5000);

        session.Execute(Sell(ResourceType.Lumber, 5000)).ThrowIfRejected();

        Assert.Equal(1_000_000 + (5000 * 25), session.State.GetPlayer(Blue).Resources.Gold);
        Assert.Equal(0, session.State.GetPlayer(Blue).Resources.Lumber);
    }

    [Fact]
    public void SecondMarketplaceTown_ImprovesTheRate_AndAGovernedOneCountsOneAndAHalf()
    {
        var (session, first) = Setup();
        var state = session.State;
        Assert.Equal(2, MarketplaceRules.MarketplaceHalves(state, Blue));

        var second = AddTown(state, TownIds.Haven, 8, 0, Blue);
        second.SetLevel(BuiltInBuildings.Marketplace, 1);
        Assert.Equal(4, MarketplaceRules.MarketplaceHalves(state, Blue));
        Assert.Equal(new MarketRate(1667, 37), MarketplaceRules.TownRate(state, Blue, ResourceType.Lumber));

        state.GetHero(A).Skills.Set(BuiltInSkills.Stewardship, SkillLevel.Basic);
        first.Governor = A;
        Assert.Equal(5, MarketplaceRules.MarketplaceHalves(state, Blue)); // X = 2.5
    }

    [Fact]
    public void TownsWithoutAMarketplace_AndOtherPlayersTowns_DoNotCount()
    {
        var (session, _) = Setup();
        var other = AddTown(session.State, TownIds.Haven, 8, 0, Blue); // no marketplace built
        Assert.Equal(2, MarketplaceRules.MarketplaceHalves(session.State, Blue));

        other.Owner = Red;
        other.SetLevel(BuiltInBuildings.Marketplace, 1);
        Assert.Equal(2, MarketplaceRules.MarketplaceHalves(session.State, Blue));
    }

    [Fact]
    public void InvalidTrades_AreRefused()
    {
        var (session, _) = Setup();
        session.State.GetPlayer(Blue).Resources = new ResourceSet(Gold: 2000, Lumber: 4);

        Assert.False(session.Execute(Buy(ResourceType.Gold, 1)).IsAccepted);
        Assert.False(session.Execute(Buy(ResourceType.Lumber, 0)).IsAccepted);
        Assert.False(session.Execute(Sell(ResourceType.Lumber, -1)).IsAccepted);
        Assert.False(session.Execute(Buy(ResourceType.Lumber, 1)).IsAccepted); // 2500 gold
        Assert.False(session.Execute(Sell(ResourceType.Lumber, 5)).IsAccepted);
        Assert.False(session.Execute(Buy(ResourceType.Lumber, int.MaxValue)).IsAccepted);
        Assert.Equal(new ResourceSet(Gold: 2000, Lumber: 4), session.State.GetPlayer(Blue).Resources);
    }

    [Fact]
    public void Trading_NeedsYourTurn()
    {
        var (session, _) = Setup();
        session.EndTurn();

        Assert.False(session.Execute(Buy(ResourceType.Lumber, 1)).IsAccepted);
    }

    [Fact]
    public void MapMarketplace_TradesForAHeroOnItsEntrance_EvenWithoutTowns()
    {
        var (session, _, market) = SetupMap(marketplace: false);
        var blue = session.State.GetPlayer(Blue);
        Assert.False(session.Execute(Buy(ResourceType.Ore, 1, market.Id)).IsAccepted); // hero is elsewhere

        session.MoveTo(A, market.VisitPosition);
        var before = blue.Resources;
        session.Execute(Buy(ResourceType.Ore, 2, market.Id)).ThrowIfRejected();
        session.Execute(Sell(ResourceType.Crystal, 4, market.Id)).ThrowIfRejected();

        Assert.Equal(before + new ResourceSet(Gold: -2000 + 500, Ore: 2, Crystal: -4), blue.Resources);
    }

    [Fact]
    public void MapMarketplace_IgnoresTheTownRate_AndDoesNotChangeIt()
    {
        var (session, _, market) = SetupMap(marketplace: true);
        session.MoveTo(A, market.VisitPosition);
        var trades = new List<ResourcesTraded>();
        session.Events.Subscribe<ResourcesTraded>(trades.Add);

        session.Execute(Buy(ResourceType.Lumber, 1, market.Id)).ThrowIfRejected();
        session.Execute(Buy(ResourceType.Lumber, 1)).ThrowIfRejected();

        Assert.Equal([1000, 2500], trades.Select(t => t.Gold));
        Assert.Equal(market.Id, trades[0].Marketplace);
        Assert.Null(trades[1].Marketplace);
    }

    [Fact]
    public void MapMarketplace_RefusesWithoutAnOwnHeroOnIt_AndOtherObjects()
    {
        var (session, town, market) = SetupMap(marketplace: false);
        session.MoveTo(A, new MapPosition(12, 7)); // near the entrance, not on it

        Assert.False(session.Execute(Buy(ResourceType.Ore, 1, market.Id)).IsAccepted);
        Assert.False(session.Execute(Buy(ResourceType.Ore, 1, town.Id)).IsAccepted); // a town is not a map marketplace
        Assert.False(session.Execute(Buy(ResourceType.Ore, 1, new MapObjectId(999))).IsAccepted);
    }

    [Fact]
    public void VisitingTheMarketplace_PublishesMapObjectVisited()
    {
        var (session, _, market) = SetupMap(marketplace: false);
        var visits = new List<MapObjectVisited>();
        session.Events.Subscribe<MapObjectVisited>(visits.Add);

        session.MoveTo(A, market.VisitPosition);

        Assert.Equal(new MapObjectVisited(A, market.Id), Assert.Single(visits));
        Assert.Equal(market.VisitPosition, session.State.GetHero(A).Position);
    }

    private static GameCommand Buy(ResourceType resource, int amount, MapObjectId? market = null) =>
        new TradeResourceCommand(Blue, resource, TradeDirection.Buy, amount, market);

    private static GameCommand Sell(ResourceType resource, int amount, MapObjectId? market = null) =>
        new TradeResourceCommand(Blue, resource, TradeDirection.Sell, amount, market);

    private static (GameSession Session, Town Town) Setup(bool marketplace = true)
    {
        var (session, town, _) = SetupMap(marketplace, withMap: false);
        return (session, town);
    }

    private static (GameSession Session, Town Town, Marketplace Market) SetupMap(bool marketplace, bool withMap = true)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        if (marketplace)
        {
            town.SetLevel(BuiltInBuildings.Marketplace, 1);
        }

        var market = withMap ? Add<Marketplace>(state, BuiltInMapObjects.Marketplace, 10, 5) : null!;
        var session = Start(state);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 100_000, Lumber: 100, Ore: 100, Crystal: 50, Mercury: 50, Sulfur: 50);
        return (session, town, market);
    }
}
