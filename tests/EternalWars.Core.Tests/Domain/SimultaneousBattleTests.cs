using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Turns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Battles and questions per player on a simultaneous day (ADR-366): each open turn has its own battle, question and map events, so the people
/// fight and answer at the same time and nobody waits for another; a battle against what another person is fighting is a conflict; the record
/// replays to the same state; a day played turn by turn after it keeps each open battle for its person's turn.
/// </summary>
public class SimultaneousBattleTests
{
    private static readonly GameOptions Simultaneous = new(FogMode.ShroudWithRefog) { SimultaneousFirstWeek = true };

    [Fact]
    public void ThePeople_FightTheirOwnBattles_AtTheSameTime_AndTheRecordReplaysToTheSameState()
    {
        var state = Fights(Simultaneous);
        var initial = MapConverter.ToDocument(state);
        var session = SimultaneousDayTests.Started(state);

        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        var blues = Assert.IsType<BattleState>(state.BattleOf(Blue));
        Assert.Same(blues, state.Battle); // Blue's open turn is the one the game shows between commands
        Assert.Equal(Blue, state.SlotPlayer);
        Assert.Equal("No battle is being fought.", session.Execute(new BattleDefendCommand(Red)).Error); // Red fights none yet

        // Red does not wait for Blue's battle: it fights its own at the same time.
        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        var reds = Assert.IsType<BattleState>(state.BattleOf(Red));
        Assert.NotSame(blues, reds);
        Assert.Equal([blues, reds], state.OpenBattles);
        Assert.Same(blues, state.Battle);
        Assert.IsType<BattleDecision>(state.DecisionFor(Red));
        Assert.Contains("\nslot 1\n", GameStateDigest.Describe(state), StringComparison.Ordinal);

        // Each commands its own battle, in any order; the other battle stays as it was.
        var blueRound = blues.Round;
        session.Execute(new BattleDefendCommand(Red)).ThrowIfRejected();
        Assert.Equal(blueRound, blues.Round);
        session.Execute(new BattleDefendCommand(Blue)).ThrowIfRejected();

        // A record taken with both battles open replays to the same state.
        Assert.Equal(GameStateDigest.Describe(state), GameStateDigest.Describe(GameReplay.Replay(GameReplay.Record(initial, session)).State));

        session.Execute(new AutoResolveBattleCommand(Red)).ThrowIfRejected();
        Assert.Null(state.BattleOf(Red));
        Assert.Same(blues, state.BattleOf(Blue));
        AnswerLevelUps(session, Red);
        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected(); // Blue still fights
        Assert.Equal([Blue], state.OpenTurns);

        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        Assert.Empty(state.OpenBattles);
        AnswerLevelUps(session, Blue);
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Assert.Equal(2, state.Date.AbsoluteDay);
        Assert.Empty(state.Objects.OfType<MonsterGuard>());

        var replayed = GameReplay.Replay(GameReplay.Record(initial, session));
        Assert.Equal(GameStateDigest.Describe(state), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void AQuestion_IsAnsweredWhileAnotherPersonFights_EachSeeingOnlyItsOwn()
    {
        var state = Fights(Simultaneous);
        var width = state.Map.Width;
        state.RemoveObject(state.Objects.OfType<MonsterGuard>().Single(m => m.Position.X > 1));
        Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, width - 2, 0);
        var session = SimultaneousDayTests.Started(state);
        var gold = state.GetPlayer(Red).Resources.Gold;

        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();

        Assert.IsType<TreasureChestDecision>(state.DecisionFor(Red));
        Assert.IsType<BattleDecision>(state.DecisionFor(Blue));
        Assert.Null(GameView.Of(state, Red).Battle);
        Assert.IsType<TreasureChestDecision>(GameView.Of(state, Red).Decision);
        Assert.Same(state.BattleOf(Blue), GameView.Of(state, Blue).Battle);
        Assert.IsType<BattleDecision>(GameView.Of(state, Blue).Decision);

        // Blue fights on while Red's question waits, and Red answers while Blue's battle goes on.
        session.Execute(new BattleDefendCommand(Blue)).ThrowIfRejected();
        session.Execute(new ResolveTreasureChestCommand(Red, TreasureChestReward.Gold)).ThrowIfRejected();
        Assert.True(state.GetPlayer(Red).Resources.Gold > gold);
        Assert.NotNull(state.BattleOf(Blue));
        Assert.Null(state.DecisionFor(Red));
        Assert.Equal("Resolve the pending decision before ending the turn.", session.Execute(new EndTurnCommand(Blue)).Error);
        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
    }

    [Fact]
    public void EachPersonsMapEvent_AsksItsOwnQuestion_AtTheSameTime()
    {
        var state = SimultaneousDayTests.Duel(Simultaneous);
        var width = state.Map.Width;
        state.SetMapEvents(new MapEventSet
        {
            Variables = new Dictionary<string, int> { ["blue"] = 0, ["red"] = 0 },
            Cells = [new EventCell(1, 0, 0, "blue asked"), new EventCell(width - 2, 0, 0, "red asked")],
            Events = [Asking("blue asked", "blue"), Asking("red asked", "red")],
        });
        var session = SimultaneousDayTests.Started(state);

        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected(); // Blue's question waits meanwhile
        Assert.IsType<MapEventDecision>(state.DecisionFor(Blue));
        Assert.IsType<MapEventDecision>(state.DecisionFor(Red));
        Assert.Equal(Blue, state.DecisionPlayer); // the host's turn's, between commands
        Assert.Contains(" running blue asked", GameStateDigest.Describe(state, GameStateDigest.SecondVersion), StringComparison.Ordinal); // an older digest's one queue
        Assert.Contains("slot 1 running red asked", GameStateDigest.Describe(state), StringComparison.Ordinal);

        session.Execute(new AnswerMapEventCommand(Red, 1)).ThrowIfRejected();
        session.Execute(new AnswerMapEventCommand(Blue, 0)).ThrowIfRejected();

        Assert.Equal(1, state.MapEvents.Variable("blue"));
        Assert.Equal(2, state.MapEvents.Variable("red"));
        Assert.Null(state.DecisionFor(Blue));
        Assert.Null(state.DecisionFor(Red));

        static MapEventDefinition Asking(string name, string variable) => new(name, MapEventKind.Hero,
        [
            new QuestionAction("Which?",
            [
                new EventAnswer("First", [new SetVariableAction(variable, VariableOp.Set, new NumberExpr(1))]),
                new EventAnswer("Second", [new SetVariableAction(variable, VariableOp.Set, new NumberExpr(2))]),
            ]),
            new EnableEventAction(null, false),
        ]);
    }

    [Fact]
    public void ABattleAgainstWhatAnotherPersonIsFighting_IsAConflict_AlliesToo()
    {
        // Blue's zone on the left, Red's on the right, one monster between them that both heroes reach in a step.
        var state = Create(Simultaneous, "A...B", ".....");
        foreach (var (x, owner) in new[] { (0, Blue), (1, Blue), (3, Red), (4, Red) })
        {
            state.Map.SetStartZone(new MapPosition(x, 0), owner);
        }

        state.AssignTeams(new Dictionary<PlayerId, TeamId> { [Blue] = new(0), [Red] = new(0) });
        Strong(state);
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 2, 0);
        (monster.Creature, monster.Count, monster.Disposition) = (BuiltInCreatures.Troll, 2, GuardDisposition.Savage);
        var session = SimultaneousDayTests.Started(state);

        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        Assert.NotNull(state.BattleOf(Blue));
        Assert.Null(state.SimultaneousConflict);

        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        Assert.Equal("Red attacked what Blue is fighting.", state.SimultaneousConflict);
        Assert.StartsWith("The day is played again turn by turn", session.Execute(new BattleDefendCommand(Blue)).Error);

        // Played on turn by turn here (no LAN host to play the day again): the later battle goes unfought, the trolls are Blue's to beat.
        session.Execute(new PlayTurnByTurnCommand(Blue, state.SimultaneousConflict!)).ThrowIfRejected();
        Assert.Null(state.BattleOf(Red));
        Assert.Same(state.BattleOf(Blue), state.Battle);
        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        AnswerLevelUps(session, Blue);
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Assert.Equal(Red, state.CurrentPlayer.Id);
        Assert.Null(state.Battle);
        Assert.Null(state.FindObject(monster.Id));
        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
    }

