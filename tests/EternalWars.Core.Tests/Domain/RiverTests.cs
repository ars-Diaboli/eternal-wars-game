using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Generation;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Rivers (ADR-079, MAP-26): an overlay like roads that makes leaving a river tile 25 % dearer, unless the step is road to road.</summary>
public class RiverTests
{
    private static int StepCost(Action<AdventureMap> setUp, Direction direction)
    {
        var state = Create(".....", ".....", "..A..", ".....", "....B");
        setUp(state.Map);
        var session = Start(state);
        var hero = state.GetHero(A);
        var before = hero.MovementPoints;
        Assert.Equal(MoveResult.Moved, session.Step(A, direction));
        return before - hero.MovementPoints;
    }

    [Fact]
    public void LeavingARiverTile_Costs25PercentMore_EnteringOneDoesNot()
    {
        Assert.Equal(100, StepCost(_ => { }, Direction.East));
        Assert.Equal(125, StepCost(m => m.SetRiver(new(2, 2), RiverType.Clear), Direction.East));
        Assert.Equal(176, StepCost(m => m.SetRiver(new(2, 2), RiverType.Muddy), Direction.NorthEast)); // 125 × 1.41
        Assert.Equal(100, StepCost(m => m.SetRiver(new(3, 2), RiverType.Clear), Direction.East));
    }

    [Fact]
    public void ARoadAcrossTheRiver_IsABridge()
    {
        Assert.Equal(50, StepCost(m =>
        {
            m.SetRiver(new(2, 2), RiverType.Icy);
            m.SetRoad(new(2, 2), RoadType.Dirt);
            m.SetRoad(new(3, 2), RoadType.Dirt);
        }, Direction.East));
        Assert.Equal(125, StepCost(m =>
        {
            m.SetRiver(new(2, 2), RiverType.Icy);
            m.SetRoad(new(2, 2), RoadType.Dirt); // the road ends: the river counts
        }, Direction.East));
    }

    [Fact]
    public void Rivers_AreInTheMapFormat_AndTheEditorPaintsAndUndoesThem()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10));
        Assert.Equal(9, editor.PaintRiver(new(4, 4), RiverType.Lava, radius: 1));
        Assert.Equal(0, editor.PaintRiver(new(4, 4), RiverType.Lava));

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));
        Assert.Contains("\"rivers\"", json, StringComparison.Ordinal);
        Assert.Equal(RiverType.Lava, restored.Map.GetRiver(new(5, 5)));
        Assert.Equal(RiverType.None, restored.Map.GetRiver(new(7, 7)));

        editor.Undo();
        Assert.False(editor.Scenario.Map.HasRivers);
        Assert.DoesNotContain("\"rivers\"", MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario)), StringComparison.Ordinal);
    }

    [Fact]
    public void TheGenerator_DrawsRiversAcrossSomeZones_OnFreeGround_TheSameForTheSameSeed()
    {
        var template = BuiltInTemplates.All[0];
        var maps = Enumerable.Range(1, 6).Select(seed => MapGenerator.Generate(template, 72, (ulong)seed)).ToList();

        Assert.Contains(maps, m => m.Rivers.Count >= RiverPlacer.MinLength);
        Assert.All(maps, m => Assert.All(m.Rivers, p => Assert.True(TerrainCatalog.Default.IsPassable(m.Map.GetTerrain(p)))));
        Assert.Equal(maps[0].Rivers.OrderBy(p => (p.Level, p.Y, p.X)).ToList(), MapGenerator.Generate(template, 72, 1).Rivers.OrderBy(p => (p.Level, p.Y, p.X)).ToList());
        Assert.Equal(4, MapGenerator.Version);
    }
}
