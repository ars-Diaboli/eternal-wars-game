using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The AI and the map events (EVT-6): answers by worth, quests worth doing, Quest Guards and Gates as obstacles until met.</summary>
public class AdventureAiEventTests
{
    private static AdventureAi Ai(GameSession session) => new(session, controlHumanSlots: true);

    private static CompareCondition LevelAtLeast(int level) =>
        new(new QuantityExpr(Quantity.HeroLevel), CompareOp.GreaterOrEqual, new NumberExpr(level));

    /// <summary>A hero event on (1, 0) asks which of two answers; each sets <c>picked</c> to its number.</summary>
    private static GameSession Asked(EventExpr first, EventExpr second)
    {
        var state = Create("A.......B");
        state.SetMapEvents(new MapEventSet
        {
            Variables = new Dictionary<string, int> { ["picked"] = 0 },
            Cells = [new EventCell(1, 0, 0, "ask")],
            Events =
            [
                new MapEventDefinition("ask", MapEventKind.Hero,
                [
                    new QuestionAction("Which?",
                    [
                        new EventAnswer("First", [new ExperienceAction(first), new SetVariableAction("picked", VariableOp.Set, new NumberExpr(1))]),
                        new EventAnswer("Second", [new ExperienceAction(second), new SetVariableAction("picked", VariableOp.Set, new NumberExpr(2))]),
                    ]),
                    new EnableEventAction(null, false),
                ]),
            ],
        });
        var session = Start(state);
        session.Step(A, Direction.East);
        Assert.IsType<MapEventDecision>(session.State.PendingDecision);
        return session;
    }

