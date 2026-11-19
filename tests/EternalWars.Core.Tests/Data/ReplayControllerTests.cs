using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Settings;

namespace EternalWars.Core.Tests.Data;

/// <summary>
/// The replay viewer's stepping (ADR-164, ADR-222): a command a frame once the views have caught up, the pauses at a turn's start and on a
/// battle's result, play and pause, jumps, skipping a battle, playing one on, and a record that diverges.
/// </summary>
public class ReplayControllerTests
{
    /// <summary>The recorded game of <see cref="ReplayPlayerTests"/>: two days ended, a battle with trolls on day 3 (position 5 on), ends of turns.</summary>
    private static ReplayController Open(AnimationSpeed speed = AnimationSpeed.Normal) =>
        new(ReplayPlayer.Open(ReplayPlayerTests.Recorded(out _)), speed);

    [Fact]
    public void ItPlays_ACommandAFrame_ToTheEnd_WhichPausesIt()
    {
        var replay = Open(AnimationSpeed.Instant);
        Assert.True(replay.IsPlaying);

        var frames = 0;
        while (replay.Advance(0.1, default).Kind == ReplayAdvance.Stepped)
        {
            frames++;
        }

        Assert.Equal(replay.Player.Count, frames);
        Assert.True(replay.Player.IsAtEnd);
        Assert.False(replay.IsPlaying);
        replay.Toggle(); // nothing to play
        Assert.False(replay.IsPlaying);
        Assert.Equal(ReplayAdvance.Waited, replay.Advance(0.1, default).Kind);
    }

    [Fact]
    public void NothingRuns_WhilePaused_OrWhileTheViewsCatchUp()
    {
        var replay = Open();

        Assert.Equal(ReplayAdvance.Waited, replay.Advance(1, new ReplayViews(MenuOpen: true)).Kind);
        replay.Toggle();
        Assert.False(replay.IsPlaying);
        Assert.Equal(ReplayAdvance.Waited, replay.Advance(1, default).Kind);
        Assert.Equal(0, replay.Player.Position);

        replay.Toggle();
        Assert.Equal(ReplayAdvance.Stepped, replay.Advance(1, default).Kind);
        Assert.Equal(1, replay.Player.Position);
    }

    [Fact]
    public void ATurnsStart_PausesAMoment_AtTheReplaysSpeed()
    {
        var normal = Open();
        normal.TurnStarted(); // half a second at normal speed
        Assert.Equal(ReplayAdvance.Waited, normal.Advance(0.3, default).Kind);
        Assert.Equal(ReplayAdvance.Waited, normal.Advance(0.3, default).Kind);
        Assert.Equal(ReplayAdvance.Stepped, normal.Advance(0.3, default).Kind);

        var instant = Open(AnimationSpeed.Instant);
        instant.TurnStarted();
        Assert.Equal(ReplayAdvance.Stepped, instant.Advance(0, default).Kind);
    }

    [Fact]
    public void ABattlesResult_StaysAMoment_ThenIsLeft()
    {
        var replay = Open();

        Assert.Equal(ReplayAdvance.Waited, replay.Advance(1, new ReplayViews(ResultShown: true)).Kind);
        Assert.Equal(ReplayAdvance.LeaveResult, replay.Advance(0.5, new ReplayViews(ResultShown: true)).Kind);
        Assert.Equal(ReplayAdvance.Waited, replay.Advance(1, new ReplayViews(ResultShown: true)).Kind); // a new moment
        Assert.Equal(0, replay.Player.Position);
    }

    [Fact]
    public void Seeking_ForgetsThePauses()
    {
        var replay = Open();
        replay.TurnStarted();
        replay.Advance(1, new ReplayViews(ResultShown: true));

        replay.Seek(replay.Player.DayPosition(2));

        Assert.Equal(2, replay.Player.Position);
        Assert.Equal(ReplayAdvance.Stepped, replay.Advance(0, default).Kind);
        replay.Advance(1, new ReplayViews(ResultShown: true));
        replay.Seek(0);
        Assert.Equal(ReplayAdvance.Waited, replay.Advance(1, new ReplayViews(ResultShown: true)).Kind); // the result's moment starts again
    }

