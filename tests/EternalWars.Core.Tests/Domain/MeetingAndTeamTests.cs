using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

public class MeetingAndTeamTests
{
    [Fact]
    public void SteppingOntoOwnHero_OpensExchangeWithoutMoving()
    {
        var session = Start(Create("AC..", "...B"));
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);

        var result = session.Movement.TryStep(A, Direction.East);

        Assert.Equal(MoveResult.Met, result);
        Assert.Equal(new MapPosition(0, 0), session.State.GetHero(A).Position);
        Assert.Equal(1500, session.State.GetHero(A).MovementPoints);
        Assert.Equal(new HeroExchangeDecision(A, C), session.State.PendingDecision);
        Assert.Contains(new HeroesMet(A, C), events);
        Assert.Throws<InvalidOperationException>(session.Turns.EndTurn);

        session.Interactions.CompleteExchange();

        Assert.Null(session.State.PendingDecision);
    }

    [Fact]
    public void MoveTo_OwnHero_WalksUpAndMeets()
    {
        var session = Start(Create("A....C", ".....B"));

        var path = session.Movement.FindPath(A, new MapPosition(5, 0))!;
        var outcome = session.Movement.MoveTo(A, new MapPosition(5, 0));

        Assert.Equal(new MapPosition(5, 0), path.Destination);
        Assert.Equal(new MovementOutcome(4, MoveResult.Met, Arrived: false), outcome);
        Assert.Equal(new MapPosition(4, 0), session.State.GetHero(A).Position);
    }

    [Fact]
    public void MoveTo_VisibleEnemy_TriggersBattleWithoutAmbush()
    {
        var session = Start(Create("A...B", "....."));
        BattleTriggered? battle = null;
        session.Events.Subscribe<BattleTriggered>(e => battle = e);

        var outcome = session.Movement.MoveTo(A, new MapPosition(4, 0));

        Assert.Equal(MoveResult.BattleStarted, outcome.StopReason);
        Assert.Equal(new BattleTriggered(A, B, IsAmbush: false), battle);
    }

    [Fact]
    public void Allies_BlockEachOther()
    {
        var state = Create("AB..", "....");
        state.AssignTeams(new Dictionary<PlayerId, TeamId> { [Blue] = new(7), [Red] = new(7) });
        var session = Start(state);

        Assert.True(state.AreAllies(Blue, Red));
        Assert.False(state.AreEnemies(Blue, Red));
        Assert.Equal(MoveResult.Occupied, session.Movement.TryStep(A, Direction.East));
        Assert.Null(session.Movement.FindPath(A, new MapPosition(1, 0)));
    }

    [Fact]
    public void Teams_DefaultToFreeForAll_AndRespectLockAndStart()
    {
        var state = Create("A..B");

        Assert.True(state.AreEnemies(Blue, Red));

        state.SetTeamsLocked(true);
        Assert.Throws<InvalidOperationException>(() => state.AssignTeams(new Dictionary<PlayerId, TeamId> { [Red] = new(0) }));

        state.SetTeamsLocked(false);
        Start(state);
        Assert.Throws<InvalidOperationException>(() => state.AssignTeams(new Dictionary<PlayerId, TeamId> { [Red] = new(0) }));
    }

    [Fact]
    public void Editor_TeamsAndLockAreUndoable()
    {
        var editor = new MapEditorSession(BlankScenario.Create(10, 8));

        editor.SetPlayerTeam(new PlayerId(1), new TeamId(0));
        editor.SetTeamsLocked(true);
        Assert.True(editor.Scenario.AreAllies(new PlayerId(0), new PlayerId(1)));
        Assert.True(editor.Scenario.TeamsLocked);

        editor.Undo();
        editor.Undo();
        Assert.False(editor.Scenario.TeamsLocked);
        Assert.True(editor.Scenario.AreEnemies(new PlayerId(0), new PlayerId(1)));
    }
}
