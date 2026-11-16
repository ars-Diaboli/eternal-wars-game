using System.Diagnostics;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// The LAN lobby (ADR-194): players join before the game exists, take open colours and chat while the host sets the game up; the game starts with
/// them seated. Hosts are found on the LAN by a UDP probe.
/// </summary>
public class LobbyTests
{
    private static readonly PlayerId Green = new(2);

    [Fact]
    public void AJoiningPlayer_TakesTheFirstOpenColour_AndSeesTheGameBeingSetUp()
    {
        using var lobby = Lobby(Here(Blue), Open(Red), Open(Green));
        var bob = Join(lobby, "Bob");

        Assert.Equal(Red, bob.LobbySeat);
        Assert.Equal("Two Towns", bob.Lobby!.Game);
        Assert.Equal(["Fog: permanent reveal"], bob.Lobby.Settings);
        Assert.Equal("Bob", bob.Lobby.Seats.Single(s => s.Id == Red).Player);
        Assert.Equal("Ann", bob.Lobby.Seats.Single(s => s.Id == Blue).Player);
        Assert.Equal([Green], bob.Lobby.Open.Select(s => s.Id));
        Assert.Equal("Bob", lobby.PlayerAt(Red));
        Assert.Equal(1, lobby.OpenColours);
    }

    [Fact]
    public void APlayer_TakesAnotherOpenColour_ButNotATakenOne()
    {
        using var lobby = Lobby(Here(Blue), Open(Red), Open(Green));
        var bob = Join(lobby, "Bob");
        var cid = Join(lobby, "Cid");

        bob.TakeSeat(Green);
        Pump(lobby, bob, cid);
        Assert.Equal(Red, bob.LobbySeat);

        bob.TakeSeat(Blue);
        Pump(lobby, bob, cid);
        Assert.Equal(Red, bob.LobbySeat);

        cid.TakeSeat(Red);
        Pump(lobby, bob, cid);
        Assert.Equal(Green, cid.LobbySeat);
        Assert.Equal("Cid", bob.Lobby!.Seats.Single(s => s.Id == Green).Player);
    }

    [Fact]
    public void AColourTheHostTakesBack_LeavesItsPlayerWaiting_UntilAnotherOpens()
    {
        using var lobby = Lobby(Here(Blue), Open(Red), Open(Green));
        var bob = Join(lobby, "Bob");
        var cid = Join(lobby, "Cid");

        Set(lobby, Here(Blue), Computer(Red), Open(Green));
        Pump(lobby, bob, cid);
        Assert.Null(bob.LobbySeat);
        Assert.Equal(["Bob"], bob.Lobby!.Waiting);

        Set(lobby, Here(Blue), Open(Red), Open(Green));
        Pump(lobby, bob, cid);
        Assert.Equal(Red, bob.LobbySeat);
        Assert.Empty(cid.Lobby!.Waiting);
    }

