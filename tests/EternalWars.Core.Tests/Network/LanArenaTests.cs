using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// Arena maps over the LAN (ADR-100, ADR-199): each player prepares on day 1 in its own view, turn by turn or at once on a map with start zones,
/// and day 2's battle is fought live between the two computers.
/// </summary>
public class LanArenaTests
{
    private static readonly GameOptions Simultaneous = new(FogMode.ShroudWithRefog) { SimultaneousFirstWeek = true, HostPlayer = Blue };

    /// <summary>
    /// Blue's hero A on the far left, Red's B on the far right, each in its own start zone (the outer six columns); the combat place in the middle
    /// of the bottom row.
    /// </summary>
    private static GameState Arena(GameOptions options)
    {
        const int width = 40;
        var rows = new[] { "A" + new string('.', width - 2) + "B" }.Concat(Enumerable.Repeat(new string('.', width), 4)).ToArray();
        var state = Create(options, rows);
        state.SetRules(ScenarioRules.Default with { Arena = true });
        Add<CombatPlace>(state, BuiltInMapObjects.CombatPlace, width / 2, 4);
        for (var y = 0; y < state.Map.Height; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                state.Map.SetStartZone(new MapPosition(x, y), Blue);
                state.Map.SetStartZone(new MapPosition(width - 1 - x, y), Red);
            }
        }

        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        return state;
    }

    [Fact]
    public void OnAnArenaMapWithStartZones_DayOneIsPlayedAtOnce_EachInItsOwnView_AndDayTwosBattleIsFoughtLive()
    {
        var (session, host) = HostGame(Arena(Simultaneous), remote: [Red]);
        var red = Join(host, "Bob", Red);
        var state = session.State;

        Assert.True(state.IsSimultaneous);
        Assert.Equal([Blue, Red], state.OpenTurns);
        Assert.False(state.CanSee(Red, state.GetHero(A))); // Blue's preparation is hidden from Red
        Assert.DoesNotContain(red.Session!.State.Heroes, h => h.Id == A);

        // Both prepare at once: Red moves and ends first, Blue goes on.
        red.Session.Execute(new MoveHeroCommand(Red, B, new MapPosition(36, 1))).ThrowIfRejected();
        red.Session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Pump(host, red);
        Assert.Equal([Blue], state.OpenTurns);
        Assert.Equal(new MapPosition(36, 1), state.GetHero(B).Position);
        Assert.Null(state.Battle);

        session.Execute(new MoveHeroCommand(Blue, A, new MapPosition(3, 2))).ThrowIfRejected();
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Pump(host, red);

        // Day 2: the battle opens by itself on both computers, both armies shown; Red commands its side from its own computer.
        Assert.NotNull(state.Battle);
        var battle = red.Session.State.Battle;
        Assert.NotNull(battle);
        Assert.Equal(state.Battle.Units.Count, battle.Units.Count);
        Assert.False(session.Execute(new BattleRetreatCommand(Red)).IsAccepted); // the host's screen cannot act for Red
        red.Session.Execute(new BattleRetreatCommand(Red)).ThrowIfRejected();
        Pump(host, red);

        Assert.Equal([Blue], state.Outcome!.Winners);
        Assert.Equal([Blue], red.Session.State.Outcome!.Winners);
        Assert.Equal(DecidingBattleResult.Won, red.Session.State.DecidingBattle!.Result);
    }

    [Fact]
    public void AnArenaMapWithoutStartZones_IsPreparedTurnByTurn_OverTheLan()
    {
        var state = Arena(Simultaneous);
        foreach (var tile in state.Map.Positions(0))
        {
            state.Map.SetStartZone(tile, null);
        }

        var (session, host) = HostGame(state, remote: [Red]);
        var red = Join(host, "Bob", Red);

        Assert.False(session.State.IsSimultaneous);
        Assert.False(red.Session!.Execute(new EndTurnCommand(Red)).IsAccepted); // Blue's turn first
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Pump(host, red);
        red.Session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Pump(host, red);

        Assert.NotNull(session.State.Battle);
        Assert.NotNull(red.Session.State.Battle);
    }

    [Fact]
    public void PlayAgain_OnTheHost_StartsTheNewGameForTheClientsToo()
    {
        var (session, host) = HostGame(Arena(Simultaneous), remote: [Red]);
        var red = Join(host, "Bob", Red);
        var lines = new List<string>();
        red.ChatReceived += (_, text) => lines.Add(text);
        var next = new GameSession(Arena(Simultaneous with { Seed = 9 }));
        next.Start();
        Assert.False(host.PlayAgain(next).IsAccepted); // not over yet

        red.Session!.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Pump(host, red);
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Pump(host, red);
        red.Session.Execute(new BattleRetreatCommand(Red)).ThrowIfRejected();
        Pump(host, red);
        Assert.True(red.Session.State.IsOver);

        host.PlayAgain(next).ThrowIfRejected();
        Pump(host, red);

        Assert.Same(next, host.Session);
        Assert.False(red.Session!.State.IsOver);
        Assert.Equal(1, red.Session.State.Date.AbsoluteDay);
        Assert.True(next.State.IsSimultaneous);
        Assert.Equal(Red, red.Session.State.CurrentPlayer.Id); // its own open turn
        Assert.Contains("Host starts the game again.", lines);
        red.Session.Execute(new MoveHeroCommand(Red, B, new MapPosition(36, 1))).ThrowIfRejected();
        Pump(host, red);
        Assert.Equal(new MapPosition(36, 1), next.State.GetHero(B).Position);
    }

    private static (GameSession Session, GameHost Host) HostGame(GameState state, PlayerId[] remote)
    {
        var session = new GameSession(state);
        session.Start();
        return (session, new GameHost(session, remote));
    }

    private static GameClient Join(GameHost host, string name, PlayerId seat)
    {
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var client = new GameClient(clientEnd);
        client.Join(name, seat);
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
