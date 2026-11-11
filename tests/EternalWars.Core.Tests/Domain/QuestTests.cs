using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Map events, step 2 (ADR-115, EVT-5): quests, the Seer's Hut, Quest Guard and Quest Gate, and the quest log.</summary>
public class QuestTests
{
    private const int Asked = 100_000;

    private static QuestStage PayGold(string proposal, params EventAction[] rewards) =>
        QuestForms.Resources(new ResourceSet(Gold: Asked), proposal, rewards);

    private static MapEventSet Quest(params QuestStage[] stages) => new()
    {
        Events = [new MapEventDefinition("quest", MapEventKind.Quest, []) { Stages = stages }],
    };

    /// <summary>Hero A at (0, 1); <paramref name="rows"/> default to open grass with Red's hero B at (5, 3).</summary>
    private static (GameSession Session, QuestSite Site) World(string definition, int x, int y, MapEventSet events, params string[] rows)
    {
        var state = Create(rows.Length > 0 ? rows : ["......", "A.....", "......", ".....B"]);
        var site = Add<QuestSite>(state, definition, x, y);
        site.Quest = "quest";
        state.SetMapEvents(events);
        var session = Start(state);
        Gold(session, Blue, 0);
        Gold(session, Red, 0);
        return (session, site);
    }

    private static void Gold(GameSession session, PlayerId player, int gold) => session.State.GetPlayer(player).Resources = new ResourceSet(Gold: gold);

    private static ResourceSet Resources(GameSession session, PlayerId player) => session.State.GetPlayer(player).Resources;

    private static void Answer(GameSession session, int option = 0) =>
        session.Execute(new AnswerMapEventCommand(session.State.CurrentPlayer.Id, option)).ThrowIfRejected();

    private static MapEventDecision Pending(GameSession session) => Assert.IsType<MapEventDecision>(session.State.PendingDecision);

    // Seer's Hut ------------------------------------------------------------------------------------------------------

    [Fact]
    public void ASeersHut_ShowsItsProposal_ThenItsProgress_AndLogsTheQuest()
    {
        // The hut's entrance is (3, 1).
        var stage = PayGold("Bring me 100000 gold.", new ResourceAction(ResourceType.Crystal, new NumberExpr(5))) with { Progress = "Still no gold?" };
        var (session, site) = World(BuiltInMapObjects.SeerHut, 2, 0, Quest(stage));

        session.MoveTo(A, site.VisitPosition);
        Assert.Equal(site.VisitPosition, session.State.GetHero(A).Position);
        var shown = Pending(session);
        Assert.Equal("Bring me 100000 gold.", shown.Text);
        Assert.False(shown.IsQuestion);
        Answer(session);
        var entry = Assert.Single(session.State.MapEvents.QuestLog(Blue));
        Assert.Equal(new QuestLogEntry(site.Id, "quest", 0, Done: false), entry);
        Assert.Equal("Bring me 100000 gold.", session.State.MapEvents.QuestText(entry));
        Assert.Empty(session.State.MapEvents.QuestLog(Red));

        session.Step(A, Direction.South);
        session.Step(A, Direction.North);
        Assert.Equal("Still no gold?", Pending(session).Text);
        Answer(session);
        Assert.Equal(0, site.Stage);
    }

    [Fact]
    public void MeetingAStage_AsksToComplete_TakesWhatItAsks_GivesTheReward_AndShowsTheNextStage()
    {
        var (session, site) = World(BuiltInMapObjects.SeerHut, 2, 0, Quest(
            PayGold("Bring me 100000 gold.", new ResourceAction(ResourceType.Crystal, new NumberExpr(5))),
            QuestForms.Level(99, "Now grow wise.")));
        Gold(session, Blue, Asked + 50);
        var completed = new List<QuestStageCompleted>();
        session.Events.Subscribe<QuestStageCompleted>(completed.Add);

        session.MoveTo(A, site.VisitPosition);
        var offer = Pending(session);
        Assert.True(offer.IsQuestion);
        Assert.Equal(["Complete", "Not now"], offer.Answers);

        Answer(session, 1); // not now: nothing taken, the stage stays
        Assert.Equal(Asked + 50, Resources(session, Blue).Gold);
        Assert.Equal(0, site.Stage);

        session.Step(A, Direction.South);
        session.Step(A, Direction.North);
        Answer(session, 0);
        Assert.Equal((50, 5), (Resources(session, Blue).Gold, Resources(session, Blue).Crystal));
        Assert.Equal(1, site.Stage);
        Assert.Equal(new QuestStageCompleted(site.Id, "quest", 0, A, Blue), Assert.Single(completed));
        Assert.Equal("Now grow wise.", Pending(session).Text);
        Answer(session);
        Assert.Equal(new QuestLogEntry(site.Id, "quest", 1, Done: false), Assert.Single(session.State.MapEvents.QuestLog(Blue)));
    }

