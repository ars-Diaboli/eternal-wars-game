using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Deposits (ADR-082): a resource vein that becomes a mine for the player who pays.</summary>
public class DepositTests
{
    private static List<T> Record<T>(GameSession session)
        where T : IEvent
    {
        var list = new List<T>();
        session.Events.Subscribe<T>(list.Add);
        return list;
    }

    [Fact]
    public void ADeposit_BecomesItsMine_ForThePlayerWhoPays()
    {
        var state = Create(".......", ".......", "...A...", "......B");
        var vein = Add<Deposit>(state, DepositRules.Id(ResourceType.Crystal), 2, 0); // entrance (3, 1)
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 5000, Ore: 8);
        var session = Start(state);
        var bought = Record<DepositBought>(session);

        session.Step(A, Direction.North);
        Assert.Equal(new DepositDecision(A, vein.Id, new ResourceSet(Gold: 3000, Ore: 5)), state.PendingDecision);
        session.Execute(new AnswerDepositCommand(Blue, Buy: true)).ThrowIfRejected();

        var mine = Assert.IsType<Mine>(state.GetObject(vein.Id));
        Assert.Equal((BuiltInMapObjects.MineId(ResourceType.Crystal), Blue), (mine.Definition.Id, mine.Owner));
        Assert.Equal(new ResourceSet(Gold: 2000, Ore: 3), state.GetPlayer(Blue).Resources);
        Assert.Single(bought);
    }

    [Fact]
    public void ADeposit_MayBeLeft_AndIsRefusedWithoutThePrice()
    {
        var state = Create(".......", ".......", "...A...", "......B");
        var vein = Add<Deposit>(state, DepositRules.Id(ResourceType.Gold), 2, 0);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 5000, Ore: 10);
        var session = Start(state);
        var refused = Record<MapObjectRefused>(session);

        session.Step(A, Direction.North);
        session.Execute(new AnswerDepositCommand(Blue, Buy: false)).ThrowIfRejected();
        Assert.IsType<Deposit>(state.GetObject(vein.Id));
        Assert.Equal(new ResourceSet(Gold: 5000, Ore: 10), state.GetPlayer(Blue).Resources);

        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 4999, Ore: 10);
        session.Interactions.Visit(state.GetHero(A), vein);
        Assert.Null(state.PendingDecision);
        Assert.Contains("5000 gold", Assert.Single(refused).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheComputer_BuysADepositItCanAfford()
    {
        var state = Create(".......", ".......", "...A...", "......B");
        var vein = Add<Deposit>(state, DepositRules.Id(ResourceType.Lumber), 2, 0);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000, Ore: 20);
        state.GetPlayer(Blue).IsHuman = false;
        Steady(state);
        var session = Start(state);

        new AdventureAi(session).PlayTurn();

        Assert.Equal(Blue, Assert.IsType<Mine>(state.GetObject(vein.Id)).Owner);
    }
}
