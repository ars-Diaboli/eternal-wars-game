using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The tournament mode (ADR-366): the lengths offered, the map's switch in the map file, what the validator calls faulty, which games refuse to
/// start, and the game's flow: ordinary building days, then the deciding battle before anyone's turn on the day after the last, which alone
/// ends the game.
/// </summary>
public class TournamentTests
{
    private static readonly ScenarioRules Rules = ScenarioRules.Default with { Tournament = true, MaxHeroes = 1 };

    private static GameState Tournament(GameOptions? options = null, ScenarioRules? rules = null)
    {
        var state = Create(options ?? GameOptions.Default, "A.#..", "..#.B");
        state.SetRules(rules ?? Rules);
        return state;
    }

    private static List<MapIssue> Errors(GameState state) => MapValidator.Validate(state).Where(i => i.Severity == IssueSeverity.Error).ToList();

    private static GameOptions Lasting(int days) => GameOptions.Default with { TournamentDays = days };

    /// <summary>Blue and Red end their turns: the next day begins.</summary>
    private static void EndDay(GameSession session)
    {
        session.EndTurn();
        session.EndTurn();
    }

    [Fact]
    public void TheLengths_AreOneToFourteenDays_ThenThreeToTwelveWholeWeeks()
    {
        Assert.Equal([.. Enumerable.Range(1, 14), 21, 28, 35, 42, 49, 56, 63, 70, 77, 84], TournamentRules.Lengths);
        Assert.All(TournamentRules.Lengths, d => Assert.True(TournamentRules.IsLength(d)));
        Assert.All([0, -7, 15, 20, 22, 30, 91, 98], d => Assert.False(TournamentRules.IsLength(d)));

        Assert.Equal(14, TournamentRules.Days(GameOptions.Default));
        Assert.Equal(28, TournamentRules.Days(GameOptions.Default with { TournamentDays = 28 }));
    }

    [Fact]
    public void TheSwitch_IsKeptInTheMapFile_AndOmittedWhenOff()
    {
        Assert.False(ScenarioRules.Default.Tournament);
        Assert.False((ScenarioRules.Default with { Tournament = true }).IsDefault);
        Assert.True((ScenarioRules.Endless with { Tournament = true }).CanBeWon);

        var tournament = MapConverter.ToDocument(Tournament());
        Assert.True(tournament.Rules!.Tournament);
        Assert.True(MapConverter.ToGameState(MapSerializer.Deserialize(MapSerializer.Serialize(tournament))).Rules.Tournament);

        var ordinary = MapConverter.ToDocument(Create("A.#..", "..#.B"));
        Assert.Null(ordinary.Rules?.Tournament);
        Assert.DoesNotContain("tournament", MapSerializer.Serialize(ordinary), StringComparison.Ordinal);
    }

    [Fact]
    public void AFitTournament_HasNoErrors_AndStarts()
    {
        var state = Tournament(GameOptions.Default with { TournamentDays = 21 });
        Assert.Empty(Errors(state));
        Assert.Null(TournamentRules.WhyNot(state));

        new GameSession(state).Start();
        Assert.True(state.IsStarted);
    }

