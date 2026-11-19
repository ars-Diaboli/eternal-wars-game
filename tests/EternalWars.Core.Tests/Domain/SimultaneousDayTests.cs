using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Events;
using EternalWars.Core.Network;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The simultaneous first week (MP-2, ADR-197): on a map with a start zone for every person, the people play each day of the first week at once,
/// each command as its own player's; the computer players play after them; a conflict (a hero entering another person's start zone, a person
/// seeing another's hero, an object two people visit) stops the day, which the LAN host plays again turn by turn, and every day after it.
/// </summary>
public class SimultaneousDayTests
{
    private static readonly PlayerId Green = new(2);
    private static readonly GameOptions Simultaneous = new(FogMode.ShroudWithRefog) { SimultaneousFirstWeek = true };

    [Fact]
    public void ThePeople_PlayEachDayOfTheFirstWeekAtOnce_EachEndingItsOwnTurn()
    {
        var session = Started(Duel(Simultaneous));
        var state = session.State;
        Assert.Equal([Blue, Red], state.OpenTurns);
        Assert.Equal(Blue, state.CurrentPlayer.Id); // the first open turn, between commands
        var gold = state.GetPlayer(Red).Resources.Gold;

        // Red acts before Blue, then Blue; each command runs as its own player's.
        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        Assert.Equal(Blue, state.CurrentPlayer.Id);

        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Assert.Equal([Red], state.OpenTurns);
        Assert.Equal(Red, state.CurrentPlayer.Id);
        Assert.Equal("Blue has ended the turn: the others still play today.", session.Execute(new StepHeroCommand(Blue, A, Direction.East)).Error);
        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();

        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Assert.Equal(2, state.Date.AbsoluteDay);
        Assert.Equal([Blue, Red], state.OpenTurns);
        Assert.Equal(gold, state.GetPlayer(Red).Resources.Gold - session.Economy.DailyIncome(Red).Gold); // one income a day
    }

    [Fact]
    public void TurnEnded_ComesOnceTheTurnIsOut_OfTodaysOpenTurns()
    {
        var session = Started(Duel(Simultaneous));
        var state = session.State;
        var seen = new List<(PlayerId Player, List<PlayerId> Open, bool Done)>();
        session.Events.Subscribe<TurnEnded>(e => seen.Add((e.Player, state.OpenTurns.ToList(), state.DoneToday.Contains(e.Player))));

        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();

        Assert.Equal([Blue, Red], seen.Select(s => s.Player));
        Assert.Equal([Red], seen[0].Open);
        Assert.Empty(seen[1].Open);
        Assert.All(seen, s => Assert.True(s.Done));
    }

    [Fact]
    public void TheDaysCommands_BeginAfterTheCommandThatOpenedIt()
    {
        var session = Started(Duel(Simultaneous));
        Assert.Equal(0, session.SimultaneousDayStart);

        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Assert.Equal(0, session.SimultaneousDayStart);
        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected(); // opens day 2

        Assert.Equal(3, session.SimultaneousDayStart);
        Assert.Equal(3, session.CommandLog.Count);
    }

    [Fact]
    public void LeavingTheOpenTurns_ClosesTheDay_OnlyWithTheLastOne()
    {
        var state = Started(Duel(Simultaneous with { HostPlayer = Red })).State;
        var days = new SimultaneousDayCoordinator(state, new EventBus());
        Assert.Equal(Red, state.CurrentPlayer.Id);

        Assert.False(days.Leave(Red));
        Assert.Equal([Blue], state.OpenTurns);
        Assert.Equal(Blue, state.CurrentPlayer.Id); // the host's turn is gone: the first open one
        Assert.False(days.Leave(Red)); // not open any more
        Assert.True(days.Leave(Blue));
        Assert.False(state.IsSimultaneous);
    }

    [Fact]
    public void ASimultaneousGame_Replays_ToTheSameState()
    {
        var state = Duel(Simultaneous);
        var initial = MapConverter.ToDocument(state);
        var session = Started(state);
        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();

        var replayed = GameReplay.Replay(GameReplay.Record(initial, session));
        Assert.Equal(GameStateDigest.Compute(session.State), GameStateDigest.Compute(replayed.State));
        Assert.Equal([Blue, Red], replayed.State.OpenTurns);
    }

