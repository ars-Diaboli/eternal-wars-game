using EternalWars.Core.Data.Content;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

public class TerrainAndRoadTests
{
    [Theory]
    [InlineData(TerrainType.Grass, null, 100)]
    [InlineData(TerrainType.Rough, null, 125)]
    [InlineData(TerrainType.Rough, TownIds.Stronghold, 125)] // rough penalises everyone
    [InlineData(TerrainType.Swamp, null, 175)]
    [InlineData(TerrainType.Swamp, TownIds.SwampBestiary, 100)]
    [InlineData(TerrainType.Lava, TownIds.Haven, 150)]
    [InlineData(TerrainType.Lava, TownIds.Inferno, 100)]
    [InlineData(TerrainType.Highlands, TownIds.Haven, 100)]
    [InlineData(TerrainType.ForestFloor, null, 150)]
    [InlineData(TerrainType.Wasteland, TownIds.Necropolis, 100)]
    public void TerrainCatalog_AppliesOpenRoughAndNativeClasses(TerrainType terrain, string? armyTown, int expected)
    {
        Assert.Equal(expected, TerrainCatalog.Default.MovementCost(terrain, armyTown));
    }

    [Fact]
    public void TerrainCatalog_CoversEveryTerrainAndRejectsInconsistentDefinitions()
    {
        Assert.All(Enum.GetValues<TerrainType>(), t => Assert.NotNull(TerrainCatalog.Default.Get(t)));
        Assert.False(TerrainCatalog.Default.IsPassable(TerrainType.Water));
        Assert.Throws<FormatException>(() => new TerrainCatalog([new(TerrainType.Grass, TerrainClass.Native, 100, [])]));
        Assert.Throws<ArgumentException>(() => new TerrainCatalog([new(TerrainType.Grass, TerrainClass.Open, 100, [])]));
    }

    [Fact]
    public void TerrainRules_CanBeOverriddenFromJson()
    {
        var overrides = TerrainDefinitionSerializer.Deserialize("""
            [ { "terrain": "Rough", "class": "Rough", "cost": 140 } ]
            """);
        var terrains = TerrainCatalog.Default.With(overrides);
        var state = new GameState(
            new AdventureMap(3, 1, fill: TerrainType.Rough),
            [new Player(Blue, "Blue", true)],
            [new Hero(A, "Alpha", Blue, new MapPosition(0, 0))],
            terrains: terrains);
        var session = Start(state);

        session.Movement.TryStep(A, Direction.East);

        Assert.Equal(1500 - 140, state.GetHero(A).MovementPoints);
        Assert.Contains("\"class\": \"Native\"", TerrainDefinitionSerializer.Serialize(TerrainCatalog.Default.All));
    }

    [Fact]
    public void Lava_PenalisesArmiesThatAreNotNative()
    {
        var session = Start(Create("Al..", "...B"));

        session.Movement.TryStep(A, Direction.East);  // leaving grass: 100
        session.Movement.TryStep(A, Direction.East);  // leaving lava: 150 (no army is native yet)

        Assert.Equal(1500 - 100 - 150, session.State.GetHero(A).MovementPoints);
    }

    [Fact]
    public void Road_CostAppliesOnlyFromRoadToRoad()
    {
        var state = Create("A....", "....B");
        Road(state, 0, 1, 2, RoadType.Gravel);
        var session = Start(state);
        var hero = state.GetHero(A);

        session.Movement.TryStep(A, Direction.East); // grass -> road: pays grass 100
        session.Movement.TryStep(A, Direction.East); // road -> road: gravel 65
        session.Movement.TryStep(A, Direction.East); // road -> grass: pays the terrain under the road, 100

        Assert.Equal(1500 - 100 - 65 - 100, hero.MovementPoints);
    }

    [Fact]
    public void PathFinder_PrefersRoadDetour()
    {
        var state = Create(
            "A.....",
            "......",
            ".....B");
        Road(state, 1, 0, 5);
        var session = Start(state);

        var path = session.Movement.FindPath(A, new MapPosition(5, 0))!;

        // Diagonal onto the road (141), 3 road steps (3 × 50), diagonal off it (141) = 432 < 500 straight.
        Assert.Equal(141 + 150 + 141, path.TotalCost);
        Assert.Contains(path.Steps, s => s.Position.Y == 1);
    }

    [Fact]
    public void Editor_PaintsAndErasesRoadsWithUndo()
    {
        var editor = new MapEditorSession(BlankScenario.Create(10, 8));
        var painted = new List<RoadsPainted>();
        editor.Events.Subscribe<RoadsPainted>(painted.Add);

        Assert.Equal(9, editor.PaintRoad(new MapPosition(4, 4), RoadType.Cobblestone, radius: 1));
        Assert.Equal(1, editor.PaintRoad(new MapPosition(4, 4), RoadType.None));
        editor.Undo();
        editor.Undo();

        Assert.False(editor.Scenario.Map.HasRoads);
        Assert.Equal(4, painted.Count);
    }

    [Fact]
    public void Validator_WarnsAboutRoadsOnImpassableTerrain()
    {
        var editor = new MapEditorSession(BlankScenario.Create(10, 8));
        editor.PaintTerrain(new MapPosition(5, 5), TerrainType.Water);
        editor.PaintRoad(new MapPosition(5, 5), RoadType.Dirt);

        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("Road on impassable"));
    }
}
