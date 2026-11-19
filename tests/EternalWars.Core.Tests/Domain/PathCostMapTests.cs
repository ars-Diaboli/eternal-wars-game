using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The cost maps answer every tile with the cost a route search for it gives, whatever order the tiles are asked in.</summary>
public class PathCostMapTests
{
    /// <summary>
    /// Two levels with roads, rough ground, water and rock; monsters side by side (a guarded tile next to two of them), linked two-way gates, a
    /// one-way gate, a pile, a garrison, a quest guard and a border guard; Blue's heroes A and C and Red's B.
    /// </summary>
    private static GameState Varied(bool withGates = true)
    {
        var state = CreateLevels(
        [
            "A...rr....~~....",
            "....rr....~~..C.",
            "..ss......##....",
            "..ss..........B.",
            "......##........",
            "~~....##....rr..",
            "................",
            "....rr......ss..",
        ],
        [
            "................",
            "...##...........",
            "...##.....~~....",
            "..........~~....",
            "........rr......",
            "................",
        ]);
        Road(state, 6, 0, 15);
        Road(state, 0, 6, 9);
        Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 8, 3);
        Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 10, 3);
        Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 3, 4, level: 1);
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Ore), 1, 3);
        Add<QuestSite>(state, BuiltInMapObjects.QuestGuard, 13, 6);
        Add<BorderPost>(state, BuiltInMapObjects.ColouredId(BuiltInMapObjects.BorderGuard, ObjectColour.Blue), 6, 7);
        Add<MapGarrison>(state, BuiltInMapObjects.Garrison, 1, 6, level: 1);
        if (withGates)
        {
            var surface = Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 4, 0);
            var underground = Add<TwoWayGate>(state, BuiltInMapObjects.SubterraneanGate, 12, 0, 1);
            Link(surface, underground);
            var entrance = Add<OneWayGateEntrance>(state, BuiltInMapObjects.OneWayMonolithEntrance, 6, 3, 1);
            var exit = Add<OneWayGateExit>(state, BuiltInMapObjects.OneWayMonolithExit, 14, 4);
            entrance.Exit = exit.Id;
        }

        return state;
    }

    private static IEnumerable<MapPosition> Tiles(GameState state)
    {
        for (var level = 0; level < state.Map.Levels; level++)
        {
            for (var y = 0; y < state.Map.Height; y++)
            {
                for (var x = 0; x < state.Map.Width; x++)
                {
                    yield return new MapPosition(x, y, level);
                }
            }
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void CostTo_IsTheSearchsCost_ForEveryTile_InAnyOrder(bool withGates, bool revealAll)
    {
        var session = Start(Varied(withGates), revealAll);
        var state = session.State;
        var finder = new PathFinder(state);
        var tiles = Tiles(state).ToList();
        var routes = 0;
        foreach (var hero in new[] { A, B, C })
        {
            var expected = tiles.Select(t => finder.FindPath(hero, t)?.TotalCost).ToList();
            routes += expected.Count(c => c is not null);

            // Near tiles first, then far ones first, then a stride through the map: a lazy search must not care.
            var inOrder = finder.CostsFrom(hero);
            Assert.Equal(expected, tiles.Select(inOrder.CostTo).ToList());
            var backwards = finder.CostsFrom(hero, reuse: inOrder);
            Assert.Equal(Enumerable.Reverse(expected), Enumerable.Reverse(tiles).Select(backwards.CostTo).ToList());
            var strided = finder.CostsFrom(hero);
            var order = Enumerable.Range(0, tiles.Count).Select(i => (i * 37) % tiles.Count).ToList();
            Assert.Equal(order.Select(i => expected[i]), order.Select(i => strided.CostTo(tiles[i])).ToList());
        }

        Assert.True(routes > (revealAll ? 300 : 60), $"only {routes} routes: the map should be open enough to test something");
    }

    [Fact]
    public void CostTo_FromAnotherTile_IsTheSearchsCostFromThere()
    {
        var session = Start(Varied());
        var finder = new PathFinder(session.State);
        foreach (var from in new[] { new MapPosition(9, 4), new MapPosition(5, 5, 1), new MapPosition(0, 7) })
        {
            var costs = finder.CostsFrom(A, from);
            Assert.All(Tiles(session.State), t => Assert.Equal(finder.FindPath(A, t, from: from)?.TotalCost, costs.CostTo(t)));
        }
    }

    /// <summary>
    /// A diagonal step on a dirt road costs 70 (50 × 1.41, rounded to even): the search's distance estimate must not count more for it, or the
    /// search settles for a dearer route over many road diagonals. The cost map has no estimate, so it is the yardstick here.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FindPath_FindsTheCheapestRoute_OverRoadDiagonals(int seed)
    {
        var random = new Random(seed);
        var rows = Enumerable.Range(0, 48)
            .Select(y => new string([.. Enumerable.Range(0, 48).Select(x => (x, y) == (0, 0) ? 'A' : "......sr#"[random.Next(9)])]))
            .ToArray();
        var state = Create(rows);
        RoadType[] roads = [RoadType.None, RoadType.Dirt, RoadType.Dirt, RoadType.Gravel, RoadType.Cobblestone];
        foreach (var tile in Tiles(state))
        {
            state.Map.SetRoad(tile, roads[random.Next(roads.Length)]);
        }

        var session = Start(state);
        var finder = new PathFinder(session.State);
        var costs = finder.CostsFrom(A);

        Assert.All(Tiles(state), t => Assert.Equal(costs.CostTo(t), finder.FindPath(A, t)?.TotalCost));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void CostFrom_IsTheSearchsCost_FromEveryTile(bool withGates, bool revealAll)
    {
        var session = Start(Varied(withGates), revealAll);
        var state = session.State;
        var finder = new PathFinder(state);
        var tiles = Tiles(state).Where(t => MovementRules.CheckTile(state, t) == MoveResult.Moved).ToList(); // a hero can stand there

        // A plain far tile, the monster tiles, the enemy hero, the quest guard, a gate's entrance and a tile under the garrison's level.
        MapPosition[] targets =
        [
            new(15, 7), new(8, 3), new(10, 3), new(3, 4, 1), state.GetHero(B).Position, new(13, 6), new(5, 1), new(14, 5, 1), new(0, 0, 1),
        ];
        var routes = 0;
        foreach (var target in targets)
        {
            var expected = tiles.Select(t => finder.FindPath(A, target, from: t)?.TotalCost).ToList();
            routes += expected.Count(c => c is not null);
            var costs = finder.CostsTo(A, target);
            Assert.Equal(expected, tiles.Select(costs.CostFrom).ToList());
        }

        Assert.True(routes > (revealAll ? 500 : 100), $"only {routes} routes");
    }

    [Fact]
    public void CostMaps_AreCountedWithTheSearches()
    {
        var session = Start(Create("A....", "~~~~~", "....."));
        var finder = new PathFinder(session.State);
        var before = PathFinder.Counters;

        var costs = finder.CostsFrom(A);
        costs.CostTo(new MapPosition(2, 0));
        costs.CostTo(new MapPosition(4, 0));

        // One map. It expands the row up to the tile before the far end: once nothing cheaper waits, the far end's cost is final.
        var counted = PathFinder.Counters - before;
        Assert.Equal((0, 1, 4), (counted.Searches, counted.Floods, counted.Expanded));
        Assert.Equal(400, costs.CostTo(new MapPosition(4, 0)));
    }

    [Fact]
    public void TheMove_FollowsTheRouteJustPlanned_WithoutSearchingAgain()
    {
        var session = Start(Create("A....."));
        var before = PathFinder.Counters.Searches;

        var planned = session.Movement.FindPath(A, new MapPosition(5, 0));
        Assert.Same(planned, session.Movement.FindPath(A, new MapPosition(5, 0)));
        session.MoveTo(A, new MapPosition(5, 0));

        Assert.Equal(1, PathFinder.Counters.Searches - before);
        Assert.Equal(new MapPosition(5, 0), session.State.GetHero(A).Position);
    }

    [Fact]
    public void AHeroMovingInAnotherGame_KeepsThisGamesRoute()
    {
        var session = Start(Create("A....."));
        var other = Start(Create("A....."));

        var planned = session.Movement.FindPath(A, new MapPosition(5, 0));
        other.MoveTo(A, new MapPosition(3, 0));

        Assert.Same(planned, session.Movement.FindPath(A, new MapPosition(5, 0)));
    }

    [Fact]
    public void ARoutePlannedBeforeTheGameChanged_IsPlannedAgain()
    {
        var session = Start(Create("A.....", "......"));
        var target = new MapPosition(5, 0);
        var first = session.Movement.FindPath(A, target)!;

        session.MoveTo(A, new MapPosition(1, 1)); // a command, and the hero moved
        var second = session.Movement.FindPath(A, target)!;

        Assert.NotSame(first, second);
        Assert.Equal((500, 141 + 300), (first.TotalCost, second.TotalCost)); // planned from where the hero stands now
    }
}
