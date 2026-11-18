using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// Who plays where on a LAN host (ADR-192, ADR-195): the remote colours, the links to the clients, the free colour a joining client gets, and the
/// colours whose player left.
/// </summary>
public class SeatManagerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void OnlyTheGamesHumanColours_ArePlayedOnOtherComputers()
    {
        var state = Create("A.B");

        Assert.Throws<ArgumentException>(() => new SeatManager(state, [new PlayerId(5)]));
        var seats = new SeatManager(state, [Red, Red]);
        Assert.Equal([Red], seats.RemoteSeats);
        Assert.True(seats.IsRemote(Red));
        Assert.False(seats.IsRemote(Blue));
    }

    [Fact]
    public void AJoiningClient_GetsTheColourItAsksFor_OrTheFirstFreeOne_AndIsTurnedAwayWhenNoneIsFree()
    {
        var state = Create("A.B");
        var seats = new SeatManager(state, [Blue, Red]);

        Assert.Null(seats.Choose(new JoinRequest(1, "Ann"), state, out var first));
        Assert.Equal(Blue, first);
        Assert.Null(seats.Choose(new JoinRequest(1, "Bob", Red), state, out var asked));
        Assert.Equal(Red, asked);

        seats.Admit(LoopbackConnection.Pair().Host, Red, "Bob");
        Assert.Equal("Bob", seats.PlayerAt(Red));
        Assert.Equal("Red is not a free colour in this game.", seats.Choose(new JoinRequest(1, "Cid", Red), state, out _));
        Assert.Throws<ArgumentException>(() => seats.Admit(LoopbackConnection.Pair().Host, Red, "Cid"));

        seats.Admit(LoopbackConnection.Pair().Host, Blue, "Ann");
        Assert.Equal("Every colour of this game is taken.", seats.Choose(new JoinRequest(1, "Cid"), state, out _));
    }

    [Fact]
    public void AColourWhosePlayerLeft_WaitsWithWhy_AndComesFirstForTheNextClient()
    {
        var state = Started(Create("A.B"));
        var seats = new SeatManager(state, [Blue, Red]);
        var bob = Seat(seats, state, Red, "Bob");

        bob.Connection.Close("Cable pulled.");
        var left = seats.Forget(bob, state);

        Assert.Equal(new SeatManager.Departure(Red, "Bob", "Cable pulled.", "Bob (Red) left the game: Cable pulled."), left);
        Assert.Empty(seats.Peers);
        Assert.Null(seats.PlayerAt(Red));
        Assert.Equal("Bob (Red) left the game: Cable pulled.", seats.LeftReason(Red));
        Assert.Null(seats.Choose(new JoinRequest(1, "Cid"), state, out var next));
        Assert.Equal(Red, next); // before Blue, which nobody has played yet

        seats.Joined(Red);
        Assert.Null(seats.LeftReason(Red));
    }

    [Fact]
    public void ALinkThatNeverHadTheGame_IsForgotten_WithoutALine()
    {
        var state = Create("A.B");
        var seats = new SeatManager(state, [Red]);
        var (hostEnd, _) = LoopbackConnection.Pair();
        seats.Accept(hostEnd, Now);

        hostEnd.Close("Gone.");

        Assert.Null(seats.Forget(Assert.Single(seats.Peers), state));
        Assert.Empty(seats.Peers);
        Assert.Null(seats.LeftReason(Red));
    }

    [Fact]
    public void LinksStillToJoin_AreTurnedAway_PastTheLimit()
    {
        var seats = new SeatManager(Create("A.B"), [Red]);
        var ends = Enumerable.Range(0, NetLimits.MaxUnjoined + 1).Select(_ => LoopbackConnection.Pair()).ToList();

        foreach (var (hostEnd, _) in ends)
        {
            seats.Accept(hostEnd, Now);
        }

        Assert.Equal(NetLimits.MaxUnjoined, seats.Peers.Count);
        Assert.All(seats.Peers, p => Assert.Equal(Now, p.Arrived));
        Assert.True(ends[^1].Host.IsClosed);
        Assert.True(ends[^1].Client.TryReceive(out var refusal));
        Assert.IsType<JoinRefused>(refusal);
    }

    [Fact]
    public void SeatedAndAdmitted_SplitTheClientsByWhetherTheyHaveTheGame()
    {
        var state = Create("A.B");
        var seats = new SeatManager(state, [Blue, Red]);
        var ann = Seat(seats, state, Blue, "Ann");
        seats.Admit(LoopbackConnection.Pair().Host, Red, "Bob");

        Assert.Equal([ann], seats.Seated);
        Assert.Equal("Bob", Assert.Single(seats.Admitted).Name);

        ann.Connection.Close("Gone.");
        Assert.Empty(seats.Seated);
    }

    [Fact]
    public void AColourHandedToTheComputer_IsNoLongerRemote_NorWaitsForAPlayer()
    {
        var state = Started(Create("A.B"));
        var seats = new SeatManager(state, [Red]);
        var bob = Seat(seats, state, Red, "Bob");
        bob.Connection.Close("Gone.");
        seats.Forget(bob, state);

        seats.HandToComputer(Red);

        Assert.Empty(seats.RemoteSeats);
        Assert.Null(seats.LeftReason(Red));
        Assert.Equal("Every colour of this game is taken.", seats.Choose(new JoinRequest(1, "Cid"), state, out _));
    }

    [Fact]
    public void CloseAll_ClosesEveryLink_AndForgetsThem()
    {
        var seats = new SeatManager(Create("A.B"), [Red]);
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        seats.Accept(hostEnd, Now);

        seats.CloseAll("The host has left the game.");

        Assert.Empty(seats.Peers);
        Assert.Equal("The host has left the game.", clientEnd.CloseReason);
    }

    private static GameState Started(GameState state)
    {
        new GameSession(state).Start();
        return state;
    }

    /// <summary>A client seated in <paramref name="seat"/> with the game, as the host leaves it after sending the game.</summary>
    private static HostPeer Seat(SeatManager seats, GameState state, PlayerId seat, string name)
    {
        seats.Admit(LoopbackConnection.Pair().Host, seat, name);
        var peer = seats.Peers.Single(p => p.Seat == seat);
        peer.Feed = new PlayerViewFeed(state, seat);
        return peer;
    }
}