    [Fact]
    public void TheComputerPlayers_PlayAfterThePeople_AndTheHostsColourIsTheCurrentPlayerWhileItsTurnIsOpen()
    {
        var session = Started(ThreeColours(Simultaneous with { HostPlayer = Green }, computer: Red));
        var state = session.State;
        Assert.Equal([Blue, Green], state.OpenTurns);
        Assert.Equal(Green, state.CurrentPlayer.Id);
        Assert.False(AdventureAi.IsComputerTurn(state));

        session.Execute(new EndTurnCommand(Green)).ThrowIfRejected();
        Assert.Equal(Blue, state.CurrentPlayer.Id);
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();

        Assert.Equal(Red, state.CurrentPlayer.Id); // the computer's turn, after the people's
        Assert.True(AdventureAi.IsComputerTurn(state));
        Assert.True(new AdventureAi(session).PlayTurn());
        Assert.Equal(2, state.Date.AbsoluteDay);
        Assert.Equal([Blue, Green], state.OpenTurns);
    }

    [Fact]
    public void AHeroEnteringAnotherPersonsStartZone_StopsTheDay_WhichIsPlayedAgainTurnByTurn()
    {
        var state = Duel(Simultaneous, zoneWidth: 8, gap: 1);
        var initial = MapConverter.ToDocument(state);
        var session = Started(state);
        var conflicts = new List<SimultaneousDayConflicted>();
        session.Events.Subscribe<SimultaneousDayConflicted>(conflicts.Add);
        var gold = state.GetPlayer(Blue).Resources.Gold;

        while (state.SimultaneousConflict is null && Moved(session.Execute(new StepHeroCommand(Blue, A, Direction.East))))
        {
        }

        var reason = Assert.Single(conflicts).Reason;
        Assert.Contains("Blue's hero Alpha entered Red's start zone", reason + " " + string.Join(' ', conflicts.Select(c => c.Reason)), StringComparison.Ordinal);
        Assert.StartsWith("The day is played again turn by turn", session.Execute(new StepHeroCommand(Red, B, Direction.West)).Error);

        // The host goes back to the day's start and plays it turn by turn: no second income, and every day after it is turn by turn too.
        var record = GameReplay.Record(initial, session);
        var again = GameReplay.Replay(record with
        {
            Commands = [.. record.Commands.Take(session.SimultaneousDayStart), new PlayTurnByTurnCommand(Blue, reason)],
            FinalDigest = null,
        });
        Assert.False(again.State.IsSimultaneous);
        Assert.True(again.State.SimultaneousEnded);
        Assert.Equal(Blue, again.State.CurrentPlayer.Id);
        Assert.Equal(gold, again.State.GetPlayer(Blue).Resources.Gold);
        Assert.False(again.Execute(new StepHeroCommand(Red, B, Direction.West)).IsAccepted);
        again.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Assert.Equal(Red, again.State.CurrentPlayer.Id);
        again.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        Assert.Equal(2, again.State.Date.AbsoluteDay);
        Assert.False(again.State.IsSimultaneous);
    }

    [Fact]
    public void APersonSeeingAnotherPersonsHero_StopsTheDay()
    {
        var session = Started(Duel(Simultaneous, zoneWidth: 2, gap: 30));
        var state = session.State;
        for (var day = 0; day < 7 && state.SimultaneousConflict is null; day++)
        {
            while (state.SimultaneousConflict is null && Moved(session.Execute(new StepHeroCommand(Blue, A, Direction.East))))
            {
            }

            if (state.SimultaneousConflict is null)
            {
                session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
                session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
            }
        }

        Assert.StartsWith("Blue saw Red's hero Bravo", state.SimultaneousConflict);
    }

