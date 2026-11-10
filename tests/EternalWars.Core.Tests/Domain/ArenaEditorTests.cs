using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The arena in the map editor (ARN-2): the switch, the validator's checks, and what objects show on the day of preparation.</summary>
public class ArenaEditorTests
{
    private static GameState Arena(params string[] rows)
    {
        var state = Create(rows);
        state.SetRules(ScenarioRules.Default with { Arena = true });
        return state;
    }

    private static List<MapIssue> Issues(GameState state, string text) =>
        MapValidator.Validate(state).Where(i => i.Message.Contains(text, StringComparison.Ordinal)).ToList();

    [Fact]
    public void AnArenaMap_WithoutACombatPlace_IsAnError_AndACombatPlaceOnAnOrdinaryMapDoesNothing()
    {
        var arena = Arena("A.#..", "..#.B");
        Assert.Equal(IssueSeverity.Error, Assert.Single(Issues(arena, "needs a combat place")).Severity);

        Add<CombatPlace>(arena, BuiltInMapObjects.CombatPlace, 3, 0);
        Assert.Empty(Issues(arena, "needs a combat place"));
        Assert.DoesNotContain(MapValidator.Validate(arena), i => i.Severity == IssueSeverity.Error);

        var ordinary = Create("A.#..", "..#.B");
        Add<CombatPlace>(ordinary, BuiltInMapObjects.CombatPlace, 3, 0);
        Assert.Equal(IssueSeverity.Warning, Assert.Single(Issues(ordinary, "not an arena")).Severity);
        Assert.Empty(Issues(ordinary, "needs a combat place"));
    }

    [Fact]
    public void EnemiesWhoseHeroesReachTheSameGround_CanMeetOnDayOne_AWallKeepsThemApart()
    {
        var open = Arena("A....", "....B");
        Add<CombatPlace>(open, BuiltInMapObjects.CombatPlace, 3, 0);
        var meeting = Assert.Single(Issues(open, "can meet on day 1"));
        Assert.Equal(IssueSeverity.Warning, meeting.Severity);
        Assert.NotNull(meeting.Position);

        var walled = Arena("A.#..", "..#.B");
        Add<CombatPlace>(walled, BuiltInMapObjects.CombatPlace, 3, 0);
        Assert.Empty(Issues(walled, "can meet on day 1"));

        // Allies may meet.
        var allies = Arena("A....", "....B");
        allies.AssignTeams(new Dictionary<PlayerId, TeamId> { [Blue] = new(0), [Red] = new(0) });
        Assert.Empty(Issues(allies, "can meet on day 1"));
    }

    [Fact]
    public void AGateCountsAsAWay_ThroughTheWall()
    {
        var state = Arena("A.#..", "..#.B");
        var west = Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 1, 0);
        var east = Add<TwoWayGate>(state, BuiltInMapObjects.TwoWayMonolith, 3, 0);
        (west.LinkedGate, east.LinkedGate) = (east.Id, west.Id);

