using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The editor's side of the map events (EVT-6): undoable edits, tree paths, the simple event forms, validator checks, node text.</summary>
public class MapEventEditorTests
{
    private static MapEditorSession Editor() => new(Create(".........", ".........", "...A.....", ".........", "C.......B"));

    private static ResourceAction Gold(int amount) => new(ResourceType.Gold, new NumberExpr(amount));

    private static MapEventDefinition Hero(string name, params EventAction[] actions) => new(name, MapEventKind.Hero, actions);

    // Edits -----------------------------------------------------------------------------------------------------------

    [Fact]
    public void Events_AreAdded_Moved_AndRemoved_Undoably_AndPublishTheirChange()
    {
        var editor = Editor();
        var changes = 0;
        editor.Events.Subscribe<MapEventsChanged>(_ => changes++);

        Assert.Equal(EditResult.Ok, editor.AddMapEvent(Hero("a")));
        Assert.Equal(EditResult.Ok, editor.AddMapEvent(Hero("b")));
        Assert.Equal(EditResult.InvalidValue, editor.AddMapEvent(Hero("a")));
        Assert.Equal(EditResult.InvalidValue, editor.AddMapEvent(Hero(" ")));
        Assert.Equal("a 2", editor.UnusedEventName("a"));
        Assert.Equal(EditResult.Ok, editor.MoveMapEvent("b", -5));
        Assert.Equal(["b", "a"], editor.Scenario.MapEvents.Set.Events.Select(e => e.Name));
        Assert.Equal(EditResult.Ok, editor.RemoveMapEvent("a"));
        Assert.Equal(["b"], editor.Scenario.MapEvents.Set.Events.Select(e => e.Name));
        Assert.Equal(4, changes);

        editor.Undo();
        editor.Undo();
        Assert.Equal(["a", "b"], editor.Scenario.MapEvents.Set.Events.Select(e => e.Name));
        Assert.Equal(EditResult.InvalidTarget, editor.RemoveMapEvent("nope"));
    }

    [Fact]
    public void Renaming_AnEvent_RenamesWhatNamesIt_AndUndoRestoresTheOldNames()
    {
        var editor = Editor();
        editor.PlaceObject(BuiltInMapObjects.PandorasBox, new(5, 0), out var boxId);
        var box = (PandorasBox)editor.Scenario.GetObject(boxId);
        editor.AddMapEvent(Hero("old", Gold(1)));
        editor.AddMapEvent(new MapEventDefinition("caller", MapEventKind.Player,
            [new IfAction(new PlayerIsHumanCondition(), [new RunEventAction("old")]) { Else = [new EnableEventAction("old", false)] }]));
        editor.SetPandorasBoxEvent(box.Id, "old");
        Assert.Equal(EditResult.Ok, editor.SetEventCell(new(1, 1), "old"));

        Assert.Equal(EditResult.InvalidValue, editor.ReplaceMapEvent("old", Hero("caller")));
        Assert.Equal(EditResult.Ok, editor.ReplaceMapEvent("old", Hero("new", Gold(2))));

        var set = editor.Scenario.MapEvents.Set;
        Assert.Equal("new", box.Event);
        Assert.Equal("new", Assert.Single(set.Cells).Event);
        var caller = set.Find("caller")!;
        Assert.Contains(MapEventTree.Actions(caller), a => a is RunEventAction { Event: "new" });
        Assert.Contains(MapEventTree.Actions(caller), a => a is EnableEventAction { Event: "new" });
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error);

