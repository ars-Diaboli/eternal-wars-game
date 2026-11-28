using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Core.Network;
using EternalWars.Core.Tests.Domain;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Network;

/// <summary>
/// A tournament over the LAN (ADR-366): every day before the battle is played at once, each person on its own computer, past the first week;
/// the battle's day opens no turns, and the battle is fought live on both computers.
/// </summary>
public class LanTournamentTests
{
    private static readonly GameOptions Options = new(FogMode.ShroudWithRefog) { SimultaneousFirstWeek = true, HostPlayer = Blue, TournamentDays = 9 };

    [Fact]
    public void EveryDayBeforeTheBattle_IsPlayedAtOnce_EachOnItsOwnComputer_AndTheBattleIsFoughtLive()
    {
        var state = SimultaneousDayTests.Duel(Options);
        state.SetRules(ScenarioRules.Default with { Tournament = true, MaxHeroes = 1 });
        var session = new GameSession(state);
        session.Start();
        var host = new GameHost(session, [Red]);
        var red = Join(host, "Bob", Red);

        for (var day = 1; day <= 9; day++)
        {
            Assert.Equal(day, red.Session!.State.Date.AbsoluteDay);
            Assert.Equal([Blue, Red], state.OpenTurns);
            Assert.Equal(Red, red.Session.State.CurrentPlayer.Id); // its own open turn on its computer
            Assert.Equal(Blue, state.CurrentPlayer.Id); // and the host's on the host
            if (day == 8)
            {
                // In the second week each still moves while the other's turn is open.
                red.Session.Execute(new MoveHeroCommand(Red, B, new MapPosition(24, 1))).ThrowIfRejected();
                session.Execute(new MoveHeroCommand(Blue, A, new MapPosition(1, 1))).ThrowIfRejected();
                Pump(host, red);
                Assert.Equal(new MapPosition(24, 1), state.GetHero(B).Position);
                Assert.Equal([Blue, Red], state.OpenTurns);
            }

            red.Session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
            Pump(host, red);
            Assert.Equal([Blue], state.OpenTurns);
            session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
            Pump(host, red);
        }

        Assert.Equal(10, state.Date.AbsoluteDay);
        Assert.False(state.IsSimultaneous);
        Assert.NotNull(state.Battle);
        Assert.NotNull(red.Session!.State.Battle);
        Assert.Equal(state.Battle.Units.Count, red.Session.State.Battle.Units.Count);
        red.Session.Execute(new BattleRetreatCommand(Red)); // no leaving the tournament's battle: the host refuses it
        Pump(host, red);
        Assert.NotNull(state.Battle);
        Assert.False(state.IsOver);
        Assert.False(red.Session.State.IsOver);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheBattleOpens_OnBothComputers_WhoeverEndsTheLastDayLast(bool clientEndsLast)
    {
        var state = SimultaneousDayTests.Duel(Options with { TournamentDays = 2 });
        state.SetRules(ScenarioRules.Default with { Tournament = true, MaxHeroes = 1 });
        var session = new GameSession(state);
        session.Start();
        var host = new GameHost(session, [Red]);
        var red = Join(host, "Bob", Red);
        var opened = new List<string>();
        session.Events.Subscribe<BattleStarted>(_ => opened.Add(session.InShownTurn ? "host" : "host, not shown"));
        red.Session!.Events.Subscribe<BattleStarted>(_ => opened.Add(red.Session.InShownTurn ? "client" : "client, not shown"));

        for (var day = 1; day <= 2; day++)
        {
            if (clientEndsLast)
            {
                session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
                Pump(host, red);
                red.Session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
            }
            else
            {
                red.Session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
                Pump(host, red);
                session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
            }

            Pump(host, red);
        }

        Assert.NotNull(red.Session.State.Battle);
        Assert.Equal(["client", "host"], opened.Order()); // both screens open it, each in its shown turn
    }

    [Fact]
    public void AChampionFromItsTownsGarrison_IsShownToTheOther_ButItsTownNever()
    {
        // Blue's zone with its town left, Red's right, far apart.
        var state = Create(Options with { TournamentDays = 1 }, [.. Enumerable.Repeat(new string('.', 30), 9), "A" + new string('.', 28) + "B"]);
        state.SetRules(ScenarioRules.Default with { Tournament = true, MaxHeroes = 1 });
        for (var y = 0; y < state.Map.Height; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                state.Map.SetStartZone(new MapPosition(x, y), Blue);
                state.Map.SetStartZone(new MapPosition(29 - x, y), Red);
            }
        }

        var town = AddTown(state, TownIds.Haven, 2, 0, Blue);
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 5));
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 5));
        var session = new GameSession(state);
        session.Start();
        var host = new GameHost(session, [Red]);
        var red = Join(host, "Bob", Red);

        session.Execute(new MoveHeroCommand(Blue, A, town.VisitPosition)).ThrowIfRejected();
        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();
        Assert.Same(state.GetHero(A), town.GarrisonHero);
        red.Session!.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Pump(host, red);
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Pump(host, red);

        Assert.NotNull(state.Battle);
        Assert.NotNull(red.Session.State.Battle); // Blue's champion came out of its town to fight
        Assert.Equal(state.GetHero(A).Army.Slots, red.Session.State.GetHero(A).Army.Slots);
        Assert.Null(red.Session.State.FindObject(town.Id)); // its town, its buildings and garrison, never seen
    }

    [Fact]
    public void APersonEndingTheDay_IsToldToEveryone_TheOnlyNewsOfTheOther()
    {
        var state = SimultaneousDayTests.Duel(Options);
        state.SetRules(ScenarioRules.Default with { Tournament = true, MaxHeroes = 1 });
        var session = new GameSession(state);
        session.Start();
        var host = new GameHost(session, [Red]);
        var red = Join(host, "Bob", Red);
        var onHost = new List<string>();
        var onClient = new List<string>();
        host.ChatReceived += (_, line) => onHost.Add(line);
        red.ChatReceived += (_, line) => onClient.Add(line);

        red.Session!.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Pump(host, red);
        Assert.Equal(["Red has ended the day."], onHost);
        Assert.Equal(["Red has ended the day."], onClient);

        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected(); // the last: the next day opens, no line
        Pump(host, red);
        Assert.Single(onHost);
        Assert.Equal(2, red.Session.State.Date.AbsoluteDay);
    }

    [Fact]
    public void AConflictInTheSecondWeek_HasTheHostPlayTheDayAgainTurnByTurn_AndTheRestTooUpToTheBattle()
    {
        var state = SimultaneousDayTests.Duel(Options, zoneWidth: 8, gap: 1);
        state.SetRules(ScenarioRules.Default with { Tournament = true, MaxHeroes = 1 });
        var initial = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();
        var host = new GameHost(session, [Red]);
        host.RecordOf = name => GameReplay.Record(initial, host.Session, name);
        GameSession? replaced = null;
        host.SessionReplaced += s => replaced = s;
        var red = Join(host, "Bob", Red);
        for (var day = 1; day <= 7; day++)
        {
            red.Session!.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
            Pump(host, red);
            session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
            Pump(host, red);
        }

        Assert.Equal(8, state.Date.AbsoluteDay);
        Assert.True(state.IsSimultaneous);
        var gold = state.GetPlayer(Red).Resources.Gold;
        for (var i = 0; i < 12 && replaced is null; i++)
        {
            red.Session!.Execute(new StepHeroCommand(Red, B, Direction.West)); // into Blue's zone: a conflict
            Pump(host, red);
        }

        Assert.NotNull(replaced);
        var again = replaced!.State;
        Assert.Equal(8, again.Date.AbsoluteDay);
        Assert.False(again.IsSimultaneous);
        Assert.Equal(Blue, again.CurrentPlayer.Id);
        Assert.Equal(gold, again.GetPlayer(Red).Resources.Gold); // the day's income once

        for (var day = 8; day <= 9; day++)
        {
            Assert.False(again.IsSimultaneous);
            replaced.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
            Pump(host, red);
            red.Session!.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
            Pump(host, red);
        }

        Assert.Equal(10, again.Date.AbsoluteDay);
        Assert.False(again.IsSimultaneous);
        Assert.NotNull(again.Battle);
        Assert.NotNull(red.Session!.State.Battle);
    }

    [Fact]
    public void PastTheFirstWeek_BothFightNeutralsInTheSameDay_EachOnItsOwnComputer_AndTheRecordReplays()
    {
        var state = SimultaneousBattleTests.Fights(Options);
        state.SetRules(ScenarioRules.Default with { Tournament = true, MaxHeroes = 1 });
        var initial = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();
        var host = new GameHost(session, [Red]);
        host.RecordOf = name => GameReplay.Record(initial, host.Session, name);
        var red = Join(host, "Bob", Red);
        for (var day = 1; day <= 7; day++)
        {
            red.Session!.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
            Pump(host, red);
            session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
            Pump(host, red);
        }

        // Day 8: each attacks the neutrals of its zone, and both battles are fought at once.
        Assert.Equal(8, state.Date.AbsoluteDay);
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        red.Session!.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        Pump(host, red);
        Assert.Equal(2, state.OpenBattles.Count);
        Assert.NotNull(red.Session.State.Battle);
        Assert.Same(state.BattleOf(Blue), state.Battle);

        red.Session.Execute(new AutoResolveBattleCommand(Red)).ThrowIfRejected();
        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        Pump(host, red);
        Assert.Empty(state.OpenBattles);
        Assert.Null(red.Session.State.Battle);
        LanSimultaneousBattleTests.AnswerLevelUps(host, red);
        SimultaneousBattleTests.AnswerLevelUps(session, Blue);
        for (var day = 8; day <= 9; day++)
        {
            Assert.True(state.IsSimultaneous);
            red.Session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
            Pump(host, red);
            session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
            Pump(host, red);
        }

        Assert.Equal(10, state.Date.AbsoluteDay);
        Assert.NotNull(state.DecidingBattle);
        Assert.NotNull(red.Session.State.Battle);
        Assert.Equal(GameStateDigest.Describe(state), GameStateDigest.Describe(GameReplay.Replay(host.RecordOf(null)).State));
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
