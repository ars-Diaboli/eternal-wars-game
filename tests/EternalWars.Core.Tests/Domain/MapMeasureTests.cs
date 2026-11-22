using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The editor's Measure tool (ADR-283): the game's route between two tiles, its steps and its days for the movement points picked.</summary>
public class MapMeasureTests
{
    private const string Row = "........................................";

    [Fact]
    public void TheRoute_CountsItsStepsAndDays_ForThePointsPicked()
    {
        var state = TestWorld.Create(Row);
        var from = new MapPosition(0, 0);

        var oneDay = MapMeasure.Route(state, from, new MapPosition(12, 0), TestWorld.Blue)!; // 12 grass steps of 125: a day of 1500
        Assert.Equal((12, 1500, 1), (oneDay.Steps, oneDay.Cost, oneDay.Days));
        Assert.Equal(new MapPosition(12, 0), oneDay.Path.Destination);

        var twoDays = MapMeasure.Route(state, from, new MapPosition(13, 0), TestWorld.Blue)!;
        Assert.Equal(2, twoDays.Days);
        Assert.Equal([0, 1], twoDays.Path.Steps.Skip(11).Select(s => s.Day)); // the 12th step ends the first day, the 13th starts the second

        var faster = MapMeasure.Route(state, from, new MapPosition(13, 0), TestWorld.Blue, dailyPoints: 3000)!;
        Assert.Equal((13, 1), (faster.Steps, faster.Days));
        Assert.Equal(4, MapMeasure.Route(state, from, new MapPosition(13, 0), TestWorld.Blue, dailyPoints: 500)!.Days);
    }

    [Fact]
    public void TheRoute_GoesRoundAVisit_AsTheGamesDoes()
    {
        var state = TestWorld.Create(Row, Row, Row);
        TestWorld.Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 5, 1);

        var route = MapMeasure.Route(state, new MapPosition(0, 1), new MapPosition(10, 1), TestWorld.Blue)!;
        Assert.DoesNotContain(route.Path.Steps, s => s.Position == new MapPosition(5, 1)); // walking onto the chest would end the route there
        Assert.Equal(new MapPosition(10, 1), route.Path.Destination);
        Assert.Equal(10, route.Steps);
    }

    [Fact]
    public void AGatesJump_IsNoStep_AndStartsNoDay()
    {
        var state = TestWorld.CreateLevels([".....", ".....", "....."], [".....", ".....", "....."]);
        var surface = TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 2, 0); // entrance (3, 1, 0)
        var underground = TestWorld.Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 0, 0, 1); // entrance (1, 1, 1)
        TestWorld.Link(surface, underground);

        var route = MapMeasure.Route(state, new MapPosition(0, 0), new MapPosition(4, 2, 1), TestWorld.Blue, MapMeasure.MinPoints)!;
        var steps = route.Path.Steps;
        var jump = steps.Select((s, i) => (Step: s, Index: i)).Single(s => s.Step.Kind == PathStepKind.Teleport);
        Assert.Equal(steps.Count - 1, route.Steps);
        Assert.Equal(steps[jump.Index - 1].Day, jump.Step.Day); // the jump is on the day of the step into the gate
        Assert.Equal(route.Steps, route.Days); // 100 points a day: a step a day
    }

    [Fact]
    public void TheRoute_EndsOnAMonsterOrAHero_AndGoesNoFurther()
    {
        var state = TestWorld.Create("....A...................................");
        TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 20, 0).Count = 10;

        Assert.Equal(new MapPosition(20, 0), MapMeasure.Route(state, new MapPosition(10, 0), new MapPosition(20, 0), TestWorld.Blue)!.Path.Destination);
        Assert.Null(MapMeasure.Route(state, new MapPosition(10, 0), new MapPosition(30, 0), TestWorld.Blue)); // the guard stands in the way
        Assert.Equal(4, MapMeasure.Route(state, new MapPosition(0, 0), new MapPosition(4, 0), TestWorld.Blue)!.Steps); // Blue's own hero: a meeting
        Assert.NotNull(MapMeasure.Route(state, new MapPosition(0, 0), new MapPosition(4, 0), TestWorld.Red)); // Red's: a battle
        Assert.Null(MapMeasure.Route(state, new MapPosition(0, 0), new MapPosition(8, 0), TestWorld.Blue)); // a hero stands in the way
    }

    [Fact]
    public void NoRoute_AcrossWater_FromWater_OrToTheSameTile()
    {
        var state = TestWorld.Create("....~~....");
        Assert.Null(MapMeasure.Route(state, new MapPosition(0, 0), new MapPosition(8, 0), TestWorld.Blue));
        Assert.Null(MapMeasure.Route(state, new MapPosition(4, 0), new MapPosition(2, 0), TestWorld.Blue));
        Assert.Null(MapMeasure.Route(state, new MapPosition(1, 0), new MapPosition(1, 0), TestWorld.Blue));
        Assert.Null(MapMeasure.Route(state, new MapPosition(1, 0), new MapPosition(20, 0), TestWorld.Blue)); // off the map
        Assert.NotNull(MapMeasure.Route(state, new MapPosition(0, 0), new MapPosition(3, 0), TestWorld.Blue));
    }

    [Theory]
    [InlineData(MapMeasure.MinPoints - 1)]
    [InlineData(MapMeasure.MaxPoints + 1)]
    public void ThePoints_AreWithinTheToolsRange(int points) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            MapMeasure.Route(TestWorld.Create(Row), new MapPosition(0, 0), new MapPosition(3, 0), TestWorld.Blue, points));
}
