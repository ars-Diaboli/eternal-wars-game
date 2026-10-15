using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

public class GateTests
{
    // Monolith footprint is ["B", "V"]: placed at (x, 0) its entrance is (x, 1).
    private static readonly string[] Field =
    [
        "A.........",
        "..........",
        "..........",
    ];

    private static (GameState State, OneWayGateEntrance Entrance, OneWayGateExit Exit) OneWayWorld()
    {
        var state = Create(Field);
        var entrance = Add<OneWayGateEntrance>(state, BuiltInMapObjects.OneWayMonolithEntrance, 2, 0); // entrance (2,1)
        var exit = Add<OneWayGateExit>(state, BuiltInMapObjects.OneWayMonolithExit, 8, 0);              // landing (8,1)
        entrance.Exit = exit.Id;
        return (state, entrance, exit);
    }

    [Fact]
    public void OneWayGate_PathTeleportsForward()
    {
        var (state, _, exit) = OneWayWorld();
        var session = Start(state);

        var path = session.Movement.FindPath(A, new MapPosition(9, 2))!;

        var teleport = Assert.Single(path.Steps, s => s.Kind == PathStepKind.Teleport);
        Assert.Equal(exit.VisitPosition, teleport.Position);
        Assert.Equal(141 + 100 + 141, path.TotalCost);
    }

    [Fact]
    public void OneWayGate_CannotBeUsedBackwards()
    {
        var (state, _, _) = OneWayWorld();
        state.GetHero(A).Position = new MapPosition(9, 2);
        var session = Start(state);

        var path = session.Movement.FindPath(A, new MapPosition(0, 2))!;

        Assert.DoesNotContain(path.Steps, s => s.Kind == PathStepKind.Teleport);
    }

    [Fact]
    public void OneWayExit_CanBeWalkedOver()
    {
        var (state, _, exit) = OneWayWorld();
        state.GetHero(A).Position = new MapPosition(7, 1);
        var session = Start(state);

        var path = session.Movement.FindPath(A, new MapPosition(9, 1))!;

        Assert.Equal(200, path.TotalCost);
        Assert.Contains(path.Steps, s => s.Position == exit.VisitPosition);
        Assert.True(session.Movement.MoveTo(A, new MapPosition(9, 1)).Arrived);
    }

    [Fact]
    public void OneWayGate_MoveToTeleportsHero()
    {
        var (state, entrance, exit) = OneWayWorld();
        var session = Start(state);
        HeroTeleported? teleported = null;
        session.Events.Subscribe<HeroTeleported>(e => teleported = e);

        var outcome = session.Movement.MoveTo(A, new MapPosition(9, 2));

        Assert.True(outcome.Arrived);
        Assert.Equal(new HeroTeleported(A, entrance.VisitPosition, exit.VisitPosition), teleported);
    }

    [Fact]
    public void DanglingGateLink_ActsAsDeadEndInsteadOfCrashing()
    {
        var (state, entrance, _) = OneWayWorld();
        entrance.Exit = new MapObjectId(99);
        var session = Start(state);

        Assert.DoesNotContain(session.Movement.FindPath(A, new MapPosition(9, 2))!.Steps, s => s.Kind == PathStepKind.Teleport);
        Assert.True(session.Movement.MoveTo(A, entrance.VisitPosition).Arrived);
    }

    [Fact]
    public void TwoWayMonolith_WorksBothWaysOnTheSameLevel()
    {
        var state = Create(Field);
        var west = Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 2, 0);
        var east = Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 8, 0);
        Link(west, east);
        var session = Start(state);

        var there = session.Movement.FindPath(A, new MapPosition(9, 2))!;
        state.GetHero(A).Position = new MapPosition(9, 2);
        var back = session.Movement.FindPath(A, new MapPosition(0, 2))!;

        Assert.Contains(there.Steps, s => s.Kind == PathStepKind.Teleport && s.Position == east.VisitPosition);
        Assert.Contains(back.Steps, s => s.Kind == PathStepKind.Teleport && s.Position == west.VisitPosition);
    }

    [Fact]
    public void Editor_SetsExitAndRemovingExitUnlinksEntrancesWithUndo()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10));
        editor.PlaceObject(BuiltInMapObjects.OneWayMonolithEntrance, new MapPosition(3, 3), out var first);
        editor.PlaceObject(BuiltInMapObjects.OneWayMonolithEntrance, new MapPosition(5, 3), out var second);
        editor.PlaceObject(BuiltInMapObjects.OneWayMonolithExit, new MapPosition(8, 3), out var exit);

        Assert.Equal(EditResult.InvalidTarget, editor.SetGateExit(first, second)); // not an exit
        Assert.Equal(EditResult.Ok, editor.SetGateExit(first, exit));
        Assert.Equal(EditResult.Ok, editor.SetGateExit(second, exit));

        editor.RemoveObject(exit);
        Assert.Null(Entrance(editor, first).Exit);
        Assert.Null(Entrance(editor, second).Exit);

        editor.Undo();
        Assert.Equal(exit, Entrance(editor, first).Exit);
        Assert.Equal(exit, Entrance(editor, second).Exit);
    }

    [Fact]
    public void Validator_ReportsUnusedExitsAndEntrancesWithoutExit()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10));
        editor.PlaceObject(BuiltInMapObjects.OneWayMonolithEntrance, new MapPosition(3, 3), out _);
        editor.PlaceObject(BuiltInMapObjects.OneWayMonolithExit, new MapPosition(8, 3), out _);

        var issues = MapValidator.Validate(editor.Scenario);

        Assert.Contains(issues, i => i.Message.Contains("has no exit"));
        Assert.Contains(issues, i => i.Message.Contains("not used by any entrance"));
    }

    private static OneWayGateEntrance Entrance(MapEditorSession editor, MapObjectId id) =>
        (OneWayGateEntrance)editor.Scenario.GetObject(id);
}
