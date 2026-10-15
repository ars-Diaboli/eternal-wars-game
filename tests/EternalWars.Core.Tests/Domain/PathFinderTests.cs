using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

public class PathFinderTests
{
    [Fact]
    public void FindPath_StraightLine()
    {
        var session = Start(Create("A....."));

        var path = session.Movement.FindPath(A, new MapPosition(5, 0));

        Assert.NotNull(path);
        Assert.Equal(5, path.Steps.Count);
        Assert.Equal(500, path.TotalCost);
        Assert.True(path.IsReachableThisTurn);
    }

    [Fact]
    public void FindPath_GoesAroundWater()
    {
        var session = Start(Create(
            "A~.",
            ".~.",
            "..."));

        var path = session.Movement.FindPath(A, new MapPosition(2, 0))!;

        Assert.Equal(100 + 141 + 141 + 100, path.TotalCost);
        Assert.DoesNotContain(path.Steps, s => session.State.Map.GetTerrain(s.Position) == TerrainType.Water);
    }

    [Fact]
    public void FindPath_PrefersCheaperDetourOverSwamp()
    {
        var session = Start(Create(
            "Asss.",
            "....."));

        var path = session.Movement.FindPath(A, new MapPosition(4, 0))!;

        Assert.Equal(141 + 100 + 100 + 141, path.TotalCost);
        Assert.All(path.Steps, s => Assert.NotEqual(TerrainType.Swamp, session.State.Map.GetTerrain(s.Position)));
    }

    [Fact]
    public void FindPath_MarksDaysFromCurrentMovementPoints()
    {
        var session = Start(Create("A....."));
        session.State.GetHero(A).MovementPoints = 250;

        var path = session.Movement.FindPath(A, new MapPosition(5, 0))!;

        // 250 -> 150 -> 50 -> last step with 50 left (allowed) -> new day.
        Assert.Equal([0, 0, 0, 1, 1], path.Steps.Select(s => s.Day));
        Assert.Equal(2, path.Turns);
        Assert.False(path.IsReachableThisTurn);
    }

    [Fact]
    public void FindPath_ChargesTerrainBeingLeft()
    {
        var session = Start(Create("Ass.", "...."));

        // Leaving grass then swamp, swamp: 100 + 175 + 175 in a straight line, vs. detour via row 2.
        var path = session.Movement.FindPath(A, new MapPosition(3, 0))!;

        Assert.Equal(141 + 100 + 141, path.TotalCost);
    }

    [Fact]
    public void FindPath_OtherHeroesBlock()
    {
        var session = Start(Create(
            "A.B.",
            "####"));

        Assert.Null(session.Movement.FindPath(A, new MapPosition(3, 0)));
    }

    [Fact]
    public void FindPath_EndsAtVisitTileButNeverPassesThrough()
    {
        var state = Create(
            "A.....",
            "######");
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Lumber), 2, 0);
        var session = Start(state);

        Assert.Null(session.Movement.FindPath(A, new MapPosition(4, 0)));
        Assert.Equal(2, session.Movement.FindPath(A, new MapPosition(2, 0))!.Steps.Count);
    }

    [Fact]
    public void FindPath_EntersObjectsOnlyThroughTheirEntrance()
    {
        var state = Create(
            "......",
            "......",
            "......",
            "A.....");
        var mine = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Gold), 1, 0); // BBB / BVB, entrance (2,1)
        var session = Start(state);

        Assert.Null(session.Movement.FindPath(A, new MapPosition(2, 0)));
        var path = session.Movement.FindPath(A, mine.VisitPosition)!;
        Assert.Equal(mine.VisitPosition, path.Destination);
        Assert.Equal(2, path.Steps[^2].Position.Y); // approached from the row below
    }

    [Fact]
    public void FindPath_RespectsFogOfWar()
    {
        var session = Start(Create("A..................."), revealAll: false);

        Assert.NotNull(session.Movement.FindPath(A, new MapPosition(5, 0)));
        Assert.Null(session.Movement.FindPath(A, new MapPosition(15, 0)));
    }

    [Fact]
    public void FindPath_UsesLinkedGatesBetweenLevels()
    {
        var state = CreateLevels(
            ["A....", ".....", "....."],
            [".....", ".....", "....."]);
        var surface = Add<SubterraneanGate>(state, BuiltInMapObjects.SubterraneanGate, 2, 0);    // entrance (3,1,0)
        var underground = Add<SubterraneanGate>(state, BuiltInMapObjects.SubterraneanGate, 0, 0, 1); // entrance (1,1,1)
        Link(surface, underground);
        var session = Start(state);

        var path = session.Movement.FindPath(A, new MapPosition(4, 2, 1))!;

        var teleport = Assert.Single(path.Steps, s => s.Kind == PathStepKind.Teleport);
        Assert.Equal(underground.VisitPosition, teleport.Position);
        Assert.Equal(0, teleport.Cost);
        Assert.Equal(1, path.Destination.Level);
    }

    [Fact]
    public void FindPath_UnlinkedGateIsADeadEnd()
    {
        var state = CreateLevels(
            ["A....", ".....", "....."],
            [".....", ".....", "....."]);
        Add<SubterraneanGate>(state, BuiltInMapObjects.SubterraneanGate, 2, 0);
        var session = Start(state);

        Assert.Null(session.Movement.FindPath(A, new MapPosition(4, 2, 1)));
    }
}
