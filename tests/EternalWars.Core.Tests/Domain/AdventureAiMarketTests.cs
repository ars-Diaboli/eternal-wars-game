using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The AI at its towns' marketplace (ADR-189): only the last step to the building it is saving for.</summary>
public class AdventureAiMarketTests
{
    /// <summary>A Haven town at (0, 0) with its entrance at (3, 6); hero A stands on it.</summary>
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

    // With every dwelling standing, the Resource Silo (marketplace level 2) is next in the build order: 2500 gold, 5 lumber, 5 ore. With
    // one marketplace a unit of lumber or ore costs 2500 gold and sells for 25.
    private const int SiloGold = 2500;

    [Fact]
    public void TheAi_BuysTheLumberAndOreTheSiloLacks_WhenTheGoldCoversItWithinHalfTheTreasury()
    {
        var (state, town, traded) = Play(new ResourceSet(Gold: 60_000), marketplace: true);

        Assert.Equal(2, town.Level(BuiltInBuildings.Marketplace));
        Assert.Equal(
            [(ResourceType.Lumber, TradeDirection.Buy, 5, 12_500), (ResourceType.Ore, TradeDirection.Buy, 5, 12_500)],
            traded.Select(t => (t.Resource, t.Direction, t.Amount, t.Gold)));
        Assert.True(state.GetPlayer(Blue).Resources.Gold <= 60_000 - 25_000 - SiloGold);
    }

    [Fact]
    public void TheAi_DoesNotTrade_WithoutAMarketplace()
    {
        var (_, town, traded) = Play(new ResourceSet(Gold: 60_000), marketplace: false);

        Assert.Empty(traded);
        Assert.Equal(0, town.Level(BuiltInBuildings.Marketplace)); // it lacks the lumber for the marketplace itself
    }

    [Fact]
    public void TheAi_DoesNotBuy_WhenTheTradeWouldTakeMoreThanHalfItsGold()
    {
        var (_, town, traded) = Play(new ResourceSet(Gold: 40_000), marketplace: true);

        Assert.Empty(traded);
        Assert.Equal(1, town.Level(BuiltInBuildings.Marketplace));
    }

    [Fact]
    public void TheAi_SellsSpareLumber_WhenOnlyGoldIsMissing_AndKeepsAStock()
    {
        // 500 gold short: 20 units of lumber, from the 75 beyond the Silo's 5 and the 20 kept (the ore has none spare).
        var (state, town, traded) = Play(new ResourceSet(Gold: SiloGold - 500, Lumber: 100, Ore: 10), marketplace: true);

        Assert.Equal(2, town.Level(BuiltInBuildings.Marketplace));
        Assert.Equal([(ResourceType.Lumber, TradeDirection.Sell, 20, 500)], traded.Select(t => (t.Resource, t.Direction, t.Amount, t.Gold)));
        Assert.Equal(100 - 20 - 5, state.GetPlayer(Blue).Resources.Lumber);
    }

    [Fact]
    public void TheAi_DoesNotSell_WhenItsSpareStockFallsShortOfTheGold()
    {
        // 1000 gold short, but only 5 lumber are spare beyond the stock it keeps (125 gold).
        var (_, town, traded) = Play(new ResourceSet(Gold: SiloGold - 1000, Lumber: 30, Ore: 10), marketplace: true);

        Assert.Empty(traded);
        Assert.Equal(1, town.Level(BuiltInBuildings.Marketplace));
    }

    /// <summary>Blue's town with a fort, a tavern, every dwelling (and a marketplace), <paramref name="resources"/> on hand: one AI turn.</summary>
    private static (GameState State, Town Town, List<ResourcesTraded> Traded) Play(ResourceSet resources, bool marketplace)
    {
        var state = Create(Rows);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Tavern, 1);
        town.SetLevel(BuiltInBuildings.Fort, 1);
        foreach (var line in state.Creatures.BaseCreaturesOf(TownIds.Haven))
        {
            town.SetDwelling(line.Id, 1);
        }

        town.SetLevel(BuiltInBuildings.Marketplace, marketplace ? 1 : 0);
        var session = Start(state);
        state.GetPlayer(Blue).Resources = resources;
        var traded = new List<ResourcesTraded>();
        session.Events.Subscribe<ResourcesTraded>(traded.Add);

        new AdventureAi(session, controlHumanSlots: true).PlayTurn();

        return (state, town, traded);
    }
}