    [Fact]
    public void TheLastStage_MarksTheQuestDone_LeavesTheHutEmpty_AndDropsTheOtherPlayersOpenEntries()
    {
        var (session, site) = World(BuiltInMapObjects.SeerHut, 2, 0, Quest(PayGold("Bring me 100000 gold.")));
        session.MoveTo(A, site.VisitPosition); // Blue cannot pay: the quest goes into Blue's log
        Answer(session);
        session.Step(A, Direction.South); // off the entrance, out of Red's way
        session.EndTurn();

        Gold(session, Red, Asked);
        session.MoveTo(B, site.VisitPosition);
        Assert.True(Pending(session).IsQuestion);
        Answer(session, 0);

        Assert.Equal(new QuestLogEntry(site.Id, "quest", 0, Done: true), Assert.Single(session.State.MapEvents.QuestLog(Red)));
        Assert.Empty(session.State.MapEvents.QuestLog(Blue));
        Assert.Null(QuestRules.Current(session.State, site));
        Assert.Same(site, session.State.FindObject(site.Id)); // a hut stays

        var empty = new List<MapObjectFoundEmpty>();
        session.Events.Subscribe<MapObjectFoundEmpty>(empty.Add);
        session.Step(B, Direction.East);
        session.Step(B, Direction.West);
        Assert.Null(session.State.PendingDecision);
        Assert.Single(empty);
    }

    [Fact]
    public void ALostBattleAmongTheActions_LeavesTheStageOpen()
    {
        var dragons = new BattleAction([new CreatureStack(BuiltInCreatures.Archer, 5000)]);
        var (session, site) = World(BuiltInMapObjects.SeerHut, 2, 0, Quest(QuestForms.Level(1, "Prove yourself.", dragons)));
        session.MoveTo(A, site.VisitPosition);
        Answer(session, 0);
        Assert.NotNull(session.State.Battle);

        session.AutoResolve();
        Assert.Equal(0, site.Stage);
    }

    // Quest Guard -----------------------------------------------------------------------------------------------------

