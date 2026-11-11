using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.Domain.ObjectVisitWorld;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The cartographer (MAP-31) and the artifact merchant (MAP-33) of the third object catalogue (ADR-079).</summary>
public class CartographerAndMerchantTests
{
    [Fact]
    public void TheCartographer_SellsTheWholeLevelFor3000_OncePerPlayer()
    {
        var (session, cartographer) = World<Cartographer>(BuiltInMapObjects.SurfaceCartographer, revealAll: false);
        var state = session.State;
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(20, 4)));

        session.Step(A, Direction.North);
        Assert.Equal(new CartographerDecision(A, cartographer.Id, 0, 3000), state.PendingDecision);
        session.Execute(new AnswerCartographerCommand(Blue, Buy: true)).ThrowIfRejected();

        Assert.Equal(47_000, state.GetPlayer(Blue).Resources.Gold);
        Assert.True(state.Fog.IsExplored(Blue, new MapPosition(20, 4)));
        var empty = new List<MapObjectFoundEmpty>();
        session.Events.Subscribe<MapObjectFoundEmpty>(empty.Add);
        session.Step(A, Direction.South); // making way for Charlie
        session.Visit(C, cartographer);
        Assert.Null(state.PendingDecision);
        Assert.Single(empty);
    }

    [Fact]
    public void TheCartographer_RefusesWithoutGold_AndTheUndergroundOneOnASingleLevelMap()
    {
        var (session, _) = World<Cartographer>(BuiltInMapObjects.SurfaceCartographer);
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);
        session.State.GetPlayer(Blue).Resources = new ResourceSet(Gold: 2999);
        session.Step(A, Direction.North);
        Assert.Null(session.State.PendingDecision);
        Assert.Contains("3000 gold", Assert.Single(refused).Reason, StringComparison.Ordinal);

        var (deep, _) = World<Cartographer>(BuiltInMapObjects.UndergroundCartographer);
        var noUnderground = new List<MapObjectRefused>();
        deep.Events.Subscribe<MapObjectRefused>(noUnderground.Add);
        deep.Step(A, Direction.North);
        Assert.Contains("no underground", Assert.Single(noUnderground).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheMerchant_LaysOutSevenMinorOrMajorArtifacts_SellsThemForTwiceTheirWorth_AndRestocksMonthly()
    {
        var (session, merchant) = World<ArtifactMerchant>(BuiltInMapObjects.ArtifactMerchant);
        var state = session.State;
        Assert.Equal(ArtifactMerchant.StockSize, merchant.Stock.Count);
        Assert.All(merchant.Stock, a => Assert.Contains(state.Artifacts.Get(a!).Tier, new[] { ArtifactTier.Minor, ArtifactTier.Major }));
        Assert.False(session.Execute(new BuyArtifactCommand(Blue, merchant.Id, 0)).IsAccepted); // nobody stands there

        session.Step(A, Direction.North);
        var artifact = merchant.Stock[0]!;
        var price = ArtifactMerchantRules.Price(state.Artifacts.Get(artifact));
        Assert.Equal(2 * ArtifactMerchantRules.Value(state.Artifacts.Get(artifact).Tier), price.Gold);
        session.Execute(new BuyArtifactCommand(Blue, merchant.Id, 0)).ThrowIfRejected();

        Assert.Contains(artifact, HeroA(session).Artifacts.Backpack);
        Assert.Equal(50_000 - price.Gold, state.GetPlayer(Blue).Resources.Gold);
        Assert.Null(merchant.Stock[0]);
        Assert.Contains("Nothing is for sale", session.Execute(new BuyArtifactCommand(Blue, merchant.Id, 0)).Error);

        session.ObjectLifecycle.StartWeek(restock: false);
        Assert.Null(merchant.Stock[0]); // a sold slot waits for the month
        session.ObjectLifecycle.StartWeek(restock: true);
        Assert.All(merchant.Stock, Assert.NotNull);
    }
}
