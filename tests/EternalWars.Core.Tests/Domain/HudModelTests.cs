using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Cursors;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Turns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The adventure HUD's route days and tags, its hotkey table and End turn's question (ADR-309).</summary>
public class HudModelTests
{
    private static HeroPath Path(params int[] days) =>
        new(A, [.. days.Select((day, i) => new PathStep(new MapPosition(i + 1, 0), PathStepKind.Walk, 100, (i + 1) * 100, day))]);

    [Fact]
    public void ARouteOverFiveDays_IsTagged_OnTheFirstTwoDays_AndTheLast()
    {
        var state = Create(new string('.', 70), "A" + new string('.', 69));
        var session = Start(state);
        var today = state.Date;

        var path = session.Movement.FindPath(A, new MapPosition(65, 1))!;
        var tags = RouteDays.Tags(path, today);

        Assert.Equal(4, path.Steps[^1].Day); // 1500 a day on grass: 15 steps a day
        Assert.Equal([0, 1, 4], tags.Select(t => t.Day));
        Assert.All(tags, t => Assert.True(t.Step == 0 || path.Steps[t.Step - 1].Day == t.Day - 1)); // each on its day's first step
        Assert.All(tags, t => Assert.Equal(t.Day, path.Steps[t.Step].Day));
        Assert.Equal([$"Day {today.AbsoluteDay}", $"Day {today.AbsoluteDay + 1}", $"Day {today.AbsoluteDay + 4}"], tags.Select(t => t.Text));
    }

    [Fact]
    public void AHeroWithNoMovementLeft_StartsItsRouteTomorrow()
    {
        var state = Create("A...................", "...................B");
        var session = Start(state);
        state.GetHero(A).MovementPoints = 0;

        var path = session.Movement.FindPath(A, new MapPosition(18, 0))!;
        var tags = RouteDays.Tags(path, state.Date);

        Assert.Equal(1, path.Steps[0].Day);
        Assert.Equal([(0, 1), (15, 2)], tags.Select(t => (t.Step, t.Day)));
        Assert.Equal($"Day {state.Date.AbsoluteDay + 1}", tags[0].Text);
    }

    [Theory]
    [InlineData(new[] { 0, 0, 0 }, new[] { 0 })] // ends today: one tag
    [InlineData(new[] { 0, 0, 1, 1 }, new[] { 0, 2 })] // tomorrow is also the last day: tagged once
    [InlineData(new[] { 0, 1, 2 }, new[] { 0, 1, 2 })]
    [InlineData(new[] { 1, 1, 2, 3, 3 }, new[] { 0, 2, 3 })] // no movement left today: the route's first two days are tomorrow and the next
    [InlineData(new[] { 0, 1, 2, 3 }, new[] { 0, 1, 3 })]
    public void TheTags_StandOnTheFirstStepOfTheRoutesFirstTwoDaysAndItsLast(int[] days, int[] tagged)
    {
        var tags = RouteDays.Tags(Path(days), new GameDate(5));

        Assert.Equal(tagged, tags.Select(t => t.Step));
        Assert.Equal(tags.Select(t => 5 + days[t.Step]), tags.Select(t => t.Date.AbsoluteDay));
    }

    [Theory]
    [InlineData(0, RouteShade.Today)]
    [InlineData(1, RouteShade.LaterOdd)]
    [InlineData(2, RouteShade.LaterEven)]
    [InlineData(3, RouteShade.LaterOdd)]
    public void LaterDays_AlternateTwoShades(int day, RouteShade shade) => Assert.Equal(shade, RouteDays.ShadeOf(day));

    [Fact]
    public void TheHotkeyTable_HasOneKeyPerAction_WithTheNewSpaceMAndZ()
    {
        Assert.Equal(Enum.GetValues<AdventureAction>(), AdventureHotkeys.All.Select(h => h.Action).Order());
        Assert.Equal(AdventureHotkeys.All.Count, AdventureHotkeys.All.Select(h => h.Key.ToUpperInvariant()).Distinct().Count());
        Assert.Equal(("Space", "M", "Z"),
            (AdventureHotkeys.Of(AdventureAction.Move).Key, AdventureHotkeys.Of(AdventureAction.Marketplace).Key, AdventureHotkeys.Of(AdventureAction.EndTurn).Key));
        Assert.Equal("Dig [G]", AdventureHotkeys.Of(AdventureAction.Dig).Hint);
        Assert.Equal(AdventureAction.NextHero, AdventureHotkeys.ActionOf("Tab"));
        Assert.Equal(AdventureAction.Menu, AdventureHotkeys.ActionOf("F10"));
        Assert.Null(AdventureHotkeys.ActionOf("E")); // the camera's, not the map's
    }

    [Fact]
    public void EndTurn_AsksAboutTheHeroesThatCanStillMove()
    {
        var state = Create("A.C.................", "...................B");
        Start(state);
        var (a, c) = (state.GetHero(A), state.GetHero(C));

        Assert.Equal($"{a.Name} and {c.Name} can still move. End the turn?", EndTurnCheck.Question(state, Blue));

        a.MovementPoints = 0;
        Assert.Equal([C], EndTurnCheck.HeroesThatCanMove(state, Blue).Select(h => h.Id));
        Assert.Equal($"{c.Name} can still move. End the turn?", EndTurnCheck.Question(state, Blue));

        c.MovementPoints = 1; // the last-step rule: any movement left is one more step
        Assert.NotNull(EndTurnCheck.Question(state, Blue));

        c.MovementPoints = 0;
        Assert.Null(EndTurnCheck.Question(state, Blue));
        Assert.Equal([B], EndTurnCheck.HeroesThatCanMove(state, Red).Select(h => h.Id)); // only the player's own
    }
}
