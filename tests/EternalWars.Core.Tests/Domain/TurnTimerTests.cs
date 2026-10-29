using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Turns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The turn timer (ADR-091, FLOW-3): the clock, its warnings, the defaults it answers and when it ends a turn.</summary>
public class TurnTimerTests
{
    private static readonly GameOptions TwoMinutes = new(TurnTimerMinutes: 2);

    [Fact]
    public void TheTimer_WarnsAndEndsTheTurn_AndTheNextTurnHasTheFullTime()
    {
        var session = Start(Create(TwoMinutes, "A...", "...B"));
        var timer = new TurnTimer(session);

        var warnings = new List<int?>();
        for (var i = 0; i < 119; i++)
        {
            warnings.Add(timer.Tick(1));
        }

        Assert.Equal([60, 30, 15, 5], warnings.OfType<int>());
        Assert.Equal(1, timer.SecondsLeft);
        Assert.Equal(Blue, session.State.CurrentPlayer.Id);

        timer.Tick(1);
        Assert.Equal(Red, session.State.CurrentPlayer.Id);
        Assert.IsType<EndTurnCommand>(session.CommandLog[^1]);

        timer.Tick(0.5);
        Assert.Equal(119.5, timer.SecondsLeft);
    }

    [Fact]
    public void TheTimer_IsOffByDefault_AndForComputerPlayers()
    {
        var off = Start(Create("A...", "...B"));
        Assert.Null(new TurnTimer(off).SecondsLeft);

        var state = Create(TwoMinutes, "A...", "...B");
        state.GetPlayer(Blue).IsHuman = false;
        var timer = new TurnTimer(Start(state));
        Assert.Null(timer.Tick(1000));
        Assert.Equal(Blue, state.CurrentPlayer.Id);
    }

    [Fact]
    public void WhenTheTimeIsUp_AChestGivesGold_AndTheTurnEnds()
    {
        var state = Create(TwoMinutes, "A...", "...B");
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 2, 0);
        chest.FixedValues = new ChestValues(1500, 1000);
        var session = Start(state);
        session.MoveTo(A, chest.VisitPosition);
        Assert.IsType<TreasureChestDecision>(state.PendingDecision);

        new TurnTimer(session).Tick(200);

        Assert.Equal(1500, state.GetPlayer(Blue).Resources.Gold);
        Assert.Equal(Red, state.CurrentPlayer.Id);
    }

    [Fact]
    public void WhenTheTimeIsUp_ALevelUpWaitsForThePlayer_ThenTheTurnEnds()
    {
        var state = Create(TwoMinutes, "A...", "...B");
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 2, 0);
        chest.FixedValues = new ChestValues(1500, 1000);
        var session = Start(state);
        session.MoveTo(A, chest.VisitPosition);
        session.Execute(new ResolveTreasureChestCommand(Blue, TreasureChestReward.Experience)).ThrowIfRejected();
        var timer = new TurnTimer(session);

        timer.Tick(200);
        var levelUp = Assert.IsType<LevelUpDecision>(state.PendingDecision);
        Assert.True(timer.IsUp);
        Assert.Equal(Blue, state.CurrentPlayer.Id);

        session.Execute(new ChooseSkillCommand(Blue, levelUp.Options[0])).ThrowIfRejected();
        timer.Tick(0.1);
        Assert.Equal(Red, state.CurrentPlayer.Id);
    }

    [Fact]
    public void TheFrontEnd_HoldsTheEndOfTheTurn()
    {
        var session = Start(Create(TwoMinutes, "A...", "...B"));
        var timer = new TurnTimer(session);

        timer.Tick(200, canEnd: false);
        Assert.True(timer.IsUp);
        Assert.Equal(Blue, session.State.CurrentPlayer.Id);

        timer.Tick(0, canEnd: true);
        Assert.Equal(Red, session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void TheClock_StopsInABattleBetweenTwoHumans()
    {
        var state = Create(TwoMinutes, "A.B", "...");
        var session = Start(state);
        var timer = new TurnTimer(session);
        timer.Tick(10);
        session.MoveTo(A, new MapPosition(2, 0));
        Assert.NotNull(state.Battle);

        timer.Tick(1000);

        Assert.True(timer.IsStopped);
        Assert.Equal(110, timer.SecondsLeft);
    }

    [Fact]
    public void ABattleAgainstTheComputer_IsFinishedFirst_ThenTheTurnEnds()
    {
        // A attacks the far stronger B and loses; Blue plays on with C (else the game would be over).
        var state = Create(TwoMinutes, "A.B", "..C");
        state.GetPlayer(Red).IsHuman = false;
        state.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 100));
        var session = Start(state);
        var timer = new TurnTimer(session);
        session.MoveTo(A, new MapPosition(2, 0));

        timer.Tick(1000);
        Assert.False(timer.IsStopped);
        Assert.IsType<BattleDecision>(state.PendingDecision);

        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        Assert.Null(state.PendingDecision);
        Assert.Equal(Blue, state.CurrentPlayer.Id);

        timer.Tick(0);
        Assert.Equal(Red, state.CurrentPlayer.Id);
    }

    [Fact]
    public void TheTimer_IsAGameOption_KeptBySavesAndReplays()
    {
        var state = Create(TwoMinutes, "A...", "...B");
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(MapConverter.ToGameState(map, options: TwoMinutes));
        session.Start();
        new TurnTimer(session).Tick(200);

        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(map, session))));

        Assert.Equal(2, replayed.State.Options.TurnTimerMinutes);
        Assert.Equal(Red, replayed.State.CurrentPlayer.Id);
    }
}
