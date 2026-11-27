using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// What the game app shows of a tournament (ADR-366, plan step 5): the new-game screen's length stepper and its words, the calendar's countdown
/// and the last day's notice, the one-hero limit on the hero card and in the taverns' and the prison's refusals, and the Thieves' Guild, which
/// tells nothing about the other player.
/// </summary>
public class TournamentScreenTests
{
    /// <summary>Blue's start zone left of the wall, Red's right of it; Alpha and Beta at the bottom corners.</summary>
    private static GameState Tournament(int days = 14, bool tournament = true)
    {
        string[] rows = [.. Enumerable.Range(0, 9).Select(y => y == 8 ? "A....#....B" : ".....#.....")];
        var state = Create(GameOptions.Default with { TournamentDays = tournament ? days : null }, rows);
        state.SetRules(ScenarioRules.Default with { Tournament = tournament, MaxHeroes = tournament ? 1 : ScenarioRules.HeroLimit });
        for (var y = 0; y < state.Map.Height; y++)
        {
            for (var x = 0; x < state.Map.Width; x++)
            {
                state.Map.SetStartZone(new MapPosition(x, y), x < 5 ? Blue : x > 5 ? Red : null);
            }
        }

        return state;
    }

    [Fact]
    public void TheLengthStepper_GoesADayAStep_ThenAWeekAStep_AndStopsAtTheEnds()
    {
        var up = new List<int> { TournamentRules.Lengths[0] };
        while (TournamentRules.Step(up[^1], 1) is var next && next != up[^1])
        {
            up.Add(next);
        }

        Assert.Equal(TournamentRules.Lengths, up);
        Assert.Equal(1, TournamentRules.Step(1, -1));
        Assert.Equal(84, TournamentRules.Step(84, 1));
        Assert.Equal(14, TournamentRules.Step(21, -1));
        Assert.Equal(21, TournamentRules.Step(14, 1));
        Assert.Equal(28, TournamentRules.Step(14, 2));

        // A length the screen never offers steps to the offered one beside it.
        Assert.Equal(21, TournamentRules.Step(16, 1));
        Assert.Equal(14, TournamentRules.Step(16, -1));
        Assert.Equal(1, TournamentRules.Step(-5, 1));
        Assert.Equal(84, TournamentRules.Step(200, -1));
    }

    [Theory]
    [InlineData(1, "1 day")]
    [InlineData(6, "6 days")]
    [InlineData(7, "7 days (1 week)")]
    [InlineData(9, "9 days")]
    [InlineData(14, "14 days (2 weeks)")]
    [InlineData(84, "84 days (12 weeks)")]
    public void TheLength_IsShownInDays_AndInWeeksWhenWhole(int days, string shown) => Assert.Equal(shown, TournamentRules.LengthText(days));

    [Theory]
    [InlineData(1, "Battle in 9 days", false)]
    [InlineData(8, "Battle in 2 days", false)]
    [InlineData(9, "Last day before the battle", true)]
    [InlineData(10, null, false)] // the battle's day
    public void TheCalendar_CountsDownToTheBattle_AndTheLastDaySaysSo(int day, string? countdown, bool lastDay)
    {
        var state = Tournament(days: 9);
        state.Date = new GameDate(day);

        Assert.Equal(countdown, TournamentRules.Countdown(state));
        Assert.Equal(lastDay, TournamentRules.IsLastDay(state));
    }

    [Fact]
    public void AnOrdinaryGame_HasNoCountdown_NorALastDay()
    {
        var state = Tournament(tournament: false);
        state.Date = new GameDate(TournamentRules.DefaultDays);

        Assert.Null(TournamentRules.Countdown(state));
        Assert.False(TournamentRules.IsLastDay(state));
    }

    [Fact]
    public void TheCountdown_FollowsTheDays_AsTheyAreEnded()
    {
        var session = Start(Tournament(days: 2));
        Assert.Equal("Battle in 2 days", TournamentRules.Countdown(session.State));

        session.EndTurn();
        session.EndTurn();

        Assert.Equal("Last day before the battle", TournamentRules.Countdown(session.State));
        Assert.True(TournamentRules.IsLastDay(session.State));
    }

    [Fact]
    public void TheHeroCard_ShowsTheLimitOfOne_AndWhy()
    {
        var tournament = Tournament();
        var (text, why) = HeroCard.Limit(tournament, Blue)!.Value;
        Assert.Equal("Hero 1/1", text);
        Assert.Contains("A tournament allows one hero a player", why, StringComparison.Ordinal);

        var ordinary = Tournament(tournament: false);
        Assert.Null(HeroCard.Limit(ordinary, Blue));

        ordinary.SetRules(ordinary.Rules with { MaxHeroes = 1 });
        Assert.Equal(("Hero 1/1", "This map allows one hero a player."), HeroCard.Limit(ordinary, Blue));
    }

