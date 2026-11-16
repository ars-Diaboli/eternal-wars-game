using System.Diagnostics;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// The LAN network layer (ADR-192): a host runs the game, clients send their own colour's commands and keep a copy that follows the host's
/// command by command.
/// </summary>
public class LanGameTests
{
    [Fact]
    public void AClientJoinsTheGameAsItStands_AndItsCopyFollowsTheHost()
    {
        var (session, host) = HostGame(remote: [Red]);
        session.Step(A, Direction.East);
        var client = Join(host);

        Assert.Equal(Red, client.Seat);
        Assert.Equal(Digest(session), Digest(client.Session!));

        session.Step(A, Direction.East);
        session.EndTurn();
        Pump(host, client);

        Assert.Equal(Red, client.Session!.State.CurrentPlayer.Id);
        Assert.Equal(session.CommandLog, client.Session.CommandLog);
        Assert.Equal(Digest(session), Digest(client.Session));
    }

    [Fact]
    public void AClientsCommand_RunsOnTheHost_AndComesBackToTheCopy()
    {
        var (session, host) = HostGame(remote: [Blue]);
        var client = Join(host);
        var copy = client.Session!;

        var result = copy.Execute(new StepHeroCommand(Blue, A, Direction.East));

        // Sent, not run: the copy waits for the host.
        Assert.True(result.IsAccepted);
        Assert.True(result.IsSent);
        Assert.Empty(copy.CommandLog);

        Pump(host, client);

        Assert.Equal(new MapPosition(1, 0), session.State.GetHero(A).Position);
        Assert.Equal(new MapPosition(1, 0), copy.State.GetHero(A).Position);
        Assert.Equal(Digest(session), Digest(copy));
    }

    [Fact]
    public void TwoClients_PlayTheirTurnsOnAHostThatPlaysNoColour()
    {
        var (session, host) = HostGame(remote: [Blue, Red]);
        var blue = Join(host, "Ann", Blue);
        var red = Join(host, "Bob", Red);
        Assert.Equal("Ann", host.PlayerAt(Blue));
        Assert.Equal("Bob", host.PlayerAt(Red));

        blue.Session!.Execute(new EndTurnCommand(Blue));
        Pump(host, blue, red);
        red.Session!.Execute(new StepHeroCommand(Red, B, Direction.West));
        red.Session.Execute(new EndTurnCommand(Red));
        Pump(host, blue, red);

        Assert.Equal(2, session.State.Date.AbsoluteDay);
        Assert.Equal(new MapPosition(3, 0), session.State.GetHero(B).Position);
        Assert.Equal(Digest(session), Digest(blue.Session));
        Assert.Equal(Digest(session), Digest(red.Session));
    }

    [Fact]
    public void AnotherColoursCommand_IsRefused_OnTheClient_OnTheHostsScreen_AndByTheHost()
    {
        var (session, host) = HostGame(remote: [Red]);
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join("Bob");
        Pump(host, client);
        session.EndTurn(); // Red's turn
        Pump(host, client);

        // The client's copy refuses another colour's command; the host's own screen refuses the remote colour's.
        Assert.False(client.Session!.Execute(new EndTurnCommand(Blue)).IsAccepted);
        var onTheHost = session.Execute(new EndTurnCommand(Red));
        Assert.False(onTheHost.IsAccepted);
        Assert.Contains("another computer", onTheHost.Error);

        // A client that sends another colour's command anyway is refused by the host.
        var refusals = new List<string>();
        client.CommandRefused += refusals.Add;
        clientEnd.Send(new SendCommand(7, new StepHeroCommand(Blue, A, Direction.East)));
        Pump(host, client);

        Assert.Equal(["You play Red."], refusals);
        Assert.Equal(new MapPosition(0, 0), session.State.GetHero(A).Position);
    }

    [Fact]
    public void ARuleRefusal_ComesBackToTheClient_AndChangesNothing()
    {
        var (session, host) = HostGame(remote: [Blue]);
        var client = Join(host);
        var refusals = new List<string>();
        client.CommandRefused += refusals.Add;

        // The copy is in step with the host, so it may send a command the host's rules refuse: buying in a town that does not exist.
        client.Session!.Execute(new BuySpellBookCommand(Blue, new MapObjectId(99)));
        Pump(host, client);

        Assert.Single(refusals);
        Assert.Empty(session.CommandLog);
        Assert.Equal(Digest(session), Digest(client.Session));
    }

    [Fact]
    public void TheHostPlaysTheComputerPlayers_AndTheClientsSeeTheirMoves()
    {
        var state = Create("A...B");
        state.GetPlayer(Red).IsHuman = false;
        var (session, host) = HostGame(state, remote: [Blue]);
        var client = Join(host);

        client.Session!.Execute(new EndTurnCommand(Blue));
        Pump(host, client);
        new AdventureAi(session).PlayTurns(limit: 1);
        Pump(host, client);

        Assert.Equal(Blue, session.State.CurrentPlayer.Id);
        Assert.True(session.CommandLog.Count > 1);
        Assert.Equal(Digest(session), Digest(client.Session));
    }

