using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// ADR-236: what was worked out from a LAN client's view stays only until the host writes into it, and a player's sight is handed out as a
/// copy.
/// </summary>
public class ViewWritesTests
{
    [Fact]
    public void ALanClientsCachedRoute_IsPlannedAgain_OnceTheHostWritesIntoItsView()
    {
        var session = GameSession.FollowingHost(Create("A...."), new EventBus());
        var sink = session.ViewSink!;
        sink.MarkExplored(Blue, Enumerable.Range(0, 5).Select(x => new MapPosition(x, 0)));
        var target = new MapPosition(4, 0);
        Assert.NotNull(session.Movement.FindPath(A, target));

        sink.AddHero(new Hero(B, "Bravo", Red, new MapPosition(2, 0), DefaultArmy()));

        Assert.Null(session.Movement.FindPath(A, target));
    }

    [Fact]
    public void EveryViewWrite_MovesTheChangeStamp()
    {
        var session = GameSession.FollowingHost(Create("A...."), new EventBus());
        var before = session.ChangeStamp;

        session.ViewSink!.SetSight(Blue, [new MapPosition(1, 0)]);

        Assert.True(session.ChangeStamp > before);
    }

    [Fact]
    public void SightOf_IsACopy_ThatLaterSightLeavesAsItWas()
    {
        var session = GameSession.FollowingHost(Create("A...."), new EventBus());
        session.ViewSink!.SetSight(Blue, [new MapPosition(1, 0)]);
        var sight = session.State.Fog.SightOf(Blue);

        session.ViewSink.SetSight(Blue, [new MapPosition(3, 0), new MapPosition(4, 0)]);

        Assert.Equal([new MapPosition(1, 0)], sight);
        Assert.NotSame(session.State.Fog.SightOf(Blue), session.State.Fog.SightOf(Blue));
    }
}
