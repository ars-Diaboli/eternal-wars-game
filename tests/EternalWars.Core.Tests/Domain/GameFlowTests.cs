using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Victory, defeat and the end of the game (ADR-057).</summary>
public class GameFlowTests
{
    private static readonly PlayerId Green = new(2);
    private static readonly HeroId D = new(4);

    private static Army Strong() => Army.Of(new CreatureStack(BuiltInCreatures.Champion, 10));

    private static Army Weak() => Army.Of(new CreatureStack(BuiltInCreatures.Footman, 1));

    /// <summary>A (Blue, top left), B (Red, top right) and D (Green, bottom left) on a 3×3 map.</summary>
    private static GameState ThreePlayers(Action<GameState>? adjust = null)
    {
        var state = Create("A.B", "...", "...");
        state.AddPlayer(new Player(Green, "Green", true));
        state.AddHero(new Hero(D, "Delta", Green, new MapPosition(0, 2), DefaultArmy()));
        adjust?.Invoke(state);
        return state;
    }

    private static List<IEvent> Record(GameSession session)
    {
        var events = new List<IEvent>();
        session.Events.Subscribe<PlayerDefeated>(events.Add);
        session.Events.Subscribe<GameEnded>(events.Add);
        return events;
    }

    /// <summary>The 10×8 map of the town scenarios: Blue (A) below the entrance of a town at the top left, Red (B) in the corner.</summary>
    private static GameState TownMap(PlayerId? townOwner, out Town town)
    {
        var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "..........", "...A.....B");
        town = AddTown(state, TownIds.Haven, 0, 0, townOwner);
        return state;
    }

    [Fact]
    public void LosingTheLastHero_DefeatsThePlayerAndSkipsItsTurns()
    {
        var state = ThreePlayers();
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);
        var events = Record(session);

        session.MoveTo(A, new MapPosition(0, 2));
        session.AutoResolve();

        Assert.True(state.GetPlayer(Green).IsDefeated);
        Assert.False(state.IsOver);
        Assert.Contains(events, e => e is PlayerDefeated { Player: var p } && p == Green);
        session.EndTurn(); // Blue
        session.EndTurn(); // Red; Green is skipped
        Assert.Equal(Blue, state.CurrentPlayer.Id);
        Assert.Equal(2, state.Date.AbsoluteDay);
    }

    [Fact]
    public void AttackerLosingItsOnlyHero_IsDefeatedAndThePlayerPassesItsTurn()
    {
        var state = ThreePlayers(s =>
        {
            s.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 1));
            s.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        });
        var session = Start(state);
        var events = Record(session);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.True(state.GetPlayer(Blue).IsDefeated);
        Assert.Equal(Red, state.CurrentPlayer.Id);
        Assert.False(state.IsOver);
        Assert.Contains(events, e => e is PlayerDefeated { Player: var p } && p == Blue);
    }

    [Fact]
    public void TheLastTeamStanding_WinsAndTheGameStopsAcceptingCommands()
    {
        var state = Create("A.B");
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);
        var events = Record(session);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.True(state.IsOver);
        Assert.Equal([Blue], state.Outcome!.Winners);
        Assert.Contains(events, e => e is GameEnded { Winners: [var winner] } && winner == Blue);
        Assert.Contains(events, e => e is PlayerDefeated { Player: var p } && p == Red);
        var refused = session.Execute(new EndTurnCommand(Blue));
        Assert.False(refused.IsAccepted);
        Assert.Contains("over", refused.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Allies_WinTogether()
    {
        var state = ThreePlayers(s =>
        {
            s.GetPlayer(Blue).Team = new TeamId(7);
            s.GetPlayer(Green).Team = new TeamId(7);
            s.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        });
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.Equal([Blue, Green], state.Outcome!.Winners);
    }

    [Fact]
    public void ALoneSide_NeverEndsTheGame_AndPlayersWithoutAnythingAreNotEliminated()
    {
        var state = Create("A..");
        var session = Start(state);

        for (var i = 0; i < 6; i++)
        {
            session.EndTurn();
        }

        Assert.False(state.IsOver);
        Assert.False(state.GetPlayer(Red).IsDefeated);
    }

    [Fact]
    public void AnEndlessScenario_DoesNotEndWhenAPlayerLosesEverything()
    {
        var state = Create("A.B");
        state.SetRules(ScenarioRules.Endless);
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.False(state.IsOver);
        Assert.False(state.GetPlayer(Red).IsDefeated);
    }

    [Fact]
    public void CaptureTown_WinsForTheCapturersTeam_EvenWithoutDefeatingEveryone()
    {
        var state = TownMap(null, out var town);
        state.SetRules(new ScenarioRules([ScenarioCondition.CaptureTown(town.Id)], []));
        var session = Start(state);

        session.MoveTo(A, town.VisitPosition);

        Assert.Equal([Blue], state.Outcome!.Winners);
        Assert.Contains("captured", state.Outcome.Reason);
        Assert.False(state.GetPlayer(Red).IsDefeated);
    }

    [Fact]
    public void CaptureTown_ForOnePlayer_IgnoresTheOtherPlayers()
    {
        var state = TownMap(null, out var town);
        state.SetRules(new ScenarioRules([ScenarioCondition.CaptureTown(town.Id, Red)], []));
        var session = Start(state);

        session.MoveTo(A, town.VisitPosition);

        Assert.False(state.IsOver);
    }

    [Fact]
    public void DefeatHero_WinsForTheTeamThatRemovedIt()
    {
        var state = Create("A.B");
        state.SetRules(new ScenarioRules([ScenarioCondition.DefeatHero(B)], [], StandardVictory: false, Elimination: false));
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.Equal([Blue], state.Outcome!.Winners);
        Assert.Contains("Bravo", state.Outcome.Reason);
    }

    [Fact]
    public void GatherResources_WinsOnceThePlayerHoldsTheAmount()
    {
        var state = Create("A.B");
        state.SetRules(new ScenarioRules([ScenarioCondition.GatherResources(ResourceType.Gold, 1500, Blue)], []));
        var session = Start(state);
        session.EndTurn();
        Assert.False(state.IsOver);

        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 2000);
        session.EndTurn();

        Assert.Equal([Blue], state.Outcome!.Winners);
    }

    [Fact]
    public void SurviveUntilDay_WinsForThePlayerStillStanding()
    {
        var state = Create("A.B");
        state.SetRules(new ScenarioRules([ScenarioCondition.SurviveUntilDay(3, Red)], []));
        var session = Start(state);

        for (var i = 0; i < 3; i++)
        {
            session.EndTurn();
            Assert.False(state.IsOver);
        }

        session.EndTurn(); // day 3 begins

        Assert.Equal([Red], state.Outcome!.Winners);
        Assert.Equal(3, state.Outcome.Date.AbsoluteDay);
    }

    [Fact]
    public void LoseTown_DefeatsItsPlayer_RemovesItsHeroesAndReleasesItsTowns()
    {
        var state = TownMap(Red, out var town);
        state.SetRules(new ScenarioRules([], [ScenarioCondition.LoseTown(town.Id, Red)]));
        var session = Start(state);
        var events = Record(session);

        session.MoveTo(A, town.VisitPosition);

        Assert.True(state.GetPlayer(Red).IsDefeated);
        Assert.DoesNotContain(state.Heroes, h => h.Id == B);
        Assert.Equal(Blue, town.Owner);
        Assert.Contains(events, e => e is PlayerDefeated { Player: var p, Reason: var reason } && p == Red && reason.StartsWith("Lost", StringComparison.Ordinal));
        Assert.Equal([Blue], state.Outcome!.Winners);
    }

    [Fact]
    public void LosingTheLastTown_StartsAGraceperiod_AndDefeatsThePlayerAfterSevenDays()
    {
        var state = TownMap(Red, out var town);
        var session = Start(state);
        var events = new List<IEvent>();
        session.Events.Subscribe<GracePeriodStarted>(events.Add);

        session.MoveTo(A, town.VisitPosition);

        Assert.Equal(1, state.GetPlayer(Red).TownlessSince!.Value.AbsoluteDay);
        Assert.Equal([new GracePeriodStarted(Red, new GameDate(8))], events);
        for (var i = 0; i < 12; i++) // through day 7
        {
            session.EndTurn();
        }

        Assert.Equal(7, state.Date.AbsoluteDay);
        Assert.False(state.GetPlayer(Red).IsDefeated);

        session.EndTurn();
        session.EndTurn(); // day 8 begins

        Assert.True(state.GetPlayer(Red).IsDefeated);
        Assert.Equal([Blue], state.Outcome!.Winners);
    }

    [Fact]
    public void OwningATownAgain_EndsTheGracePeriod()
    {
        var state = TownMap(Red, out var town);
        var session = Start(state);
        var ended = new List<GracePeriodEnded>();
        session.Events.Subscribe<GracePeriodEnded>(ended.Add);
        session.MoveTo(A, town.VisitPosition);
        Assert.NotNull(state.GetPlayer(Red).TownlessSince);

        town.Owner = Red;
        session.EndTurn();

        Assert.Null(state.GetPlayer(Red).TownlessSince);
        Assert.Equal([new GracePeriodEnded(Red)], ended);
    }

    [Fact]
    public void GraceDaysAreConfigurable_AndZeroTurnsTheCountdownOff()
    {
        var state = TownMap(Red, out var town);
        state.SetRules(ScenarioRules.Default with { GraceDays = 2 });
        var session = Start(state);
        session.MoveTo(A, town.VisitPosition);
        session.EndTurn();
        session.EndTurn();
        Assert.False(state.GetPlayer(Red).IsDefeated);
        session.EndTurn();
        session.EndTurn(); // day 3 = 1 + 2
        Assert.True(state.GetPlayer(Red).IsDefeated);

        var off = TownMap(Red, out var offTown);
        off.SetRules(ScenarioRules.Default with { GraceDays = 0 });
        var offSession = Start(off);
        offSession.MoveTo(A, offTown.VisitPosition);
        for (var i = 0; i < 30; i++)
        {
            offSession.EndTurn();
        }

        Assert.False(off.GetPlayer(Red).IsDefeated);
    }

    [Fact]
    public void APlayerStartingWithoutATown_IsWarnedAbout_UnlessTheGracePeriodIsOff()
    {
        // A campaign-like map (ADR-075): heroes only, no towns.
        var state = Create("A........B");
        Assert.Equal(2, MapValidator.Validate(state).Count(i => i.Message.Contains("starts without a town", StringComparison.Ordinal)));

        state.SetRules(ScenarioRules.Default with { GraceDays = 0 });
        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Message.Contains("starts without a town", StringComparison.Ordinal));
    }

    [Fact]
    public void APlayerWhoNeverHadATown_HasNoCountdown()
    {
        var state = Create("A.B");
        var session = Start(state);

        for (var i = 0; i < 20; i++)
        {
            session.EndTurn();
        }

        Assert.False(state.IsOver);
        Assert.Null(state.GetPlayer(Red).TownlessSince);
    }

    [Fact]
    public void LoseHero_DefeatsItsPlayer()
    {
        var state = ThreePlayers();
        state.SetRules(new ScenarioRules([], [ScenarioCondition.LoseHero(B, Red)]));
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);

        session.MoveTo(A, new MapPosition(2, 0));
        session.AutoResolve();

        Assert.True(state.GetPlayer(Red).IsDefeated);
        Assert.False(state.IsOver); // Blue and Green are left
    }

    [Fact]
    public void TimeLimit_DefeatsThePlayerWhoDidNotWin()
    {
        var state = Create("A.B");
        state.SetRules(new ScenarioRules([], [ScenarioCondition.TimeLimit(2, Blue)]));
        var session = Start(state);

        for (var i = 0; i < 4; i++)
        {
            session.EndTurn();
        }

        Assert.True(state.GetPlayer(Blue).IsDefeated);
        Assert.Equal([Red], state.Outcome!.Winners);
    }

    [Fact]
    public void ADefeatedPlayersMines_BecomeNeutral()
    {
        var state = TownMap(Red, out var town);
        var mine = Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Gold), 7, 0);
        mine.Owner = Red;
        state.SetRules(new ScenarioRules([], [ScenarioCondition.LoseTown(town.Id, Red)]));
        var session = Start(state);

        session.MoveTo(A, town.VisitPosition);

        Assert.Null(mine.Owner);
    }

    [Fact]
    public void SameMapAndCommands_ReplayToTheSameOutcome()
    {
        var state = TownMap(null, out var town);
        state.SetRules(new ScenarioRules([ScenarioCondition.CaptureTown(town.Id)], []));
        var initial = MapConverter.ToDocument(state);
        var session = Start(state, revealAll: false);
        session.MoveTo(A, town.VisitPosition);
        var record = EternalWars.Core.Data.Records.GameReplay.Record(initial, session);

        var replayed = EternalWars.Core.Data.Records.GameReplay.Replay(record);

        Assert.Equal([Blue], replayed.State.Outcome!.Winners);
        Assert.Equal(GameStateDigest.Compute(state), GameStateDigest.Compute(replayed.State));
    }

    // Map format and editor ------------------------------------------------------------------------

    [Fact]
    public void Rules_RoundTripThroughTheMapFile()
    {
        var state = TownMap(Red, out var town);
        var rules = new ScenarioRules(
            [ScenarioCondition.CaptureTown(town.Id, Blue), ScenarioCondition.GatherResources(ResourceType.Crystal, 20), ScenarioCondition.SurviveUntilDay(30, Red), ScenarioCondition.DefeatHero(B)],
            [ScenarioCondition.LoseTown(town.Id, Red), ScenarioCondition.LoseHero(A, Blue), ScenarioCondition.TimeLimit(40)],
            StandardVictory: false,
            Elimination: false,
            GraceDays: 3);
        state.SetRules(rules);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));
        var loaded = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(rules.Victory, loaded.Rules.Victory);
        Assert.Equal(rules.Defeat, loaded.Rules.Defeat);
        Assert.False(loaded.Rules.StandardVictory);
        Assert.False(loaded.Rules.Elimination);
        Assert.Equal(3, loaded.Rules.GraceDays);
    }

    [Fact]
    public void DefaultRules_AreOmittedFromTheMapFile_AndOlderMapsLoadWithThem()
    {
        var state = Create("A.B");

        var document = MapConverter.ToDocument(state);

        Assert.Null(document.Rules);
        Assert.True(MapConverter.ToGameState(document with { FormatVersion = 9 }).Rules.IsDefault);
    }

    [Fact]
    public void TheEditor_SetsRulesUndoably()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10, 1));
        var changes = 0;
        editor.Events.Subscribe<ScenarioRulesChanged>(_ => changes++);
        var rules = new ScenarioRules([ScenarioCondition.SurviveUntilDay(10)], []);

        editor.SetScenarioRules(rules);
        Assert.Same(rules, editor.Scenario.Rules);

        Assert.True(editor.Undo());
        Assert.True(editor.Scenario.Rules.IsDefault);
        Assert.True(editor.Redo());
        Assert.Same(rules, editor.Scenario.Rules);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void TheValidator_FlagsConditionsThatPointAtNothing_AndTrivialOnes()
    {
        var state = TownMap(Blue, out var town);
        state.SetRules(new ScenarioRules(
            [
                ScenarioCondition.CaptureTown(new MapObjectId(99)),
                ScenarioCondition.DefeatHero(new HeroId(99)),
                ScenarioCondition.GatherResources(ResourceType.Gold, 0),
                ScenarioCondition.SurviveUntilDay(1),
                ScenarioCondition.CaptureTown(town.Id, Blue),
            ],
            [new ScenarioCondition(ConditionKind.LoseHero, null, Hero: A), ScenarioCondition.LoseTown(town.Id, Red)]));

        var messages = MapValidator.Validate(state).Select(i => (i.Severity, i.Message)).ToList();

        Assert.Equal(5, messages.Count(m => m.Severity == IssueSeverity.Error));
        Assert.Contains(messages, m => m.Message.Contains("owned from the start", StringComparison.Ordinal));
        Assert.Contains(messages, m => m.Message.Contains("lose at once", StringComparison.Ordinal));
    }

    [Fact]
    public void TheValidator_WarnsWhenNobodyCanWin()
    {
        var state = Create("A.B");
        state.SetRules(ScenarioRules.Endless);

        Assert.Contains(MapValidator.Validate(state), i => i.Message.Contains("Nobody can win", StringComparison.Ordinal));
    }

    [Fact]
    public void Describe_NamesTheTownHeroAndPlayer()
    {
        var state = TownMap(Red, out var town);

        Assert.Equal($"Blue: Capture {town.Name}", ScenarioRules.Describe(state, ScenarioCondition.CaptureTown(town.Id, Blue)));
        Assert.Equal("Defeat Bravo", ScenarioRules.Describe(state, ScenarioCondition.DefeatHero(B)));
        Assert.Equal("Gather 500 lumber", ScenarioRules.Describe(state, ScenarioCondition.GatherResources(ResourceType.Lumber, 500)));
        Assert.Equal("Red: Fail to win by day 12", ScenarioRules.Describe(state, ScenarioCondition.TimeLimit(12, Red)));
    }
}