    [Fact]
    public void Allies_MayEnterEachOthersStartZones_AndSeeEachOthersHeroes()
    {
        var state = Duel(Simultaneous, zoneWidth: 8, gap: 1);
        state.AssignTeams(new Dictionary<PlayerId, TeamId> { [Blue] = new(0), [Red] = new(0) });
        var session = Started(state);

        while (state.GetHero(A).Position.X < 14 && Moved(session.Execute(new StepHeroCommand(Blue, A, Direction.East))))
        {
        }

        Assert.Equal(Red, state.Map.GetStartZone(state.GetHero(A).Position));
        Assert.True(state.CanSee(Blue, state.GetHero(B)));
        Assert.Null(state.SimultaneousConflict);
        Assert.True(state.IsSimultaneous);
        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Message.Contains("start zones touch", StringComparison.Ordinal));
    }

    [Fact]
    public void TheDaysArePlayedTurnByTurn_WithoutAStartZoneForEveryPerson_OrAfterTheFirstWeek()
    {
        var state = Duel(Simultaneous);
        foreach (var tile in state.Map.StartZoneTiles(Red).ToList())
        {
            state.Map.SetStartZone(tile, null);
        }

        Assert.False(Started(state).State.IsSimultaneous);

        var session = Started(Duel(Simultaneous, gap: 40));
        for (var day = 1; day <= 7; day++)
        {
            Assert.True(session.State.IsSimultaneous);
            session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
            session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        }

        Assert.Equal(8, session.State.Date.AbsoluteDay);
        Assert.False(session.State.IsSimultaneous);
        Assert.Equal(Blue, session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void ThePersonTheComputerTakesOver_PlaysItsTurnAfterThePeople()
    {
        var session = Started(Duel(Simultaneous));
        session.Execute(new HandToComputerCommand(Red)).ThrowIfRejected();
        Assert.Equal([Blue], session.State.OpenTurns);

        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Assert.Equal(Red, session.State.CurrentPlayer.Id);
        Assert.True(new AdventureAi(session).PlayTurn());
        Assert.Equal(2, session.State.Date.AbsoluteDay);
        Assert.False(session.State.IsSimultaneous); // one person left: turn by turn
    }

    [Fact]
    public void OnALanGame_EachClientPlaysItsOwnOpenTurn_AndAConflictHasTheHostPlayTheDayAgain()
    {
        var state = Duel(Simultaneous with { HostPlayer = Blue }, zoneWidth: 8, gap: 1);
        var initial = MapConverter.ToDocument(state);
        GameSession.CheckRefusals = true;
        var session = new GameSession(state);
        session.Start();
        var host = new GameHost(session, [Red]);
        host.RecordOf = name => GameReplay.Record(initial, host.Session, name);
        GameSession? replaced = null;
        host.SessionReplaced += s => replaced = s;
        var lines = new List<string>();

        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var red = new GameClient(clientEnd);
        red.ChatReceived += (_, text) => lines.Add(text);
        red.Join("Bob", Red);
        Pump(host, red);
        Assert.Equal(Red, red.Session!.State.CurrentPlayer.Id); // its own turn is open on its computer
        Assert.Equal(Blue, session.State.CurrentPlayer.Id); // and the host's on the host

        var first = red.Session;
        for (var i = 0; i < 12 && replaced is null; i++)
        {
            red.Session!.Execute(new StepHeroCommand(Red, B, Direction.West));
            Pump(host, red);
        }

        Assert.NotNull(replaced);
        Assert.Same(replaced, host.Session);
        Assert.False(replaced!.State.IsSimultaneous);
        Assert.Equal(Blue, replaced.State.CurrentPlayer.Id);
        Assert.NotSame(first, red.Session);
        Assert.Equal(Blue, red.Session!.State.CurrentPlayer.Id);
        Assert.Contains(lines, l => l.Contains("entered Blue's start zone", StringComparison.Ordinal) && l.EndsWith("turn by turn, and so is every day after it.", StringComparison.Ordinal));

        // The game goes on in the new session.
        replaced.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Pump(host, red);
        Assert.Equal(Red, red.Session.State.CurrentPlayer.Id);
        red.Session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        Pump(host, red);
        Assert.Equal(replaced.CommandLog.Count, host.RecordOf(null).Commands.Count);
    }

    [Fact]
    public void TheTurnTimer_IsOneClockForTheDay_AndEndsEveryOpenTurnWhenItIsUp()
    {
        var session = Started(Duel(Simultaneous with { TurnTimerMinutes = 2 }));
        var timer = new TurnTimer(session);
        timer.Tick(30);
        Assert.Equal([Blue, Red], timer.TimedPlayers);

        // Blue ends its turn: the day's clock runs on for Red, not a new turn's.
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        timer.Tick(30);
        Assert.Equal(60, timer.SecondsLeft);
        Assert.Equal([Red], timer.TimedPlayers);

        timer.Tick(60);
        Assert.Equal(2, session.State.Date.AbsoluteDay);
        Assert.Equal([Blue, Red], session.State.OpenTurns);
        timer.Tick(0);
        Assert.Equal(120, timer.SecondsLeft); // a new day, the full time

        // Up with both turns open: both end, and the next day opens.
        timer.Tick(120);
        Assert.Equal(3, session.State.Date.AbsoluteDay);
        Assert.Equal([new EndTurnCommand(Blue), new EndTurnCommand(Red)], session.CommandLog.TakeLast(2));
    }

    [Fact]
    public void OnALanGame_TheHostsClockIsTheOnlyOne_ItEndsTheClientsTurn_AndTheClientShowsIt()
    {
        var state = Duel(Simultaneous with { HostPlayer = Blue, TurnTimerMinutes = 1 });
        GameSession.CheckRefusals = true;
        var session = new GameSession(state);
        session.Start();
        var host = new GameHost(session, [Red]);
        var (hostEnd, clientEnd) = LoopbackConnection.Pair();
        host.Accept(hostEnd);
        var red = new GameClient(clientEnd);
        red.Join("Bob", Red);
        Pump(host, red);
        Assert.Null(red.Clock);

        var timer = new TurnTimer(session, host.ExecuteAsHost);
        Assert.False(session.Execute(new EndTurnCommand(Red)).IsAccepted); // the host's screen does not play Red
        timer.Tick(10.5);
        host.ShowClock(timer.Clock);
        Pump(host, red);
        Assert.Equal(new TurnClock(50, false, false), red.Clock);

        timer.Tick(50);
        host.ShowClock(timer.Clock);
        Pump(host, red);
        Assert.Equal(2, session.State.Date.AbsoluteDay);
        Assert.Equal(2, red.Session!.State.Date.AbsoluteDay);
        Assert.Equal([Blue, Red], session.CommandLog.TakeLast(2).Select(c => c.Player));

        timer.Tick(0);
        host.ShowClock(timer.Clock);
        Pump(host, red);
        Assert.Equal(new TurnClock(60, false, false), red.Clock);
    }

    /// <summary>
    /// Blue's hero A on the left, Red's B on the right, <paramref name="gap"/> tiles between their zones, each <paramref name="zoneWidth"/> tiles
    /// wide; the heroes stand at their zones' outer ends.
    /// </summary>
    private static GameState Duel(GameOptions options, int zoneWidth = 3, int gap = 20)
    {
        var width = (2 * zoneWidth) + gap;
        var row = "A" + new string('.', width - 2) + "B";
        var state = Create(options, row, new string('.', width));
        for (var y = 0; y < 2; y++)
        {
            for (var x = 0; x < zoneWidth; x++)
            {
                state.Map.SetStartZone(new MapPosition(x, y), Blue);
                state.Map.SetStartZone(new MapPosition(width - 1 - x, y), Red);
            }
        }

        return state;
    }

    private static GameState ThreeColours(GameOptions options, PlayerId computer)
    {
        var two = Duel(options, gap: 30);
        var heroes = two.Heroes.ToList();
        var middle = new MapPosition(two.Map.Width / 2, 0);
        var state = new GameState(two.Map,
            [new Player(Blue, "Blue", computer != Blue), new Player(Red, "Red", computer != Red), new Player(Green, "Green", computer != Green)],
            [heroes[0], heroes[1], new Hero(C, "Charlie", Green, middle, DefaultArmy())], options: options);
        state.Map.SetStartZone(middle, Green);
        return state;
    }

    private static bool Moved(CommandResult result) => result is { IsAccepted: true, Outcome: MoveResult.Moved };

    private static GameSession Started(GameState state)
    {
        GameSession.CheckRefusals = true;
        var session = new GameSession(state);
        session.Start();
        return session;
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
