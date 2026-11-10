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
    private static GameRecordDocument Recorded(out GameSession played)
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
