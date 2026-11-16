using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// Saves of LAN games (ADR-196): the host's save loads in a LAN game, on one computer for several people (hot-seat) or with the computer playing
/// colours; a client saves the host's whole record, which loads on one computer; a client's replay is the host's record.
/// </summary>
public sealed class LanSavesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ew-lan-saves-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void AClient_SavesTheHostsWholeRecord_WhichLoadsOnOneComputer()
    {
        var (session, host, initial) = HostGame(remote: [Red]);
        var red = Join(host, Red);
        session.Execute(new StepHeroCommand(Blue, A, Direction.West)).ThrowIfRejected();
        session.EndTurn();
        Pump(host, red);
        red.Session!.Execute(new StepHeroCommand(Red, B, Direction.East)).ThrowIfRejected();
        Pump(host, red);

        var record = Ask(host, red, "Red's save");

        Assert.Equal("Red's save", record.Save!.Name);
        Assert.Equal(session.CommandLog.Count, record.Commands.Count);
        Assert.Equal(initial.Name, record.Map.Name);

        var path = GameSaves.SaveRecord(_folder, "Red's save", record);
        var (loaded, _) = GameSaves.Load(path);
        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(loaded.State));
        Assert.Equal([Blue, Red], GameSaves.People(loaded.State)); // both play on this computer (hot-seat) unless the computer takes one over
    }

    [Fact]
    public void AClientsReplay_IsTheHostsRecord_WithoutASaveLabel()
    {
        var (session, host, _) = HostGame(remote: [Red]);
        var red = Join(host, Red);
        session.Execute(new StepHeroCommand(Blue, A, Direction.West)).ThrowIfRejected();
        Pump(host, red);

        var record = Ask(host, red, saveName: null);
        Assert.Null(record.Save);

        var path = GameSaves.SaveReplayRecord(_folder, "Red's replay", record);
        var replayed = GameReplay.Replay(GameRecordSerializer.Load(path));
        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(replayed.State));
    }

    [Fact]
    public void ARecordRequest_IsRefused_WhenTheHostKeepsNoRecord_OrTheGameHasNotStarted()
    {
        var (_, host, _) = HostGame(remote: [Red]);
        host.RecordOf = null;
        var red = Join(host, Red);
        string? error = null;
        red.RequestRecord("x", (_, e) => error = e);
        Pump(host, red);
        Assert.Equal("The host keeps no record of this game.", error);

        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var waiting = new GameClient(clientEnd);
        waiting.RequestRecord("x", (_, e) => error = e);
        Assert.Equal("The game has not started yet.", error);
    }

    [Fact]
    public void TheHostsSave_IsHostedAgain_AndWaitsForAPlayerToTakeTheRemoteColour()
    {
        var (original, initial) = NewGame();
        original.Execute(new StepHeroCommand(Blue, A, Direction.West)).ThrowIfRejected();
        original.EndTurn(); // Red's turn
        var path = GameSaves.Save(_folder, "LAN", initial, original);

        var (loaded, record) = GameSaves.Load(path);
        GameSession.CheckRefusals = true;
        using var host = new GameHost(loaded, [Red]) { RecordOf = name => GameReplay.Record(record.Map, loaded, name) };
        host.Poll();
        Assert.True(host.IsPaused); // Red's turn, and nobody plays it yet

        var red = Join(host, Red);
        Assert.False(host.IsPaused);
        Assert.Equal(Red, red.Session!.State.CurrentPlayer.Id);
        red.Session.Execute(new StepHeroCommand(Red, B, Direction.East)).ThrowIfRejected();
        Pump(host, red);
        Assert.Equal(original.CommandLog.Count + 1, loaded.CommandLog.Count);

        // The loaded game's record goes on from the save's.
        var again = Ask(host, red, "Later");
        Assert.Equal(loaded.CommandLog.Count, again.Commands.Count);
        Assert.Equal(GameStateDigest.Compute(loaded.State), GameStateDigest.Compute(GameReplay.Replay(again).State));
    }

    [Fact]
    public void TheHostsSave_LoadsOnOneComputer_WithTheComputerPlayingAColour_AndItsSaveKeepsThat()
    {
        var (original, initial) = NewGame();
        original.Execute(new StepHeroCommand(Blue, A, Direction.West)).ThrowIfRejected();
        var path = GameSaves.Save(_folder, "LAN", initial, original);

        var (loaded, record) = GameSaves.Load(path);
        loaded.Execute(new HandToComputerCommand(Red)).ThrowIfRejected();
        Assert.Equal([Blue], GameSaves.People(loaded.State));
        Assert.False(loaded.Execute(new HandToComputerCommand(Blue)).IsAccepted); // a person plays at least one colour

        loaded.EndTurn();
        Assert.True(new AdventureAi(loaded).PlayTurn()); // Red's turn is the computer's
        Assert.Equal(Blue, loaded.State.CurrentPlayer.Id);

        var (reloaded, _) = GameSaves.Load(GameSaves.Save(_folder, "single", record.Map, loaded));
        Assert.False(reloaded.State.GetPlayer(Red).IsHuman);
        Assert.Equal(GameStateDigest.Compute(loaded.State), GameStateDigest.Compute(reloaded.State));
    }

    private static (GameSession Session, MapDocument Initial) NewGame()
    {
        var state = Create("..A.B..");
        var initial = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();
        return (session, initial);
    }

    private static (GameSession Session, GameHost Host, MapDocument Initial) HostGame(PlayerId[] remote)
    {
        GameSession.CheckRefusals = true;
        var (session, initial) = NewGame();
        var host = new GameHost(session, remote) { RecordOf = name => GameReplay.Record(initial, session, name) };
        return (session, host, initial);
    }

    private static GameRecordDocument Ask(GameHost host, GameClient client, string? saveName)
    {
        GameRecordDocument? record = null;
        string? error = null;
        client.RequestRecord(saveName, (r, e) => (record, error) = (r, e));
        Pump(host, client);
        Assert.Null(error);
        return Assert.IsType<GameRecordDocument>(record);
    }

    private static GameClient Join(GameHost host, PlayerId seat)
    {
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join("Bob", seat);
        Pump(host, client);
        Assert.True(client.Failure is null, client.Failure);
        Assert.NotNull(client.Session);
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
