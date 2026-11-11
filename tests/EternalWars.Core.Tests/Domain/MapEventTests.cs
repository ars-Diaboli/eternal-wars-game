using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Map events, step 1 (ADR-115, EVT-1 … EVT-4): event cells, turn-start player and town events, conditions, variables and actions.</summary>
public class MapEventTests
{
    private static NumberExpr N(int value) => new(value);

    private static MapEventSet HeroEvent(params EventAction[] actions) => new()
    {
        Events = [new MapEventDefinition("cell", MapEventKind.Hero, actions)],
        Cells = [new EventCell(2, 1, 0, "cell")],
    };

    /// <summary>Hero A at (0, 1); the event cell at (2, 1).</summary>
    private static GameSession World(MapEventSet events, Action<GameState>? setup = null)
    {
        var state = Create("......", "A.....", "......", ".....B");
        setup?.Invoke(state);
        state.SetMapEvents(events);
        return Start(state);
    }

    private static int Gold(GameSession session, PlayerId player) => session.State.GetPlayer(player).Resources.Gold;

    private static void Answer(GameSession session, int option = 0) =>
        session.Execute(new AnswerMapEventCommand(session.State.CurrentPlayer.Id, option)).ThrowIfRejected();

    [Fact]
    public void ACell_RunsOnEveryStep_WithoutStoppingTheRoute_AndOnceDisablesItself()
    {
        var session = World(HeroEvent(new ResourceAction(ResourceType.Gold, N(100))));
        var gold = Gold(session, Blue);

        var outcome = session.MoveTo(A, new MapPosition(4, 1));
        Assert.True(outcome.Arrived);
        Assert.Equal(gold + 100, Gold(session, Blue));

        session.Step(A, Direction.West);
        session.Step(A, Direction.West);
        Assert.Equal(gold + 200, Gold(session, Blue));

        var once = World(HeroEvent(new ResourceAction(ResourceType.Gold, N(100)), new EnableEventAction(null, false)));
        once.Step(A, Direction.East);
        once.Step(A, Direction.East);
        once.Step(A, Direction.East);
        once.Step(A, Direction.West);
        Assert.Equal(gold + 100, Gold(once, Blue));
        Assert.False(once.State.MapEvents.IsEnabled("cell"));
    }

    [Fact]
    public void AMessage_StopsTheRoute_ShowsItsRewards_AndGivesThemOnceRead()
    {
        var session = World(HeroEvent(new MessageAction("A hermit shares his savings.")
        {
            Rewards = [new ResourceAction(ResourceType.Gold, N(500)), new ExperienceAction(N(200))],
        }));
        var hero = session.State.GetHero(A);
        var (gold, experience) = (Gold(session, Blue), hero.Experience);

        var outcome = session.MoveTo(A, new MapPosition(4, 1));
        Assert.Equal(MoveResult.DecisionPending, outcome.StopReason);
        Assert.Equal(new MapPosition(2, 1), hero.Position);
        var decision = Assert.IsType<MapEventDecision>(session.State.PendingDecision);
        Assert.Equal(["+500 Gold", "+200 experience"], decision.Lines);
        Assert.False(decision.IsQuestion);
        Assert.Equal(gold, Gold(session, Blue));

        Assert.False(session.Execute(new AnswerMapEventCommand(Blue, 1)).IsAccepted);
        Answer(session);
        Assert.Equal(gold + 500, Gold(session, Blue));
        Assert.Equal(experience + 200, hero.Experience);
        Assert.Null(session.State.PendingDecision);
    }

    [Fact]
    public void AQuestion_RunsTheChosenAnswer_AndVariablesSteerTheBranches()
    {
        var events = HeroEvent(
            new SetVariableAction("visits", VariableOp.Add, N(1)),
            new IfAction(new CompareCondition(new VariableExpr("visits"), CompareOp.Greater, N(1)), [new MessageAction("Back again?")]),
            new QuestionAction("Pay 1000 gold for training?", [
                new EventAnswer("Yes", [new ResourceAction(ResourceType.Gold, N(-1000)), new PrimaryStatAction(PrimaryStat.Attack, N(2))]),
                new EventAnswer("No", [new StopAction()]),
            ]),
            new SetVariableAction("trained", VariableOp.Add, N(1))) with
        {
            Variables = new Dictionary<string, int> { ["visits"] = 0, ["trained"] = 0 },
        };
        var session = World(events, state => state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 5000));
        var hero = session.State.GetHero(A);
        var (gold, attack) = (Gold(session, Blue), hero.Attack);

