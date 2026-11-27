using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The tournament mode (ADR-366): the lengths offered, the map's switch in the map file, what the validator calls faulty, which games refuse to
/// start, and the game's flow: ordinary building days, then the deciding battle before anyone's turn on the day after the last, which alone
/// ends the game. The battle: the coin toss, neutral ground, no leaving it, and the endings without one. Isolation: sight and spells kept inside
/// the start zone, and the taverns, the Grail and the artifact rolls each player's own.
/// </summary>
public class TournamentTests
{
    private static readonly ScenarioRules Rules = ScenarioRules.Default with { Tournament = true, MaxHeroes = 1 };

    /// <summary>A fit tournament: Blue's start zone left of the wall, Red's right of it.</summary>
    private static GameState Tournament(GameOptions? options = null, ScenarioRules? rules = null)
    {
        var state = Create(options ?? GameOptions.Default, "A.#..", "..#.B");
        state.SetRules(rules ?? Rules);
        Zone(state, Blue, 0, 1);
        Zone(state, Red, 3, 4);
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
    public void AMapOfTheVersionBeforeTheSwitch_StillLoads_AndIsNoTournament()
    {
        const string old = """
            {
              "formatVersion": 44, "name": "Old", "width": 8, "height": 4, "terrain": [ [ "gggggggg", "gggggggg", "gggggggg", "gggggggg" ] ],
              "players": [ { "name": "Blue", "isHuman": true }, { "name": "Red", "isHuman": true } ],
              "rules": { "elimination": false, "maxHeroes": 1 },
              "objects": []
            }
            """;

        var state = TestFormat.Load(old);

        Assert.True(MapDocument.CurrentFormatVersion >= 45);
        Assert.Equal(MapDocument.CurrentFormatVersion, MapSerializer.Deserialize(old).FormatVersion); // upgraded as it was read
        Assert.False(state.Rules.Tournament);
        Assert.Equal(1, state.Rules.MaxHeroes);
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

    /// <summary>A wider fit tournament: Blue's zone the columns 0–7, the wall column 8, Red's zone 9–16; Alpha and Bravo by the wall.</summary>
    private static GameState Valleys()
    {
        var rows = Enumerable.Repeat("........#........", 9).Append(".......A#B.......").ToArray();
        var state = Create(rows);
        state.SetRules(Rules);
        Zone(state, Blue, 0, 7);
        Zone(state, Red, 9, 16);
        return state;
    }

    private static List<string> TournamentWarnings(GameState state) =>
        [.. MapValidator.Validate(state).Where(i => i.Severity == IssueSeverity.Warning && i.Message.Contains("much richer", StringComparison.Ordinal)).Select(i => i.Message)];

    [Fact]
    public void ATournament_NeedsAStartZoneForEachPlayer()
    {
        Assert.Empty(Errors(Valleys()));

        var state = Valleys();
        Zone(state, null, 9, 16);

        Assert.Equal("Red has no start zone: each player of a tournament builds up in a zone of its own.", Assert.Single(Errors(state)).Message);
        Assert.DoesNotContain(MapValidator.Validate(state), i => i.Message.Contains("a simultaneous first week", StringComparison.Ordinal));
    }

    [Fact]
    public void AWayBetweenTheStartZones_IsAnError_ThroughAGapOrAPortal()
    {
        var gap = Valleys();
        gap.Map.SetTerrain(new MapPosition(8, 3), TerrainType.Grass); // a tile of neither zone
        var error = Assert.Single(Errors(gap));
        Assert.Equal("Blue's and Red's start zones are joined: a hero can walk, or go through a gate, portal or teleporter, from one into the other.", error.Message);
        Assert.Equal(Red, gap.Map.GetStartZone(Assert.IsType<MapPosition>(error.Position)));

        var portal = Valleys();
        Link(Add<TwoWayGate>(portal, BuiltInMapObjects.TwoWayMonolith, 2, 2), Add<TwoWayGate>(portal, BuiltInMapObjects.TwoWayMonolith, 13, 2));
        Assert.Contains(Errors(portal), i => i.Message.Contains("start zones are joined", StringComparison.Ordinal));

        foreach (var (from, to) in new[] { (13, 2), (2, 13) }) // a one-way portal joins them whichever way it leads
        {
            var oneWay = Valleys();
            var exit = Add<OneWayGateExit>(oneWay, BuiltInMapObjects.OneWayMonolithExit, to, 2);
            Add<OneWayGateEntrance>(oneWay, BuiltInMapObjects.OneWayMonolithEntrance, from, 2).Exit = exit.Id;
            Assert.Contains(Errors(oneWay), i => i.Message.Contains("start zones are joined", StringComparison.Ordinal));
        }

        var town = Valleys(); // Blue's town in Red's zone: a hero hired there stands in it
        AddTown(town, TownIds.Haven, 10, 0, Blue);
        Assert.Contains(Errors(town), i => i.Message.Contains("start zones are joined", StringComparison.Ordinal));

        var guarded = Valleys(); // a guard or a mine in the way stops nobody; only a blocked cell does
        Add<Mine>(guarded, BuiltInMapObjects.MineId(ResourceType.Gold), 7, 2); // its entrance (8, 3) on the gap
        guarded.Map.SetTerrain(new MapPosition(8, 3), TerrainType.Grass);
        Assert.Contains(Errors(guarded), i => i.Message.Contains("start zones are joined", StringComparison.Ordinal));
        guarded.Map.SetTerrain(new MapPosition(8, 3), TerrainType.Rock);
        guarded.Map.SetTerrain(new MapPosition(8, 2), TerrainType.Grass); // under the mine's blocked middle cell
        Assert.DoesNotContain(Errors(guarded), i => i.Message.Contains("start zones are joined", StringComparison.Ordinal));
    }

    [Fact]
    public void AMuchRicherStartZone_IsAWarning_ByTownsMinesAndTreasure()
    {
        var state = Valleys();
        AddTown(state, TownIds.Haven, 0, 0, Blue);
        Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 0, 7);
        Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 10, 2);
        Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Gold), 10, 4);
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 12, 6).Amount = 3000;

        Assert.Equal(
        [
            "Blue's start zone is much richer than Red's: 1 town against 0.",
            "Red's start zone is much richer than Blue's: 1 gold mine against 0.",
        ], TournamentWarnings(state));

        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 13, 6).Amount = 1000;
        Assert.Contains(
            "Red's start zone is much richer than Blue's: treasure worth 4000 gold against 0 (the generator lets alike starts be 15 % off their mean, at least 1500 gold).",
            TournamentWarnings(state));

        Assert.Empty(TournamentWarnings(Valleys()));
        var ordinary = Valleys();
        ordinary.SetRules(ScenarioRules.Default);
        AddTown(ordinary, TownIds.Haven, 0, 0, Blue);
        Assert.Empty(TournamentWarnings(ordinary));
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
        var plan = state.DecidingBattle!;
        Assert.True(plan.Attacker == Blue
            ? (plan.AttackerHero, plan.Defender, plan.DefenderHero) == (A, Red, B)
            : (plan.Attacker, plan.AttackerHero, plan.Defender, plan.DefenderHero) == (Red, B, Blue, A)); // the coin toss's sides
        Assert.Equal((plan.AttackerHero, plan.DefenderHero), (state.Battle!.Attacker.Hero, state.Battle.Defender.Hero));
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
        Assert.Equal([Blue], state.Outcome!.Winners); // Red, without a hero, loses without a battle
        Assert.Equal("Blue won the tournament battle. Red had no hero.", state.Outcome.Reason);
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
    public void OnSimultaneousDays_EveryDayBeforeTheBattle_IsPlayedAtOnce_PastTheFirstWeek_AndTheBattlesDayOpensNoTurns()
    {
        var session = SimultaneousDayTests.Started(SimultaneousTournament(days: 9));
        var state = session.State;
        var opened = new List<int>();
        session.Events.Subscribe<SimultaneousDayStarted>(e => opened.Add(e.Date.AbsoluteDay));
        opened.Add(state.Date.AbsoluteDay);

        for (var day = 1; day <= 9; day++)
        {
            Assert.Equal(day, state.Date.AbsoluteDay);
            Assert.Equal([Blue, Red], state.OpenTurns);
            session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
            Assert.Equal([Blue], state.OpenTurns); // Blue goes on
            session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
        }

        Assert.Equal(Enumerable.Range(1, 9), opened);
        Assert.Equal(10, state.Date.AbsoluteDay);
        Assert.NotNull(state.Battle);
        Assert.False(state.IsSimultaneous);
        Assert.Equal(DecidingBattleResult.Fighting, state.DecidingBattle!.Result);
    }

    [Fact]
    public void ATournamentsSimultaneousDays_NeedTheOption_AndAConflictEndsThem_TheRestTurnByTurn()
    {
        var off = SimultaneousDayTests.Started(SimultaneousTournament(days: 9, simultaneous: false));
        Assert.False(off.State.IsSimultaneous);

        var session = SimultaneousDayTests.Started(SimultaneousTournament(days: 9));
        var state = session.State;
        for (var day = 1; day <= 7; day++)
        {
            session.Execute(new EndTurnCommand(Blue)).ThrowIfRejected();
            session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();
        }

        Assert.True(state.IsSimultaneous); // day 8, the second week
        session.Execute(new PlayTurnByTurnCommand(Blue, "a test")).ThrowIfRejected();
        Assert.False(state.IsSimultaneous);
        Assert.Equal(Blue, state.CurrentPlayer.Id);
        EndDay(session);

        Assert.Equal(9, state.Date.AbsoluteDay);
        Assert.False(state.IsSimultaneous);
        Assert.Equal(Blue, state.CurrentPlayer.Id);
        EndDay(session);
        Assert.NotNull(state.Battle);
    }

    /// <summary>A tournament of <paramref name="days"/> on two start zones far apart, played by two people, at once when <paramref name="simultaneous"/>.</summary>
    private static GameState SimultaneousTournament(int days, bool simultaneous = true)
    {
        var state = SimultaneousDayTests.Duel(new GameOptions(FogMode.ShroudWithRefog) { SimultaneousFirstWeek = simultaneous, TournamentDays = days });
        state.SetRules(Rules);
        return state;
    }

    [Fact]
    public void APlayTest_StartsBeforeTheBattleDay()
    {
        var state = Tournament(Lasting(2));
        Assert.Null(new PlayTestArguments(1, Day: 2).WhyNot(state));
        Assert.Equal("A tournament's battle is on day 3: the play-test starts before it.", new PlayTestArguments(1, Day: 3).WhyNot(state));
    }

    /// <summary>
    /// A one-day tournament on <paramref name="seed"/>, <paramref name="setUp"/> before it starts and <paramref name="started"/> after (a player
    /// without a hero or a town at the start takes no part), played to its battle.
    /// </summary>
    private static GameState AtTheBattle(ulong seed = 1, Action<GameState>? setUp = null, Action<GameState>? started = null, params string[] rows)
    {
        var state = Create(Lasting(1) with { Seed = seed }, rows.Length > 0 ? rows : ["A.#..", "..#.B"]);
        state.SetRules(Rules);
        setUp?.Invoke(state);
        var session = Start(state);
        started?.Invoke(state);
        EndDay(session);
        return state;
    }

    /// <summary>Paints the columns <paramref name="fromX"/> to <paramref name="toX"/> of every row as <paramref name="player"/>'s start zone (null: nobody's).</summary>
    private static void Zone(GameState state, PlayerId? player, int fromX, int toX)
    {
        for (var y = 0; y < state.Map.Height; y++)
        {
            for (var x = fromX; x <= toX; x++)
            {
                state.Map.SetStartZone(new MapPosition(x, y), player);
            }
        }
    }

    [Fact]
    public void TheAttacker_IsChosenByACoinTossFromTheSeed_AndAnnounced()
    {
        var attackers = Enumerable.Range(1, 12).Select(seed => AtTheBattle((ulong)seed).DecidingBattle!.Attacker).ToList();
        Assert.Contains(Blue, attackers);
        Assert.Contains(Red, attackers);
        Assert.Equal(AtTheBattle(5).DecidingBattle!.Attacker, AtTheBattle(5).DecidingBattle!.Attacker);

        var plan = AtTheBattle(3).DecidingBattle!;
        Assert.Matches(@"^(Alpha \(Blue\)|Bravo \(Red\)) won the coin toss and attacked (Alpha \(Blue\)|Bravo \(Red\))\.$", plan.Summary);
    }

    [Fact]
    public void TheBattle_IsOnNeutralGround_NeverASiege_AndATownLendsItsDefenderNothing()
    {
        string[] rows = [.. Enumerable.Range(0, 8).Select(y => y == 7 ? "A..........B" : "............")];
        void HomeWithBrotherhood(GameState state)
        {
            var home = AddTown(state, TownIds.Haven, 0, 0, Blue);
            home.SetLevel(BuiltInBuildings.Tavern, MoraleAndLuck.BrotherhoodLevel);
            state.GetHero(A).Position = home.VisitPosition;
        }

        // A seed on which Blue defends, standing in its own town with a Brotherhood.
        var state = Enumerable.Range(1, 20).Select(seed => AtTheBattle((ulong)seed, HomeWithBrotherhood, rows: rows)).First(s => s.DecidingBattle!.Defender == Blue);
        var battle = state.Battle!;

        Assert.Contains(battle.Terrain, new[] { TerrainType.Sand, TerrainType.Beach, TerrainType.Dirt }); // no town is native to it; the map is grass
        Assert.Empty(battle.NativeTowns);
        Assert.Null(battle.Siege);
        Assert.Equal(0, battle.Defender.ArmyMorale); // no Brotherhood: the hero's army alone
    }

    [Fact]
    public void NeitherSide_MayRetreatOrSurrender_NorDoesTheComputerLeave()
    {
        var state = AtTheBattle(setUp: s =>
        {
            s.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 50));
            s.GetHero(B).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 1));
        });
        var battle = state.Battle!;
        var red = battle.Attacker.Owner == Red ? BattleSide.Attacker : BattleSide.Defender;

        Assert.Equal(SurrenderRules.TournamentRefusal, SurrenderRules.CheckRetreat(battle, red));
        Assert.Equal(SurrenderRules.TournamentRefusal, SurrenderRules.CheckRetreat(battle, red.Opponent()));
        Assert.Equal(SurrenderRules.TournamentRefusal, SurrenderRules.Check(state, battle, red));
        Assert.False(SurrenderRules.ShouldRetreat(battle, red)); // one footman against fifty champions, and still no leaving
        Assert.True(battle.Copy().NoLeaving);
    }

    [Fact]
    public void TheRetreatAndSurrenderCommands_AreRefused()
    {
        var state = Tournament(Lasting(1));
        var session = Start(state);
        EndDay(session);

        Assert.Equal(SurrenderRules.TournamentRefusal, session.Execute(new BattleRetreatCommand(Red)).Error);
        Assert.Equal(SurrenderRules.TournamentRefusal, session.Execute(new BattleSurrenderCommand(Blue)).Error);
        Assert.Equal(DecidingBattleResult.Fighting, state.DecidingBattle!.Result);
    }

    [Fact]
    public void ASideWithoutAHeroOrAnArmy_LosesWithoutABattle_WhicheverSideTheCoinTossGaveIt()
    {
        var noHero = Enumerable.Range(1, 12).Select(seed => AtTheBattle((ulong)seed, started: s => s.RemoveHero(s.GetHero(B)))).ToList();
        var noArmy = Enumerable.Range(1, 12).Select(seed => AtTheBattle((ulong)seed, setUp: s => s.GetHero(B).Army.Set(0, null))).ToList();

        foreach (var (games, reason) in new[] { (noHero, "Red had no hero."), (noArmy, "Bravo had no army.") })
        {
            Assert.Contains(games, s => s.DecidingBattle!.Attacker == Red);
            Assert.Contains(games, s => s.DecidingBattle!.Defender == Red);
            Assert.All(games, s =>
            {
                Assert.Null(s.Battle);
                Assert.Equal([Blue], s.Outcome!.Winners);
                Assert.Equal($"Blue won the tournament battle. {reason}", s.Outcome.Reason);
            });
        }
    }

    [Fact]
    public void NeitherSideHavingAHero_OrOneNoHeroAndTheOtherNoArmy_IsADraw()
    {
        var none = AtTheBattle(started: s =>
        {
            s.RemoveHero(s.GetHero(A));
            s.RemoveHero(s.GetHero(B));
        });
        Assert.Equal(DecidingBattleResult.Draw, none.DecidingBattle!.Result);
        Assert.Empty(none.Outcome!.Winners);
        Assert.Matches(@"^The tournament battle ended in a draw\. Neither (Blue nor Red|Red nor Blue) had a hero\.$", none.Outcome.Reason);

        var empty = AtTheBattle(started: s =>
        {
            s.RemoveHero(s.GetHero(B));
            s.GetHero(A).Army.Set(0, null);
        });
        Assert.Equal(DecidingBattleResult.Draw, empty.DecidingBattle!.Result);
        Assert.Empty(empty.Outcome!.Winners);
        Assert.Equal("The tournament battle ended in a draw. Red had no hero, and Alpha (Blue) no army.", empty.Outcome.Reason);
        Assert.False(ScoreRules.IsRanked(empty));
    }

    [Fact]
    public void SightAndScouting_StopAtTheZonesEdge()
    {
        GameState Played(bool tournament)
        {
            var state = Create(new GameOptions(FogMode.ShroudWithRefog), ".....#.......", "....A#......B", ".....#.......");
            if (tournament)
            {
                state.SetRules(Rules);
            }

            Zone(state, Blue, 0, 4);
            Zone(state, Red, 6, 12);
            state.GetHero(A).Skills.Set(BuiltInSkills.Scouting, SkillLevel.Expert);
            state.GetHero(B).Skills.Set(BuiltInSkills.Scouting, SkillLevel.Expert);
            Start(state, revealAll: false);
            return state;
        }

        // On an ordinary map Expert Scouting sees the other hero eight tiles away, and Bravo's sees nine tiles into Blue's side.
        var ordinary = Played(tournament: false);
        Assert.True(ordinary.GetHero(A).ScoutingRadius >= 8);
        Assert.True(ordinary.Fog.IsExplored(Blue, new MapPosition(8, 1)));
        Assert.True(ordinary.CanSee(Blue, ordinary.GetHero(B)));
        Assert.True(ordinary.Fog.IsExplored(Red, new MapPosition(3, 1)));

        var state = Played(tournament: true);
        Assert.True(state.Fog.IsExplored(Blue, new MapPosition(0, 1)));
        Assert.True(state.Fog.IsInSight(Blue, new MapPosition(0, 1)));
        Assert.True(state.Fog.IsInSight(Blue, new MapPosition(5, 1))); // the wall, in no zone, stays in reach
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(8, 1)));
        Assert.False(state.Fog.IsInSight(Blue, new MapPosition(8, 1)));
        Assert.False(state.Fog.IsExplored(Red, new MapPosition(3, 1)));
        Assert.False(state.CanSee(Blue, state.GetHero(B)));
    }

    [Fact]
    public void DimensionDoorAndTownPortal_GoNowhereOutsideTheZone()
    {
        var (state, home, away) = Casting(tournament: true);
        var hero = state.GetHero(A);
        var session = Start(state);
        CommandResult Cast(string spell, MapPosition? target = null) => session.Execute(new CastAdventureSpellCommand(Blue, A, spell, target));

        Assert.Equal(AdventureMagic.OutsideZoneRefusal, Cast(AdventureMagic.DimensionDoor, new MapPosition(19, 7)).Error);
        Assert.Equal(new MapPosition(13, 7), hero.Position);
        Assert.True(Cast(AdventureMagic.DimensionDoor, new MapPosition(15, 7)).IsAccepted); // inside the zone it works
        Assert.Equal(new MapPosition(15, 7), hero.Position);

        // The own town in Red's zone is six tiles away, the one at home twelve: Town Portal goes home.
        Assert.True(Cast(AdventureMagic.TownPortal).IsAccepted);
        Assert.Equal(home.VisitPosition, hero.Position);
        Assert.NotEqual(away.VisitPosition, hero.Position);
    }

    [Fact]
    public void OnAnOrdinaryMap_TheSameSpellsGoIntoTheOtherZone()
    {
        var (state, _, away) = Casting(tournament: false);
        var hero = state.GetHero(A);
        var session = Start(state);
        CommandResult Cast(string spell, MapPosition? target = null) => session.Execute(new CastAdventureSpellCommand(Blue, A, spell, target));

        Assert.True(Cast(AdventureMagic.DimensionDoor, new MapPosition(19, 7)).IsAccepted);
        Assert.Equal(new MapPosition(19, 7), hero.Position);
        Assert.True(Cast(AdventureMagic.TownPortal).IsAccepted); // the nearest own town
        Assert.Equal(away.VisitPosition, hero.Position);
    }

    /// <summary>
    /// Alpha at (13, 7) with Dimension Door and Town Portal; Blue's zone is the columns 0 to 16, Red's 18 to 25, and Blue owns a town in each:
    /// <c>home</c> with its entrance at (3, 6), <c>away</c> at (21, 6).
    /// </summary>
    private static (GameState State, Town Home, Town Away) Casting(bool tournament)
    {
        string[] rows = [.. Enumerable.Range(0, 8).Select(y => y == 7 ? ".............A...#.......B" : ".................#........")];
        var state = Create(rows);
        if (tournament)
        {
            state.SetRules(Rules);
        }

        Zone(state, Blue, 0, 16);
        Zone(state, Red, 18, 25);
        var home = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var away = AddTown(state, TownIds.Haven, 18, 0, Blue);
        var hero = state.GetHero(A);
        hero.HasSpellBook = true;
        hero.Knowledge = 10;
        hero.Learn(AdventureMagic.DimensionDoor);
        hero.Learn(AdventureMagic.TownPortal);
        return (state, home, away);
    }

    [Fact]
    public void EachPlayer_HasATavernPoolOfItsOwn()
    {
        var hero = HeroCatalog.Default.Standard.First().Id;
        var state = Tournament();
        state.GetHero(B).Definition = hero;
        var ordinary = Create("A.#..", "..#.B");
        ordinary.GetHero(B).Definition = hero;

        Assert.Contains(hero, TavernRules.Taken(ordinary, Blue));
        Assert.DoesNotContain(hero, TavernRules.Taken(state, Blue)); // Red's hero is still in Blue's pool
        Assert.Contains(hero, TavernRules.Taken(state, Red));
    }

    [Fact]
    public void AHeroWhoFell_IsReplacedFromTheTavern_StillOneAtATime()
    {
        string[] rows = [.. Enumerable.Range(0, 9).Select(y => y == 8 ? "A........B" : "..........")];
        var state = Create(rows);
        state.SetRules(Rules);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.SetLevel(BuiltInBuildings.Tavern, 1);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000);
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 1, 8);
        (monster.Creature, monster.Count, monster.Disposition) = (BuiltInCreatures.Troll, 50, GuardDisposition.Savage);
        var session = Start(state);
        string Offer() => TavernRules.Offers(state, Blue, town)[0].Id;

        Assert.Contains("at most 1 heroes", session.Execute(new HireHeroCommand(Blue, town.Id, Offer())).Error!, StringComparison.Ordinal);

        // Alpha attacks fifty trolls with ten footmen and falls.
        session.Execute(new StepHeroCommand(Blue, A, Direction.East)).ThrowIfRejected();
        session.Execute(new AutoResolveBattleCommand(Blue)).ThrowIfRejected();
        Assert.Null(state.Battle);
        Assert.Empty(state.HeroesOf(Blue));

        Assert.True(session.Execute(new HireHeroCommand(Blue, town.Id, Offer())).IsAccepted);
        Assert.Single(state.HeroesOf(Blue));
        Assert.Contains("at most 1 heroes", session.Execute(new HireHeroCommand(Blue, town.Id, Offer())).Error!, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePlayers_CannotTrade()
    {
        var session = Start(Tournament());
        Assert.Equal(TournamentRules.NoTradingRefusal, session.Execute(new SendResourcesCommand(Blue, Red, new ResourceSet(Gold: 100))).Error);
    }

    [Fact]
    public void EachZone_WithAnObelisk_HasAGrailOfItsOwn_AndItsOwnPuzzle()
    {
        string[] rows = [.. Enumerable.Range(0, 30).Select(y => y == 2 ? "..A" + new string('.', 54) + "B.." : new string('.', 60))];
        var state = Create(rows);
        state.SetRules(Rules);
        Zone(state, Blue, 0, 29);
        Zone(state, Red, 30, 59);
        Add<Obelisk>(state, BuiltInMapObjects.Obelisk, 5, 5);
        Add<Obelisk>(state, BuiltInMapObjects.Obelisk, 8, 25);
        Start(state);

        Assert.Null(state.Grail.Position);
        var site = Assert.IsType<GrailSite>(GrailRules.SiteOf(state, Blue));
        Assert.Equal(Blue, state.Map.GetStartZone(Assert.IsType<MapPosition>(site.Position)));
        Assert.Null(GrailRules.SiteOf(state, Red)); // Red's zone has no obelisk
        var puzzle = GrailRules.Puzzle(state, Blue)!;
        Assert.Equal(2, puzzle.Obelisks);
        Assert.All(puzzle.OutOfReach!, cell => Assert.Equal(Red, state.Map.GetStartZone(cell))); // the puzzle hides what lies past the wall
        Assert.All(puzzle.OutOfReach!, cell => Assert.False(puzzle.Shows(cell)));
        Assert.Null(GrailRules.Puzzle(state, Red));

        var hero = state.GetHero(A);
        hero.Position = site.Position!.Value;
        new GameSession(state).Execute(new DigCommand(Blue, A)).ThrowIfRejected();
        Assert.True(site.IsFound);
    }

    [Fact]
    public void TheArtifactRolls_ArePerZone_SoBothZonesCanHoldTheSameArtifact()
    {
        var relics = ArtifactCatalog.Default.OfTier(ArtifactTier.Relic).Select(a => a.Id).Order().ToList();
        var left = relics[^1];
        ArtifactId? Rolled(bool tournament, ulong seed)
        {
            var width = relics.Count + 3;
            var state = Create(new GameOptions(Seed: seed), "A" + new string('.', width - 2) + "B", new string('.', width));
            if (tournament)
            {
                state.SetRules(Rules);
            }

            Zone(state, Blue, 0, width - 3);
            Zone(state, Red, width - 2, width - 1);
            for (var i = 0; i < relics.Count - 1; i++)
            {
                Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Relic), i + 1, 1).Artifact = relics[i]; // Blue's zone holds all but one
            }

            var open = Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(ArtifactTier.Relic), width - 1, 1);
            Start(state);
            return open.Artifact;
        }

        Assert.All(Enumerable.Range(1, 5), seed => Assert.Equal(left, Rolled(tournament: false, (ulong)seed))); // only the one left is free
        Assert.Contains(Enumerable.Range(1, 10), seed => Rolled(tournament: true, (ulong)seed) != left); // Red's zone rolls from every relic
    }
}