        editor.Undo();
        Assert.Equal("old", box.Event);
        Assert.Equal("old", editor.Scenario.MapEvents.Set.Cells.Single().Event);
        Assert.Contains(MapEventTree.Actions(editor.Scenario.MapEvents.Set.Find("caller")!), a => a is RunEventAction { Event: "old" });
    }

    [Fact]
    public void Removing_AnEvent_TakesItsCells_AndClearsTheObjectsThatNamedIt()
    {
        var editor = Editor();
        editor.PlaceObject(BuiltInMapObjects.SeerHut, new(5, 0), out var hutId);
        var hut = (QuestSite)editor.Scenario.GetObject(hutId);
        editor.AddMapEvent(new MapEventDefinition("quest", MapEventKind.Quest, []) { Stages = [QuestForms.Level(2, "Grow up.")] });
        editor.SetQuestSiteQuest(hut.Id, "quest");

        editor.RemoveMapEvent("quest");

        Assert.Null(hut.Quest);
        editor.Undo();
        Assert.Equal("quest", hut.Quest);
    }

    [Fact]
    public void Variables_AreDeclaredChangedAndRemoved()
    {
        var editor = Editor();

        Assert.Equal(EditResult.Ok, editor.SetMapVariable(" gold ", 5));
        Assert.Equal(EditResult.Ok, editor.SetMapVariable("gold", 7));
        Assert.Equal(EditResult.InvalidValue, editor.SetMapVariable("", 1));
        Assert.Equal(7, editor.Scenario.MapEvents.Set.Variables["gold"]);
        Assert.Equal(EditResult.Ok, editor.RemoveMapVariable("gold"));
        Assert.Empty(editor.Scenario.MapEvents.Set.Variables);
        editor.Undo();
        Assert.Equal(7, editor.Scenario.MapEvents.Set.Variables["gold"]);
    }

    [Fact]
    public void EventCells_GoOnFreePassableGround_OfAHeroEvent()
    {
        var editor = new MapEditorSession(Create("A.~......", ".........", "C.......B"));
        editor.PlaceObject(BuiltInMapObjects.PandorasBox, new(5, 0), out _);
        editor.AddMapEvent(Hero("cell"));
        editor.AddMapEvent(new MapEventDefinition("turn", MapEventKind.Player, []));

        Assert.Equal(EditResult.ImpassableTerrain, editor.SetEventCell(new(2, 0), "cell"));
        Assert.Equal(EditResult.Overlaps, editor.SetEventCell(new(5, 0), "cell"));
        Assert.Equal(EditResult.OutOfBounds, editor.SetEventCell(new(20, 0), "cell"));
        Assert.Equal(EditResult.InvalidTarget, editor.SetEventCell(new(3, 1), "turn"));
        Assert.Equal(EditResult.Ok, editor.SetEventCell(new(3, 1), "cell"));
        Assert.Equal(EditResult.Ok, editor.SetEventCell(new(3, 1), null));
        Assert.Equal(EditResult.InvalidTarget, editor.SetEventCell(new(3, 1), null));
        Assert.Empty(editor.Scenario.MapEvents.Set.Cells);
    }

    // Tree paths ------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTree_IsEditedByPath_AtAnyDepth()
    {
        var branch = new IfAction(new PlayerIsHumanCondition(), [Gold(1)]) { Else = [Gold(2)] };
        var question = new QuestionAction("Which?", [new EventAnswer("One", []), new EventAnswer("Two", [branch])]);
        var definition = Hero("e", question);
        EventStep[] inBranch = [new(0, 0), new(1, 0)]; // the question, its second answer's first action (the if)

        Assert.Same(branch, MapEventTreeEdit.Get(definition, inBranch));
        Assert.Null(MapEventTreeEdit.Get(definition, [new(0, 3)]));

        var edited = MapEventTreeEdit.Insert(definition, inBranch, 1, 1, Gold(3)); // the else list, after Gold(2)
        edited = MapEventTreeEdit.Replace(edited, [.. inBranch, new(0, 0)], Gold(10));
        edited = MapEventTreeEdit.Move(edited, [.. inBranch, new(1, 1)], -1);
        var changed = (IfAction)MapEventTreeEdit.Get(edited, inBranch)!;
        Assert.Equal([Gold(10)], changed.Then);
        Assert.Equal([Gold(3), Gold(2)], changed.Else);
        Assert.Equal([new EventStep(0, 0), new EventStep(1, 0), new EventStep(1, 0)], MapEventTreeEdit.Moved(edited, [.. inBranch, new(1, 1)], -1));

        edited = MapEventTreeEdit.Remove(edited, inBranch);
        Assert.Empty(((QuestionAction)edited.Actions[0]).Answers[1].Actions);
        Assert.Equal("Which?", ((QuestionAction)edited.Actions[0]).Text);
        Assert.Throws<ArgumentOutOfRangeException>(() => MapEventTreeEdit.Remove(definition, [new(0, 4)]));
    }

    [Fact]
    public void AQuestsLists_AreItsStages()
    {
        var quest = new MapEventDefinition("q", MapEventKind.Quest, []) { Stages = [QuestForms.Level(2, "a"), QuestForms.Level(3, "b", Gold(5))] };

        Assert.Equal(["Stage 1", "Stage 2"], MapEventTreeEdit.ListNames(quest));
        var edited = MapEventTreeEdit.Insert(quest, [], 0, 0, Gold(1));
        Assert.Equal([Gold(1)], edited.Stages[0].Actions);
        Assert.Equal([Gold(5)], edited.Stages[1].Actions);

        edited = MapEventTreeEdit.SetStage(edited, 0, null);
        Assert.Equal("b", Assert.Single(edited.Stages).Proposal);
    }

    // Simple event forms ----------------------------------------------------------------------------------------------

    [Fact]
    public void ATimedEvent_RunsOnItsDays_ForThePlayersItIsFor()
    {
        var state = Create("A.......B");
        state.SetMapEvents(new MapEventSet
        {
            Events = [SimpleEventForms.Timed("tax", "", 2, 3, new EventTriggers([Blue.Value]), new SimpleRewards { Resources = new Dictionary<ResourceType, int> { [ResourceType.Gold] = 100 } })],
        });
        var session = Start(state);
        var gold = (Blue: session.State.GetPlayer(Blue).Resources.Gold, Red: session.State.GetPlayer(Red).Resources.Gold);

        for (var i = 0; i < 2 * 8; i++) // to the start of Blue's turn on day 9
        {
            session.Execute(new EndTurnCommand(session.State.CurrentPlayer.Id)).ThrowIfRejected();
        }

        // Days 2, 5 and 8 (players without towns or mines have no income).
        Assert.Equal(9, session.State.Date.AbsoluteDay);
        Assert.Equal(gold.Blue + 300, session.State.GetPlayer(Blue).Resources.Gold);
        Assert.Equal(gold.Red, session.State.GetPlayer(Red).Resources.Gold);
    }

    [Fact]
    public void AnEventCellForm_GivesItsRewardsOnce_ToThePlayersItIsFor()
    {
        var state = Create("A.......B");
        state.SetMapEvents(new MapEventSet
        {
            Cells = [new EventCell(1, 0, 0, "find")],
            Events = [SimpleEventForms.Cell("find", "You find a purse.", EventTriggers.Anyone, once: true, new SimpleRewards { Experience = 100 })],
        });
        var session = Start(state);
        var hero = session.State.GetHero(A);

        session.Step(A, Direction.East);
        Assert.Equal("You find a purse.", Assert.IsType<MapEventDecision>(session.State.PendingDecision).Text);
        session.Execute(new AnswerMapEventCommand(Blue, 0)).ThrowIfRejected();
        session.Step(A, Direction.West);
        session.Step(A, Direction.East);

        Assert.Null(session.State.PendingDecision);
        Assert.Equal(100, hero.Experience);
    }

    [Fact]
    public void ATownEventForm_BuildsAndAddsCreaturesToHire()
    {
        var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "..........", "...A.....B");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var line = TownRules.Lines(state, town).First(l => l.Tier == 1);
        state.SetMapEvents(new MapEventSet
        {
            Events =
            [
                SimpleEventForms.Town("fair", [town.Id.Value], "", 1, 0, EventTriggers.Anyone, SimpleRewards.None, [BuiltInBuildings.Tavern],
                    new Dictionary<string, int> { [line.Id] = 7 }),
            ],
        });
        var before = town.AvailableOf(line.Id);

        Start(state);

        Assert.Equal(1, town.Level(BuiltInBuildings.Tavern));
        Assert.Equal(before + 7, town.AvailableOf(line.Id));
    }

    [Theory]
    [InlineData(1, 0, new[] { 1 })]
    [InlineData(2, 1, new[] { 2, 3, 4, 5 })]
    [InlineData(3, 4, new[] { 3, 7, 11 })]
    public void OnDays_HoldsOnTheFirstDayAndEveryPeriodAfter(int first, int every, int[] days)
    {
        var condition = SimpleEventForms.OnDays(first, every);
        var held = Enumerable.Range(1, 12).Where(day => Holds(Substitute(condition, day))).ToList();

        Assert.Equal(days, held.Where(d => d <= days.Max()).ToArray());
    }

    /// <summary>The condition with the day replaced by a number, tested by the game's own evaluator through a one-event game.</summary>
    private static EventCondition Substitute(EventCondition condition, int day)
    {
        EventExpr Expr(EventExpr e) => e switch
        {
            QuantityExpr { Quantity: Quantity.Day } => new NumberExpr(day),
            ArithmeticExpr a => a with { Left = Expr(a.Left), Right = Expr(a.Right) },
            _ => e,
        };
        return condition switch
        {
            AllCondition all => new AllCondition([.. all.Conditions.Select(c => Substitute(c, day))]),
            CompareCondition compare => compare with { Left = Expr(compare.Left), Right = Expr(compare.Right) },
            _ => condition,
        };
    }

    private static bool Holds(EventCondition condition)
    {
        var state = Create("A.B");
        state.SetMapEvents(new MapEventSet
        {
            Variables = new Dictionary<string, int> { ["hit"] = 0 },
            Events = [new MapEventDefinition("test", MapEventKind.Player, [new IfAction(condition, [new SetVariableAction("hit", VariableOp.Set, new NumberExpr(1))])])],
        });
        return Start(state).State.MapEvents.Variable("hit") == 1;
    }

    // Validator -------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheValidator_ShowsTheLoadErrors_WithTheirEvent()
    {
        var editor = Editor();
        editor.AddMapEvent(Hero("broken", new RunEventAction("missing")));
        editor.SetEventCell(new(1, 1), "broken");

        var error = Assert.Single(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error);
        Assert.Equal("broken", error.Event);
        Assert.Equal(new MapPosition(1, 1), error.Position);
        Assert.Contains("missing", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheValidator_Warns_AboutSelfRunningUnusedAndUnfinishableEvents()
    {
        var editor = Editor();
        editor.PlaceObject(BuiltInMapObjects.SeerHut, new(5, 0), out var hutId);
        editor.SetMapVariable("unread", 1);
        editor.SetMapVariable("read", 1);
        editor.AddMapEvent(new MapEventDefinition("ping", MapEventKind.Player, [new RunEventAction("pong")]));
        editor.AddMapEvent(new MapEventDefinition("pong", MapEventKind.Player,
            [new IfAction(new CompareCondition(new VariableExpr("read"), CompareOp.Equal, new NumberExpr(1)), [new RunEventAction("ping")])]));
        editor.AddMapEvent(Hero("lonely"));
        editor.AddMapEvent(new MapEventDefinition("sleeper", MapEventKind.Player, []) { Enabled = false });
        editor.AddMapEvent(new MapEventDefinition("unused quest", MapEventKind.Quest, []) { Stages = [QuestForms.Level(2, "a")] });
        editor.AddMapEvent(new MapEventDefinition("hard quest", MapEventKind.Quest, [])
        {
            Stages = [QuestForms.Level(99, "Reach level 99."), QuestForms.Artifacts(["no-such-artifact"], "Bring it.")],
        });
        editor.SetQuestSiteQuest(hutId, "hard quest");

        var warnings = MapValidator.Validate(editor.Scenario).Where(i => i.Severity == IssueSeverity.Warning).Select(i => i.Message).ToList();

        Assert.Contains("Event 'ping' runs itself (ping → pong → ping); runs deeper than 16 are skipped.", warnings);
        Assert.Contains(warnings, w => w.StartsWith("Event 'pong' runs itself", StringComparison.Ordinal));
        Assert.Contains("Event 'lonely' is never run: no event cell, Pandora's box or other event runs it.", warnings);
        Assert.Contains("Event 'sleeper' starts disabled and no event enables it.", warnings);
        Assert.Contains(warnings, w => w.StartsWith("Event 'unused quest' is not the quest", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.StartsWith("Quest 'hard quest', stage 1 cannot be met: heroes stop at level", StringComparison.Ordinal));
        Assert.Contains("Quest 'hard quest', stage 2 cannot be met: there is no artifact 'no-such-artifact'.", warnings);
        Assert.Contains("The variable 'unread' is never read.", warnings);
        Assert.DoesNotContain(warnings, w => w.Contains("'read'", StringComparison.Ordinal));
    }

    // Text and JSON ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Nodes_ReadAsOneLine()
    {
        Assert.Equal("If hero level ≥ 5 and not the player is a person",
            MapEventText.Describe(new IfAction(new AllCondition([
                new CompareCondition(new QuantityExpr(Quantity.HeroLevel), CompareOp.GreaterOrEqual, new NumberExpr(5)),
                new NotCondition(new PlayerIsHumanCondition())]), [])));
        Assert.Equal("Give random 1…6 Gold", MapEventText.Describe(new ResourceAction(ResourceType.Gold, new RandomExpr(1, 6))));
        Assert.Equal("Take 5 Lumber", MapEventText.Describe(new ResourceAction(ResourceType.Lumber, new NumberExpr(-5))));
        Assert.Equal("x += (y × 2)", MapEventText.Describe(new SetVariableAction("x", VariableOp.Add, new ArithmeticExpr(ArithmeticOp.Multiply, new VariableExpr("y"), new NumberExpr(2)))));
        Assert.Equal("Disable this event", MapEventText.Describe(new EnableEventAction(null, false)));
    }

    [Fact]
    public void ANode_IsWrittenAndReadAsTheMapFileWritesIt_AndAnIncompleteOneIsRefused()
    {
        var message = new MessageAction("Hello") { Rewards = [Gold(5)] };

        var text = MapEventNodeJson.Write(message);
        Assert.Contains("\"type\": \"message\"", text, StringComparison.Ordinal);
        Assert.Equal(MapEventNodeJson.Write(message), MapEventNodeJson.Write(MapEventNodeJson.ReadAction(text)));
        Assert.Throws<FormatException>(() => MapEventNodeJson.ReadAction("{ \"type\": \"message\" }"));
        Assert.Throws<FormatException>(() => MapEventNodeJson.ReadAction("{ \"type\": \"if\", \"then\": [] }"));
        Assert.Throws<FormatException>(() => MapEventNodeJson.ReadAction("{ \"type\": \"nonsense\" }"));
        Assert.Throws<FormatException>(() => MapEventNodeJson.ReadAction("not json"));

        var stage = QuestForms.Level(3, "Grow.", Gold(1));
        var read = MapEventNodeJson.ReadStage(MapEventNodeJson.Write(stage), stage.Actions);
        Assert.Equal(stage.Proposal, read.Proposal);
        Assert.Equal(stage.Actions, read.Actions);
        Assert.DoesNotContain("resource", MapEventNodeJson.Write(stage), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryTemplate_FitsTheMap_AndReadsBackFromItsText()
    {
        var state = Create("A.......B");
        var actions = MapEventTemplates.Actions(state, otherEvent: "other", variable: "x");
        var conditions = MapEventTemplates.Conditions("x");
        var set = new MapEventSet
        {
            Variables = new Dictionary<string, int> { ["x"] = 0 },
            Cells = [new EventCell(3, 0, 0, "all")],
            Events =
            [
                new MapEventDefinition("all", MapEventKind.Hero, [.. actions.Select(a => a.Action), .. conditions.Select(c => new IfAction(c.Condition, []))]),
                new MapEventDefinition("other", MapEventKind.Player, []),
            ],
        };

        Assert.Empty(MapEventRules.Problems(state, set));
        foreach (var (_, action) in actions)
        {
            Assert.Equal(MapEventNodeJson.Write(action), MapEventNodeJson.Write(MapEventNodeJson.ReadAction(MapEventNodeJson.Write(action))));
        }

        foreach (var kind in Enum.GetValues<MapEventKind>())
        {
            Assert.Empty(MapEventRules.Problems(state, new MapEventSet { Events = [MapEventTemplates.Blank("new", kind)] }));
        }
    }

    // Played --------------------------------------------------------------------------------------------------------

    [Fact]
    public void EventsWrittenInTheEditor_PlayInTheGame()
    {
        var editor = Editor();
        editor.AddMapEvent(SimpleEventForms.Cell("purse", "You find a purse.", EventTriggers.Anyone, once: true,
            new SimpleRewards { Resources = new Dictionary<ResourceType, int> { [ResourceType.Gold] = 250 } }));
        editor.SetEventCell(new(4, 2), "purse");
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Event is not null);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var session = new GameSession(MapConverter.ToGameState(MapSerializer.Deserialize(json)));
        session.Start();
        var gold = session.State.GetPlayer(Blue).Resources.Gold;
        session.Step(A, Direction.East);
        Assert.Equal("You find a purse.", Assert.IsType<MapEventDecision>(session.State.PendingDecision).Text);
        session.Execute(new AnswerMapEventCommand(Blue, 0)).ThrowIfRejected();

        Assert.Equal(gold + 250, session.State.GetPlayer(Blue).Resources.Gold);
    }

    [Fact]
    public void AMapWhoseEventsDoNotFit_OpensInTheEditor_ButNotInTheGame()
    {
        var editor = Editor();
        editor.AddMapEvent(Hero("broken", new RunEventAction("missing")));
        var document = MapConverter.ToDocument(editor.Scenario);

        Assert.Throws<FormatException>(() => MapConverter.ToGameState(document));
        var opened = MapConverter.ToGameState(document, checkEvents: false);
        Assert.Equal("broken", Assert.Single(opened.MapEvents.Set.Events).Name);
    }
}
