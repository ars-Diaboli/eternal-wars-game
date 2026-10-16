using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Native terrain and Pathfinding (ADR-032, ADR-045), and income from every source together (ADR-025).</summary>
public class NativeTerrainAndIncomeTests
{
    [Theory]
    [InlineData(SkillLevel.None, 200)]
    [InlineData(SkillLevel.Basic, 175)]
    [InlineData(SkillLevel.Advanced, 150)]
    [InlineData(SkillLevel.Expert, 125)]
    public void Pathfinding_CutsTheNonNativePenalty_25_50_75Percent(SkillLevel pathfinding, int swampCost)
    {
        var state = Create("As.", "..B");
        var hero = state.GetHero(A);
        hero.Skills.Set(BuiltInSkills.Pathfinding, pathfinding);
        var swamp = new MapPosition(1, 0);

        Assert.Equal(swampCost, MovementRules.StepCost(state, hero, swamp, new MapPosition(2, 0), Direction.East));
        Assert.Equal((int)Math.Round(swampCost * 1.41), MovementRules.StepCost(state, hero, swamp, new MapPosition(2, 1), Direction.SouthEast));
    }

    [Fact]
    public void NativeArmy_PaysTheBaseCost_SoPathfindingHasNothingToCut()
    {
        var state = Create("As.", "..B");
        var hero = state.GetHero(A);
        hero.Army.Set(0, new CreatureStack("swamp-bestiary.lizardman", 10));
        var swamp = new MapPosition(1, 0);

        Assert.Equal(100, MovementRules.StepCost(state, hero, swamp, new MapPosition(2, 0), Direction.East));
        hero.Skills.Set(BuiltInSkills.Pathfinding, SkillLevel.Expert);
        Assert.Equal(100, MovementRules.StepCost(state, hero, swamp, new MapPosition(2, 0), Direction.East));

        hero.Army.Set(1, new CreatureStack("swamp-bestiary.kappa", 3)); // still all Swamp Bestiary
        Assert.Equal(TownIds.SwampBestiary, MovementRules.ArmyTown(state, hero));
        hero.Army.Set(2, new CreatureStack(BuiltInCreatures.Imp, 1)); // one foreigner: penalty, cut by Pathfinding
        Assert.Equal(125, MovementRules.StepCost(state, hero, swamp, new MapPosition(2, 0), Direction.East));
    }

    [Fact]
    public void PlannedRoutes_AndSteps_UseTheSameCost()
    {
        var state = Create("Asss.B"); // one row: the route has to cross the swamp
        state.GetHero(A).Skills.Set(BuiltInSkills.Pathfinding, SkillLevel.Basic);
        var session = Start(state);
        var hero = state.GetHero(A);
        var start = hero.MovementPoints;
        var path = session.Movement.FindPath(A, new MapPosition(4, 0))!;

        var outcome = session.MoveTo(A, new MapPosition(4, 0));

        Assert.True(outcome.Arrived);
        Assert.Equal(path.TotalCost, start - hero.MovementPoints);
        Assert.Equal(100 + (3 * 175), path.TotalCost); // grass is native to the Haven footmen, then swamp at 200 − 25 %
    }

    [Fact]
    public void DailyIncome_AddsMines_Towns_SkillsAndSpecialties()
    {
        var state = Create("A.........", "..........", "..........", "..........", "..........", "..........", "..........", "..........", ".........B");
        Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Gold), 7, 0).Owner = Blue;
        var town = AddTown(state, TownIds.Haven, 1, 1, Blue);
        town.SetLevel(BuiltInBuildings.Marketplace, 2);
        var hero = state.GetHero(A);
        hero.Skills.Set(BuiltInSkills.Treasury, SkillLevel.Basic);
        hero.Skills.Set(BuiltInSkills.Prospecting, SkillLevel.Basic);
        var session = Start(state);

        // Mine 1000 + town hall 500 + Treasury 250 gold; resource silo 1 + Prospecting 1 lumber and ore.
        Assert.Equal(new ResourceSet(Gold: 1750, Lumber: 2, Ore: 2), session.Economy.DailyIncome(Blue));
        Assert.Equal(new ResourceSet(Gold: 1750, Lumber: 2, Ore: 2), state.GetPlayer(Blue).Resources); // paid on turn 1
        Assert.Equal(ResourceSet.Empty, session.Economy.DailyIncome(Red));
    }
}
