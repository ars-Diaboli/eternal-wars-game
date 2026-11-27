using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// The adventure AI in a tournament (ADR-366): it plays its zone and never looks past it, keeps its one hero within a walk of home and out of
/// costly fights as the battle nears, weighs mines and buildings by the days left, and on the last day gathers everything into the hero, as on
/// an arena's preparation day (ADR-160).
/// </summary>
public class TournamentAiTests
{
    private static readonly ScenarioRules Rules = ScenarioRules.Default with { Tournament = true, MaxHeroes = 1 };

    /// <summary>
    /// A map <paramref name="width"/> wide and 10 high: Alpha on row 8 at <paramref name="heroX"/>, Bravo in the bottom right corner behind a wall
    /// three columns from the edge. In a tournament (<paramref name="days"/> long) Blue's zone is left of the wall and Red's right of it.
    /// </summary>
    private static GameState Map(int width, int heroX, int? days, bool tournament = true)
    {
        string Row(int y) => string.Concat(Enumerable.Range(0, width).Select(x =>
            x == width - 4 ? '#' : y == 8 && x == heroX ? 'A' : y == 9 && x == width - 1 ? 'B' : '.'));
        var state = Create(GameOptions.Default with { TournamentDays = tournament ? days : null }, [.. Enumerable.Range(0, 10).Select(Row)]);
        if (tournament)
        {
            state.SetRules(Rules);
            Zone(state, Blue, 0, width - 5);
            Zone(state, Red, width - 3, width - 1);
        }

        return state;
    }

    private static void Zone(GameState state, PlayerId player, int fromX, int toX)
    {
        for (var y = 0; y < state.Map.Height; y++)
        {
            for (var x = fromX; x <= toX; x++)
            {
                state.Map.SetStartZone(new MapPosition(x, y), player);
            }
        }
    }

    private static long Power(GameState state, Army army) => army.Stacks.Sum(s => (long)s.Stack.Count * state.Creatures.Get(s.Stack.Creature).FightValue);

