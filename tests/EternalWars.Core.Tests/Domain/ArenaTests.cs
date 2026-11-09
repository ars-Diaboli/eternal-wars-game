using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The arena mode (ADR-097, ADR-100): a day of preparation, then one battle at a combat place decides the game.</summary>
public class ArenaTests
{
    private static readonly ScenarioRules Arena = ScenarioRules.Default with { Arena = true };

    private static Army Strong() => Army.Of(new CreatureStack(BuiltInCreatures.Champion, 10));

    /// <summary>
    /// A (Blue) at the top left, B (Red) at the top right, a combat place in the middle of the bottom row: the attack cell (4, 4) is blocked, the
    /// defender stands on (5, 4).
    /// </summary>
    private static GameState Map(out CombatPlace place, Action<GameState>? adjust = null)
    {
        var state = Create("A........B", "..........", "..........", "..........", "..........");
        state.SetRules(Arena);
        place = Add<CombatPlace>(state, BuiltInMapObjects.CombatPlace, 4, 4);
        adjust?.Invoke(state);
        return state;
    }

    /// <summary>Blue and Red end day 1; day 2 begins with the battle.</summary>
    private static void ToBattleDay(GameSession session)
    {
        session.EndTurn();
        session.EndTurn();
    }

    [Fact]
    public void TheCombatPlace_PutsTheDefenderOnItsEntrance_AndTheAttackerOnItsBlockedCell()
    {
        var state = Create("....", "....");
        var west = Add<CombatPlace>(state, BuiltInMapObjects.CombatPlace, 1, 0);
        var north = Add<CombatPlace>(state, BuiltInMapObjects.VerticalCombatPlace, 3, 0);

        Assert.Equal((new MapPosition(2, 0), new MapPosition(1, 0)), (west.VisitPosition, west.AttackCell));
        Assert.Equal((new MapPosition(3, 1), new MapPosition(3, 0)), (north.VisitPosition, north.AttackCell));
        Assert.Equal(FootprintCell.Blocked, state.ObjectCellAt(west.AttackCell)!.Value.Cell);
    }

    [Fact]
    public void OnThePreparationDay_HeroesMoveNearlyWithoutLimit_AndTheNextDayIsNoLongerOne()
    {
        var state = Map(out _);
        var session = Start(state);
        var hero = state.GetHero(A);

        Assert.True(ArenaRules.IsPreparationDay(state));
        Assert.Equal((ArenaRules.PreparationMovement, ArenaRules.PreparationMovement), (hero.MovementPoints, hero.MaxMovementPoints));
        session.MoveTo(A, new MapPosition(8, 0));
        session.MoveTo(A, new MapPosition(0, 3));
        Assert.Equal(new MapPosition(0, 3), hero.Position);
        Assert.True(hero.MovementPoints > ArenaRules.PreparationMovement - 3000);
    }

    [Fact]
    public void AnOrdinaryMap_KeepsTheDailyAllowance()
    {
        var state = Create("A........B");
        Start(state);

        Assert.False(ArenaRules.IsPreparationDay(state));
        Assert.Equal(1500, state.GetHero(A).MaxMovementPoints);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void OnThePreparationDay_ATownBuildsAsMuchAsItCanPay(bool arena, bool secondBuilds)
    {
        var state = Create(new GameOptions(Complexity: Complexity.Beginner), "..........", "..........", "..........", "..........", "..........", "..........", "..........", "...A.....B");
        if (arena)
        {
            state.SetRules(Arena);
        }

        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var session = Start(state);

        session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.MageGuild)).ThrowIfRejected();
        var second = session.Execute(new BuildCommand(Blue, town.Id, BuiltInBuildings.Blacksmith));