    [Fact]
    public void SkippingABattle_RunsItsCommands_ToItsEnd()
    {
        var replay = Open();
        replay.Seek(5); // the battle with the trolls
        var end = replay.Player.BattleEndPosition();

        replay.SkipBattle();

        Assert.Equal(end, replay.Player.Position);
        Assert.Null(replay.Player.Session.State.Battle);
    }

    [Fact]
    public void ABattle_IsPlayedOn_InASeparateGame_WhileTheReplayWaitsPaused()
    {
        var replay = Open();
        Assert.Null(replay.TakeOver()); // on the map: "Play from here" plays the game on instead
        Assert.False(replay.BackToReplay());

        replay.Seek(5);
        var session = replay.TakeOver();

        Assert.NotNull(session);
        Assert.NotSame(replay.Player.Session, session);
        Assert.True(replay.IsTakingOver);
        Assert.False(replay.IsPlaying);
        Assert.Null(replay.TakeOver()); // once

        Assert.True(replay.BackToReplay());
        Assert.False(replay.IsTakingOver);
        Assert.False(replay.IsPlaying);
        Assert.Equal(5, replay.Player.Position);
    }

    [Fact]
    public void AGameThatDiffersFromItsRecord_PausesTheReplay_AndIsPutBackWhereItWas()
    {
        var replay = Open();
        var diverged = replay.Player.Session;
        diverged.Execute(new EndTurnCommand(diverged.State.CurrentPlayer.Id)).ThrowIfRejected(); // from outside the record

        var frame = replay.Advance(0, default);

        Assert.Equal(ReplayAdvance.Diverged, frame.Kind);
        Assert.Contains("diverged", frame.Problem);
        Assert.False(replay.IsPlaying);
        Assert.Equal(0, replay.Player.Position);
        Assert.NotSame(diverged, replay.Player.Session); // a new session, as the record has it
        Assert.Empty(replay.Player.Session.CommandLog);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void TheMenu_HeroesSteps_AndABattlesAnimations_EachHoldTheReplay(bool menu, bool steps, bool battle)
    {
        var replay = Open(AnimationSpeed.Instant);
        var views = new ReplayViews(MenuOpen: menu, StepsPlaying: steps, BattleAnimating: battle);

        Assert.True(views.Hold);
        Assert.Equal(ReplayAdvance.Waited, replay.Advance(1, views).Kind);
        Assert.Equal(ReplayAdvance.Stepped, replay.Advance(1, default).Kind);
        Assert.False(new ReplayViews(ResultShown: true).Hold); // a result stays its moment, then is left
    }

    [Fact]
    public void PlayFromHere_IsTheBattlesOnInABattle_AndTheGamesOnTheMap()
    {
        var replay = Open();

        Assert.Equal(replay.Player.CanPlayOn, replay.CanPlayFromHere(battleShown: false));
        Assert.Equal(replay.Player.CanTakeOver, replay.CanPlayFromHere(battleShown: true));
        replay.Seek(5); // in the battle with the trolls
        Assert.Equal(replay.Player.CanTakeOver, replay.CanPlayFromHere(battleShown: true));
        Assert.True(replay.CanPlayFromHere(battleShown: true));
    }

    [Fact]
    public void TheMap_IsSeenThroughTheChosenPlayer_OrWhole()
    {
        var replay = Open();
        var state = replay.Player.Session.State;

        Assert.Null(replay.Viewer(state));
        replay.ViewerIndex = 2;
        Assert.Equal(TestWorld.Red, replay.Viewer(state));
        replay.ViewerIndex = 3;
        Assert.Null(replay.Viewer(state));
    }

    [Fact]
    public void TheStatus_SaysWhereTheReplayIs_AndItsEnd()
    {
        var replay = Open();
        var state = replay.Player.Session.State;
        Assert.Equal($"{state.Date}, Blue's turn   ·   command 0 of {replay.Player.Count}", replay.Status());

        replay.Seek(replay.Player.Count);
        Assert.EndsWith("   ·   The end of the replay", replay.Status());
    }
}