    [Fact]
    public void TheTavern_RefusesASecondHero_SayingATournamentAllowsOne()
    {
        var state = Tournament();
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue); // entrance (3, 6)
        town.SetLevel(BuiltInBuildings.Tavern, 1);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000);
        var session = Start(state);

        var refusal = session.Execute(new HireHeroCommand(Blue, town.Id, TavernRules.Offers(state, Blue, town)[0].Id)).Error;

        Assert.Equal(
            $"A tournament allows one hero a player, and Blue has {state.GetHero(A).Name}. Another may be hired only once it has fallen.", refusal);
        Assert.Equal(refusal, TavernRules.CheckHire(state, town, TavernRules.Offers(state, Blue, town)[0].Id));
    }

    [Fact]
    public void AHeroInTheGarrison_IsStillTheOneHero_SoTheTavernRefusesASecond()
    {
        var state = Tournament();
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue); // entrance (3, 6)
        town.SetLevel(BuiltInBuildings.Tavern, 1);
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000);
        var hero = state.GetHero(A);
        hero.Position = new MapPosition(3, 6);
        var session = Start(state);
        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();

        var refusal = session.Execute(new HireHeroCommand(Blue, town.Id, TavernRules.Offers(state, Blue, town)[0].Id)).Error;

        Assert.Equal($"A tournament allows one hero a player, and Blue has {hero.Name}. Another may be hired only once it has fallen.", refusal);
        Assert.Same(hero, Assert.Single(state.AllHeroesOf(Blue)));
        Assert.True(state.HeroLimitReached(Blue)); // a map event's hero, a prison and the map taverns ask the same
        session.Execute(new LeaveGarrisonCommand(Blue, town.Id)).ThrowIfRejected(); // coming out keeps it the one hero
        Assert.Same(hero, Assert.Single(state.HeroesOf(Blue)));
    }

    [Fact]
    public void AMapTavern_RefusesASecondHero_SayingATournamentAllowsOne()
    {
        var state = Tournament();
        var tavern = Add<MapTavern>(state, BuiltInMapObjects.MapTavern, 0, 6); // entrance (1, 7)
        state.GetPlayer(Blue).Resources = new ResourceSet(Gold: 20_000);
        var session = Start(state);
        session.Step(A, Direction.NorthEast);

        var refusal = session.Execute(new HireAtMapTavernCommand(Blue, tavern.Id, MapTavernRules.Offers(state, tavern)[0].Id)).Error;

        Assert.Equal(TavernRules.LimitRefusal(state, Blue), refusal);
        Assert.StartsWith("A tournament allows one hero a player", refusal, StringComparison.Ordinal);
        Assert.Single(state.HeroesOf(Blue));
    }

    [Fact]
    public void TheRumours_TellNothingOfTheOtherPlayer_InATournament()
    {
        var state = Tournament();
        AddTown(state, TownIds.Haven, 0, 0, Blue);
        var redTreasure = Add<ArtifactPickup>(state, BuiltInMapObjects.ArtifactId(Core.Domain.Artifacts.ArtifactTier.Minor), 8, 2);
        var artifact = state.Artifacts.All.OrderBy(a => a.Id).First();
        redTreasure.Artifact = artifact.Id;
        Start(state);
        string[] treasure = ["The {treasure} lies in the {where}."];

        Assert.Null(Rumours.OfWeek(state, Blue, ["{mosttowns} is strong."])); // no ranks
        Assert.Null(Rumours.OfWeek(state, Blue, treasure)); // Red's zone is out of Blue's reach
        Assert.Equal($"The {artifact.Name} lies in the north-east.", Rumours.OfWeek(state, Red, treasure));

        var ordinary = Tournament(tournament: false);
        AddTown(ordinary, TownIds.Haven, 0, 0, Blue);
        Start(ordinary);
        Assert.Equal("Blue is strong.", Rumours.OfWeek(ordinary, Blue, ["{mosttowns} is strong."]));
    }

    [Theory]
    [InlineData(1, "Blue may have only one hero on this map.")]
    [InlineData(2, "Blue may have at most 2 heroes on this map.")]
    public void TheLimitRefusal_OnAnOrdinaryMap_NamesTheMapsLimit(int limit, string refusal)
    {
        var state = Create("A....", "....B");
        state.SetRules(ScenarioRules.Default with { MaxHeroes = limit });

        Assert.Equal(refusal, TavernRules.LimitRefusal(state, Blue));
    }

    [Fact]
    public void APrisoner_StaysInPrison_ForTheTournamentsOneHero()
    {
        var state = Tournament();
        Add<Prison>(state, BuiltInMapObjects.Prison, 0, 7); // entrance (1, 7)
        var session = Start(state);
        var refused = new List<MapObjectRefused>();
        session.Events.Subscribe<MapObjectRefused>(refused.Add);

        session.Step(A, Direction.NorthEast);

        var reason = Assert.Single(refused).Reason;
        Assert.StartsWith("A tournament allows one hero a player", reason, StringComparison.Ordinal);
        Assert.EndsWith("The prisoner stays.", reason, StringComparison.Ordinal);
        Assert.Single(state.HeroesOf(Blue));
    }

    [Fact]
    public void TheThievesGuild_TellsNothingOfTheOtherPlayer_InATournament()
    {
        var tournament = Tournament();
        Assert.Equal(ThievesGuild.TournamentRefusal, ThievesGuild.Closed(tournament));
        Assert.Empty(ThievesGuild.Table(tournament, 1));
        Assert.Empty(ThievesGuild.Table(tournament)); // the Den of Thieves' whole table

        var ordinary = Tournament(tournament: false);
        Assert.Null(ThievesGuild.Closed(ordinary));
        Assert.Equal(ThievesGuild.RowsFor(1).Count, ThievesGuild.Table(ordinary, 1).Count);
    }
}