    [Fact]
    public void AnArenaThatIsATournament_IsAnError_AndDoesNotStart()
    {
        var state = Tournament(rules: Rules with { Arena = true });
        Assert.Contains(Errors(state), i => i.Message.Contains("an arena and a tournament", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => new GameSession(state).Start());
    }

    [Fact]
    public void TheSetup_CannotMakeATournamentsPlayersAllies()
    {
        var state = Tournament();
        var refused = Assert.Throws<InvalidOperationException>(() => state.AssignTeams(new Dictionary<PlayerId, TeamId> { [Blue] = new(0), [Red] = new(0) }));
        Assert.Equal("The two players of a tournament cannot be allies.", refused.Message);
        Assert.NotEqual(state.GetPlayer(Blue).Team, state.GetPlayer(Red).Team);

        state.AssignTeams(new Dictionary<PlayerId, TeamId> { [Blue] = new(3), [Red] = new(5) });
        Assert.Null(TournamentRules.WhyNot(state));
    }

    [Fact]
    public void APlayTest_OfAnUnfitTournament_SaysWhyNot()
    {
        var eight = Tournament(rules: Rules with { MaxHeroes = ScenarioRules.HeroLimit });
        Assert.Equal(TournamentRules.WhyNot(eight), new PlayTestArguments(1).WhyNot(eight));
        Assert.Null(new PlayTestArguments(1).WhyNot(Tournament()));
    }

    [Fact]
    public void ATournament_NeedsTwoPlayersWhoAreNotAllies()
    {
        var allies = Tournament(rules: ScenarioRules.Default);
        allies.AssignTeams(new Dictionary<PlayerId, TeamId> { [Blue] = new(0), [Red] = new(0) });
        allies.SetRules(Rules); // a map whose default teams are allied
        Assert.Contains(Errors(allies), i => i.Message.Contains("cannot be allies", StringComparison.Ordinal));
        Assert.Throws<InvalidOperationException>(() => new GameSession(allies).Start());

        Player[] three = [new(Blue, "Blue", true), new(Red, "Red", true), new(new PlayerId(2), "Green", true)];
        var crowd = new GameState(new AdventureMap(5, 2), three, []);
        crowd.SetRules(Rules);
        Assert.Contains(Errors(crowd), i => i.Message.Contains("exactly two players; the map has 3", StringComparison.Ordinal));
        Assert.NotNull(TournamentRules.WhyNot(crowd));
    }

    [Fact]
    public void ATournament_AllowsEachPlayerOneHero_TheMapsLimitAndEachPlayers()
    {
        var eight = Tournament(rules: Rules with { MaxHeroes = ScenarioRules.HeroLimit });
        Assert.Equal(2, Errors(eight).Count(i => i.Message.Contains("one hero a player", StringComparison.Ordinal)));
        Assert.Throws<InvalidOperationException>(() => new GameSession(eight).Start());

        var red = Tournament();
        red.SetHeroLimit(Red, 2);
        Assert.Equal("A tournament allows one hero a player; Red may have 2.", Assert.Single(Errors(red)).Message);
        Assert.NotNull(TournamentRules.WhyNot(red));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(15)]
    [InlineData(30)]
    [InlineData(91)]
    public void ALengthThatIsNotOffered_DoesNotStart(int days)
    {
        var state = Tournament(GameOptions.Default with { TournamentDays = days });
        Assert.Contains($"{days} days is neither", TournamentRules.WhyNot(state), StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => new GameSession(state).Start());
    }

    [Fact]
    public void ALength_OnAMapThatIsNoTournament_DoesNotStart_AndAnOrdinaryMapHasNoTournamentProblems()
    {
        var ordinary = Create(GameOptions.Default with { TournamentDays = 14 }, "A.#..", "..#.B");
        Assert.Equal("Only a tournament has a length.", TournamentRules.WhyNot(ordinary));
        Assert.Throws<InvalidOperationException>(() => new GameSession(ordinary).Start());

        Assert.Empty(TournamentRules.MapProblems(Create("A.#..", "..#.B")));
    }

    [Fact]
    public void TheLength_IsKeptInSavesAndReplays()
    {
        var state = Tournament(GameOptions.Default with { TournamentDays = 42 });
        var map = MapConverter.ToDocument(state);
        var session = new GameSession(state);
        session.Start();

        var loaded = GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(GameReplay.Record(map, session)));

        Assert.Equal(42, loaded.Options.TournamentDays);
        Assert.Equal(42, GameReplay.Replay(loaded).State.Options.TournamentDays);
    }

    [Fact]
    public void TheBattleDay_IsTheDayAfterTheLast_AndOnlyAnArenaOrATournamentHasOne()
    {
        Assert.Equal(15, TournamentRules.BattleDay(GameOptions.Default));
        Assert.Equal(2, TournamentRules.BattleDay(Lasting(1)));
        Assert.Equal(85, DecidingBattles.Day(Tournament(Lasting(84))));

        var arena = Create("A.#..", "..#.B");
        arena.SetRules(ScenarioRules.Default with { Arena = true });
        Assert.Equal(ArenaRules.BattleDay, DecidingBattles.Day(arena));
        Assert.True(DecidingBattles.Decides(Rules));
        Assert.Null(DecidingBattles.Day(Create("A.#..", "..#.B")));
        Assert.False(DecidingBattles.Decides(ScenarioRules.Default));
    }

    [Fact]
    public void TheBuildingDays_AreOrdinary_AndTheBattleStartsOnTheDayAfterTheLast_BeforeAnyTurn()
    {
        var state = Tournament(Lasting(2));
        var session = Start(state);
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);

        EndDay(session);
        Assert.Equal(2, state.Date.AbsoluteDay);
        Assert.Null(state.DecidingBattle);
        Assert.Contains(events, e => e is TurnStarted { Date.AbsoluteDay: 2 });