    [Fact]
    public void OnTheLastDay_TheHeroWalksHomeFirst_AndTakesTheTownsWholeArmy()
    {
        var state = Map(40, heroX: 12, days: 1);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue); // entrance (3, 6), nine tiles west of Alpha
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Pikeman, 20));
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 24, 8); // twelve tiles east: after it, home is out of reach today
        var session = Start(state);
        var hero = state.GetHero(A);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        Assert.True(town.Garrison.IsEmpty);
        Assert.Contains(hero.Army.Stacks, s => s.Stack.Creature == BuiltInCreatures.Pikeman && s.Stack.Count == 20);
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void OnTheLastDay_ATownTheLastStepReaches_IsWithinWalkingReach()
    {
        // The route home costs more than the day's movement, but the last step starts with some left, so the hero gets there today.
        var state = Map(50, heroX: 18, days: 1);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Pikeman, 20));
        Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 30, 8); // twelve tiles east: after it, home is out of reach today
        var session = Start(state);
        var hero = state.GetHero(A);
        var route = session.Movement.FindPath(A, town.VisitPosition)!;
        Assert.True(route.TotalCost > hero.MovementPoints && route.IsReachableThisTurn, $"cost {route.TotalCost} of {hero.MovementPoints}");
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        Assert.True(town.Garrison.IsEmpty);
        Assert.Equal(town.VisitPosition, hero.Position);
        Assert.Equal(0, ai.Refused);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OnTheLastDay_AHeroOutOfWalkingReach_CastsTownPortalHome(bool knowsTheSpell)
    {
        var state = Map(60, heroX: 45, days: 1); // 42 tiles from the town's entrance: three days' walk
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        town.Garrison.Set(0, new CreatureStack(BuiltInCreatures.Pikeman, 20));
        var hero = state.GetHero(A);
        hero.HasSpellBook = true;
        hero.Knowledge = 10;
        if (knowsTheSpell)
        {
            hero.Learn(AdventureMagic.TownPortal);
        }

        var session = Start(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        Assert.Equal(knowsTheSpell, town.Garrison.IsEmpty);
        Assert.Equal(knowsTheSpell, hero.Position == town.VisitPosition);
        Assert.Equal(0, ai.Refused);
    }

    [Theory]
    [InlineData(null, 30, true)] // an ordinary map: the chest, 27 tiles east, is worth the two days' walk
    [InlineData(14, 30, true)] // two weeks to go: there is time to walk back
    [InlineData(3, 30, false)] // the chest is farther than the hero could walk back by the last day
    [InlineData(null, 16, true)] // 13 tiles east: within a day's walk of 15
    [InlineData(2, 16, false)] // a day's walk back, but the hero keeps a quarter of it for detours (on the last day it may go)
    public void BeforeTheLastDay_TheHeroSetsOutOnlyForWhatItCanWalkBackFrom(int? days, int chestX, bool setsOut)
    {
        var state = Map(50, heroX: 3, days: days, tournament: days is not null);
        AddTown(state, TownIds.Haven, 0, 0, Blue);
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, chestX, 8);
        var session = Start(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        Assert.Equal(setsOut, session.CommandLog.OfType<MoveHeroCommand>().Any(m => m.Target == chest.VisitPosition));
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void AMine_IsWorthTheIncomeLeftBeforeTheBattle()
    {
        string Valued(int? days)
        {
            var state = Map(30, heroX: 3, days: days, tournament: days is not null);
            Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Gold), 9, 4);
            var session = Start(state);
            return Assert.Single(new AdventureAi(session, controlHumanSlots: true).Explain(A), l => l.Contains("mine.gold", StringComparison.Ordinal));
        }

        Assert.Contains("value 8000", Valued(null), StringComparison.Ordinal); // eight days of 1000 gold
        Assert.Contains("value 8000", Valued(28), StringComparison.Ordinal);
        Assert.Contains("value 2000", Valued(3), StringComparison.Ordinal); // taken today, it pays on days 2 and 3
    }

    [Theory]
    [InlineData(false, false)] // a strong army with no enemy town known goes looking for one before the chest
    [InlineData(true, true)] // in a tournament there is no enemy to find: the chest first
    public void AStrongHero_HuntsForTheEnemy_ExceptInATournament(bool tournament, bool chestFirst)
    {
        var state = Map(40, heroX: 20, days: 14, tournament: tournament);
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 30));
        var chest = Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 18, 8); // in sight, two tiles west
        var session = Start(state, revealAll: false);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        Assert.Equal(chestFirst, session.CommandLog.OfType<MoveHeroCommand>().First().Target == chest.VisitPosition);
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void TheHero_NeverExploresTowardTheOtherZone()
    {
        // Red's zone begins with the wall, so Blue never sees past the last column of its own: there is nothing left to explore.
        var state = Create(GameOptions.Default, "A....##....", ".....##....", ".....##...B");
        state.SetRules(Rules);
        Zone(state, Blue, 0, 4);
        Zone(state, Red, 5, 10);
        var session = Start(state, revealAll: false);
        var hero = state.GetHero(A);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(Enumerable.Range(0, 5).All(x => state.Fog.IsExplored(Blue, new MapPosition(x, 1))));
        Assert.False(state.Fog.IsExplored(Blue, new MapPosition(5, 1)));
        Assert.True(ai.PlayTurn());

        Assert.Equal(new MapPosition(0, 0), hero.Position);
        Assert.Contains(ai.Explain(A), l => l == "  explore: nothing");
    }

    [Theory]
    [InlineData(14, true)]
    [InlineData(3, false)] // the battle is three days away: a fallen army could not be rebuilt
    public void InTheLastDays_TheHeroFightsOnlyForSmallLosses(int days, bool fought)
    {
        GameState Setup()
        {
            var state = Map(30, heroX: 3, days: days);
            state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Footman, 30));
            return state;
        }

        // A stack of wolves the footmen beat every time, losing more than the share allowed in the last days but less than an ordinary fight's.
        var probe = Setup();
        var estimator = new BattleEstimator(probe);
        var weights = AiWeights.Default;
        var count = Enumerable.Range(1, 80).FirstOrDefault(n =>
        {
            var estimate = estimator.Fight(probe.GetHero(A), Army.Of(new CreatureStack(BuiltInCreatures.Wolf, n)));
            return estimate.IsSafe(weights.MonsterLoss) && !estimate.IsSafe(weights.ArenaLoss);
        });
        Assert.True(count > 0);

        var state = Setup();
        var monster = Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 6, 8);
        (monster.Creature, monster.Count, monster.Disposition) = (BuiltInCreatures.Wolf, count, GuardDisposition.Savage);
        var ai = new AdventureAi(Start(state), controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        Assert.Equal(fought, !state.Objects.Contains(monster));
        Assert.Equal(0, ai.Refused);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData(28, true)] // 27 days of a lumber and an ore pay back the silo's 3750 gold's worth
    [InlineData(14, false)] // 13 days do not: the blacksmith instead
    public void ABuildingThatOnlyAddsIncome_IsBuiltWhenItPaysBeforeTheBattle(int? days, bool silo)
    {
        var state = Map(30, heroX: 20, days: days, tournament: days is not null);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        foreach (var tier in Enumerable.Range(1, 5))
        {
            town.SetDwelling(TownRules.Lines(state, town).First(l => l.Tier == tier).Id, 1);
        }

        town.SetLevel(BuiltInBuildings.Marketplace, 1); // its next level, the Resource Silo, is next to build: a lumber and an ore a day
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20000, Lumber: 50, Ore: 50, Crystal: 20, Mercury: 20, Sulfur: 20);
        var session = Start(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        var built = Assert.Single(session.CommandLog.OfType<BuildCommand>()).Building.Value;
        Assert.Equal(silo ? BuiltInBuildings.Marketplace : BuiltInBuildings.Blacksmith, built);
        Assert.Equal(0, ai.Refused);
    }

    [Fact]
    public void InTheLastDays_TheTownStillBuildsOnceADay()
    {
        var state = Map(30, heroX: 20, days: 5);
        AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 50000, Lumber: 100, Ore: 100, Crystal: 50, Mercury: 50, Sulfur: 50);
        var session = Start(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        Assert.Single(session.CommandLog, c => c is BuildCommand or BuildDwellingCommand);
        Assert.Equal(0, ai.Refused);
    }

    [Theory]
    [InlineData(null, BuiltInCreatures.Pikeman, false)] // an ordinary town builds up its first tier and hires a second hero, for the weeks to come
    [InlineData(5, BuiltInCreatures.Monk, true)] // five days before the battle: the dwelling with the most army for the gold, its monks bought at once
    public void InTheLastDays_TheTownSpendsEverythingOnTheArmy(int? days, string dwelling, bool armed)
    {
        var state = Map(30, heroX: 20, days: days, tournament: days is not null);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 3000, Lumber: 10, Ore: 10);
        var session = Start(state);
        var ai = new AdventureAi(session, controlHumanSlots: true);

        Assert.True(ai.PlayTurn());

        Assert.Contains(session.CommandLog, c => c is BuildDwellingCommand { Line: var line } && line.Value == dwelling);
        Assert.Equal(armed, Power(state, town.Garrison) > 3000);
        Assert.Equal(0, ai.Refused);
    }
}
