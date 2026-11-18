using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Turns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The game director (ADR-222): the colours played here, when the computer players play, when the turn clock runs and ends a turn, and which
/// person's turn the map turns to.
/// </summary>
public class GameDirectorTests
{
    private static readonly GameOptions TwoMinutes = new(TurnTimerMinutes: 2);

    [Fact]
    public void OnOneComputer_EveryPersonPlaysHere_AndTheMapShowsTheCurrentPlayer()
    {
        var state = Create("A...", "...B");
        state.GetPlayer(Red).IsHuman = false;
        var director = new GameDirector(Start(state));

        Assert.True(director.PlaysHere(Blue));
        Assert.False(director.PlaysHere(Red)); // a computer
        Assert.False(director.PlaysElsewhere(Red));
        Assert.Equal(Blue, director.Viewer());
        Assert.False(director.IsPaused);
    }

    [Fact]
    public void TheViewer_IsTheFirstPlayerHere_WhenTheCurrentPlaysElsewhere_ElseTheCurrent()
    {
        var session = Start(Create("A...", "...B")); // Blue's turn

        Assert.Equal(Red, new GameDirector(session, new LocalSeats { PlaysElsewhere = p => p == Blue }).Viewer());
        Assert.Equal(Blue, new GameDirector(session, new LocalSeats { PlaysElsewhere = _ => true }).Viewer());
    }

    [Fact]
    public void TheComputer_PlaysItsTurn_OnlyWhenNothingHoldsTheGame()
    {
        var state = Create("A...", "...B");
        state.GetPlayer(Blue).IsHuman = false;
        var session = Start(state);
        var paused = true;
        var director = new GameDirector(session, new LocalSeats { IsPaused = () => paused });

        Assert.NotNull(director.Ai);
        Assert.False(director.PlayComputer(screensOpen: false)); // paused
        paused = false;
        Assert.False(director.PlayComputer(screensOpen: true)); // the menu, a battle scene or the auto combat question
        Assert.Equal(Blue, state.CurrentPlayer.Id);

        Assert.True(director.PlayComputer(screensOpen: false));
        Assert.Equal(Red, state.CurrentPlayer.Id);
        Assert.False(director.PlayComputer(screensOpen: false)); // a person's turn
    }

    [Fact]
    public void TheComputer_WaitsForABattle_OnlyWhereTheBattleIsPlayedElsewhere()
    {
        Assert.False(PlayAfterAttackingAPerson(new LocalSeats { ComputersWaitForBattles = true }));
        Assert.True(PlayAfterAttackingAPerson(LocalSeats.OneComputer));

        static bool PlayAfterAttackingAPerson(LocalSeats seats)
        {
            var state = Create("A.B");
            state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
            state.GetPlayer(Blue).IsHuman = false; // Red stays a person
            var director = new GameDirector(Start(state), seats);
            Assert.True(director.PlayComputer(screensOpen: false));
            Assert.NotNull(state.Battle); // Red commands its side
            return director.PlayComputer(screensOpen: false);
        }
    }

    [Fact]
    public void TheReplayViewer_RunsNeitherTheComputerNorTheClock()
    {
        var state = Create(TwoMinutes, "A...", "...B");
        state.GetPlayer(Blue).IsHuman = false;
        var watching = new GameDirector(Start(state), watching: true);

        Assert.Null(watching.Ai);
        Assert.Null(watching.Timer);
        Assert.False(watching.PlayComputer(screensOpen: false));
        Assert.Null(watching.RunClock(1, showingComputerMoves: false, screensOpen: false));
        Assert.Equal(Blue, state.CurrentPlayer.Id);
    }

    [Fact]
    public void TheClock_IsOff_WithoutATurnTimer()
    {
        var director = new GameDirector(Start(Create("A...", "...B")));

        Assert.Null(director.Timer);
        Assert.Null(director.RunClock(1000, showingComputerMoves: false, screensOpen: false));
    }