        Assert.Single(Issues(state, "can meet on day 1"));
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(1, false)]
    public void AnEventThatDoesSomethingOnlyAfterDayOne_IsPointedOut(int day, bool warned)
    {
        var state = Arena("A.#..", "..#.B");
        state.SetMapEvents(new MapEventSet
        {
            Events =
            [
                SimpleEventForms.Timed("supplies", "Supplies arrive.", day, 0, EventTriggers.Anyone,
                    new SimpleRewards { Resources = new Dictionary<ResourceType, int> { [ResourceType.Gold] = 500 } }),
            ],
        });

        var issues = Issues(state, "only after day 1");
        Assert.Equal(warned, issues.Count == 1);
        Assert.All(issues, i => Assert.Equal("supplies", i.Event));
    }

    [Fact]
    public void OnlyTheDateDecidesALaterDay_OtherConditionsAreNotGuessed()
    {
        var day = new QuantityExpr(Quantity.Day);
        EventAction Gold() => new ResourceAction(ResourceType.Gold, new NumberExpr(100));
        MapEventDefinition Player(EventCondition condition) => new("e", MapEventKind.Player, [new IfAction(condition, [Gold()])]);

        Assert.True(MapEventChecks.RunsOnlyAfter(Player(new CompareCondition(new QuantityExpr(Quantity.Week), CompareOp.Equal, new NumberExpr(2))), 1));
        Assert.True(MapEventChecks.RunsOnlyAfter(Player(new AllCondition([new PlayerIsHumanCondition(), new CompareCondition(day, CompareOp.Greater, new NumberExpr(1))])), 1));
        Assert.False(MapEventChecks.RunsOnlyAfter(Player(new CompareCondition(day, CompareOp.GreaterOrEqual, new NumberExpr(1))), 1));
        Assert.False(MapEventChecks.RunsOnlyAfter(Player(new PlayerIsHumanCondition()), 1));
        Assert.False(MapEventChecks.RunsOnlyAfter(Player(new AnyCondition([])), 1)); // "never" is not a later day
        Assert.False(MapEventChecks.RunsOnlyAfter(new MapEventDefinition("e", MapEventKind.Player, [new IfAction(new CompareCondition(day, CompareOp.Equal, new NumberExpr(5)), [Gold()]), Gold()]), 1));
        Assert.False(MapEventChecks.RunsOnlyAfter(Player(new CompareCondition(day, CompareOp.Equal, new NumberExpr(5))), 5));
    }

    [Fact]
    public void TheConditionsOfAnArenaMap_AreSaidToBeIgnored()
    {
        var state = Arena("A.#..", "..#.B");
        Add<CombatPlace>(state, BuiltInMapObjects.CombatPlace, 3, 0);
        Assert.Empty(Issues(state, "ignores the victory and defeat conditions"));

        state.SetRules(state.Rules with { Victory = [ScenarioCondition.SurviveUntilDay(10, null)] });
        Assert.Single(Issues(state, "ignores the victory and defeat conditions"));
        Assert.Empty(Issues(state, "Nobody can win"));
    }

    [Fact]
    public void TheSwitch_IsAnUndoableEditorChange()
    {
        var editor = new MapEditorSession(EternalWars.Core.Domain.Scenarios.BlankScenario.Create(12, 8));

        Assert.Equal(EditResult.Ok, editor.Map.SetRules(editor.Scenario.Rules with { Arena = true }));
        Assert.True(editor.Scenario.Rules.Arena);
        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("needs a combat place", StringComparison.Ordinal));

        Assert.True(editor.Undo());
        Assert.False(editor.Scenario.Rules.Arena);
    }

    /// <summary>A Pandora's Box, a Witch Hut, a Shrine and a Seer's Hut, on an arena map or an ordinary one.</summary>
    private static (GameState State, PandorasBox Box, WitchHut Hut, Shrine Shrine, QuestSite Seer) Objects(bool arena)
    {
        var state = arena ? Arena("A..........", "...........", "...........", "..........B") : Create("A..........", "...........", "...........", "..........B");
        var box = Add<PandorasBox>(state, BuiltInMapObjects.PandorasBox, 2, 0);
        box.Contents = new BoxContents { Resources = new ResourceSet(Gold: 2500), Attack = 2 };
        var hut = Add<WitchHut>(state, BuiltInMapObjects.WitchHut, 4, 0);
        hut.Skills = [EternalWars.Core.Domain.Heroes.BuiltInSkills.Logistics, EternalWars.Core.Domain.Heroes.BuiltInSkills.Archery];
        var shrine = Add<Shrine>(state, BuiltInMapObjects.ShrineId(1), 6, 0);
        var seer = Add<QuestSite>(state, BuiltInMapObjects.SeerHut, 8, 0);
        seer.Quest = "seer";
        state.SetMapEvents(new MapEventSet
        {
            Events =
            [
                new MapEventDefinition("seer", MapEventKind.Quest, [])
                {
                    Stages = [new QuestStage(new CompareCondition(new QuantityExpr(Quantity.HeroLevel), CompareOp.GreaterOrEqual, new NumberExpr(3)), "Grow a little.",
                        [new MessageAction("Well done."), new ResourceAction(ResourceType.Gold, new NumberExpr(800))])],
                },
            ],
        });
        Start(state);
        return (state, box, hut, shrine, seer);
    }

    [Fact]
    public void OnTheDayOfPreparation_BoxesHutsShrinesAndQuestsShowWhatTheyHold()
    {
        var (state, box, hut, shrine, seer) = Objects(arena: true);
        string Text(MapObject o) => string.Join(" | ", InfoSheets.MapObject(state, Blue, o, detailed: false).Lines);

        Assert.Contains("2500 gold", Text(box), StringComparison.Ordinal);
        Assert.Contains("+2 Attack", Text(box), StringComparison.Ordinal);
        Assert.Contains("Teaches Logistics or Archery", Text(hut), StringComparison.Ordinal);
        Assert.Contains($"Teaches {state.Spells.Get(shrine.Spell!).Name}", Text(shrine), StringComparison.Ordinal);
        var quest = Text(seer);
        Assert.Contains("Quest: Grow a little.", quest, StringComparison.Ordinal);
        Assert.Contains("Wants: hero level ≥ 3", quest, StringComparison.Ordinal);
        Assert.Contains("Then: Give 800 Gold", quest, StringComparison.Ordinal);
        Assert.DoesNotContain("Well done", quest, StringComparison.Ordinal);
    }

    [Fact]
    public void OnAnOrdinaryMap_TheContentsStayHidden()
    {
        var (state, box, hut, shrine, seer) = Objects(arena: false);
        foreach (var mapObject in new MapObject[] { box, hut, shrine, seer })
        {
            Assert.Empty(EternalWars.Core.Domain.Magic.AdventureIntel.ArenaContents(state, mapObject));
            var text = string.Join(" | ", InfoSheets.MapObject(state, Blue, mapObject, detailed: false).Lines);
            Assert.DoesNotContain("2500", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Teaches", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Wants", text, StringComparison.Ordinal);
        }
    }
}
