using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// Map events for stories (ADR-242): dialogues, ending the game from an event, and the captured and battle events that react to a town changing
/// hands and a battle won.
/// </summary>
public class MapEventStoryTests
{
    private static NumberExpr N(int value) => new(value);

    private static MapEventSet Player(params EventAction[] actions) =>
        new() { Events = [new MapEventDefinition("start", MapEventKind.Player, [new IfAction(new PlayerIsCondition(0), actions)])] };

    private static GameSession World(MapEventSet events, Action<GameState>? setup = null)
    {
        var state = Create("......", "A.....", "......", ".....B");
        setup?.Invoke(state);
        state.SetMapEvents(events);
        return Start(state);
    }

    private static void Answer(GameSession session) => session.Execute(new AnswerMapEventCommand(session.State.CurrentPlayer.Id, 0)).ThrowIfRejected();

    private static int Gold(GameSession session, PlayerId player) => session.State.GetPlayer(player).Resources.Gold;

    [Fact]
    public void ADialogue_ShowsAPersonItsLines_ARosterHeroByName_ThenTheEventGoesOn()
    {
        var session = World(Player(
            new DialogueAction([new DialogueLine("haven.maren", "Hold the ford."), new DialogueLine("", "The river is low.")]),
            new ResourceAction(ResourceType.Gold, N(100))));
        var gold = Gold(session, Blue);

        var dialogue = Assert.IsType<MapEventDecision>(session.State.PendingDecision);
        Assert.Equal([new DialogueLine("Maren Holt", "Hold the ford."), new DialogueLine("", "The river is low.")], dialogue.Dialogue);
        Assert.Equal("Hold the ford.", dialogue.Text);
        Assert.False(dialogue.IsQuestion);

        Answer(session);
        Assert.Equal(gold + 100, Gold(session, Blue));
    }

    [Fact]
    public void ADialogue_IsNotShownToAComputerPlayer()
    {
        var session = World(Player(new DialogueAction([new DialogueLine("", "Unseen.")]), new ResourceAction(ResourceType.Gold, N(100))),
            s => s.GetPlayer(Blue).IsHuman = false);

        Assert.Null(session.State.PendingDecision);
    }

    [Fact]
    public void EndingTheGameWon_EndsItForThePlayersTeam_WithTheReason_AndStopsTheEvent()
    {
        var session = World(Player(new EndGameAction(Won: true) { Text = "The ford is held." }, new ResourceAction(ResourceType.Gold, N(100))));

        Assert.True(session.State.IsOver);
        Assert.Equal([Blue], session.State.Outcome!.Winners);
        Assert.Equal("The ford is held.", session.State.Outcome.Reason);
        Assert.Null(session.State.ScriptedEnd);
    }

    [Fact]
    public void EndingTheGameLost_DefeatsThePlayer_SoTheOtherSideWins()
    {
        var session = World(Player(new EndGameAction(Won: false)));

        Assert.True(session.State.GetPlayer(Blue).IsDefeated);
        Assert.Equal([Red], session.State.Outcome!.Winners);
    }

    [Fact]
    public void ACapturedEvent_RunsWhenItsTownFalls_ForTheNewOwnerAndTheHeroThatTookIt()
    {
        var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "..........", "..........",
            "...A.....B");
        var taken = AddTown(state, TownIds.Necropolis, 0, 0, Red);
        state.SetMapEvents(new MapEventSet
        {
            Events =
            [
                new MapEventDefinition("taken", MapEventKind.Captured, [new ResourceAction(ResourceType.Gold, N(500)), new ExperienceAction(N(1000))])
                {
                    Towns = [taken.Id.Value],
                },
            ],
        });
        var session = Start(state);
        var gold = Gold(session, Blue);

        session.MoveTo(A, taken.VisitPosition);

        Assert.Equal(Blue, taken.Owner);
        Assert.Equal(1000, session.State.GetHero(A).Experience);
        Assert.Equal(gold + 500, Gold(session, Blue));
    }

    [Fact]
    public void ABattleEvent_RunsForTheWinnerOfABattle_NotForAnEventsOwnBattle()
    {
        var state = Create("......", "A.....", "......", ".....B");
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 1, 1);
        (monster.Creature, monster.Count, monster.Disposition, monster.NeverFlees) = (BuiltInCreatures.Wolf, 2, GuardDisposition.Savage, true);
        state.SetMapEvents(new MapEventSet
        {
            Variables = new Dictionary<string, int> { ["won"] = 0 },
            Cells = [new EventCell(3, 1, 0, "ambush")],
            Events =
            [
                new MapEventDefinition("victory", MapEventKind.Battle,
                    [new IfAction(new MonsterDefeatedCondition(monster.Id.Value), [new SetVariableAction("won", VariableOp.Add, N(1))])]),
                new MapEventDefinition("ambush", MapEventKind.Hero,
                    [new BattleAction([new CreatureStack(BuiltInCreatures.Wolf, 1)]), new RemoveCellAction()]),
            ],
        });
        var session = Start(state);

        var first = session.Step(A, Direction.East); // onto the monster
        Assert.True(session.State.Battle is not null, $"first: {first} {session.State.PendingDecision} at {session.State.GetHero(A).Position}");
        session.AutoResolve();
        Assert.Equal(1, session.State.MapEvents.Variable("won"));

        session.Step(A, Direction.East); // where the monster stood: the hero stayed where it fought from
        session.Step(A, Direction.East);
        var second = session.Step(A, Direction.East); // the event cell: its own battle
        Assert.True(session.State.Battle is not null, $"second: {second} {session.State.PendingDecision} at {session.State.GetHero(A).Position}");
        session.AutoResolve();
        Assert.Equal(1, session.State.MapEvents.Variable("won"));
    }

    [Fact]
    public void TheValidator_WarnsAboutACapturedEventWithoutTowns_AndOnlyTownAndCapturedEventsNameTowns()
    {
        var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "..........", "...A.....B");
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var lonely = new MapEventSet { Events = [new MapEventDefinition("lonely", MapEventKind.Captured, [new MessageAction("Taken.")])] };
        var wrong = new MapEventSet { Events = [new MapEventDefinition("wrong", MapEventKind.Battle, []) { Towns = [town.Id.Value] }] };

        Assert.Contains(MapEventChecks.Warnings(state, lonely), w => w.Message.Contains("names no town", StringComparison.Ordinal));
        Assert.Contains(MapEventRules.Problems(state, wrong), p => p.Contains("Only a town or captured event names towns", StringComparison.Ordinal));
    }

    [Fact]
    public void Dialogues_AndAWonEnd_DoNotFavourPeople_ButAWinOnlyPeopleGetDoes()
    {
        static MapEventSet PeopleOnly(params EventAction[] actions) => new()
        {
            Events = [new MapEventDefinition("e", MapEventKind.Player, [new IfAction(new PlayerIsHumanCondition(), actions)])],
        };

        Assert.Null(MapEventChecks.FavoursPeople(PeopleOnly(new DialogueAction([new DialogueLine("", "Hi.")]), new EndGameAction(Won: false))));
        Assert.Equal("e", MapEventChecks.FavoursPeople(PeopleOnly(new EndGameAction(Won: true))));
    }
}
