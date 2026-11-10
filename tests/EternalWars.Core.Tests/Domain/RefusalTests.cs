using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// A refused command changes nothing (ADR-036): the answer to a decision is checked in full before anything is spent, taken or cleared. Every test
/// game also checks it for each refusal (<see cref="GameSession.CheckRefusals"/>).
/// </summary>
public class RefusalTests
{
    [Fact]
    public void TheCheck_IsOnForTheTests() => Assert.True(GameSession.CheckRefusals);

    [Fact]
    public void AChestAnswer_WhoseChestIsGone_IsRefused_AndTheDecisionStillWaits()
    {
        var state = Create("A...", "...B");
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 2, 0);
        chest.FixedValues = new ChestValues(1500, 1000);
        var session = Start(state);
        session.MoveTo(A, chest.VisitPosition);
        var decision = Assert.IsType<TreasureChestDecision>(state.PendingDecision);
        var gold = state.GetPlayer(Blue).Resources.Gold;
        state.RemoveObject(chest);

        var result = session.Execute(new ResolveTreasureChestCommand(Blue, TreasureChestReward.Gold));

        Assert.False(result.IsAccepted);
        Assert.Contains("Unknown map object", result.Error, StringComparison.Ordinal);
        Assert.Equal(decision, state.PendingDecision);
        Assert.Equal(gold, state.GetPlayer(Blue).Resources.Gold);
    }

    [Fact]
    public void ADepositBought_WithoutThePrice_OrOnceItIsGone_IsRefused_AndNothingIsSpent()
    {
        var state = Create(".......", ".......", "...A...", "......B");
        var vein = Add<Deposit>(state, DepositRules.Id(ResourceType.Crystal), 2, 0); // entrance (3, 1)
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 5000, Ore: 8);
        var session = Start(state);
        session.Step(A, Direction.North);
        var decision = Assert.IsType<DepositDecision>(state.PendingDecision);

        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 100);
        Assert.False(session.Execute(new AnswerDepositCommand(Blue, Buy: true)).IsAccepted);
        Assert.Equal((decision, new ResourceSet(Gold: 100)), (state.PendingDecision, state.GetPlayer(Blue).Resources));

        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 5000, Ore: 8);
        state.RemoveObject(vein);
        Assert.False(session.Execute(new AnswerDepositCommand(Blue, Buy: true)).IsAccepted);
        Assert.Equal((decision, new ResourceSet(Gold: 5000, Ore: 8)), (state.PendingDecision, state.GetPlayer(Blue).Resources));
    }
}
