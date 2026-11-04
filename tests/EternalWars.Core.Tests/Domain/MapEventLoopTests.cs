using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Map events, step 3 (ADR-115): loops, running another event, and a Pandora's Box that runs a hero event (MAP-35).</summary>
public class MapEventLoopTests
{
    private static NumberExpr N(int value) => new(value);

    private static ResourceAction Gold(int amount) => new(ResourceType.Gold, N(amount));

    /// <summary>The hero event "cell" with the actions, on the cell (2, 1), plus the other events.</summary>
    private static MapEventSet Cell(IReadOnlyList<EventAction> actions, params MapEventDefinition[] others) => new()
    {
        Events = [new MapEventDefinition("cell", MapEventKind.Hero, actions), .. others],
        Cells = [new EventCell(2, 1, 0, "cell")],
    };

    /// <summary>Hero A at (0, 1), Blue starting with no gold; stepping east once runs the cell.</summary>
    private static GameSession World(MapEventSet events)
    {
        var state = Create("......", "A.....", "......", ".....B");
        state.SetMapEvents(events);
        var session = Start(state);
        session.State.GetPlayer(Blue).Resources = ResourceSet.Empty;
        return session;
    }

    private static int GoldOf(GameSession session) => session.State.GetPlayer(Blue).Resources.Gold;

    private static void Answer(GameSession session, int option = 0) =>
        session.Execute(new AnswerMapEventCommand(session.State.CurrentPlayer.Id, option)).ThrowIfRejected();

    private static GameSession RunCell(MapEventSet events)
    {
        var session = World(events);
        session.Step(A, Direction.East);
        session.Step(A, Direction.East);
        return session;
    }

    // Loops ------------------------------------------------------------------------------------------------------------

    [Fact]
    public void ARepeat_RunsItsActionsThatManyTimes_NoneForZero_AndAtMostTheCap()
    {
        Assert.Equal(300, GoldOf(RunCell(Cell([new RepeatAction(N(3), [Gold(100)])]))));
        Assert.Equal(0, GoldOf(RunCell(Cell([new RepeatAction(N(0), [Gold(100)]), new RepeatAction(N(-2), [Gold(100)])]))));
        Assert.Equal(MapEventService.MaxLoopRounds, GoldOf(RunCell(Cell([new RepeatAction(N(1_000_000), [Gold(1)])]))));

        // A random count is drawn once, when the loop starts: every round adds the same.
        var drawn = RunCell(Cell([new RepeatAction(new RandomExpr(2, 5), [Gold(10)])]));
        Assert.Contains(GoldOf(drawn), new[] { 20, 30, 40, 50 });
    }

    [Fact]
    public void AWhile_TestsItsConditionBeforeEveryRound_AndStopsAtTheCap()
    {
        var count = new CompareCondition(new VariableExpr("n"), CompareOp.Less, N(5));
        var events = Cell([new WhileAction(count, [new SetVariableAction("n", VariableOp.Add, N(1)), Gold(100)])]) with
        {
            Variables = new Dictionary<string, int> { ["n"] = 0 },
        };
        var session = RunCell(events);
        Assert.Equal((5, 500), (session.State.MapEvents.Variable("n"), GoldOf(session)));

        var never = new CompareCondition(N(1), CompareOp.Equal, N(1));
        Assert.Equal(MapEventService.MaxLoopRounds, GoldOf(RunCell(Cell([new WhileAction(never, [Gold(1)])]))));
    }

    [Fact]
    public void AMessageInALoop_PausesIt_AndTheLoopGoesOnOnceRead()
    {
        var session = RunCell(Cell([new RepeatAction(N(2), [new MessageAction("Again.") { Rewards = [Gold(100)] }]), Gold(1)]));

        Assert.Equal("Again.", Assert.IsType<MapEventDecision>(session.State.PendingDecision).Text);
        Answer(session);
        Assert.Equal(100, GoldOf(session));
        Assert.IsType<MapEventDecision>(session.State.PendingDecision);
        Answer(session);
        Assert.Equal(201, GoldOf(session));
        Assert.Null(session.State.PendingDecision);
    }

    // Running another event --------------------------------------------------------------------------------------------

    [Fact]
    public void RunningAnEvent_TakesItsActionsForTheSameHero_AndAStopInItEndsOnlyIt()
    {
        var shared = new MapEventDefinition("shared", MapEventKind.Player, [Gold(100), new ExperienceAction(N(50)), new StopAction(), Gold(1000)]);
        var ran = new List<MapEventRan>();
        var session = World(Cell([new RunEventAction("shared"), Gold(10), new RunEventAction("shared")], shared));
        session.Events.Subscribe<MapEventRan>(ran.Add);
        var experience = session.State.GetHero(A).Experience;

        session.Step(A, Direction.East);
        session.Step(A, Direction.East);

        Assert.Equal(210, GoldOf(session));
        Assert.Equal(experience + 100, session.State.GetHero(A).Experience);
        Assert.Equal(["cell", "shared", "shared"], ran.Select(r => r.Event));
        Assert.All(ran, r => Assert.Equal(A, r.Hero));
    }

    [Fact]
    public void RunningADisabledEvent_SkipsIt_AndAnEventsOwnDisableMeansTheRunOne()
    {
        // "once" inside the shared event disables the shared event, not the cell.
        var shared = new MapEventDefinition("shared", MapEventKind.Hero, [Gold(100), new EnableEventAction(null, false)]);
        var session = World(Cell([new RunEventAction("shared"), Gold(1)], shared));

        session.Step(A, Direction.East);
        session.Step(A, Direction.East);
        session.Step(A, Direction.East);
        session.Step(A, Direction.West);

        Assert.Equal(102, GoldOf(session));
        Assert.True(session.State.MapEvents.IsEnabled("cell"));
        Assert.False(session.State.MapEvents.IsEnabled("shared"));
    }