    [Fact]
    public void ABattleFoughtFromAClient_EndsTheSameOnBothComputers()
    {
        var state = Create("A.B", "...");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var (session, host) = HostGame(state, remote: [Blue]);
        var client = Join(host);
        var copy = client.Session!;

        copy.Execute(new MoveHeroCommand(Blue, A, new MapPosition(2, 0)));
        Pump(host, client);
        Assert.IsType<BattleDecision>(copy.State.PendingDecision);

        copy.Execute(new AutoResolveBattleCommand(Blue));
        Pump(host, client);

        Assert.Null(session.State.Battle);
        Assert.Null(copy.State.Battle);
        Assert.Equal(Digest(session), Digest(copy));
    }

    [Fact]
    public void AJoin_IsRefused_ForAnotherProtocol_OrWhenNoColourIsFree()
    {
        var (_, host) = HostGame(remote: [Red]);
        Join(host);

        var full = Connect(host, out var fullEnd);
        full.Join("Late");
        Pump(host, full);
        Assert.Equal("Every colour of this game is taken.", full.Failure);
        Assert.True(fullEnd.IsClosed);

        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        clientEnd.Send(new JoinRequest(NetSerializer.ProtocolVersion + 1, "Future"));
        host.Poll();
        Assert.True(clientEnd.TryReceive(out var refusal));
        Assert.Contains("protocol", Assert.IsType<JoinRefused>(refusal).Reason);
    }

    [Fact]
    public void APlayerWhoLeaves_FreesTheColour_AndJoinsAgainWithTheGameAsItStandsThen()
    {
        var (session, host) = HostGame(remote: [Red]);
        var left = new List<(PlayerId, string)>();
        host.Left += (seat, name, _) => left.Add((seat, name));
        var first = Join(host, "Bob");

        first.Dispose();
        session.Step(A, Direction.East);
        host.Poll();
        Assert.Equal([(Red, "Bob")], left);
        Assert.Null(host.PlayerAt(Red));

        var again = Join(host, "Bob", Red);
        Assert.Equal(Digest(session), Digest(again.Session!));
    }

    [Fact]
    public void AClientThatFallsBehind_StopsInsteadOfPlayingAnotherGame()
    {
        var (session, host) = HostGame(remote: [Red]);
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join("Bob");
        Pump(host, client);

        hostEnd.Send(new CommandApplied(5, new EndTurnCommand(Blue)));
        client.Poll();

        Assert.Contains("this copy has 0", client.Failure);
        Assert.False(client.Session!.Execute(new EndTurnCommand(Red)).IsAccepted);
        Assert.Equal(Digest(session), Digest(client.Session));
    }

    [Fact]
    public void OverTcp_AClientOnThisComputer_PlaysWithTheHost()
    {
        var (session, host) = HostGame(remote: [Red]);
        using var listener = new TcpGameListener(port: 0, loopbackOnly: true);
        using var connection = TcpConnection.Connect("127.0.0.1", listener.Port, TimeSpan.FromSeconds(5));
        var client = new GameClient(connection);
        client.Join("Bob");

        Until(() =>
        {
            while (listener.TryAccept(out var accepted))
            {
                host.Accept(accepted);
            }

            Pump(host, client);
            return client.Session is not null;
        });
        session.EndTurn();
        Until(() =>
        {
            Pump(host, client);
            return client.Session!.CommandLog.Count == 1;
        });
        client.Session!.Execute(new StepHeroCommand(Red, B, Direction.West));
        Until(() =>
        {
            Pump(host, client);
            return client.Session.CommandLog.Count == 2;
        });

        Assert.Equal(new MapPosition(3, 0), session.State.GetHero(B).Position);
        Assert.Equal(Digest(session), Digest(client.Session));
    }

    private static (GameSession Session, GameHost Host) HostGame(PlayerId[] remote) => HostGame(Create("A...B"), remote);

    /// <summary>Starts the game the way the game app does: the map as it was at the start is what clients replay from.</summary>
    private static (GameSession Session, GameHost Host) HostGame(GameState state, PlayerId[] remote)
    {
        GameSession.CheckRefusals = true;
        var initial = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();
        return (session, new GameHost(session, initial, remote));
    }

    private static GameClient Connect(GameHost host, out LoopbackConnection clientEnd)
    {
        var (hostEnd, end) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        clientEnd = end;
        return new GameClient(end);
    }

    private static GameClient Join(GameHost host, string name = "Bob", PlayerId? seat = null)
    {
        var client = Connect(host, out _);
        client.Join(name, seat);
        Pump(host, client);
        Assert.Null(client.Failure);
        Assert.NotNull(client.Session);
        return client;
    }

    /// <summary>Lets the messages go to and fro until nothing is left in flight (loopback links deliver at once).</summary>
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

    private static void Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), "The messages did not arrive in time.");
            Thread.Sleep(5);
        }
    }

    private static string Digest(GameSession session) => GameStateDigest.Compute(session.State);
}