    [Fact]
    public void ABattleBetweenTwoPeoplesArmies_IsAConflict()
    {
        // The heroes see each other when the day opens, so only the attack is one.
        var state = Create(Simultaneous, "..AB..", "......");
        foreach (var (x, owner) in new[] { (0, Blue), (1, Blue), (4, Red), (5, Red) })
        {
            state.Map.SetStartZone(new MapPosition(x, 0), owner);
        }

        Strong(state);
        var session = SimultaneousDayTests.Started(state);
        Assert.True(state.IsSimultaneous);

        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();

        Assert.Equal("Blue attacked Red.", state.SimultaneousConflict);
        Assert.StartsWith("The day is played again turn by turn", session.Execute(new StepHeroCommand(Red, B, Direction.East)).Error);
    }

    [Fact]
    public void WhenTheTimeIsUp_AnOpenBattleHoldsOnlyItsOwnTurn()
    {
        var session = SimultaneousDayTests.Started(Fights(Simultaneous with { TurnTimerMinutes = 1 }));
        var state = session.State;
        var timer = new TurnTimer(session);
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();

        timer.Tick(61);

        Assert.Equal([Blue], state.OpenTurns); // Red's turn ended; Blue's waits for its battle
        Assert.Equal(1, state.Date.AbsoluteDay);
        Assert.NotNull(state.BattleOf(Blue));
    }