        Assert.Equal(secondBuilds, second.IsAccepted);
        Assert.Equal(secondBuilds, !TownRules.HasBuiltToday(state, town));
    }

    [Fact]
    public void DayTwo_MovesTheChampionsToTheCombatPlace_AndStartsTheBattleBeforeAnyTurn()
    {
        var state = Map(out var place);
        var session = Start(state);
        var events = new List<IEvent>();
        session.Events.Subscribe<IEvent>(events.Add);

        ToBattleDay(session);

        // Nobody stood on the combat place: the first player in turn order attacks, the next enemy defends.
        Assert.Equal(new ArenaBattle(place.Id, Blue, A, Red, B), state.Arena);
        Assert.Equal((place.AttackCell, place.VisitPosition), (state.GetHero(A).Position, state.GetHero(B).Position));
        Assert.IsType<BattleDecision>(state.PendingDecision);
        Assert.Equal((A, B), (state.Battle!.Attacker.Hero, state.Battle.Defender.Hero));
        Assert.Equal(2, state.Date.AbsoluteDay);
        var chosen = events.FindIndex(e => e is ArenaChampionsChosen);
        Assert.True(events.FindIndex(e => e is DayStarted { Date.AbsoluteDay: 2 }) < chosen);
        Assert.True(chosen < events.FindIndex(e => e is BattleStarted));
        Assert.DoesNotContain(events, e => e is TurnStarted { Date.AbsoluteDay: 2 });
        Assert.Contains(events, e => e is HeroTeleported { Hero: var h } && h == B);
    }

    [Fact]
    public void TheBattlesWinner_WinsTheGame_WithoutExperience()
    {
        var state = Map(out _, s => s.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10)));
        var session = Start(state);
        ToBattleDay(session);

        session.AutoResolve();

        Assert.Equal((ArenaResult.Won, Blue), (state.Arena!.Result, state.Arena.Winner));
        Assert.True(state.IsOver);
        Assert.Equal([Blue], state.Outcome!.Winners);
        Assert.Equal("Blue won the arena battle.", state.Outcome.Reason);
        Assert.Equal(0, state.GetHero(A).Experience);
        Assert.False(ScoreRules.IsRanked(state));
        Assert.False(session.Execute(new EndTurnCommand(Blue)).IsAccepted);
    }

    [Fact]
    public void AHeroOnACombatPlace_MakesItsOwnerTheDefender_ThereAndNowhereElse()
    {
        CombatPlace? second = null;
        var state = Map(out var first, s => second = Add<CombatPlace>(s, BuiltInMapObjects.VerticalCombatPlace, 8, 2));
        var session = Start(state);
        session.EndTurn(); // Blue
        session.MoveTo(B, second!.VisitPosition);
        session.EndTurn(); // Red

        Assert.Equal(new ArenaBattle(second.Id, Blue, A, Red, B), state.Arena);
        Assert.NotEqual(first.Id, state.Arena!.CombatPlace);
        Assert.Equal((second.AttackCell, second.VisitPosition), (state.GetHero(A).Position, state.GetHero(B).Position));
    }

    [Fact]
    public void TheDefenderComesFromThePlayerOnTheCombatPlace_EvenIfItIsNotFirstInTurnOrder()
    {
        var state = Map(out var place);
        var session = Start(state);
        session.EndTurn(); // Blue
        session.MoveTo(B, place.VisitPosition);
        session.EndTurn(); // Red

        Assert.Equal((Blue, Red), (state.Arena!.Attacker, state.Arena.Defender));
        Assert.Equal((Blue, Red), (state.Battle!.Attacker.Owner, state.Battle.Defender.Owner));
    }

    [Fact]
    public void EachSide_FightsWithItsStrongestHero_AndAHeroInTheWayStepsAside()
    {
        var state = Map(out var place, s =>
        {
            s.AddHero(new Hero(C, "Charlie", Blue, new MapPosition(0, 2), Strong()));
        });
        var session = Start(state);
        session.MoveTo(A, place.VisitPosition); // the weaker hero claims the place for Blue
        ToBattleDay(session);

        Assert.Equal(new ArenaBattle(place.Id, Red, B, Blue, C), state.Arena);
        Assert.Equal(place.VisitPosition, state.GetHero(C).Position);
        Assert.Equal(new MapPosition(0, 2), state.GetHero(A).Position);
        Assert.Equal(place.AttackCell, state.GetHero(B).Position);
    }

    [Fact]
    public void ATeamWithoutAHeroOnTheMap_SendsItsGarrisonHero()
    {
        var state = Create("..........", "..........", "..........", "..........", "..........", "..........", "...A......", ".........B");
        state.SetRules(Arena);
        var town = AddTown(state, TownIds.Haven, 0, 0, Blue);
        var place = Add<CombatPlace>(state, BuiltInMapObjects.CombatPlace, 7, 3);
        var session = Start(state);
        session.Execute(new EnterGarrisonCommand(Blue, town.Id)).ThrowIfRejected();
        Assert.Empty(state.HeroesOf(Blue));

        ToBattleDay(session);

        var hero = state.GetHero(A);
        Assert.Null(town.GarrisonHero);
        Assert.Contains(hero, state.Heroes);
        Assert.Equal(place.AttackCell, hero.Position);
        Assert.Equal(10, hero.Army.Stacks.Sum(s => s.Stack.Count));
        Assert.True(town.Garrison.IsEmpty);
    }

    [Fact]
    public void RetreatingLosesTheGame()
    {
        var state = Map(out _);
        var session = Start(state);
        ToBattleDay(session);

        session.Execute(new BattleRetreatCommand(Red)).ThrowIfRejected();

        Assert.Equal([Blue], state.Outcome!.Winners);
        Assert.Equal(ArenaResult.Won, state.Arena!.Result);
    }

    [Fact]
    public void ASideWithoutAHero_IsALossForBoth()
    {
        var state = Map(out _, s => s.RemoveHero(s.GetHero(B)));
        var session = Start(state);
        ToBattleDay(session);

        Assert.Equal(ArenaResult.NoBattle, state.Arena!.Result);
        Assert.Null(state.Battle);
        Assert.True(state.IsOver);
        Assert.Empty(state.Outcome!.Winners);
        Assert.Equal("Nobody was left to fight the arena battle.", state.Outcome.Reason); // Red never took part
    }

    [Fact]
    public void BothArmiesGone_IsADraw_ALossForBoth()
    {
        var state = Map(out _, s =>
        {
            s.GetHero(A).Army.Set(0, null);
            s.GetHero(B).Army.Set(0, null);
        });
        var session = Start(state);
        ToBattleDay(session);

        Assert.Equal(ArenaResult.Draw, state.Arena!.Result);
        Assert.Empty(state.Outcome!.Winners);
    }

    [Fact]
    public void ASideWithoutAnArmy_LosesWithoutABattle()
    {
        var state = Map(out _, s => s.GetHero(B).Army.Set(0, null));
        var session = Start(state);
        ToBattleDay(session);

        Assert.Null(state.Battle);
        Assert.Equal([Blue], state.Outcome!.Winners);
    }

    [Fact]
    public void TheMapsConditions_DoNotCount_AndNobodyIsEliminatedBeforeTheBattle()
    {
        var state = Create("A........B", "..........", "..........", "..........", "..........");
        state.SetRules(Arena with { Victory = [ScenarioCondition.SurviveUntilDay(1, Red)] });
        state.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 10));
        var session = Start(state);

        Assert.False(state.IsOver);
        session.MoveTo(A, new MapPosition(9, 0)); // Red loses its only hero on day 1
        session.AutoResolve();

        Assert.False(state.IsOver);
        Assert.False(state.GetPlayer(Red).IsDefeated);
        session.EndTurn();
        session.EndTurn();
        Assert.True(state.IsOver);
        Assert.Empty(state.Outcome!.Winners); // Red had no hero for the battle
    }

    [Fact]
    public void TheComputer_NeverRetreatsFromTheArenaBattle()
    {
        var state = Map(out _, s => s.GetHero(A).Army.Set(0, new CreatureStack(BuiltInCreatures.Champion, 20)));
        state.GetPlayer(Red).IsHuman = false;
        var session = Start(state);
        var events = new List<IEvent>();
        session.Events.Subscribe<BattleEnded>(events.Add);
        session.EndTurn(); // Blue
        session.Execute(new EndTurnCommand(Red)).ThrowIfRejected();

        session.AutoResolve();

        Assert.Equal(BattleEndReason.Defeated, Assert.IsType<BattleEnded>(Assert.Single(events)).Reason);
        Assert.Equal([Blue], state.Outcome!.Winners);
    }
}
