using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Where a hero can walk on a fresh map (<see cref="MapConnectivity"/>), which the generated-map check uses.</summary>
public class MapConnectivityTests
{
    [Fact]
    public void Rock_EndsTheWay_AndAGuardInTheGap_DoesNot()
    {
        var walled = Create("A.#..", "..#..", "..#.B");
        Assert.DoesNotContain(new MapPosition(3, 0), MapConnectivity.Reachable(walled, new MapPosition(0, 0)));

        var guarded = Create("A.#..", ".....", "..#.B");
        Add<MonsterGuard>(guarded, BuiltInMapObjects.Monster, 2, 1);
        var reached = MapConnectivity.Reachable(guarded, new MapPosition(0, 0));
        Assert.Contains(new MapPosition(2, 1), reached);
        Assert.Contains(new MapPosition(4, 2), reached);
    }

    [Fact]
    public void ARivalsTown_BehindRock_IsReported_ForThePlayerWhoCannotReachIt()
    {
        // Blue's town is behind the rock for Red's hero B; Blue's side has no rival town to reach.
        var state = Create(
            "...........#....",
            "...........#....",
            "...........#....",
            "...........#....",
            "...........#....",
            "...........#....",
            "...........#..B.",
            "A..........#....");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);

        var (player, unreached) = Assert.Single(MapConnectivity.UnreachableRivalTowns(state));

        Assert.Equal(Red, player.Id);
        Assert.Same(town, unreached);
    }
}