        EndDay(session);
        Assert.Equal(3, state.Date.AbsoluteDay);
        Assert.Equal((Blue, A, Red, B), (state.DecidingBattle!.Attacker, state.DecidingBattle.AttackerHero, state.DecidingBattle.Defender, state.DecidingBattle.DefenderHero));
        Assert.Equal((A, B), (state.Battle!.Attacker.Hero, state.Battle.Defender.Hero));
        Assert.Contains(events, e => e is ChampionsChosen);
        Assert.DoesNotContain(events, e => e is TurnStarted { Date.AbsoluteDay: 3 });
    }

    [Fact]
    public void TheBattlesWinner_WinsTheTournament_WithoutExperience_AndItIsNeverRanked()
    {
        var state = Tournament(Lasting(1));
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);
        EndDay(session);

        session.AutoResolve();

        Assert.Equal((DecidingBattleResult.Won, Blue), (state.DecidingBattle!.Result, state.DecidingBattle.Winner));
        Assert.Equal([Blue], state.Outcome!.Winners);
        Assert.Equal("Blue won the tournament battle.", state.Outcome.Reason);
        Assert.Equal(0, state.GetHero(A).Experience);
        Assert.False(ScoreRules.IsRanked(state));
    }

    [Fact]
    public void TheMapsConditions_DoNotCount_AndNobodyIsOutBeforeTheBattle()
    {
        var state = Tournament(Lasting(3), Rules with { Victory = [ScenarioCondition.SurviveUntilDay(1, Red)], Defeat = [ScenarioCondition.TimeLimit(2, Blue)] });
        var session = Start(state);
        state.RemoveHero(state.GetHero(B)); // Red holds neither a hero nor a town

        EndDay(session);
        EndDay(session);
        Assert.Equal(3, state.Date.AbsoluteDay);
        Assert.False(state.IsOver);
        Assert.DoesNotContain(state.Players, p => p.IsDefeated);

        EndDay(session);
        Assert.True(state.IsOver);
        Assert.Empty(state.Outcome!.Winners);
        Assert.Equal("Red had no hero for the tournament battle.", state.Outcome.Reason);
    }

    [Fact]
    public void TheValidator_WarnsOfNoTeamsOrGracePeriod_AndTheMapIsATournament()
    {
        // Red without a hero or a town: an ordinary map warns that the game cannot end by defeating enemies, and of the grace period.
        var ordinary = Create("A.#..", "..#.B");
        ordinary.RemoveHero(ordinary.GetHero(B));
        var tournament = Tournament(rules: Rules with { StandardVictory = false });
        tournament.RemoveHero(tournament.GetHero(B));
        static bool Teams(MapIssue i) => i.Message.StartsWith("Fewer than two teams", StringComparison.Ordinal);
        static bool Grace(MapIssue i) => i.Message.Contains("starts without a town", StringComparison.Ordinal);
        static bool NobodyWins(MapIssue i) => i.Message.StartsWith("Nobody can win", StringComparison.Ordinal);

        Assert.Contains(MapValidator.Validate(ordinary), Teams);
        Assert.Contains(MapValidator.Validate(ordinary), Grace);
        Assert.DoesNotContain(MapValidator.Validate(tournament), Teams);
        Assert.DoesNotContain(MapValidator.Validate(tournament), Grace);
        Assert.DoesNotContain(MapValidator.Validate(tournament), NobodyWins);

        Assert.Equal(MapKind.Tournament, MapKinds.Of(tournament));
        Assert.Equal(MapKind.Campaign, MapKinds.Of(ordinary)); // townless people make an ordinary map campaign-like, never a tournament
    }

    [Fact]
    public void AMapEventEndingTheGame_CountsForNothing_AndLaterEventsStillRun()
    {
        var state = Tournament(Lasting(3));
        state.SetMapEvents(new MapEventSet
        {
            Events =
            [
                new MapEventDefinition("end", MapEventKind.Player, [new IfAction(new PlayerIsCondition(0), [new EndGameAction(Won: true)])]),
                new MapEventDefinition("gold", MapEventKind.Player, [new IfAction(new PlayerIsCondition(0), [new ResourceAction(ResourceType.Gold, new NumberExpr(100))])]),
            ],
        });
        var session = Start(state);
        var gold = state.GetPlayer(Blue).Resources.Gold;

        EndDay(session);
        EndDay(session);

        Assert.False(state.IsOver);
        Assert.Null(state.ScriptedEnd);
        Assert.True(state.GetPlayer(Blue).Resources.Gold > gold + session.Economy.DailyIncome(Blue).Gold * 2);
    }

    [Fact]
    public void OnSimultaneousDays_TheBattleStillStartsOnTheDayAfterTheLast_BeforeAnyTurn()
    {
        var state = SimultaneousDayTests.Duel(new GameOptions(FogMode.ShroudWithRefog) { SimultaneousFirstWeek = true, TournamentDays = 2 });
        state.SetRules(Rules);
        var session = SimultaneousDayTests.Started(state);
        Assert.Equal([Blue, Red], state.OpenTurns);

        void EndBoth()
        {
            session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
            session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        }

        EndBoth();
        Assert.Equal([Blue, Red], state.OpenTurns);
        EndBoth();

        Assert.Equal(3, state.Date.AbsoluteDay);
        Assert.NotNull(state.Battle);
        Assert.Empty(state.OpenTurns);
        Assert.Equal(DecidingBattleResult.Fighting, state.DecidingBattle!.Result);
    }

    [Fact]
    public void APlayTest_StartsBeforeTheBattleDay()
    {
        var state = Tournament(Lasting(2));
        Assert.Null(new PlayTestArguments(1, Day: 2).WhyNot(state));
        Assert.Equal("A tournament's battle is on day 3: the play-test starts before it.", new PlayTestArguments(1, Day: 3).WhyNot(state));
    }
}