    [Fact]
    public void AQuestGuard_IsAWallRoutesGoAround_ABumpShowsTheQuest_AndCompletingItRemovesTheGuard()
    {
        var (session, guard) = World(BuiltInMapObjects.QuestGuard, 2, 1, Quest(PayGold("Pay the toll.")));
        var hero = session.State.GetHero(A);

        var around = session.Movement.FindPath(A, new MapPosition(4, 1));
        Assert.NotNull(around);
        Assert.DoesNotContain(around.Steps, s => s.Position == guard.Position);

        var bump = session.MoveTo(A, guard.Position);
        Assert.Equal(MoveResult.QuestVisited, bump.StopReason);
        Assert.Equal(new MapPosition(1, 1), hero.Position);
        Assert.Equal("Pay the toll.", Pending(session).Text);
        Answer(session);

        Gold(session, Blue, Asked);
        Assert.Equal(MoveResult.QuestVisited, session.Step(A, Direction.East));
        Answer(session, 0);
        Assert.Null(session.State.FindObject(guard.Id));
        Assert.Equal(0, Resources(session, Blue).Gold);
        Assert.True(Assert.Single(session.State.MapEvents.QuestLog(Blue)).Done);
        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.East));
    }

    // Quest Gate ------------------------------------------------------------------------------------------------------

    private static readonly string[] Walled = ["..#...", "A.....", "..#...", "..#..B"];

    [Fact]
    public void AQuestGate_BarsThoseWhoDoNotMeetItsQuest_AndLetsTheOthersThroughWithoutStopping()
    {
        var gate = new MapEventDefinition("quest", MapEventKind.Quest, []) { Stages = [new(new CompareCondition(new QuantityExpr(Quantity.Resource, "Gold"), CompareOp.GreaterOrEqual, new NumberExpr(Asked)), "Only the rich pass.", [])] };
        var (session, site) = World(BuiltInMapObjects.QuestGate, 2, 1, new MapEventSet { Events = [gate] }, Walled);
        var hero = session.State.GetHero(A);
        var beyond = new MapPosition(4, 1);

        Assert.Null(session.Movement.FindPath(A, beyond));
        Assert.Equal(MoveResult.QuestVisited, session.MoveTo(A, site.Position).StopReason);
        Assert.Equal("Only the rich pass.", Pending(session).Text);
        Answer(session);
        Assert.False(Assert.Single(session.State.MapEvents.QuestLog(Blue)).Done);

        Gold(session, Blue, Asked);
        Assert.True(session.MoveTo(A, beyond).Arrived);
        Assert.Null(session.State.PendingDecision);
        Assert.Equal(Asked, Resources(session, Blue).Gold); // a gate takes nothing
        Assert.Same(site, session.State.FindObject(site.Id));
        Assert.True(Assert.Single(session.State.MapEvents.QuestLog(Blue)).Done);
        Assert.Equal(beyond, hero.Position);
    }

    [Fact]
    public void AQuestObjectWithoutAQuest_StandsEmptyOrBarsEveryone_AndTheValidatorWarns()
    {
        var state = Create(Walled);
        var gate = Add<QuestSite>(state, BuiltInMapObjects.QuestGate, 2, 1);
        Assert.Contains(MapValidator.Validate(state), i => i.Severity == IssueSeverity.Warning && i.Message.Contains("no quest", StringComparison.Ordinal));
        var session = Start(state);

        Assert.Null(session.Movement.FindPath(A, new MapPosition(4, 1)));
        Assert.Equal(MoveResult.QuestVisited, session.MoveTo(A, gate.Position).StopReason);
        Assert.Null(session.State.PendingDecision);
    }

    // Ready forms -----------------------------------------------------------------------------------------------------

    [Fact]
    public void TheReadyForms_TakeTheCreaturesAndArtifactsTheyAskFor()
    {
        var artifact = EternalWars.Core.Domain.Artifacts.ArtifactCatalog.Default.All.First().Id;
        var (session, site) = World(BuiltInMapObjects.SeerHut, 2, 0, Quest(
            QuestForms.Creatures([new CreatureStack(BuiltInCreatures.Footman, 4)], "Lend me four footmen."),
            QuestForms.Artifacts([artifact], "Bring me the relic.")));
        var hero = session.State.GetHero(A);
        hero.Artifacts.AddToBackpack(artifact);

        session.MoveTo(A, site.VisitPosition);
        Answer(session, 0);
        Assert.Equal(6, hero.Army.Stacks.Single().Stack.Count);
        Assert.Equal("Bring me the relic.", Pending(session).Text);
        Answer(session);

        session.Step(A, Direction.South);
        session.Step(A, Direction.North);
        Answer(session, 0);
        Assert.DoesNotContain(artifact, hero.Artifacts.Backpack);
        Assert.True(Assert.Single(session.State.MapEvents.QuestLog(Blue)).Done);
    }

    // Map format and loading ------------------------------------------------------------------------------------------

    [Fact]
    public void Quests_AndQuestObjects_RoundTripThroughTheMapFormat()
    {
        var state = Create("......", "A.....", "......", ".....B");
        var hut = Add<QuestSite>(state, BuiltInMapObjects.SeerHut, 2, 0);
        hut.Quest = "quest";
        Add<QuestSite>(state, BuiltInMapObjects.QuestGuard, 5, 0);
        var events = Quest(PayGold("Pay.", new ExperienceAction(new NumberExpr(10))) with { Progress = "Well?", Completion = "Pay now?" },
            QuestForms.DefeatMonster(7, "Slay the beast."));
        state.SetMapEvents(events);

        var json = TestFormat.Save(state);
        Assert.True(MapDocument.CurrentFormatVersion >= 32);
        var restored = TestFormat.Load(json);

        var quest = Assert.Single(restored.MapEvents.Set.Events);
        Assert.Equal(MapEventKind.Quest, quest.Kind);
        Assert.Equal(2, quest.Stages.Count);
        Assert.Equal(("Well?", "Pay now?"), (quest.Stages[0].Progress, quest.Stages[0].Completion));
        Assert.Equal(json, TestFormat.Save(restored));
        var sites = restored.ObjectsOf<QuestSite>().OrderBy(s => s.Id.Value).ToList();
        Assert.Equal(["quest", null], sites.Select(s => s.Quest));
        Assert.Equal([MapObjectKind.SeerHut, MapObjectKind.QuestGuard], sites.Select(s => s.Kind));
    }

    public static TheoryData<string> WrongQuests => ["unknown", "not-a-quest", "shared", "gate-stages", "random", "actions", "no-proposal", "stages-elsewhere"];

    [Theory]
    [MemberData(nameof(WrongQuests))]
    public void WrongQuests_AreRefused(string wrong)
    {
        var state = Create(Walled);
        var first = Add<QuestSite>(state, wrong == "gate-stages" ? BuiltInMapObjects.QuestGate : BuiltInMapObjects.SeerHut, 3, 0);
        first.Quest = wrong switch { "unknown" => "nothing", "not-a-quest" => "cell", _ => "quest" };
        if (wrong == "shared")
        {
            Add<QuestSite>(state, BuiltInMapObjects.SeerHut, 3, 2).Quest = "quest";
        }

        var stage = wrong switch
        {
            "gate-stages" => QuestForms.Level(2, "Grow."),
            "random" => new QuestStage(new CompareCondition(new RandomExpr(1, 6), CompareOp.Equal, new NumberExpr(6)), "Roll a six.", []),
            "no-proposal" => QuestForms.Level(2, " "),
            _ => QuestForms.Level(2, "Grow."),
        };
        var quest = new MapEventDefinition("quest", MapEventKind.Quest, wrong == "actions" ? [new StopAction()] : []) { Stages = wrong == "gate-stages" ? [stage, stage] : [stage] };
        var cell = new MapEventDefinition("cell", MapEventKind.Hero, []) { Stages = wrong == "stages-elsewhere" ? [stage] : [] };

        Assert.Throws<ArgumentException>(() => state.SetMapEvents(new MapEventSet { Events = [quest, cell] }));
    }

    [Fact]
    public void AMapWithQuestObjectsButNoEvents_IsCheckedOnLoad()
    {
        var state = Create("......", "A.....", "......", ".....B");
        Add<QuestSite>(state, BuiltInMapObjects.SeerHut, 2, 0).Quest = "missing";

        Assert.Throws<FormatException>(() => MapConverter.ToGameState(MapConverter.ToDocument(state)));
    }
}