    [Theory]
    [InlineData(100, 2)] // a random 0 … 100 is worth 50: less than a sure 60
    [InlineData(200, 1)] // 0 … 200 is worth 100: more
    public void AQuestion_TakesTheAnswerWorthTheMost_RandomNumbersAtTheirAverage(int max, int expected)
    {
        var session = Asked(new RandomExpr(0, max), new NumberExpr(60));
        var ai = Ai(session);

        ai.PlayTurn();

        Assert.Equal(expected, session.State.MapEvents.Variable("picked"));
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void ABattleItWouldLose_IsWorthLessThanNothing()
    {
        var state = Create("A.......B");
        state.SetMapEvents(new MapEventSet
        {
            Variables = new Dictionary<string, int> { ["picked"] = 0 },
            Cells = [new EventCell(1, 0, 0, "ask")],
            Events =
            [
                new MapEventDefinition("ask", MapEventKind.Hero,
                [
                    new QuestionAction("Fight the dragons for a fortune?",
                    [
                        new EventAnswer("Fight", [new BattleAction([new CreatureStack(BuiltInCreatures.Footman, 500)]), new ResourceAction(ResourceType.Gold, new NumberExpr(20_000)),
                            new SetVariableAction("picked", VariableOp.Set, new NumberExpr(1))]),
                        new EventAnswer("Leave", [new SetVariableAction("picked", VariableOp.Set, new NumberExpr(2))]),
                    ]),
                    new EnableEventAction(null, false),
                ]),
            ],
        });
        var session = Start(state);
        session.Step(A, Direction.East);
        var ai = Ai(session);

        ai.PlayTurn();

        Assert.Equal(2, session.State.MapEvents.Variable("picked"));
        Assert.NotNull(session.State.Heroes.FirstOrDefault(h => h.Id == A));
    }

    /// <summary>A Seer's Hut at (2, 0) (entrance (3, 1)) whose one stage asks level 1, takes the footmen, and gives <paramref name="gold"/>.</summary>
    private static (GameSession Session, QuestSite Hut) Hut(int gold, int level = 1)
    {
        var state = Create("........", "A.......", "........", ".......B");
        var hut = Add<QuestSite>(state, BuiltInMapObjects.SeerHut, 2, 0);
        hut.Quest = "hut";
        state.SetMapEvents(new MapEventSet
        {
            Events =
            [
                new MapEventDefinition("hut", MapEventKind.Quest, [])
                {
                    Stages = [new QuestStage(LevelAtLeast(level), "Give me your footmen.",
                        [new CreaturesAction(BuiltInCreatures.Footman, new NumberExpr(-5)), new ResourceAction(ResourceType.Gold, new NumberExpr(gold))])],
                },
            ],
        });
        var session = Start(state);
        session.State.GetPlayer(Blue).Resources = ResourceSet.Empty;
        return (session, hut);
    }

    [Theory]
    [InlineData(100, false)] // five footmen are worth more than 100 gold
    [InlineData(20_000, true)]
    public void ASeersHut_IsDone_OnlyWhenItGivesMoreThanItTakes(int gold, bool done)
    {
        var (session, hut) = Hut(gold);
        var ai = Ai(session);

        session.MoveTo(A, hut.VisitPosition);
        ai.PlayTurn();

        Assert.Equal(done ? 1 : 0, hut.Stage);
        Assert.Equal(done ? 5 : 10, session.State.GetHero(A).Army.Stacks.Sum(s => s.Stack.Count));
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void AHeroGoesToASeersHutWorthDoing()
    {
        var (session, hut) = Hut(20_000);
        Steady(session.State);

        Ai(session).PlayTurn();

        Assert.Equal(1, hut.Stage);
        Assert.Equal(20_000, session.State.GetPlayer(Blue).Resources.Gold);
    }

    [Fact]
    public void AHeroDoesNotGoToASeersHutItDoesNotMeet()
    {
        var (session, hut) = Hut(20_000, level: 30);

        Ai(session).PlayTurn();

        Assert.Equal(0, hut.Stage);
        Assert.Empty(session.State.MapEvents.QuestLog(Blue)); // it never went to look
    }

    /// <summary>A corridor with a Quest Guard or Gate at (3, 0); the only unexplored ground is behind it.</summary>
    private static (GameSession Session, QuestSite Site) Barrier(string definition, int level, params EventAction[] actions)
    {
        var state = Create("A..............................................B");
        var site = Add<QuestSite>(state, definition, 3, 0);
        site.Quest = "barrier";
        state.SetMapEvents(new MapEventSet
        {
            Events = [new MapEventDefinition("barrier", MapEventKind.Quest, []) { Stages = [new QuestStage(LevelAtLeast(level), "Pass when you are ready.", actions)] }],
        });
        return (Start(state, revealAll: false), site);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(30, false)]
    public void AQuestGuard_IsAnObstacle_UntilTheHeroMeetsIt(int level, bool removed)
    {
        var (session, guard) = Barrier(BuiltInMapObjects.QuestGuard, level);
        var ai = Ai(session);

        ai.PlayTurn();

        Assert.Equal(!removed, session.State.Objects.Contains(guard));
        Assert.True(session.State.GetHero(A).Position.X < 3 || removed);
        Assert.Equal(0, ai.Refused);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(30, false)]
    public void AHeroExploresThroughAQuestGate_OnlyWhenItMeetsItsQuest(int level, bool through)
    {
        var (session, _) = Barrier(BuiltInMapObjects.QuestGate, level);
        var ai = Ai(session);

        ai.PlayTurn();

        Assert.Equal(through, session.State.GetHero(A).Position.X > 3);
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void ItDoesNotLookAheadAtEventCells()
    {
        // A cell that would give a fortune is not a target: with nothing else to do, the hero only explores (the map is all in sight).
        var state = Create("A...B");
        state.SetMapEvents(new MapEventSet
        {
            Cells = [new EventCell(2, 0, 0, "fortune")],
            Events = [new MapEventDefinition("fortune", MapEventKind.Hero, [new ResourceAction(ResourceType.Gold, new NumberExpr(50_000))])],
        });
        var session = Start(state);
        session.State.GetPlayer(Blue).Resources = ResourceSet.Empty;

        Ai(session).PlayTurn();

        Assert.Equal(0, session.State.GetPlayer(Blue).Resources.Gold);
    }
}
