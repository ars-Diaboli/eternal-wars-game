using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Armies;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

public class PathFinderTests
{
    [Fact]
    public void Counters_CountEachSearch_ItsRoute_AndTheTilesItExpanded()
    {
        var session = Start(Create("A....", "~~~~~", "B~..."));
        var before = PathFinder.Counters;

        session.Movement.FindPath(A, new MapPosition(4, 0));
        session.Movement.FindPath(B, new MapPosition(4, 2));
        session.Movement.FindPath(A, new MapPosition(0, 1));

        // The route along the row expands its five tiles; the walled-in hero expands only its own; the water target stops before searching.
        var counted = PathFinder.Counters - before;
        Assert.Equal((3, 1, 5 + 1), (counted.Searches, counted.Found, counted.Expanded));
        Assert.True(counted.Time > TimeSpan.Zero);
    }

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
        var surface = Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 2, 0);    // entrance (3,1,0)
        var underground = Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 0, 0, 1); // entrance (1,1,1)
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
        Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 2, 0);
        var session = Start(state);

        Assert.Null(session.Movement.FindPath(A, new MapPosition(4, 2, 1)));
    }

    [Fact]
    public void Searches_ReusingTheThreadsBuffers_StartAfresh_WhateverTheMapOrHeroBefore()
    {
        // Imps are at home on lava; footmen pay its full cost.
        var state = Create("Clll.", "~~~~~", "Alll.");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Imp, 5));
        var session = Start(state);
        var big = Start(Create(new string('.', 40), new string('.', 40), "A" + new string('.', 39)));
        string?[] Searches() =>
        [
            Route(session.Movement.FindPath(A, new MapPosition(4, 2))),
            Route(session.Movement.FindPath(C, new MapPosition(4, 0))),
            Route(session.Movement.FindPath(C, new MapPosition(4, 2))), // across the water: nothing left over from A's search
            Route(big.Movement.FindPath(A, new MapPosition(39, 2))), // a bigger map grows the buffers
        ];

        string?[] fresh = [];
        var thread = new Thread(() => fresh = Searches()); // a thread that has never searched
        thread.Start();
        thread.Join();

        Assert.Equal(fresh, Searches());
        Assert.Equal(fresh, Searches()); // the same buffers again, after the big map
        Assert.Null(fresh[2]);
        Assert.NotEqual(fresh[0]![^20..], fresh[1]![^20..]); // the lava costs differ by army

        static string? Route(HeroPath? path) => path is null ? null : string.Join(",", path.Steps.Select(s => $"{s.Position.X}:{s.Cost}:{s.Day}"));
    }
}
