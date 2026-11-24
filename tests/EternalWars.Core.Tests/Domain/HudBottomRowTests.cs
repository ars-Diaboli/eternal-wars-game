using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Cursors;
using EternalWars.Core.Domain.Heroes;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The adventure HUD's bottom row (ADR-309): the action grid's order, Move's refusals, the hero card's signed morale and luck.</summary>
public class HudBottomRowTests
{
    [Fact]
    public void TheGrid_IsTwoRowsOfFive_MoveFirst_EveryActionWithAHotkey_ButTheListsAndEndTurn()
    {
        Assert.Equal(2 * AdventureActionGrid.Columns, AdventureActionGrid.Order.Count);
        Assert.Equal(AdventureAction.Move, AdventureActionGrid.Order[0]);
        Assert.Equal(AdventureActionGrid.Order.Count, AdventureActionGrid.Order.Distinct().Count());
        Assert.Equal(
            Enum.GetValues<AdventureAction>().Except([AdventureAction.NextHero, AdventureAction.NextTown, AdventureAction.EndTurn]).Order(),
            AdventureActionGrid.Order.Order());
    }

    [Fact]
    public void Move_WalksTheSelectedHeroAlongItsRoute_AndSaysWhyNot()
    {
        var state = Create("A.C.................", "...................B");
        var session = Start(state);
        var (a, c) = (state.GetHero(A), state.GetHero(C));
        var route = session.Movement.FindPath(A, new MapPosition(10, 0))!;

        Assert.Null(AdventureActionGrid.MoveRefusal(a, route));
        Assert.Equal("Select a hero first", AdventureActionGrid.MoveRefusal(null, route));
        Assert.Equal("Click a cell to plan a route first", AdventureActionGrid.MoveRefusal(a, null));
        Assert.Equal("Click a cell to plan a route first", AdventureActionGrid.MoveRefusal(c, route)); // another hero's route

        a.MovementPoints = 0;
        Assert.Equal($"{a.Name} has no movement left today", AdventureActionGrid.MoveRefusal(a, route));
        Assert.Equal($"{a.Name} has no movement left today", AdventureActionGrid.MoveRefusal(a, session.Movement.FindPath(A, new MapPosition(10, 0))));
    }

    [Theory]
    [InlineData(2, "+2")]
    [InlineData(0, "0")]
    [InlineData(-1, "−1")]
    public void MoraleAndLuck_AreSigned(int value, string text) => Assert.Equal(text, HeroCard.Signed(value));
}
