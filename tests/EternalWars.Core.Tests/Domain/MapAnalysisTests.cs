using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Generation;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The core's map analysis (EDIT-9, ADR-263): reach in days, guard strength per cell, the fairness numbers per start.</summary>
public class MapAnalysisTests
{
    private const string Row = "........................................";

    /// <summary>Two starts more than 3 days apart: 70 tiles of grass at 125 is 6 days.</summary>
    private static readonly string Wide = new('.', 80);

    [Fact]
    public void Reach_IsTheGamesStepCostOverADay_AndGoesOnPastGuardsAndPickups()
    {
        var state = TestWorld.Create(Row);
        TestWorld.Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 20, 0);
        var monster = TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 30, 0);
        monster.Count = 10;
        var reach = new MapAnalysis(state).Reach(new MapPosition(0, 0), TestWorld.Blue);

        Assert.Equal(0, reach.DaysTo(new MapPosition(0, 0)));
        Assert.Equal(1, reach.DaysTo(new MapPosition(1, 0)));
        Assert.Equal(1, reach.DaysTo(new MapPosition(12, 0))); // 12 grass steps of 125 (at home nowhere): one day of 1500
        Assert.Equal(2, reach.DaysTo(new MapPosition(13, 0)));
        Assert.Equal(1500, reach.CostTo(new MapPosition(12, 0)));

        // A route in the game would end on the pile and at the guard; the analysis walks on, as a hero that takes and fights does.
        Assert.Equal(25 * 125, reach.CostTo(new MapPosition(25, 0)));
        Assert.Equal(35 * 125, reach.CostTo(new MapPosition(35, 0)));
        Assert.Equal(3, reach.DaysTo(new MapPosition(35, 0)));
    }

    [Fact]
    public void Reach_IsAtHomeOnTheTerrainOfTheStartsFaction()
    {
        var state = TestWorld.Create(Row);
        var analysis = new MapAnalysis(state);

        Assert.Equal(1250, analysis.Reach(new MapPosition(0, 0), TestWorld.Blue).CostTo(new MapPosition(10, 0)));
        Assert.Equal(1000, analysis.Reach(new MapPosition(0, 0), TestWorld.Blue, TownIds.Haven).CostTo(new MapPosition(10, 0))); // Haven is at home on grass
        Assert.Throws<ArgumentOutOfRangeException>(() => analysis.Reach(new MapPosition(40, 0), TestWorld.Blue));
    }

    [Fact]
    public void Reach_DoesNotCrossWater()
    {
        var state = TestWorld.Create("....~~....");
        var reach = new MapAnalysis(state).Reach(new MapPosition(0, 0), TestWorld.Blue);

        Assert.NotNull(reach.DaysTo(new MapPosition(3, 0)));
        Assert.Null(reach.DaysTo(new MapPosition(4, 0)));
        Assert.Null(reach.DaysTo(new MapPosition(7, 0)));
    }

    [Fact]
    public void GuardStrength_IsTheArmyPowerOfTheMonstersGuardingACell_AnOpenCountTheMiddleOfItsRange()
    {
        var state = TestWorld.Create(Row, Row, Row);
        var wolves = TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 10, 1);
        wolves.Count = 10;
        var open = TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 30, 1);
        var wolf = state.Creatures.Get(BuiltInCreatures.Wolf);
        var analysis = new MapAnalysis(state);

        Assert.Equal(10L * wolf.FightValue, analysis.GuardStrength(new MapPosition(11, 2)));
        Assert.Equal(10L * wolf.FightValue, analysis.GuardStrength(new MapPosition(10, 1))); // the monster's own tile
        Assert.Equal(0, analysis.GuardStrength(new MapPosition(20, 1)));
        var (min, max) = GuardService.CountRange(wolf.Tier);
        Assert.Equal((long)((min + max) / 2) * wolf.FightValue, analysis.GuardStrength(new MapPosition(29, 0)));
        Assert.Equal(analysis.GuardStrength(new MapPosition(29, 0)), MapAnalysis.GuardPower(open, state.Creatures));
    }

    [Fact]
    public void GuardPower_OfARandomMonster_IsTheMeanOfItsCandidates_AndOfAGarrisonItsTroops()
    {
        var state = TestWorld.Create(Row);
        var random = TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 10, 0);
        random.RandomTier = 2;
        random.Count = 10;
        var garrison = TestWorld.Add<MapGarrison>(state, BuiltInMapObjects.Garrison, 20, 0);
        garrison.Troops.Set(0, new CreatureStack(BuiltInCreatures.Footman, 7));

        var candidates = PlaceholderRules.MonsterCandidates(state.Creatures, 2);
        Assert.NotEmpty(candidates);
        Assert.Equal((long)Math.Round(candidates.Average(c => 10.0 * c.FightValue)), MapAnalysis.GuardPower(random, state.Creatures));
        Assert.Equal(7L * state.Creatures.Get(BuiltInCreatures.Footman).FightValue, MapAnalysis.GuardPower(garrison, state.Creatures));
    }

    [Fact]
    public void TreasureValue_IsInGold_AsTheGeneratorValuesIt()
    {
        var state = TestWorld.Create(Row);
        var lumber = TestWorld.Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Lumber), 1, 0);
        lumber.Amount = 5;
        var chest = TestWorld.Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 3, 0);
        chest.FixedValues = new ChestValues(1500, 1000);
        var openGold = TestWorld.Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 5, 0);
        var sign = TestWorld.Add<MapObject>(state, BuiltInMapObjects.Sign, 7, 0);

        Assert.Equal(500, MapAnalysis.TreasureValue(lumber, state.Artifacts));
        Assert.Equal(1500, MapAnalysis.TreasureValue(chest, state.Artifacts));
        Assert.Equal(750, MapAnalysis.TreasureValue(openGold, state.Artifacts)); // 500 to 1000 when the game starts
        Assert.Equal(0, MapAnalysis.TreasureValue(sign, state.Artifacts));

        // A chest of experience only is worth its experience; an open one the rolls' average (1000 … 2500 gold, or a minor artifact).
        chest.FixedValues = new ChestValues(0, 1000);
        Assert.Equal(1000, MapAnalysis.TreasureValue(chest, state.Artifacts));
        var open = TestWorld.Add<TreasureChest>(state, BuiltInMapObjects.TreasureChest, 9, 0);
        Assert.Equal(((30 * 1000) + (30 * 1500) + (25 * 2000) + (10 * 2500) + (5 * MapAnalysis.ArtifactValue(ArtifactTier.Minor))) / 100, MapAnalysis.TreasureValue(open, state.Artifacts));

        // A box: its resources in gold, its experience and its artifacts.
        var box = TestWorld.Add<PandorasBox>(state, BuiltInMapObjects.PandorasBox, 11, 0);
        box.Contents = new BoxContents { Resources = new ResourceSet(Gold: 1000, Ore: 2), Experience = 500 };
        Assert.Equal(1000 + 200 + 500, MapAnalysis.TreasureValue(box, state.Artifacts));
        Assert.Equal(TreasurePlacerValues(), (MapAnalysis.GoldValue(ResourceType.Crystal), MapAnalysis.ArtifactValue(ArtifactTier.Major)));
    }

    /// <summary>The generator values treasure with the analysis's numbers.</summary>
    private static (int, int) TreasurePlacerValues() =>
        (TreasurePlacer.GoldValue(ResourceType.Crystal), TreasurePlacer.ArtifactValue(ArtifactTier.Major));

    [Fact]
    public void Fairness_MeasuresEveryStart_AndWarnsWhereOneIsFarFromTheMean()
    {
        var rows = Enumerable.Repeat(Wide, 10).ToArray();
        var state = TestWorld.Create(rows);
        var blue = TestWorld.AddTown(state, TownIds.Haven, 0, 0, TestWorld.Blue);
        var red = TestWorld.AddTown(state, TownIds.Haven, 72, 0, TestWorld.Red);
        TestWorld.Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 22, 6);
        TestWorld.Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 68, 6);
        TestWorld.Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 5, 8).Amount = 1000;
        TestWorld.Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 74, 8).Amount = 2000;
        var garrison = TestWorld.Add<MapGarrison>(state, BuiltInMapObjects.Garrison, 10, 8); // Blue's: it lets Blue through, it guards against Red
        garrison.Owner = TestWorld.Blue;
        garrison.Troops.Set(0, new CreatureStack(BuiltInCreatures.Footman, 7));

        var fairness = new MapAnalysis(state).Fairness();

        Assert.Equal([TestWorld.Blue, TestWorld.Red], fairness.Starts.Select(s => s.Player));
        var (b, r) = (fairness.Starts[0], fairness.Starts[1]);
        Assert.Equal((blue.VisitPosition, red.VisitPosition), (b.Start, r.Start));
        Assert.Equal(2, b.DaysToMine[ResourceType.Ore]); // Haven is at home on grass: 100 a step
        Assert.Equal(1, r.DaysToMine[ResourceType.Ore]);
        Assert.Equal(1, b.MinesWithin3[ResourceType.Ore]);
        Assert.Equal(b.DaysToTown, r.DaysToTown); // each other's town
        Assert.NotNull(b.DaysToTown);
        Assert.Null(b.DaysToProvider);
        Assert.Equal((1000, 2000), (b.TreasureWithin3, r.TreasureWithin3));
        Assert.Equal(3000, b.TreasureWithin7); // the other start's pile is within a week too
        Assert.Equal(0, b.GuardsWithin7);
        Assert.Equal(MapAnalysis.GuardPower(garrison, state.Creatures), r.GuardsWithin7);

        // 1000 and 2000 are a third from their mean of 1500: both are warned. The mines are a day apart, no more than whole days allow,
        // and the towns are alike.
        Assert.Equal(2, fairness.Warnings.Count(w => w.Contains("treasure within 3 days", StringComparison.Ordinal)));
        Assert.DoesNotContain(fairness.Warnings, w => w.Contains("mine", StringComparison.Ordinal) || w.Contains("town", StringComparison.Ordinal));
    }

    [Fact]
    public void Fairness_WarnsAStartThatReachesNoneWhereAnotherDoes()
    {
        var rows = Enumerable.Repeat(Wide, 10).ToArray();
        var state = TestWorld.Create(rows);
        TestWorld.AddTown(state, TownIds.Haven, 0, 0, TestWorld.Blue);
        TestWorld.AddTown(state, TownIds.Haven, 72, 0, TestWorld.Red);
        TestWorld.Add<ResourceProvider>(state, BuiltInMapObjects.Windmill, 8, 6);
        for (var y = 0; y < 10; y++)
        {
            state.Map.SetTerrain(new MapPosition(40, y), TerrainType.Water); // two islands
        }

        var fairness = new MapAnalysis(state).Fairness();

        Assert.NotNull(fairness.Starts[0].DaysToProvider);
        Assert.Null(fairness.Starts[1].DaysToProvider);
        Assert.Null(fairness.Starts[0].DaysToTown);
        Assert.Contains(fairness.Warnings, w => w.StartsWith("Player 2: days to the nearest resource provider: none", StringComparison.Ordinal));
    }
}
