using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// When a game shown on this computer is autosaved (ADR-058, ADR-231): once started, then at each turn's start once the command that began the
/// turn has settled; never in the replay viewer nor on a LAN client.
/// </summary>
public class AutosaveScheduleTests
{
    [Fact]
    public void AGame_IsSavedOnceStarted_ThenAfterTheCommandThatBeganEachTurn()
    {
        var session = Start(Create("A...B"));
        var schedule = new AutosaveSchedule(session);
        var saves = 0;
        session.Events.Subscribe<TurnStarted>(_ => schedule.TurnStarted());
        session.Events.Subscribe<CommandExecuted>(_ => saves += schedule.CommandExecuted() ? 1 : 0);

        Assert.True(schedule.Started());
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        Assert.Equal(0, saves); // not a turn's start

        session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected(); // Red's turn starts inside it
        Assert.Equal(1, saves);
        session.Execute(new StepHeroCommand(Red, B, Direction.West)).ThrowIfRejected();
        Assert.Equal(1, saves);
    }

    [Fact]
    public void ATurnStartedBeforeTheGameStarted_IsTheStartsSave()
    {
        var schedule = new AutosaveSchedule(Start(Create("A...B")));
        schedule.TurnStarted();

        Assert.True(schedule.Started());
        Assert.False(schedule.CommandExecuted());
    }

    [Fact]
    public void TheReplayViewer_AndALanClient_NeverAutosave()
    {
        var watched = new AutosaveSchedule(Start(Create("A...B")), watching: true);
        var client = new AutosaveSchedule(GameSession.FollowingHost(Create("A...B"), new EventBus()));

        foreach (var schedule in new[] { watched, client })
        {
            Assert.False(schedule.Saves);
            Assert.False(schedule.Started());
            schedule.TurnStarted();
            Assert.False(schedule.CommandExecuted());
        }
    }
}