        session.MoveTo(A, new MapPosition(2, 1));
        Assert.Equal(["Yes", "No"], Assert.IsType<MapEventDecision>(session.State.PendingDecision).Answers);
        Answer(session, 0);
        Assert.Equal((gold - 1000, attack + 2), (Gold(session, Blue), hero.Attack));
        Assert.Equal(1, session.State.MapEvents.Variable("trained"));

        session.Step(A, Direction.East);
        session.Step(A, Direction.West);
        Assert.Equal("Back again?", Assert.IsType<MapEventDecision>(session.State.PendingDecision).Text);
        Answer(session);
        Answer(session, 1);
        Assert.Equal(2, session.State.MapEvents.Variable("visits"));
        Assert.Equal(1, session.State.MapEvents.Variable("trained"));
        Assert.Equal(gold - 1000, Gold(session, Blue));
    }

    [Fact]
    public void ARunningEvent_IsPartOfTheGameState_AndItsDigest_WhereItIsInItsActionsToo()
    {
        var events = HeroEvent(new MessageAction("One."), new MessageAction("Two."));
        var session = World(events);
        var idle = GameStateDigest.Describe(session.State);

        session.Step(A, Direction.East);
        session.Step(A, Direction.East);
        var first = GameStateDigest.Describe(session.State);
        Assert.NotNull(session.State.MapEvents.Current);
        Assert.Contains("running cell", first, StringComparison.Ordinal);

        Answer(session);
        var second = GameStateDigest.Describe(session.State);
        Assert.Equal("Two.", Assert.IsType<MapEventDecision>(session.State.PendingDecision).Text);
        Assert.NotEqual(Running(first), Running(second)); // the run moved on in its actions

        Answer(session);
        Assert.Null(session.State.MapEvents.Current);
        Assert.DoesNotContain("running", GameStateDigest.Describe(session.State), StringComparison.Ordinal);
        Assert.DoesNotContain("running", idle, StringComparison.Ordinal);

        static string Running(string digest) => digest[digest.IndexOf("running", StringComparison.Ordinal)..].Split('\n')[0];
    }

    [Fact]
    public void PlayerEvents_RunAtEveryTurnStart_ForThePlayersTheirConditionsAllow()
    {
        var events = new MapEventSet
        {
            Events =
            [
                new MapEventDefinition("welcome", MapEventKind.Player, [
                    new IfAction(new PlayerIsCondition(0), [new MessageAction("Welcome, Blue."), new EnableEventAction(null, false)]),
                ]),
                new MapEventDefinition("crystal", MapEventKind.Player, [
                    new IfAction(new AllCondition([new PlayerIsCondition(1), new CompareCondition(new QuantityExpr(Quantity.Day), CompareOp.GreaterOrEqual, N(2))]),
                        [new ResourceAction(ResourceType.Crystal, N(3))]),
                ]),
            ],
        };
        var session = World(events);

        var welcome = Assert.IsType<MapEventDecision>(session.State.PendingDecision);
        Assert.Equal(MapEventDecision.NoHero, welcome.Hero);
        Answer(session);

        session.EndTurn(); // Red, day 1
        Assert.Equal(0, session.State.GetPlayer(Red).Resources.Crystal);
        session.EndTurn(); // Blue, day 2: the welcome ran once
        Assert.Null(session.State.PendingDecision);
        session.EndTurn(); // Red, day 2
        Assert.Equal(3, session.State.GetPlayer(Red).Resources.Crystal);
    }

    [Fact]
    public void ATurnStartMessage_NamesThePlaceItConcerns_ForTheCamera()
    {
        static MapEventDecision First(MapEventSet events, Action<GameState>? setup = null) =>
            Assert.IsType<MapEventDecision>(World(events, setup).State.PendingDecision);

        static MapEventSet Player(params EventAction[] actions) => new() { Events = [new MapEventDefinition("start", MapEventKind.Player, [new IfAction(new PlayerIsCondition(0), actions)])] };

        // Nothing touched: no place, the camera stays with the first hero.
        Assert.Null(First(Player(new MessageAction("Hello."))).Place);

        // An area the event revealed before the message, or one the message's rewards reveal.
        Assert.Equal(new MapPosition(4, 3), First(Player(new RevealAction(4, 3, 0, 2), new MessageAction("Look there."))).Place);
        Assert.Equal(new MapPosition(5, 0), First(Player(new MessageAction("Look there.") { Rewards = [new RevealAction(5, 0, 0, 1)] })).Place);

        // A question after a hero was given: where the hero stands.
        var roster = Create("......", "A.....", "......", ".....B");
        var free = roster.HeroRoster.All.First(h => roster.Heroes.All(x => x.Definition != h.Id)).Id;
        roster.SetMapEvents(Player(new GiveHeroAction(free) { X = 3, Y = 0 }, new QuestionAction("Welcome them?", [new EventAnswer("Yes", []), new EventAnswer("No", [])])));
        var given = Assert.IsType<MapEventDecision>(Start(roster).State.PendingDecision);
        Assert.Equal(new MapPosition(3, 0), given.Place);
        Assert.Contains(roster.Heroes, h => h.Definition == free && h.Position == new MapPosition(3, 0));

        // A town event: its town.
        var state = Create(Enumerable.Repeat("....................", 10).Append("A..................B").ToArray());
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.SetMapEvents(new MapEventSet { Events = [new MapEventDefinition("fair", MapEventKind.Town, [new MessageAction("The fair is on.")]) { Towns = [town.Id.Value] }] });
        Assert.Equal(town.VisitPosition, Assert.IsType<MapEventDecision>(Start(state).State.PendingDecision).Place);

        // A hero event: none, the hero is in view.
        var session = World(HeroEvent(new RevealAction(5, 3, 0, 1), new MessageAction("A cell.")));
        session.MoveTo(A, new MapPosition(2, 1));
        Assert.Null(Assert.IsType<MapEventDecision>(session.State.PendingDecision).Place);
    }

    [Fact]
    public void TownEvents_RunForTheOwnerAndForNeutralTowns_AndBuildWhatTheBuildingNeeds()
    {
        Town? own = null, neutral = null;
        var state = Create(Enumerable.Repeat("....................", 10).Append("A..................B").ToArray());
        own = AddTown(state, TownIds.Haven, 0, 0, Blue);
        neutral = AddTown(state, TownIds.Haven, 10, 0);
        state.SetMapEvents(new MapEventSet
        {
            Events =
            [
                new MapEventDefinition("rise", MapEventKind.Town, [new BuildAction(BuiltInBuildings.TownHall, 2), new CreaturesAction(BuiltInCreatures.Footman, N(5))])
                {
                    Towns = [own.Id.Value, neutral.Id.Value],
                },
            ],
        });
        var session = Start(state);

        foreach (var town in new[] { own, neutral })
        {
            Assert.Equal(2, town.Level(BuiltInBuildings.TownHall));
            Assert.True(town.Level(BuiltInBuildings.Blacksmith) >= 1 && town.Level(BuiltInBuildings.Marketplace) >= 1 && town.Level(BuiltInBuildings.MageGuild) >= 1);
            Assert.Equal(5, town.Garrison.Stacks.Where(s => s.Stack.Creature == BuiltInCreatures.Footman).Sum(s => s.Stack.Count));
        }

        session.EndTurn(); // Red's turn: neither town is Red's
        Assert.Equal(5, own.Garrison.Stacks.Sum(s => s.Stack.Count));
        session.EndTurn(); // day 2: the neutral town at the start of the day, then Blue's
        Assert.Equal(10, own.Garrison.Stacks.Sum(s => s.Stack.Count));
        Assert.Equal(10, neutral.Garrison.Stacks.Sum(s => s.Stack.Count));
    }

    [Fact]
    public void ABattle_WonGoesOn_LostStopsTheEvent()
    {
        var reward = new ResourceAction(ResourceType.Gold, N(1000));
        var won = World(HeroEvent(new BattleAction([new CreatureStack(BuiltInCreatures.Wolf, 2)]), reward));
        var gold = Gold(won, Blue);
        won.Step(A, Direction.East);
        won.Step(A, Direction.East);
        Assert.IsType<BattleDecision>(won.State.PendingDecision);
        won.AutoResolve();
        Assert.Equal(gold + 1000, Gold(won, Blue));
        Assert.Contains(won.State.Heroes, h => h.Id == A);

        var lost = World(HeroEvent(new BattleAction([new CreatureStack(BuiltInCreatures.Wolf, 300)]), reward));
        lost.Step(A, Direction.East);
        lost.Step(A, Direction.East);
        lost.AutoResolve();
        Assert.DoesNotContain(lost.State.Heroes, h => h.Id == A);
        Assert.Equal(gold, Gold(lost, Blue));
        Assert.False(lost.MapEvents.IsBusy);
    }

    [Fact]
    public void CreaturesForAFullArmy_AskThePersonWhatToKeep()
    {
        string[] seven = [BuiltInCreatures.Footman, BuiltInCreatures.Pikeman, BuiltInCreatures.Archer, BuiltInCreatures.Monk,
            BuiltInCreatures.Zealot, BuiltInCreatures.Crusader, BuiltInCreatures.Guardian];
        var session = World(HeroEvent(new CreaturesAction(BuiltInCreatures.Wolf, N(4))), state =>
        {
            for (var slot = 0; slot < seven.Length; slot++)
            {
                state.GetHero(A).Army.Set(slot, new CreatureStack(seven[slot], 1));
            }
        });

        session.Step(A, Direction.East);
        session.Step(A, Direction.East);
        var decision = Assert.IsType<MapEventArmyDecision>(session.State.PendingDecision);
        Assert.Equal(8, decision.Stacks.Count);
        Assert.False(session.Execute(new ChooseEventArmyCommand(Blue, [0, 1, 2, 3, 4, 5, 6, 7])).IsAccepted);

        session.Execute(new ChooseEventArmyCommand(Blue, [7, 0, 1, 2, 3, 4, 5])).ThrowIfRejected();
        var army = session.State.GetHero(A).Army;
        Assert.Contains(army.Stacks, s => s.Stack == new CreatureStack(BuiltInCreatures.Wolf, 4));
        Assert.DoesNotContain(army.Stacks, s => s.Stack.Creature == BuiltInCreatures.Guardian);
    }

    [Fact]
    public void AMonsterAnEventNames_RemembersWhoBeatIt()
    {
        MonsterGuard? monster = null;
        var state = Create("......", "A.....", "......", ".....B");
        monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 1, 1);
        (monster.Creature, monster.Count, monster.Disposition, monster.NeverFlees) = (BuiltInCreatures.Wolf, 2, GuardDisposition.Savage, true);
        state.SetMapEvents(new MapEventSet
        {
            Variables = new Dictionary<string, int> { ["slain"] = 0 },
            Events =
            [
                new MapEventDefinition("bounty", MapEventKind.Player, [
                    new IfAction(new MonsterDefeatedCondition(monster.Id.Value), [new SetVariableAction("slain", VariableOp.Set, N(1))]),
                ]),
            ],
        });
        var session = Start(state);

        session.Step(A, Direction.East); // onto the monster: the fight starts
        session.AutoResolve();
        Assert.Equal(Blue, session.State.MapEvents.MonstersDefeated[monster.Id]);

        session.EndTurn(); // Red: beaten by Blue, not Red
        Assert.Equal(0, session.State.MapEvents.Variable("slain"));
        session.EndTurn(); // Blue
        Assert.Equal(1, session.State.MapEvents.Variable("slain"));
    }

    [Fact]
    public void Events_RoundTripThroughTheMapFormat_AndReplayExactly()
    {
        var state = Create("......", "A.....", "......", ".....B");
        state.SetMapEvents(HeroEvent(
            new SetVariableAction("roll", VariableOp.Set, new RandomExpr(1, 6)),
            new QuestionAction("Take the chest?", [
                new EventAnswer("Yes", [new ResourceAction(ResourceType.Gold, new ArithmeticExpr(ArithmeticOp.Multiply, new VariableExpr("roll"), N(100)))]),
                new EventAnswer("No", []),
            ])) with { Variables = new Dictionary<string, int> { ["roll"] = 0 } });

        var map = MapConverter.ToDocument(state);
        var json = MapSerializer.Serialize(map);
        Assert.True(MapDocument.CurrentFormatVersion >= 31);
        Assert.Contains("\"type\": \"question\"", json);
        Assert.Equal(json, TestFormat.Save(TestFormat.Load(json)));

        var session = new GameSession(TestFormat.Load(json, options: new GameOptions(Seed: 5)));
        session.Start();
        session.Step(A, Direction.East);
        session.Step(A, Direction.East);
        Answer(session, 0);
        Assert.InRange(session.State.MapEvents.Variable("roll"), 1, 6);

        var replayed = GameReplay.Replay(GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(map, session))));
        Assert.Equal(GameStateDigest.Describe(session.State), GameStateDigest.Describe(replayed.State));
    }

    [Fact]
    public void EventsThatDoNotFitTheMap_AreRefused()
    {
        var state = Create("..#...", "A.....");
        Assert.Throws<ArgumentException>(() => state.SetMapEvents(HeroEvent(new SetVariableAction("unknown", VariableOp.Set, N(1)))));
        Assert.Throws<ArgumentException>(() => state.SetMapEvents(HeroEvent() with { Cells = [new EventCell(2, 0, 0, "cell")] }));
        Assert.Throws<ArgumentException>(() => state.SetMapEvents(HeroEvent(new QuestionAction("Only one?", [new EventAnswer("Yes", [])]))));
        Assert.Throws<ArgumentException>(() => state.SetMapEvents(HeroEvent(new SkillAction("no-such-skill", SkillLevel.Basic))));

        var map = MapConverter.ToDocument(state) with { Events = HeroEvent(new EnableEventAction("missing", true)) };
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(map));
    }
}
