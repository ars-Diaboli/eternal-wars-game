using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>The chat of a LAN game (ADR-192): each line goes to every client that has the game and to the host's screen.</summary>
public class ChatRelayTests
{
    [Fact]
    public void APlayersLine_IsCleaned_AndGoesToTheClientsWithTheGame_AndTheHost()
    {
        var (chat, annEnd, bobEnd, heard) = Relay();

        chat.Say("Ann", "  hi\u0007 there ");

        Assert.Equal([new ChatLine("Ann", "hi there")], Received(annEnd));
        Assert.Empty(Received(bobEnd)); // seated in the lobby, it does not have the game yet
        Assert.Equal([("Ann", "hi there")], heard);
    }

    [Fact]
    public void ABlankLine_IsNotSent()
    {
        var (chat, annEnd, _, heard) = Relay();

        chat.Say("Ann", " \t ");
        chat.Say("Ann", null);

        Assert.Empty(Received(annEnd));
        Assert.Empty(heard);
    }

    [Fact]
    public void TheGamesOwnLines_HaveNoWriter()
    {
        var (chat, annEnd, _, heard) = Relay();

        chat.Announce("Bob plays Red.");

        Assert.Equal([new ChatLine("", "Bob plays Red.")], Received(annEnd));
        Assert.Equal([("", "Bob plays Red.")], heard);
    }

    /// <summary>A chat with Ann seated in Blue with the game and Bob seated in Red from the lobby, still without it.</summary>
    private static (ChatRelay Chat, LoopbackConnection AnnEnd, LoopbackConnection BobEnd, List<(string, string)> Heard) Relay()
    {
        var state = Create("A.B");
        var seats = new SeatManager(state, [Blue, Red]);
        var (annHost, annEnd) = LoopbackConnection.Pair();
        var (bobHost, bobEnd) = LoopbackConnection.Pair();
        seats.Admit(annHost, Blue, "Ann");
        seats.Admit(bobHost, Red, "Bob");
        seats.Peers.Single(p => p.Seat == Blue).Feed = new PlayerViewFeed(state, Blue);
        var chat = new ChatRelay(seats);
        var heard = new List<(string, string)>();
        chat.Received += (from, text) => heard.Add((from, text));
        return (chat, annEnd, bobEnd, heard);
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
