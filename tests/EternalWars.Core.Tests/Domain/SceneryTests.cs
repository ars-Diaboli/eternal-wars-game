using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Generation;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Scenery (ADR-079, MAP-25): blocking decoration from <c>scenery.csv</c>, several sizes per kind, walls as 1×N pieces.</summary>
public class SceneryTests
{
    private static IReadOnlyList<MapObjectDefinition> All => MapObjectCatalog.Default.OfKind(MapObjectKind.Scenery).ToList();

    private static string KindOf(MapObjectDefinition d) => d.Id.Split('.')[1];

    [Fact]
    public void TheTable_HasItsColumns_EveryKindInSeveralSizes_AndNoEntrances()
    {
        Assert.Equal(SceneryTable.Columns, CsvTable.Parse(SceneryTable.EmbeddedTable(), "Scenery", []).Header);
        string[] kinds = ["rock", "tree", "dead-tree", "pond", "bush", "mushrooms", "flowers", "fissure", "mound", "ruins", "bones", "wall"];
        Assert.Equal(kinds.Order(StringComparer.Ordinal), All.Select(KindOf).Distinct().Order(StringComparer.Ordinal));
        Assert.All(kinds, kind => Assert.True(All.Where(d => KindOf(d) == kind).Select(d => d.Footprint.OccupiedCells.Count).Distinct().Count() >= 2, kind));
        Assert.All(All, d =>
        {
            Assert.False(d.Footprint.HasVisit);
            Assert.All(d.Footprint.OccupiedCells, c => Assert.Equal(FootprintCell.Blocked, c.Cell));
            Assert.NotEmpty(d.Terrains);
            Assert.All(d.Terrains, t => Assert.True(TerrainCatalog.Default.IsPassable(t), $"{d.Id} on {t}"));
        });
    }

    [Fact]
    public void Walls_AreStraightPieces_Horizontal_Vertical_AndDiagonal()
    {
        var walls = All.Where(d => KindOf(d) == "wall").ToList();
        Assert.Contains(walls, d => d.Footprint.Height == 1 && d.Footprint.Width >= 2);
        Assert.Contains(walls, d => d.Footprint.Width == 1 && d.Footprint.Height >= 2);
        Assert.Contains(walls, d => d.Footprint.Rows.SequenceEqual(["B.", ".B"]));
        Assert.Contains(walls, d => d.Footprint.Rows.SequenceEqual([".B", "B."]));
        Assert.All(walls, d => Assert.Equal(Math.Max(d.Footprint.Width, d.Footprint.Height), d.Footprint.OccupiedCells.Count)); // 1 × N
        Assert.Throws<FormatException>(() => Footprint.Obstacle(["BV"]));
        Assert.Throws<FormatException>(() => Footprint.Obstacle([".."]));
    }

    [Fact]
    public void Scenery_BlocksEveryCell()
    {
        var state = Create(".........", ".........", "...A.....", ".........", "C.......B");
        Add<Scenery>(state, "scenery.wall.d2", 3, 0); // (3, 0) and (4, 1)
        var session = Start(state);

        Assert.Equal(MoveResult.Blocked, session.Step(A, Direction.NorthEast));
        Assert.Equal(MoveResult.Moved, session.Step(A, Direction.North)); // (3, 1) is the free corner
        Assert.Equal(MoveResult.Blocked, session.Step(A, Direction.North));
    }

    [Fact]
    public void Scenery_IsInTheMapFormat_AndTheEditorPlacesIt()
    {
        var editor = new MapEditorSession(BlankScenario.Create(12, 10));
        Assert.Equal(EditResult.Ok, editor.PlaceObject("scenery.pond.medium", new(2, 2), out _));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Severity == IssueSeverity.Error);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));
        Assert.Contains("\"kind\": \"scenery\"", json, StringComparison.Ordinal);
        Assert.Equal("scenery.pond.medium", Assert.Single(restored.ObjectsOf<Scenery>()).Definition.Id);
    }

    [Fact]
    public void TheGenerator_TurnsRockClumpsIntoScenery_ForTheGround_TheSameForTheSameSeed()
    {
        var template = BuiltInTemplates.All[0];
        var map = MapGenerator.Generate(template, 72, 3);
        var scenery = map.Objects.OfType<Scenery>().ToList();

        Assert.NotEmpty(scenery);
        Assert.All(scenery, s => Assert.All(s.OccupiedCells(), c => Assert.NotEqual(TerrainPainter.WallTerrain, map.Map.GetTerrain(c.Position))));
        Assert.Contains(scenery, s => s.Definition.Id != SceneryPlacer.Fallback);
        Assert.Contains(scenery, s => s.Definition.Footprint.OccupiedCells.Count > 1);
        Assert.Equal(scenery.Select(s => (s.Definition.Id, s.Position)), MapGenerator.Generate(template, 72, 3).Objects.OfType<Scenery>().Select(s => (s.Definition.Id, s.Position)));
    }
}