    [Fact]
    public void ThePersonTheComputerTakesOverInABattle_HasItFoughtToItsEnd_AndTheOtherBattleGoesOn()
    {
        var session = SimultaneousDayTests.Started(Fights(Simultaneous));
        var state = session.State;
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        var blues = state.BattleOf(Blue);

        session.Execute(new HandToComputerCommand(Red)).ThrowIfRejected();

        Assert.Null(state.BattleOf(Red));
        Assert.Same(blues, state.BattleOf(Blue));
        Assert.Equal([Blue], state.OpenTurns);
        Assert.Same(blues, state.Battle);
    }

    [Fact]
    public void ASaveLoadedOnOneComputer_GoesOnTurnByTurn_EachOpenBattleInItsPersonsTurn()
    {
        var state = Fights(Simultaneous);
        var initial = MapConverter.ToDocument(state);
        var played = SimultaneousDayTests.Started(state);
        played.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        played.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        var saved = GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(initial, played, "both in battle")));

        var session = GameReplay.Replay(saved);
        Assert.Equal(2, session.State.OpenBattles.Count);
        SavedGameStart.GoOn(session, [], lan: false);
        var loaded = session.State;

        // Blue's turn first, with its battle; Red's battle waits for Red's turn.
        Assert.False(loaded.IsSimultaneous);
        Assert.Equal(Blue, loaded.CurrentPlayer.Id);
        Assert.Same(loaded.BattleOf(Blue), loaded.Battle);
        var redRound = loaded.BattleOf(Red)!.Round;
        Assert.False(session.Execute(new BattleDefendCommand(Red)).IsAccepted); // not Red's turn: its battle waits
        Assert.Equal(redRound, loaded.BattleOf(Red)!.Round);
        Assert.False(session.Execute(new EndTurnCommand(Blue)).IsAccepted);

        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        AnswerLevelUps(session, Blue);
        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        Assert.Equal(Red, loaded.CurrentPlayer.Id);
        Assert.Same(loaded.BattleOf(Red), loaded.Battle);
        session.Execute(new AutoResolveBattleCommand(Red)).ThrowIfRejected();
        AnswerLevelUps(session, Red);
        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();

        Assert.Equal(2, loaded.Date.AbsoluteDay);
        Assert.False(loaded.IsSimultaneous);
        Assert.Empty(loaded.OpenBattles);
    }

    [Fact]
    public void TheReplayViewer_FollowsOneBattleAtATime_TheOthersFoughtOffScreen()
    {
        var state = Fights(Simultaneous);
        var initial = MapConverter.ToDocument(state);
        var played = SimultaneousDayTests.Started(state);
        played.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        played.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        played.Execute(new BattleDefendCommand(Red)).ThrowIfRejected();
        played.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        played.Execute(new BattleDefendCommand(Red)).ThrowIfRejected();
        var player = ReplayPlayer.Open(GameReplay.Record(initial, played));
        var watched = player.Session.State;

        player.Step(); // Blue's battle begins: the viewer shows it
        Assert.Equal(Blue, player.Session.Watched);
        Assert.Same(watched.BattleOf(Blue), watched.Battle);
        player.Step(); // Red's begins meanwhile, and goes on: still Blue's
        player.Step();
        Assert.Equal(Blue, player.Session.Watched);
        Assert.Same(watched.BattleOf(Blue), watched.Battle);

        player.Step(); // Blue's is over: Red's, which goes on
        Assert.Equal(Red, player.Session.Watched);
        Assert.Same(watched.BattleOf(Red), watched.Battle);
        Assert.True(player.IsInBattle(player.Position));

        // A jump back follows the battles alike.
        player.Seek(2);
        Assert.Equal(Blue, player.Session.Watched);
        Assert.Same(player.Session.State.BattleOf(Blue), player.Session.State.Battle);
    }

    [Fact]
    public void TheEventsOfAnotherPersonsBattle_ComeWhileTheGameIsNotInTheShownTurn()
    {
        var session = SimultaneousDayTests.Started(Fights(Simultaneous with { HostPlayer = Blue }));
        var heard = new List<(bool Shown, PlayerId? Slot)>();
        session.Events.Subscribe<BattleStarted>(_ => heard.Add((session.InShownTurn, session.State.SlotPlayer)));
        session.Events.Subscribe<DecisionRequested>(_ => heard.Add((session.InShownTurn, session.State.SlotPlayer)));

        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        Assert.True(session.InShownTurn); // between commands: the host's turn
        Assert.Null(session.State.Battle);
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();

        Assert.Equal([(false, Red), (false, Red), (true, Blue), (true, Blue)], heard);
        Assert.Same(session.State.BattleOf(Blue), session.State.Battle);
    }

    /// <summary>
    /// <see cref="SimultaneousDayTests.Duel"/> with strong armies and a few savage trolls in front of each hero: a step towards the other hero
    /// attacks them.
    /// </summary>
    internal static GameState Fights(GameOptions options, int zoneWidth = 3, int gap = 20)
    {
        var state = SimultaneousDayTests.Duel(options, zoneWidth, gap);
        Strong(state);
        foreach (var x in new[] { 1, state.Map.Width - 2 })
        {
            var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, x, 0);
            (monster.Creature, monster.Count, monster.Disposition) = (BuiltInCreatures.Troll, 2, GuardDisposition.Savage);
        }

        return state;
    }

    internal static void Strong(GameState state)
    {
        foreach (var hero in state.Heroes)
        {
            hero.Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 60));
        }
    }

    /// <summary>Takes the first skill of each level-up <paramref name="player"/> is asked.</summary>
    internal static void AnswerLevelUps(GameSession session, PlayerId player)
    {
        while (session.State.DecisionFor(player) is LevelUpDecision levelUp)
        {
            session.Execute(new ChooseSkillCommand(player, levelUp.Options[0])).ThrowIfRejected();
        }
    }
}