    [Fact]
    public void AnEventRunningItself_StopsAtTheDepthCap()
    {
        var session = RunCell(Cell([Gold(1), new RunEventAction("cell")]));
        Assert.Equal(MapEventService.MaxRunDepth, GoldOf(session));
    }

    [Fact]
    public void RunningAnUnknownEventOrAQuest_IsRefused()
    {
        var state = Create("......", "A.....");
        Assert.Throws<ArgumentException>(() => state.SetMapEvents(Cell([new RunEventAction("missing")])));
        var quest = new MapEventDefinition("quest", MapEventKind.Quest, []) { Stages = [QuestForms.Level(5, "Grow.")] };
        Assert.Throws<ArgumentException>(() => state.SetMapEvents(Cell([new RunEventAction("quest")], quest)));
    }

    [Fact]
    public void LoopsAndRuns_RoundTripThroughTheMapFormat_AndReplayExactly()
    {
        var state = Create("......", "A.....", "......", ".....B");
        var shared = new MapEventDefinition("shared", MapEventKind.Player, [new ResourceAction(ResourceType.Gold, new RandomExpr(1, 100))]);
        state.SetMapEvents(Cell(
            [
                new RepeatAction(new RandomExpr(1, 3), [new RunEventAction("shared")]),
                new WhileAction(new CompareCondition(new VariableExpr("n"), CompareOp.Less, N(2)), [new SetVariableAction("n", VariableOp.Add, N(1))]),
            ],
            shared) with { Variables = new Dictionary<string, int> { ["n"] = 0 } });

        var map = MapConverter.ToDocument(state);
        var json = MapSerializer.Serialize(map);
        Assert.True(MapDocument.CurrentFormatVersion >= 33);
        Assert.Contains("\"type\": \"repeat\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"while\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\": \"runEvent\"", json, StringComparison.Ordinal);
        Assert.Equal(json, MapSerializer.Serialize(MapConverter.ToDocument(MapConverter.ToGameState(MapSerializer.Deserialize(json)))));

        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Deserialize(json), options: new GameOptions(Seed: 9)));
        session.Start();
        session.Step(A, Direction.East);
        session.Step(A, Direction.East);
        Assert.Equal(2, session.State.MapEvents.Variable("n"));

        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(map, session))));
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    // Pandora's Box ----------------------------------------------------------------------------------------------------

    /// <summary>A box at (3, 1) above hero A at (3, 2), running the hero event "box".</summary>
    private static (GameSession Session, PandorasBox Box) BoxWorld(BoxContents contents, params EventAction[] actions)
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        var box = Add<PandorasBox>(state, BuiltInMapObjects.PandorasBox, 3, 1);
        (box.Contents, box.Event) = (contents, "box");
        state.SetMapEvents(new MapEventSet { Events = [new MapEventDefinition("box", MapEventKind.Hero, actions)] });
        var session = Start(state);
        session.State.GetPlayer(Blue).Resources = ResourceSet.Empty;
        return (session, box);
    }

    [Fact]
    public void ABoxWithAnEvent_GivesItsContents_ThenRunsTheEventForTheOpener()
    {
        var (session, box) = BoxWorld(new BoxContents { Resources = new ResourceSet(Gold: 500) }, new MessageAction("A voice laughs.") { Rewards = [Gold(1000)] });
        var ran = new List<MapEventRan>();
        session.Events.Subscribe<MapEventRan>(ran.Add);

        session.Step(A, Direction.North);
        Assert.IsType<PandorasBoxDecision>(session.State.PendingDecision);
        session.Execute(new AnswerPandorasBoxCommand(Blue, Open: true)).ThrowIfRejected();

        Assert.Null(session.State.FindObject(box.Id));
        Assert.Equal(500, GoldOf(session));
        var message = Assert.IsType<MapEventDecision>(session.State.PendingDecision);
        Assert.Equal((A, "box", "A voice laughs."), (message.Hero, message.Event, message.Text));
        Answer(session);
        Assert.Equal(1500, GoldOf(session));
        Assert.Equal(A, Assert.Single(ran).Hero);
    }

    [Fact]
    public void ABoxWithOnlyAnEvent_IsValid_ItsEventMustBeAHeroEvent_AndItIsInTheMapFormat()
    {
        var editor = new MapEditorSession(Create(".........", ".........", "...A.....", ".........", "C.......B"));
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.PandorasBox, new(3, 1), out var placed));
        var box = (PandorasBox)editor.Scenario.GetObject(placed);
        Assert.Equal(EditResult.Ok, editor.SetPandorasBoxEvent(box.Id, " box "));
        Assert.Equal("box", box.Event);
        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error && i.Message.Contains("not a hero event", StringComparison.Ordinal));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("is empty", StringComparison.Ordinal));
        editor.Undo();
        Assert.Null(box.Event);
        editor.Redo();

        var map = MapConverter.ToDocument(editor.Scenario);
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(map)); // no event "box"
        map = map with { Events = new MapEventSet { Events = [new MapEventDefinition("box", MapEventKind.Hero, [Gold(7)])] } };
        var json = MapSerializer.Serialize(map);
        Assert.Contains("\"event\": \"box\"", json, StringComparison.Ordinal);
        Assert.Equal("box", MapConverter.ToGameState(MapSerializer.Deserialize(json)).ObjectsOf<PandorasBox>().Single().Event);

        var wrong = map with { Events = new MapEventSet { Events = [new MapEventDefinition("box", MapEventKind.Player, [])] } };
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(wrong));
    }
}