    [Fact]
    public void TheClock_RunsAndWarns_AndEndsTheTurnWhenTheTimeIsUp()
    {
        var session = Start(Create(TwoMinutes, "A...", "...B"));
        var director = new GameDirector(session);
        var warnings = new List<int>();

        for (var i = 0; i < 119; i++)
        {
            var frame = director.RunClock(1, showingComputerMoves: false, screensOpen: false)!.Value;
            if (frame.Warning is { } warning)
            {
                warnings.Add(warning);
            }
        }

        Assert.Equal([60, 30, 15, 5], warnings);
        Assert.Equal(new ClockFrame(1, false, false, null), director.RunClock(0, showingComputerMoves: false, screensOpen: false));
        Assert.Equal(Blue, session.State.CurrentPlayer.Id);

        director.RunClock(1, showingComputerMoves: false, screensOpen: false);
        Assert.Equal(Red, session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void TheClock_StandsStill_WhileComputerMovesAreShown_AndWhileTheGameIsPaused()
    {
        var session = Start(Create(TwoMinutes, "A...", "...B"));
        var paused = false;
        var director = new GameDirector(session, new LocalSeats { IsPaused = () => paused });

        director.RunClock(1000, showingComputerMoves: true, screensOpen: false);
        paused = true;
        var frame = director.RunClock(1000, showingComputerMoves: false, screensOpen: false);

        Assert.Equal(120, frame!.Value.SecondsLeft);
        Assert.Equal(Blue, session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void ATurnPlayedHere_DoesNotEndOnTheClock_WhileAScreenIsOpen()
    {
        var session = Start(Create(TwoMinutes, "A...", "...B"));
        var director = new GameDirector(session);

        var frame = director.RunClock(200, showingComputerMoves: false, screensOpen: true);

        Assert.True(frame!.Value.Up);
        Assert.Equal(Blue, session.State.CurrentPlayer.Id);

        director.RunClock(0, showingComputerMoves: false, screensOpen: false);
        Assert.Equal(Red, session.State.CurrentPlayer.Id);
    }

    [Fact]
    public void ATurnPlayedElsewhere_EndsOnTheClock_WhateverThisScreenShows_ThroughTheSeatsCommands()
    {
        var session = Start(Create(TwoMinutes, "A...", "...B"));
        var run = new List<GameCommand>();
        var shared = new List<TurnClock>();
        var director = new GameDirector(session, new LocalSeats
        {
            PlaysElsewhere = p => p == Blue,
            Execute = command =>
            {
                run.Add(command);
                return session.Execute(command);
            },
            ShareClock = shared.Add,
        });

        director.RunClock(200, showingComputerMoves: false, screensOpen: true);

        Assert.Equal([new EndTurnCommand(Blue)], run);
        Assert.Equal(Red, session.State.CurrentPlayer.Id);
        Assert.Equal(new TurnClock(0, false, true), shared[^1]);

        director.RunClock(0, showingComputerMoves: false, screensOpen: true);
        Assert.Equal(new TurnClock(120, false, false), shared[^1]); // Red's turn, with the full time
    }

    [Fact]
    public void AClockShownFromElsewhere_WarnsWhenItPassesAWarning()
    {
        TurnClock? clock = null;
        var director = new GameDirector(Start(Create("A...", "...B")), new LocalSeats { SharedClock = () => clock });

        Assert.Null(director.RunClock(1, showingComputerMoves: false, screensOpen: false)); // nothing sent yet
        clock = new TurnClock(62, false, false);
        Assert.Equal(new ClockFrame(62, false, false, null), director.RunClock(1, showingComputerMoves: false, screensOpen: false));
        clock = new TurnClock(59, false, false);
        Assert.Equal(60, director.RunClock(1, showingComputerMoves: false, screensOpen: false)!.Value.Warning);
        Assert.Null(director.RunClock(1, showingComputerMoves: false, screensOpen: false)!.Value.Warning);
        clock = new TurnClock(null, true, false);
        Assert.Equal(new ClockFrame(null, true, false, null), director.RunClock(1, showingComputerMoves: false, screensOpen: false));
    }

    [Fact]
    public void APersonsTurn_IsShownAtOnce_OrOnceTheComputersMovesHaveBeenShown()
    {
        var state = Create("A...", "...B");
        state.GetPlayer(Red).IsHuman = false;
        var director = new GameDirector(Start(state));

        Assert.True(director.TurnStarted(Blue, showingComputerMoves: false));
        Assert.False(director.HasTurnToShow);
        Assert.False(director.TurnStarted(Red, showingComputerMoves: false)); // a computer's turn leaves the map with the last person

        Assert.False(director.TurnStarted(Blue, showingComputerMoves: true));
        Assert.True(director.HasTurnToShow);
        Assert.Equal(Blue, director.TakeTurnToShow());
        Assert.False(director.HasTurnToShow);
        Assert.Null(director.TakeTurnToShow());
    }

    [Fact]
    public void ATurnPlayedElsewhere_IsNotShownHere()
    {
        var director = new GameDirector(Start(Create("A...", "...B")), new LocalSeats { PlaysElsewhere = p => p == Red });

        Assert.False(director.TurnStarted(Red, showingComputerMoves: false));
        Assert.False(director.TurnStarted(Red, showingComputerMoves: true));
        Assert.False(director.HasTurnToShow);
    }
}
