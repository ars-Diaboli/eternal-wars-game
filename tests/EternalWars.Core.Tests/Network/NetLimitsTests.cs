using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
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
        var lines = new List<string>();
        host.ChatReceived += (_, text) => lines.Add(text);

        for (var i = 0; i < NetLimits.MaxMessagesPerPoll + 4; i++)
        {
            clientEnd.Send(new ChatSay($"line {i}"));
        }

        host.Poll();
        Assert.Equal(NetLimits.MaxMessagesPerPoll, lines.Count);

        host.Poll();
        Assert.Equal(NetLimits.MaxMessagesPerPoll + 4, lines.Count);
        Assert.False(hostEnd.IsClosed);
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
