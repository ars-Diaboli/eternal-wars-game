using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// The pause of a LAN game (ADR-195): a remote colour without a player that holds the game up pauses it, every command is refused meanwhile, and
/// the clients and the host's screen are told when it starts and ends.
/// </summary>
public class PausePolicyTests
{
    [Fact]
    public void NothingHoldsUpAGame_NotStartedYet()
    {
        var state = Create("A.B");
        var pause = new PausePolicy(new SeatManager(state, [Blue]));

        Assert.Empty(pause.Waiting(state));
        Assert.Null(pause.Reason(state));
        Assert.Null(pause.Refusal(state));
    }

    [Fact]
    public void AColourNobodyHasJoined_HoldsUpTheGame_OnlyInItsTurn()
    {
        var state = Started(Create("A.B")); // Blue's turn

        var red = new PausePolicy(new SeatManager(state, [Red]));
        Assert.Empty(red.Waiting(state));

        var blue = new PausePolicy(new SeatManager(state, [Blue]));
        Assert.Equal([(Blue, "Nobody plays Blue yet.")], blue.Waiting(state));
        Assert.Equal("Nobody plays Blue yet. The game waits for a player to join Blue, or for the host to let the computer play it.", blue.Reason(state));
        Assert.Equal(CommandResult.Rejected("The game is paused: waiting for Blue."), blue.Refusal(state));
    }

    [Fact]
    public void AColourWhosePlayerLeft_HoldsUpTheGame_OutsideItsTurnToo()
    {
        var state = Started(Create("A.B"));
        var seats = new SeatManager(state, [Red]);
        var pause = new PausePolicy(seats);
        var bob = Seat(seats, state, Red, "Bob");
        Assert.Empty(pause.Waiting(state));

        bob.Connection.Close("Cable pulled.");
        seats.Forget(bob, state);

        Assert.Equal([(Red, "Bob (Red) left the game: Cable pulled.")], pause.Waiting(state));
    }

    [Fact]
    public void TheClientsAndTheHostsScreen_AreToldOnlyWhenThePauseChanges()
    {
        var state = Started(Create("A.B"));
        var seats = new SeatManager(state, [Blue, Red]);
        var pause = new PausePolicy(seats);
        var changes = 0;
        pause.Changed += () => changes++;
        var bob = Seat(seats, state, Red, "Bob", out var bobEnd);

        pause.Send(state);
        pause.Send(state);

        Assert.Equal(1, changes);
        var paused = Assert.IsType<GamePaused>(Assert.Single(Received(bobEnd)));
        Assert.Equal(pause.Reason(state), paused.Reason);

        // A client that gets the game while paused is told so; the next Send tells everyone that its join ends the pause.
        var ann = Seat(seats, state, Blue, "Ann", out var annEnd);
        pause.TellJoined(ann);
        Assert.Equal([new GamePaused(paused.Reason)], Received(annEnd));

        pause.Send(state);
        Assert.Equal(2, changes);
        Assert.Equal([new GamePaused(null)], Received(bobEnd));
        Assert.Equal([new GamePaused(null)], Received(annEnd));

        pause.TellJoined(bob);
        Assert.Empty(Received(bobEnd));
    }

    [Fact]
    public void HandingAColourOver_IsMarkedOnlyWhileItsCommandRuns()
    {
        var pause = new PausePolicy(new SeatManager(Create("A.B"), [Red]));
        var during = false;

        var result = pause.HandOver(() =>
        {
            during = pause.IsHandingOver;
            return CommandResult.Accepted();
        });

        Assert.True(during);
        Assert.True(result.IsAccepted);
        Assert.False(pause.IsHandingOver);
        Assert.Throws<InvalidOperationException>(() => pause.HandOver(() => throw new InvalidOperationException()));
        Assert.False(pause.IsHandingOver);
    }

    private static GameState Started(GameState state)
    {
        new GameSession(state).Start();
        return state;
    }

    private static HostPeer Seat(SeatManager seats, GameState state, PlayerId seat, string name) => Seat(seats, state, seat, name, out _);

    /// <summary>A client seated in <paramref name="seat"/> with the game; <paramref name="clientEnd"/> receives what the host sends it.</summary>
    private static HostPeer Seat(SeatManager seats, GameState state, PlayerId seat, string name, out LoopbackConnection clientEnd)
    {
        var (hostEnd, client) = LoopbackConnection.Pair();
        clientEnd = client;
        seats.Admit(hostEnd, seat, name);
        var peer = seats.Peers.Single(p => p.Seat == seat);
        peer.Feed = new PlayerViewFeed(state, seat);
        return peer;
    }

    private static List<NetMessage> Received(LoopbackConnection clientEnd)
    {
        var messages = new List<NetMessage>();
        while (clientEnd.TryReceive(out var message))
        {
            messages.Add(message);
        }

        return messages;
    }
}