    [Fact]
    public void TheChat_GoesToEveryone_InTheLobbyAndDuringTheGame()
    {
        var lobby = Lobby(Here(Blue), Open(Red));
        var lines = new List<string>();
        lobby.ChatReceived += (from, text) => lines.Add($"{from}: {text}");
        var bob = Join(lobby, "Bob");
        var heard = new List<string>();
        bob.ChatReceived += (from, text) => heard.Add($"{from}: {text}");

        bob.Say("  hello\n ");
        Pump(lobby, bob);
        lobby.Say("hi Bob");
        Pump(lobby, bob);
        Assert.Equal([": Bob joined.", "Bob: hello", "Ann: hi Bob"], lines);
        Assert.Equal(["Bob: hello", "Ann: hi Bob"], heard);

        var (session, host) = Start(lobby, Create("A...B"), bob);
        lines.Clear();
        heard.Clear();
        host.ChatReceived += (from, text) => lines.Add($"{from}: {text}");
        bob.Say("my turn?");
        Pump(host, bob);
        host.Say("soon");
        Pump(host, bob);
        Assert.Equal(["Bob: my turn?", "Ann: soon"], lines);
        Assert.Equal(["Bob: my turn?", "Ann: soon"], heard);
        Assert.Equal(Blue, session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void StartingTheGame_SeatsThePlayersInTheirColours_AndTurnsAwayAPlayerWithoutOne()
    {
        var lobby = Lobby(Here(Blue), Open(Red));
        var bob = Join(lobby, "Bob");
        var cid = Join(lobby, "Cid");
        Assert.Null(cid.LobbySeat);

        var (session, host) = Start(lobby, Create("A...B"), bob, cid);

        Assert.NotNull(bob.Session);
        Assert.Equal(Red, bob.Seat);
        Assert.Equal("Bob", host.PlayerAt(Red));
        Assert.Equal("The game started without a colour for you.", cid.Failure);

        session.EndTurn();
        Pump(host, bob);
        bob.Session!.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        Pump(host, bob);
        Assert.Equal(new MapPosition(3, 0), session.State.GetHero(B).Position);
    }

    [Fact]
    public void AnOpenColourAtTheStart_WaitsForAPlayer_WhoJoinsTheGame()
    {
        var lobby = Lobby(Here(Blue), Open(Red));
        var (_, host) = Start(lobby, Create("A...B"));
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var bob = new GameClient(clientEnd);

        bob.Join("Bob");
        Pump(host, bob);

        Assert.Equal(Red, bob.Seat);
        Assert.NotNull(bob.Session);
        Assert.Null(bob.Lobby);
    }

    [Fact]
    public void AnotherVersion_IsTurnedAway_FromTheLobby()
    {
        using var lobby = Lobby(Here(Blue), Open(Red));
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        lobby.Accept(hostEnd);

        clientEnd.Send(new JoinRequest(NetSerializer.ProtocolVersion - 1, "Old"));
        lobby.Poll();

        Assert.True(clientEnd.TryReceive(out var answer));
        Assert.StartsWith("The host plays protocol", Assert.IsType<JoinRefused>(answer).Reason);
        Assert.Null(lobby.PlayerAt(Red));
    }

    [Fact]
    public void APlayerWhoLeavesTheLobby_FreesTheirColour()
    {
        using var lobby = Lobby(Here(Blue), Open(Red));
        var changes = 0;
        lobby.Changed += () => changes++;
        var bob = Join(lobby, "Bob");
        Assert.Equal("Bob", lobby.PlayerAt(Red));

        bob.Dispose();
        lobby.Poll();

        Assert.Null(lobby.PlayerAt(Red));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void ABrowser_FindsAHostOnThisComputer_AndForgetsItWhenItStopsAnswering()
    {
        var info = new LanGameInfo(NetSerializer.ProtocolVersion, "Ann", "Two Towns", 1, 47_999, Started: false);
        var announcer = new LanAnnouncer(info, port: 0);
        using var browser = new LanBrowser(announcer.Port);

        browser.Probe();
        Until(() => browser.Poll() && browser.Games.Count > 0);
        Assert.Equal(info, Assert.Single(browser.Games).Info);
        Assert.EndsWith(":47999", browser.Games[0].Address);

        announcer.Info = info with { OpenColours = 0 };
        browser.Probe();
        Until(() => browser.Poll() && browser.Games[0].Info.OpenColours == 0);

        announcer.Dispose();
        for (var i = 0; i < 3; i++)
        {
            browser.Probe();
            browser.Poll();
        }

        Assert.Empty(browser.Games);
    }

    private static LobbySeat Here(PlayerId id) => new(id, Name(id), SeatKind.Host, "Ann", "");

    private static LobbySeat Open(PlayerId id) => new(id, Name(id), SeatKind.Remote, null, "");

    private static LobbySeat Computer(PlayerId id) => new(id, Name(id), SeatKind.Computer, null, "");

    private static string Name(PlayerId id) => id == Blue ? "Blue" : id == Red ? "Red" : "Green";

    private static LobbyHost Lobby(params LobbySeat[] seats)
    {
        var lobby = new LobbyHost("Ann");
        Set(lobby, seats);
        return lobby;
    }

    private static void Set(LobbyHost lobby, params LobbySeat[] seats) => lobby.SetGame("Two Towns", ["Fog: permanent reveal"], seats);

    private static GameClient Join(LobbyHost lobby, string name)
    {
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        lobby.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join(name);
        Pump(lobby, client);
        Assert.True(client.Failure is null, client.Failure);
        Assert.NotNull(client.Lobby);
        return client;
    }

    /// <summary>Starts the game the way the game app does: the host first, then the session, then the clients get it.</summary>
    private static (GameSession Session, GameHost Host) Start(LobbyHost lobby, GameState state, params GameClient[] clients)
    {
        var session = new GameSession(state);
        var host = lobby.StartGame(session);
        Pump(host, clients); // nobody is seated before the start
        Assert.All(clients, c => Assert.Null(c.Session));
        session.Start();
        Pump(host, clients);
        return (session, host);
    }

    private static void Pump(LobbyHost lobby, params GameClient[] clients)
    {
        for (var i = 0; i < 3; i++)
        {
            lobby.Poll();
            foreach (var client in clients)
            {
                client.Poll();
            }
        }
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

    private static void Until(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(10), "The answer did not arrive in time.");
            Thread.Sleep(5);
        }
    }
}
