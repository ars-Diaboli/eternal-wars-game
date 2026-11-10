using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The adventure map's status line: a move's result, a planned route, an armed spell's prompt.</summary>
public class MoveOutcomeTextTests
{
    [Fact]
    public void AnAcceptedMove_SaysItArrived_AndARefusal_SaysWhy()
    {
        var state = Create("A.....", "......", ".....B");
        var session = Start(state);

        Assert.Equal("Arrived (3 steps).", MoveOutcomeText.Of(session.Execute(new MoveHeroCommand(Blue, A, new MapPosition(3, 0)))));
        Assert.Equal("Refused: no", MoveOutcomeText.Of(new CommandResult(false, "no")));
        Assert.Equal("", MoveOutcomeText.Of(new CommandResult(true)));
    }

    [Fact]
    public void AStoppedMove_GivesTheReason_AndTheStepsTaken()
    {
        Assert.Equal("No movement left today. (2 steps).", MoveOutcomeText.Describe(new MovementOutcome(2, MoveResult.NotEnoughMovementPoints, Arrived: false)));
        Assert.Equal("The way is blocked.", MoveOutcomeText.Describe(MoveResult.Blocked));
        Assert.Equal("Battle!", MoveOutcomeText.Describe(MoveResult.BattleStarted));
        Assert.Equal("other", MoveOutcomeText.Describe("other"));
        Assert.Equal("", MoveOutcomeText.Describe(null));
    }

    [Fact]
    public void ARoute_CountsItsStepsAndDays()
    {
        var state = Create("A.....", "......", ".....B");
        var session = Start(state);
        var path = session.Movement.FindPath(A, new MapPosition(4, 0))!;

        Assert.Equal($"4 steps, {path.Turns} day(s). Click again to go.", MoveOutcomeText.Route(path));
        Assert.Equal(1, path.Turns);
    }

    [Fact]
    public void AnArmedSpell_SaysWhatToClick()
    {
        var state = Create("A.....", "......", ".....B");
        var hero = state.GetHero(A);
        hero.HasSpellBook = true;
        hero.Learn(AdventureMagic.DimensionDoor);

        Assert.Equal(
            $"Dimension Door: click a tile up to {AdventureMagic.DimensionDoorRange(1)} tiles away in a straight line.",
            AdventureSpellPrompt.Aim(state, A, AdventureMagic.DimensionDoor));
        Assert.Equal("Distant Vision: click a cell in sight.", AdventureSpellPrompt.Aim(state, A, AdventureMagic.DistantVision));
        Assert.Equal("Eavesdrop: click an enemy town in sight.", AdventureSpellPrompt.Aim(state, A, AdventureMagic.Eavesdrop)); // rank I
        Assert.Equal("Town Portal: click an own town.", AdventureSpellPrompt.Aim(state, A, AdventureMagic.TownPortal));
    }
}
