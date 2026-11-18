using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// A LAN game's <see cref="GameDirector"/> (ADR-222): the host plays all but the remote colours, runs the computer players and the one clock;
/// a client plays its own colour and shows the host's clock. And what a host announces to the computers looking for games.
/// </summary>
public class LanSeatsTests
{
    private static readonly GameOptions TwoMinutes = new(TurnTimerMinutes: 2);

    [Fact]
    public void TheHost_PlaysAllButTheRemoteColours_AndRunsTheComputerAndTheClock()
    {
        var (session, host) = HostGame(Create(TwoMinutes, "A...", "...B"), remote: [Red]);
        var director = new GameDirector(session, LanSeats.Of(host));

        Assert.True(director.PlaysHere(Blue));
        Assert.True(director.PlaysElsewhere(Red));
        Assert.False(director.PlaysHere(Red));
        Assert.NotNull(director.Ai);
        Assert.NotNull(director.Timer);
        Assert.Equal(Blue, director.Viewer());
    }

    [Fact]
    public void TheHostsClock_EndsARemoteColoursTurn_AndTheClientShowsIt()
    {
        var (session, host) = HostGame(Create(TwoMinutes, "A...", "...B"), remote: [Red]);
        var client = Join(host);
        var director = new GameDirector(session, LanSeats.Of(host));
        session.EndTurn(); // Red's turn
        Pump(host, client);

        // The host's screen holds only its own turns: Red's ends whatever it shows, by a command run as the host's.
        director.RunClock(200, showingComputerMoves: false, screensOpen: true);
        director.RunClock(0, showingComputerMoves: false, screensOpen: true); // Blue's turn starts with the full time
        Pump(host, client);

        Assert.Equal(Blue, session.State.CurrentPlayer.Id);
        Assert.Equal(2, session.State.Date.AbsoluteDay);
        Assert.Equal(new TurnClock(120, false, false), client.Clock);

        var mine = new GameDirector(client.Session!, LanSeats.Of(client));
        Assert.Null(mine.Ai);
        Assert.Null(mine.Timer);
        Assert.True(mine.PlaysHere(Red));
        Assert.True(mine.PlaysElsewhere(Blue));
        Assert.Equal(Red, mine.Viewer());
        Assert.Equal(new ClockFrame(120, false, false, null), mine.RunClock(1, showingComputerMoves: false, screensOpen: false));
    }

    [Fact]
    public void TheHost_IsPaused_WhileARemoteColourHasNobody_AndTheComputerWaits()
    {
        var state = Create("A...", "...B");
        state.GetPlayer(Blue).IsHuman = false;
        var (session, host) = HostGame(state, remote: [Red]); // Blue's turn: the host's computer plays it
        var client = Join(host, Red);
        var director = new GameDirector(session, LanSeats.Of(host));
        Assert.False(director.IsPaused);

        client.Dispose(); // Bob leaves: the game waits for Red, outside Red's turn too
        host.Poll();

        Assert.True(director.IsPaused);
        Assert.False(director.PlayComputer(screensOpen: false));
        Assert.Equal(Blue, session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void AClient_IsPaused_WhenTheHostSaysSo()
    {
        var (_, host) = HostGame(Create("A...", "...B"), remote: [Blue, Red]); // Blue's turn, nobody plays Blue
        var client = Join(host, Red);

        Assert.NotNull(client.Paused);
        Assert.True(new GameDirector(client.Session!, LanSeats.Of(client)).IsPaused);
    }

    [Fact]
    public void TheHostAnnounces_ItsLobby_ThenItsGame_ThenNoOpenColour()
    {
        var info = new LanGameInfo(NetSerializer.ProtocolVersion, "Ann", "", 0, 47820, Started: false);
        using var lobby = new LobbyHost("Ann");
        lobby.SetGame("Two Towns", [], [new LobbySeat(Blue, "Blue", SeatKind.Host, "Ann", ""), new LobbySeat(Red, "Red", SeatKind.Remote, null, "")]);

        Assert.Equal(info with { Game = "Two Towns", OpenColours = 1 }, LanAnnouncer.Describe(info, lobby, null, "ignored"));

        var (_, host) = HostGame(Create("A...", "...B"), remote: [Red]);
        var started = LanAnnouncer.Describe(info, null, host, "Two Towns");
        Assert.Equal(info with { Game = "Two Towns", OpenColours = 1, Started = true }, started);

        Join(host);
        Assert.Equal(0, LanAnnouncer.Describe(info, null, host, "Two Towns").OpenColours);
        Assert.Equal(started with { OpenColours = 0 }, LanAnnouncer.Describe(started, null, null, null));
    }

    private static (GameSession Session, GameHost Host) HostGame(GameState state, PlayerId[] remote)
    {
        var session = new GameSession(state);
        session.Start();
        return (session, new GameHost(session, remote));
    }

    private static GameClient Join(GameHost host, PlayerId? seat = null)
    {
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join("Bob", seat);
        Pump(host, client);
        Assert.True(client.Failure is null, client.Failure);
        return client;
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
}
