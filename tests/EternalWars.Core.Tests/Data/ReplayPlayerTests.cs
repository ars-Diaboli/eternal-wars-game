using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Data;

/// <summary>The replay viewer's core (ADR-101, ADR-164): stepping, the turn and day index, seeking, battles and "Play from here".</summary>
public class ReplayPlayerTests
{
    /// <summary>
    /// Two human players end two days, then Blue's hero attacks trolls on day 3 and fights two rounds by hand before quick combat, then both
    /// end the turn.
    /// </summary>
    internal static GameRecordDocument Recorded(out GameSession played)
    {
        var state = TestWorld.Create(".A.....", ".......", "......B");
        state.GetHero(TestWorld.A).Army.Set(1, new CreatureStack(BuiltInCreatures.Archer, 15));
        var monster = TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 2, 1);
        (monster.Creature, monster.Count, monster.Disposition) = (BuiltInCreatures.Troll, 4, GuardDisposition.Savage);
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 77)));
        session.Start();
        for (var i = 0; i < 4; i++)
        {
            session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id)).ThrowIfRejected();
        }

        session.Execute(new StepHeroCommand(TestWorld.Blue, TestWorld.A, Direction.SouthEast)).ThrowIfRejected();
        for (var i = 0; i < 2 && session.State.Battle is not null; i++)
        {
            session.Execute(new BattleDefendCommand(TestWorld.Blue)).ThrowIfRejected();
        }

        if (session.State.Battle is not null)
        {
            session.Execute(new AutoResolveBattleCommand(TestWorld.Blue)).ThrowIfRejected();
        }

        session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id)).ThrowIfRejected();
        session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id)).ThrowIfRejected();
        played = session;
        return GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(map, session)));
    }

    [Fact]
    public void Stepping_ThroughTheRecord_ReachesTheRecordedState_AndPublishesTheEvents()
    {
        var record = Recorded(out var played);
        var player = ReplayPlayer.Open(record);
        var moves = 0;
        player.Session.Events.Subscribe<CommandExecuted>(_ => moves++);

        Assert.Equal(0, player.Position);
        while (player.Step())
        {
        }

        Assert.True(player.IsAtEnd);
        Assert.Equal(record.Commands.Count, player.Position);
        Assert.Equal(record.Commands.Count, moves);
        Assert.Equal(GameStateDigest.Describe(played.State), GameStateDigest.Describe(player.Session.State));
        Assert.False(player.Step());
    }

    [Fact]
    public void AJumpForward_PlaysOnTheWatchedGame_UnheardByItsViews_ToTheSameState()
    {
        // ADR-234: a jump forward costs only the commands it skips; the views are built again on the state afterwards.
        var record = Recorded(out var played);
        var player = ReplayPlayer.Open(record);
        player.Seek(2);
        var watched = player.Session;
        var heard = 0;
        watched.Events.Subscribe<CommandExecuted>(_ => heard++);

        player.Seek(player.Count);

        Assert.Same(watched, player.Session);
        Assert.Equal(0, heard);
        Assert.Equal(player.Count, player.Position);
        Assert.Equal(GameStateDigest.Describe(played.State), GameStateDigest.Describe(player.Session.State));

        player.Seek(4); // back: a new game replayed from the start
        Assert.NotSame(watched, player.Session);
        Assert.Equal(4, player.Position);
        var fresh = ReplayPlayer.Open(record);
        fresh.Seek(4);
        Assert.Equal(GameStateDigest.Describe(fresh.Session.State), GameStateDigest.Describe(player.Session.State));

        player.Step();
        var stepped = player.Session;
        player.Reload(); // the record's game put back where it is
        Assert.NotSame(stepped, player.Session);
        Assert.Equal(5, player.Position);
        Assert.Equal(GameStateDigest.Describe(stepped.State), GameStateDigest.Describe(player.Session.State));
    }

    [Fact]
    public void TheTurns_AreIndexed_ByWhereTheyStart_TheirDayAndPlayer()
    {
        var player = ReplayPlayer.Open(Recorded(out _));

        Assert.Equal(
            [(1, TestWorld.Blue), (1, TestWorld.Red), (2, TestWorld.Blue), (2, TestWorld.Red), (3, TestWorld.Blue), (3, TestWorld.Red), (4, TestWorld.Blue)],
            player.Turns.Select(t => (t.Date.AbsoluteDay, t.Player)));
        Assert.Equal([0, 1, 2, 3, 4], player.Turns.Take(5).Select(t => t.Position));
        Assert.Equal(4, player.LastDay);
        Assert.Equal(player.Count, player.Turns[^1].Position);
    }

    [Fact]
    public void Seeking_ToADay_StartsItsFirstTurn_BackOrForward_LikeStepping()
    {
        var record = Recorded(out _);
        var player = ReplayPlayer.Open(record);
        var stepped = ReplayPlayer.Open(record);

        player.Seek(player.DayPosition(3));
        Assert.Equal(3, player.Session.State.Date.AbsoluteDay);
        Assert.Equal(TestWorld.Blue, player.Session.State.CurrentPlayer.Id);
        Assert.Equal(4, player.Position);
        while (stepped.Position < player.Position)
        {
            stepped.Step();
        }

        Assert.Equal(GameStateDigest.Describe(stepped.Session.State), GameStateDigest.Describe(player.Session.State));

        player.Seek(player.DayPosition(1)); // back
        Assert.Equal(1, player.Session.State.Date.AbsoluteDay);
        Assert.Equal(0, player.Position);
        Assert.Equal(0, player.DayPosition(-5));
        Assert.Equal(player.DayPosition(4), player.DayPosition(99));
    }

    [Fact]
    public void NextTurn_AndNextDay_AreWhereTheyStart_OrTheEnd()
    {
        var player = ReplayPlayer.Open(Recorded(out _));

        Assert.Equal(1, player.NextTurnPosition());
        Assert.Equal(2, player.NextDayPosition());
        player.Seek(player.NextDayPosition());
        Assert.Equal(2, player.CurrentTurn.Date.AbsoluteDay);
        player.Seek(player.DayPosition(4));
        Assert.Equal(player.Count, player.NextTurnPosition());
        Assert.Equal(player.Count, player.NextDayPosition());
    }

    [Fact]
    public void InABattle_TheViewerKnowsWhereItEnds_AndMayTakeItOver_WithoutChangingTheReplay()
    {
        var player = ReplayPlayer.Open(Recorded(out _));
        Assert.False(player.CanTakeOver);
        Assert.Null(player.BattleEndPosition());
        Assert.Throws<InvalidOperationException>(() => player.TakeOver());

        player.Seek(5); // after the step into the trolls
        Assert.True(player.IsInBattle(5));
        Assert.NotNull(player.Session.State.Battle);
        Assert.True(player.CanTakeOver);
        var end = player.BattleEndPosition()!.Value;
        Assert.True(end > 5);
        Assert.False(player.IsInBattle(end));

        var watched = GameStateDigest.Describe(player.Session.State);
        var own = player.TakeOver();
        Assert.NotSame(player.Session, own);
        Assert.Equal(watched, GameStateDigest.Describe(own.State));
        own.Execute(new AutoResolveBattleCommand(TestWorld.Blue)).ThrowIfRejected();
        Assert.Null(own.State.Battle);
        Assert.Equal(watched, GameStateDigest.Describe(player.Session.State));
        Assert.Equal(5, player.Position);

        player.Seek(end);
        Assert.Null(player.Session.State.Battle);
        Assert.True(player.Step()); // the replay goes on from where the battle ended
    }

    [Fact]
    public void ABattleEndedByQuickCombat_IsTakenOver_FromJustBeforeItsEnd()
    {
        // ADR-233: quick combat plays the rest of the battle in one command, which the viewer then shows move by move.
        var record = Recorded(out _);
        var player = ReplayPlayer.Open(record);
        player.Seek(5);
        var end = player.BattleEndPosition()!.Value;
        Assert.IsType<AutoResolveBattleCommand>(record.Commands[end - 1]);
        player.Seek(end - 1);
        var before = GameStateDigest.Describe(player.Session.State);

        player.Seek(end);
        Assert.True(player.TakesOverBeforeItsEnd);
        Assert.False(player.TakesOverFromTheStart);
        Assert.True(player.CanTakeOver);
        var own = player.TakeOver();

        Assert.Equal(before, GameStateDigest.Describe(own.State));
        Assert.Equal(TestWorld.Blue, own.Battles.Commander(BattleSide.Attacker));
        Assert.Equal(end, player.Position);
        Assert.True(player.Step());
        Assert.False(player.CanTakeOver);
    }

    /// <summary>
    /// Blue, a person, ends its turn; Red, the computer, steps its hero into trolls, a battle the core fights within that one command (position
    /// 2 is after it); then Red ends its turn.
    /// </summary>
    internal static GameRecordDocument RecordedComputerBattle()
    {
        var state = TestWorld.Create(".A.....", ".......", "......B");
        state.GetHero(TestWorld.B).Army.Set(1, new CreatureStack(BuiltInCreatures.Archer, 15));
        var monster = TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 5, 1);
        (monster.Creature, monster.Count, monster.Disposition) = (BuiltInCreatures.Troll, 4, GuardDisposition.Savage);
        state.GetPlayer(TestWorld.Red).IsHuman = false; // in the map, so the record replays it as the computer
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: new GameOptions(Seed: 78)));
        session.Start();
        session.Execute(new EndTurnCommand(TestWorld.Blue)).ThrowIfRejected();
        session.Execute(new StepHeroCommand(TestWorld.Red, TestWorld.B, Direction.NorthWest)).ThrowIfRejected();
        Assert.Null(session.State.Battle); // fought whole within the step
        session.Execute(new EndTurnCommand(TestWorld.Red)).ThrowIfRejected();
        var recorded = GameReplay.Record(map, session);
        return GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(recorded));
    }

    [Fact]
    public void ABattleOnlyTheComputerFought_IsTakenOver_FromItsStart_WithAPersonCommandingTheAttacker()
    {
        // ADR-233: the battle has no point between its moves in the record, so it is fought again from its start.
        var player = ReplayPlayer.Open(RecordedComputerBattle());
        player.Seek(1); // before the step: nothing to take over
        Assert.False(player.CanTakeOver);
        Assert.Null(player.TakeOverAttacker);

        Assert.True(player.Step());
        Assert.False(player.IsInBattle(player.Position));
        Assert.True(player.TakesOverFromTheStart);
        Assert.True(player.CanTakeOver);
        Assert.Equal(TestWorld.Red, player.TakeOverAttacker);
        var watched = GameStateDigest.Describe(player.Session.State);

        var own = player.TakeOver();

        Assert.NotSame(player.Session, own);
        Assert.True(own.State.GetPlayer(TestWorld.Red).IsHuman);
        Assert.NotNull(own.State.Battle);
        Assert.Equal(TestWorld.Red, own.Battles.Commander(BattleSide.Attacker));
        Assert.Null(own.Battles.Commander(BattleSide.Defender)); // the trolls stay the computer's
        own.Execute(new AutoResolveBattleCommand(TestWorld.Red)).ThrowIfRejected();
        Assert.Null(own.State.Battle);

        // The record and the watched game do not change.
        Assert.False(player.Session.State.GetPlayer(TestWorld.Red).IsHuman);
        Assert.Equal(watched, GameStateDigest.Describe(player.Session.State));
        Assert.Equal(2, player.Position);
        Assert.True(player.Step());
        Assert.False(player.CanTakeOver); // only right after the battle's command
    }

    [Fact]
    public void TheController_TakesOverTheComputersBattle_AndSaysWhoThePersonPlays()
    {
        var replay = new ReplayController(ReplayPlayer.Open(RecordedComputerBattle()));
        replay.Seek(2);
        Assert.True(replay.CanPlayFromHere(battleShown: true));

        var session = replay.TakeOver(out var problem);

        Assert.NotNull(session);
        Assert.Null(problem);
        Assert.Contains("from its start as Red", replay.TakeOverStatus, StringComparison.Ordinal);
        Assert.True(replay.BackToReplay());
        Assert.Empty(replay.TakeOverStatus);
    }

    [Fact]
    public void AComputersColour_IsHandedToAPerson_OnceAndOnlyWhileInTheGame()
    {
        var player = ReplayPlayer.Open(RecordedComputerBattle());
        player.Seek(1); // Red's turn
        var session = player.Session;
        var handed = new List<PlayerId>();
        session.Events.Subscribe<PlayerHandedToPerson>(e => handed.Add(e.Player));

        session.Execute(new HandToPersonCommand(TestWorld.Red)).ThrowIfRejected();

        Assert.True(session.State.GetPlayer(TestWorld.Red).IsHuman);
        Assert.Equal([TestWorld.Red], handed);
        Assert.False(session.Execute(new HandToPersonCommand(TestWorld.Red)).IsAccepted); // a person plays it already
        session.State.GetPlayer(TestWorld.Red).IsHuman = false;
        session.State.GetPlayer(TestWorld.Red).IsDefeated = true;
        Assert.False(session.Execute(new HandToPersonCommand(TestWorld.Red)).IsAccepted); // out of the game
    }

    [Fact]
    public void OnTheMap_TheGameMayBePlayedOn_AsItsOwnGame_WhoseLogIsTheRecordCutThere()
    {
        var record = Recorded(out _);
        var player = ReplayPlayer.Open(record);
        player.Seek(player.DayPosition(2));
        Assert.True(player.CanPlayOn);
        Assert.False(player.CanTakeOver);

        var watched = GameStateDigest.Describe(player.Session.State);
        var own = player.PlayOn();
        Assert.NotSame(player.Session, own);
        Assert.Equal(watched, GameStateDigest.Describe(own.State));
        Assert.Equal(record.Commands.Take(player.Position), own.CommandLog);

        own.Execute(new EndTurnCommand(own.State.CurrentPlayer.Id)).ThrowIfRejected();
        Assert.Equal(watched, GameStateDigest.Describe(player.Session.State));
        Assert.Equal(2, player.Position);

        // Saved, it loads like any save: replayed to the same state.
        var saved = GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(record.Map, own, saveName: "on")));
        var loaded = ReplayPlayer.Open(saved);
        loaded.Seek(loaded.Count);
        Assert.Equal(GameStateDigest.Describe(own.State), GameStateDigest.Describe(loaded.Session.State));
    }

    [Fact]
    public void PlayingOn_IsNotOffered_InABattle_OrWithoutAPersonInTheGame()
    {
        var player = ReplayPlayer.Open(Recorded(out _));
        player.Seek(5); // in the battle with the trolls: that is "take over"
        Assert.False(player.CanPlayOn);
        Assert.Throws<InvalidOperationException>(() => player.PlayOn());

        player.Seek(0);
        foreach (var p in player.Session.State.Players)
        {
            p.IsHuman = false;
        }

        Assert.False(player.CanPlayOn);
    }

    [Fact]
    public void AGamePlayedOn_IsShown_ThroughTheCurrentPerson_ElseTheNextInTurnOrder()
    {
        var state = TestWorld.Create(".A.....", ".......", "......B");
        Assert.Equal(TestWorld.Blue, ReplayPlayer.PersonToShow(state));

        state.GetPlayer(TestWorld.Blue).IsHuman = false;
        Assert.Equal(TestWorld.Red, ReplayPlayer.PersonToShow(state));

        state.GetPlayer(TestWorld.Red).IsDefeated = true;
        Assert.Null(ReplayPlayer.PersonToShow(state));
    }

    [Fact]
    public void ASave_PlaysToItsLastCommand_InTheBattleItWasSavedIn()
    {
        var record = Recorded(out _);
        var save = record with { Commands = record.Commands.Take(6).ToList(), FinalDigest = null };
        var player = ReplayPlayer.Open(save);

        player.Seek(player.Count);
        Assert.True(player.IsAtEnd);
        Assert.NotNull(player.Session.State.Battle);
        Assert.Equal(player.Count, player.BattleEndPosition());
    }

    [Fact]
    public void ARecord_ThatDoesNotReplay_FailsToOpen()
    {
        var record = Recorded(out _);

        Assert.Throws<InvalidOperationException>(() => ReplayPlayer.Open(record with { FinalDigest = "other" }));
        Assert.Throws<InvalidOperationException>(() =>
            ReplayPlayer.Open(record with { Commands = [new EndTurnCommand(TestWorld.Red)] })); // not Red's turn
    }

    [Fact]
    public void TheReplaysFolder_ListsItsReplays_NewestFirst_WithTheirMap()
    {
        var folder = Path.Combine(Path.GetTempPath(), "ew-replays-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Empty(GameSaves.ListReplays(folder));
            var record = Recorded(out var played);
            var map = record.Map;
            GameSaves.SaveReplay(folder, "first", map, played);
            File.SetLastWriteTimeUtc(Path.Combine(folder, "first" + GameRecordSerializer.FileExtension), DateTime.UtcNow.AddMinutes(-5));
            GameSaves.SaveReplay(folder, "second", map, played);
            File.WriteAllText(Path.Combine(folder, "broken" + GameRecordSerializer.FileExtension), "{");
            File.SetLastWriteTimeUtc(Path.Combine(folder, "broken" + GameRecordSerializer.FileExtension), DateTime.UtcNow.AddMinutes(-10));

            var listed = GameSaves.ListReplays(folder);

            Assert.Equal(["second", "first"], listed.Where(e => e.Info is not null).Select(e => e.Info!.Name));
            Assert.Equal(map.Name, listed[0].Info!.Map);
            Assert.NotNull(Assert.Single(listed, e => e.Info is null).Error);
            Assert.Empty(GameSaves.List(folder)); // saves are another kind of file
        }
        finally
        {
            if (Directory.Exists(folder))
            {
                Directory.Delete(folder, recursive: true);
            }
        }
    }
}
