using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// What a link may cost a LAN host (ADR-203): small messages from clients, a join within seconds, a few links waiting to join, a bounded share
/// of each frame, and the game's record at most every few seconds.
/// </summary>
public class NetLimitsTests
{
    [Fact]
    public void OverTcp_AClientsMessageLongerThanTheLimit_ClosesItsLink_BeforeItArrives()
    {
        using var listener = new TcpGameListener(port: 0, loopbackOnly: true);
        using var raw = new TcpClient("127.0.0.1", listener.Port);
        var accepted = Accept(listener);

        // Only the length is sent: the host must not wait for (or allocate) the body.
        Span<byte> header = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(header, NetLimits.MaxClientMessageBytes + 1);
        raw.GetStream().Write(header);

        Until(() => accepted.IsClosed);
        Assert.Contains("at most 65536", accepted.CloseReason);
    }

    [Fact]
    public void OverTcp_AMessageTheHostCannotRead_ClosesItsLink()
    {
        using var listener = new TcpGameListener(port: 0, loopbackOnly: true);
        using var raw = new TcpClient("127.0.0.1", listener.Port);
        var accepted = Accept(listener);

        Frame(raw, """{"type":"nothingKnown"}"""u8);

        Until(() => accepted.IsClosed);
    }

    [Fact]
    public void OverTcp_AClientFloodingTheHost_IsCutOff_OnceTooManyMessagesWait()
    {
        // A small cap, so the test does not hang on hundreds of reads on a busy machine; the host's links use MaxClientMessagesWaiting.
        var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        try
        {
            using var raw = new TcpClient("127.0.0.1", ((IPEndPoint)server.LocalEndpoint).Port);
            using var accepted = new TcpConnection(server.AcceptTcpClient(), NetLimits.MaxClientMessageBytes, maxWaiting: 3);
            var say = NetSerializer.Serialize(new ChatSay("hi"));

            for (var i = 0; i < 4; i++)
            {
                Frame(raw, say);
            }

            Until(() => accepted.IsClosed);
            Assert.Contains("More than 3 messages waiting", accepted.CloseReason);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void ALinkThatDoesNotJoin_IsTurnedAway_AfterTheJoinTimeout()
    {
        var (_, host, time) = HostGame();
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        var client = new GameClient(clientEnd);
        host.Accept(hostEnd);

        time.Advance(NetLimits.JoinTimeout);
        host.Poll();
        Assert.False(hostEnd.IsClosed);

        time.Advance(TimeSpan.FromSeconds(1));
        host.Poll();
        client.Poll();

        Assert.True(hostEnd.IsClosed);
        Assert.Contains("No join request came within 10 seconds", client.Failure);
    }

    [Fact]
    public void AJoinedClient_HasNoJoinTimeout()
    {
        var (_, host, time) = HostGame();
        var bob = Join(host);

        time.Advance(TimeSpan.FromMinutes(5));
        Pump(host, bob);

        Assert.Null(bob.Failure);
        Assert.Equal("Bob", host.PlayerAt(Red));
    }

    [Fact]
    public void TooManyLinksWaitingToJoin_TurnAwayTheNextOne_UntilOneJoins()
    {
        var (_, host, _) = HostGame();
        var waiting = Enumerable.Range(0, NetLimits.MaxUnjoined).Select(_ => Connect(host)).ToList();

        var (late, lateClient) = Connect(host);
        lateClient.Poll();
        Assert.True(late.IsClosed);
        Assert.Contains("Too many players are joining", lateClient.Failure);
        Assert.All(waiting, w => Assert.False(w.HostEnd.IsClosed));

        waiting[0].Client.Join("Bob");
        Pump(host, waiting[0].Client);
        Assert.Equal("Bob", host.PlayerAt(Red));

        var (next, _) = Connect(host);
        Assert.False(next.IsClosed);
    }

    [Fact]
    public void TheHost_TakesABoundedNumberOfMessagesFromALink_EachPoll()
    {
        var (_, host, _) = HostGame();
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var bob = new GameClient(clientEnd);
        bob.Join("Bob");
        Pump(host, bob);
        // Commands for another colour, each answered with a refusal (chat lines would run into the chat's own allowance).
        for (var i = 0; i < NetLimits.MaxMessagesPerPoll + 4; i++)
        {
            clientEnd.Send(new SendCommand(i, new EndTurnCommand(Blue)));
        }

        host.Poll();
        Assert.Equal(NetLimits.MaxMessagesPerPoll, Refusals(clientEnd));

        host.Poll();
        Assert.Equal(4, Refusals(clientEnd));
        Assert.False(hostEnd.IsClosed);

        static int Refusals(INetConnection client)
        {
            var count = 0;
            while (client.TryReceive(out var message))
            {
                count += message is CommandRefused ? 1 : 0;
            }

            return count;
        }
    }

    [Fact]
    public void OverTcp_AClientThatStopsReading_DoesNotHoldUpTheHostsSends_AndIsCutOff()
    {
        var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        try
        {
            // The client never reads; small socket buffers fill after a few kilobytes.
            using var raw = new TcpClient { ReceiveBufferSize = 1024 };
            raw.Connect(IPAddress.Loopback, ((IPEndPoint)server.LocalEndpoint).Port);
            var socket = server.AcceptTcpClient();
            socket.SendBufferSize = 1024;
            using var host = new TcpConnection(socket, sendTimeout: TimeSpan.FromMilliseconds(300));
            var line = new ChatLine("", new string('x', 20_000));

            var clock = Stopwatch.StartNew();
            for (var i = 0; i < 200; i++)
            {
                host.Send(line);
            }

            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(2), $"Sending took {clock.Elapsed}.");
            Until(() => host.IsClosed);
            Assert.Contains("the other side stopped reading", host.CloseReason);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void OverTcp_MoreWaitingToSendThanTheCap_ClosesTheLinkAtOnce()
    {
        var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        try
        {
            using var raw = new TcpClient { ReceiveBufferSize = 1024 };
            raw.Connect(IPAddress.Loopback, ((IPEndPoint)server.LocalEndpoint).Port);
            var socket = server.AcceptTcpClient();
            socket.SendBufferSize = 1024;
            using var host = new TcpConnection(socket, maxBytesWaitingToSend: 100_000);
            var line = new ChatLine("", new string('x', 20_000));

            for (var i = 0; i < 20 && !host.IsClosed; i++)
            {
                host.Send(line);
            }

            Assert.True(host.IsClosed);
            Assert.Contains("More than 100000 bytes wait to be sent", host.CloseReason);
        }
        finally
        {
            server.Stop();
        }
    }

    [Fact]
    public void OverTcp_WhatWasSentBeforeTheLinkCloses_StillArrives()
    {
        using var listener = new TcpGameListener(port: 0, loopbackOnly: true);
        using var client = TcpConnection.Connect("127.0.0.1", listener.Port, TimeSpan.FromSeconds(5));
        var host = Accept(listener);

        host.Send(new JoinRefused("No."));
        host.Close("No.");

        NetMessage? got = null;
        Until(() => client.TryReceive(out got));
        Assert.Equal(new JoinRefused("No."), got);
        Until(() => client.IsClosed);
    }

    [Fact]
    public void TheChat_DropsAPlayersLinesBeyondTheAllowance_TellingThePlayerOnce()
    {
        var (_, host, time) = HostGame();
        var bob = Join(host);
        var lines = new List<string>();
        host.ChatReceived += (from, text) => lines.Add($"{from}: {text}");
        var heard = new List<string>();
        bob.ChatReceived += (from, text) => heard.Add($"{from}: {text}");

        for (var i = 0; i < NetLimits.ChatLines + 3; i++)
        {
            bob.Say($"line {i}");
        }

        Pump(host, bob);
        Assert.Equal(NetLimits.ChatLines, lines.Count);
        Assert.Equal(1, heard.Count(h => h == ": " + ChatAllowance.TooFast));

        time.Advance(NetLimits.ChatWindow);
        bob.Say("again");
        Pump(host, bob);
        Assert.Equal("Bob: again", lines[^1]);
    }

    [Fact]
    public void TheLobbysChat_HasTheSameAllowance()
    {
        var time = new ManualTime();
        using var lobby = new LobbyHost("Ann") { Time = time };
        lobby.SetGame("Two Towns", [], [new LobbySeat(Red, "Red", SeatKind.Remote, null, "")]);
        var lines = new List<string>();
        lobby.ChatReceived += (from, _) => lines.Add(from);
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        lobby.Accept(hostEnd);
        var bob = new GameClient(clientEnd);
        bob.Join("Bob");
        lobby.Poll();

        for (var i = 0; i < NetLimits.ChatLines + 3; i++)
        {
            bob.Say($"line {i}");
        }

        lobby.Poll();
        Assert.Equal(NetLimits.ChatLines, lines.Count(from => from == "Bob"));
    }

    [Fact]
    public void TheLobby_TurnsAwayAJoiningPlayer_WhenNoColourIsOpenAndTooManyWait()
    {
        using var lobby = new LobbyHost("Ann") { Time = new ManualTime() };
        lobby.SetGame("Two Towns", [], [new LobbySeat(Red, "Red", SeatKind.Remote, null, "")]);
        var clients = Enumerable.Range(0, NetLimits.MaxWaitingForColour + 2).Select(i => JoinLobby(lobby, $"P{i}")).ToList();

        Assert.Equal("P0", lobby.PlayerAt(Red));
        Assert.Equal(NetLimits.MaxWaitingForColour, lobby.Lobby.Waiting.Count);
        Assert.All(clients.Take(NetLimits.MaxWaitingForColour + 1), c => Assert.Null(c.Failure));
        Assert.Contains($"No colour is open and {NetLimits.MaxWaitingForColour} players already wait", clients[^1].Failure);

        static GameClient JoinLobby(LobbyHost lobby, string name)
        {
            var (hostEnd, clientEnd) = LoopbackConnection.Pair();
            lobby.Accept(hostEnd);
            var client = new GameClient(clientEnd);
            client.Join(name);
            lobby.Poll();
            client.Poll();
            return client;
        }
    }

    [Fact]
    public void TheRecordInterval_IsTheColours_SoJoiningAgainDoesNotStartItOver()
    {
        var (_, host, _) = HostGame();
        var (bobEnd, bob) = Connect(host);
        bob.Join("Bob");
        Pump(host, bob);
        Assert.Null(Ask(host, bob).Error);

        bobEnd.Close("Gone.");
        host.Poll();
        var again = Join(host);

        var (record, error) = Ask(host, again);
        Assert.Null(record);
        Assert.Contains("wait 5 seconds between saves", error);
    }

    [Fact]
    public void AClient_GetsTheGamesRecord_AtMostOnceInTheRecordInterval()
    {
        var (_, host, time) = HostGame();
        var bob = Join(host);

        Assert.Null(Ask(host, bob).Error);

        time.Advance(NetLimits.RecordInterval - TimeSpan.FromSeconds(1));
        var (record, error) = Ask(host, bob);
        Assert.Null(record);
        Assert.Contains("wait 5 seconds between saves", error);

        time.Advance(TimeSpan.FromSeconds(1));
        Assert.Null(Ask(host, bob).Error);
    }

    [Fact]
    public void TheLobby_TurnsAwayALinkThatDoesNotJoin_AndTooManyWaiting()
    {
        var time = new ManualTime();
        using var lobby = new LobbyHost("Ann") { Time = time };
        lobby.SetGame("Two Towns", [], [new LobbySeat(Red, "Red", SeatKind.Remote, null, "")]);
        var waiting = Enumerable.Range(0, NetLimits.MaxUnjoined).Select(_ => LoopbackConnection.Pair()).ToList();
        foreach (var (hostEnd, _) in waiting)
        {
            lobby.Accept(hostEnd);
        }

        var (late, _) = LoopbackConnection.Pair();
        lobby.Accept(late);
        Assert.True(late.IsClosed);

        var bob = new GameClient(waiting[0].Client);
        bob.Join("Bob");
        lobby.Poll();
        time.Advance(NetLimits.JoinTimeout + TimeSpan.FromSeconds(1));
        lobby.Poll();

        Assert.False(waiting[0].Host.IsClosed);
        Assert.All(waiting.Skip(1), w => Assert.True(w.Host.IsClosed));
        Assert.Equal("Bob", lobby.PlayerAt(Red));
    }

    private static (GameSession Session, GameHost Host, ManualTime Time) HostGame()
    {
        var state = Create("..A.B..");
        var initial = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();
        var time = new ManualTime();
        var host = new GameHost(session, [Red]) { Time = time, RecordOf = name => GameReplay.Record(initial, session, name) };
        return (session, host, time);
    }

    private static (LoopbackConnection HostEnd, GameClient Client) Connect(GameHost host)
    {
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        return (hostEnd, new GameClient(clientEnd));
    }

    private static GameClient Join(GameHost host)
    {
        var (_, client) = Connect(host);
        client.Join("Bob");
        Pump(host, client);
        Assert.True(client.Failure is null, client.Failure);
        return client;
    }

    private static (GameRecordDocument? Record, string? Error) Ask(GameHost host, GameClient client)
    {
        (GameRecordDocument? Record, string? Error) answer = default;
        var answered = false;
        client.RequestRecord(null, (r, e) => (answer, answered) = ((r, e), true));
        Pump(host, client);
        Assert.True(answered);
        return answer;
    }

    private static void Pump(GameHost host, params GameClient[] clients)
    {
        for (var i = 0; i < 3; i++)
        {
            host.Poll();
            foreach (var client in clients)
            {
                client.Poll();
            }
        }
    }

    private static INetConnection Accept(TcpGameListener listener)
    {
        INetConnection? accepted = null;
        Until(() => listener.TryAccept(out accepted));
        return accepted!;
    }

    private static void Frame(TcpClient client, ReadOnlySpan<byte> body)
    {
        // One write a message, sent at once: small writes held back for acknowledgements would take seconds.
        client.NoDelay = true;
        var frame = new byte[4 + body.Length];
        BinaryPrimitives.WriteInt32BigEndian(frame, body.Length);
        body.CopyTo(frame.AsSpan(4));
        client.GetStream().Write(frame);
    }

    private static void Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(30), "Timed out.");
            Thread.Sleep(5);
        }
    }

    /// <summary>A clock the test moves by hand.</summary>
    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
