using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The editor's Scatter (EDIT-6, ADR-252): the generator's scenery choice on an area.</summary>
public class ScatterEditorTests
{
    private static readonly ScatterOptions Everywhere = new(Density: 100, KeepPathsOpen: false);

    [Fact]
    public void TheSameMapAndSeed_GiveTheSameScatter_AndTheNextOneDiffers()
    {
        var first = Editor();
        var second = Editor();
        var area = new TileArea(5, 5, 24, 19, 0).Tiles().ToList();

        var placed = ScatterEditor.Scatter(first, area);
        Assert.Equal(placed, ScatterEditor.Scatter(second, Enumerable.Reverse(area))); // the order the tiles are named in does not matter

        Assert.InRange(placed, 20, 200);
        Assert.Equal(Snapshot(first), Snapshot(second));
        Assert.Equal(first.Scenario.RandomState, second.Scenario.RandomState);
        Assert.Equal(placed, first.Scenario.Objects.Count(o => o is Scenery));

        // The generator has moved on: the same area of another map of the same seed gets other scenery now.
        var third = Editor();
        ScatterEditor.Scatter(first, new TileArea(26, 5, 38, 19, 0).Tiles());
        ScatterEditor.Scatter(third, new TileArea(26, 5, 38, 19, 0).Tiles());
        Assert.NotEqual(Sceneries(first, 26), Sceneries(third, 26));
    }

    [Fact]
    public void FullDensity_CoversEveryFreeTile_WithSceneryMadeForItsTerrain_InsideTheArea()
    {
        var editor = Editor();
        var area = new TileArea(10, 10, 21, 17, 0);
        editor.Terrain.Paint(new TileArea(16, 10, 21, 17, 0).Tiles(), TerrainType.Swamp);
        editor.History.Clear();

        var placed = ScatterEditor.Scatter(editor, area.Tiles(), Everywhere);

        var scenery = editor.Scenario.Objects.OfType<Scenery>().ToList();
        Assert.Equal(placed, scenery.Count);
        Assert.All(area.Tiles(), t => Assert.IsType<Scenery>(editor.Scenario.ObjectCellAt(t)?.Object));
        Assert.All(scenery, s =>
        {
            Assert.All(s.OccupiedCells(), c => Assert.True(area.Contains(c.Position), $"{s} lies outside the area"));
            Assert.Contains(editor.Scenario.Map.GetTerrain(s.OccupiedCells().First().Position), s.Definition.Terrains);
            Assert.Equal(default, s.Orientation);
        });
        Assert.Contains(scenery, s => s.Definition.Footprint.OccupiedCells.Count > 1); // pieces of several tiles where tiles are side by side
        Assert.True(scenery.Select(s => SceneryPlacer.KindOf(s.Definition)).Distinct().Count() >= 3);
        Assert.Equal(1, editor.History.Count);
        Assert.Equal("Scatter scenery", editor.History.UndoDescription);
    }

    [Fact]
    public void TheDensity_SaysHowMuchOfTheAreaIsCovered()
    {
        var area = new TileArea(2, 4, 37, 25, 0).Tiles().ToList();
        int Covered(int density)
        {
            var editor = Editor();
            ScatterEditor.Scatter(editor, area, new ScatterOptions(density, KeepPathsOpen: false));
            return area.Count(t => editor.Scenario.ObjectCellAt(t) is not null);
        }

        var (thin, half) = (Covered(10), Covered(50));
        Assert.InRange(thin, area.Count * 5 / 100, area.Count * 15 / 100);
        Assert.InRange(half, area.Count * 42 / 100, area.Count * 58 / 100);
        Assert.Equal(area.Count, Covered(100));

        Assert.Throws<ArgumentOutOfRangeException>(() => ScatterEditor.Scatter(Editor(), area, new ScatterOptions(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ScatterEditor.Scatter(Editor(), area, new ScatterOptions(101)));
    }

    [Fact]
    public void OnlyTheChosenKinds_AreScattered_AndWithNoneForTheTerrain_NothingHappens()
    {
        var editor = Editor();
        var area = new TileArea(10, 10, 20, 16, 0).Tiles().ToList();
        string[] kinds = ["bush", "tree"];

        var placed = ScatterEditor.Scatter(editor, area, Everywhere with { Kinds = kinds.ToHashSet() });

        Assert.True(placed > 0);
        Assert.All(editor.Scenario.Objects.OfType<Scenery>(), s => Assert.Contains(SceneryPlacer.KindOf(s.Definition), kinds));
        Assert.Contains("tree", ScatterEditor.Kinds(editor.Catalog));
        Assert.Equal(ScatterEditor.Kinds(editor.Catalog).Order(StringComparer.Ordinal), ScatterEditor.Kinds(editor.Catalog));
        Assert.Equal(12, ScatterEditor.Kinds(editor.Catalog).Count);

        // No such kind, or none of the kind made for the terrain: no piece, no undo step, and the random numbers stay where they were.
        var other = Editor();
        var random = other.Scenario.RandomState;
        Assert.Equal(0, ScatterEditor.Scatter(other, area, Everywhere with { Kinds = new HashSet<string> { "no such kind" } }));
        var onGrass = other.Catalog.OfKind(MapObjectKind.Scenery).Where(d => d.Terrains.Contains(TerrainType.Grass)).Select(SceneryPlacer.KindOf).ToHashSet();
        var notOnGrass = ScatterEditor.Kinds(other.Catalog).First(k => !onGrass.Contains(k));
        Assert.Equal(0, ScatterEditor.Scatter(other, area, Everywhere with { Kinds = new HashSet<string> { notOnGrass } }));
        Assert.False(other.History.CanUndo);
        Assert.Equal(random, other.Scenario.RandomState);
        Assert.DoesNotContain(other.Scenario.Objects, o => o is Scenery);
    }

    [Fact]
    public void WhatIsNotFreeGround_GetsNoScenery()
    {
        var editor = Editor();
        var road = new MapPosition(10, 10);
        var river = new MapPosition(11, 10);
        var water = new MapPosition(12, 10);
        var cell = new MapPosition(13, 10);
        editor.Terrain.PaintRoad(road, RoadType.Dirt);
        editor.Terrain.PaintRiver(river, RiverType.Clear);
        editor.Terrain.Paint(water, TerrainType.Water);
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("here", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(cell, "here"));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.MineId(Core.Domain.Economy.ResourceType.Gold), new MapPosition(15, 9), out var mine));
        var hero = editor.Scenario.Heroes[0];
        Assert.Equal(EditResult.Ok, editor.Heroes.Move(hero.Id, new MapPosition(9, 10)));
        var objects = editor.Scenario.Objects.Count;

        var area = new TileArea(8, 8, 19, 12, 0);
        var placed = ScatterEditor.Scatter(editor, area.Tiles().Append(new MapPosition(-1, 3)).Append(new MapPosition(3, 99)).Append(new MapPosition(3, 3, 5)), Everywhere);

        Assert.Equal(objects + placed, editor.Scenario.Objects.Count);
        Assert.All(new[] { road, river, water, cell, hero.Position }, t => Assert.Null(editor.Scenario.ObjectCellAt(t)));
        Assert.All(editor.Scenario.GetObject(mine).OccupiedCells(), c => Assert.Equal(mine, editor.Scenario.ObjectCellAt(c.Position)!.Value.Object.Id));
        var freeTiles = area.Tiles().Count() - 5 - 6;
        Assert.Equal(freeTiles, area.Tiles().Count(t => editor.Scenario.ObjectCellAt(t)?.Object is Scenery));
    }

    [Fact]
    public void WithPathsKeptOpen_WhateverCouldBeWalkedTo_StillCan()
    {
        var editor = Editor();
        var map = editor.Scenario.Map;

        // A lake with one causeway, a chest in a pocket, a mine against the edge.
        editor.Terrain.Paint(new TileArea(18, 0, 20, 29, 0).Tiles().Where(t => t.Y != 14), TerrainType.Water);
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TreasureChest, new MapPosition(30, 3), out var chest));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.MineId(Core.Domain.Economy.ResourceType.Gold), new MapPosition(30, 27), out var mine));
        var start = editor.Scenario.Heroes[0].Position;
        var before = MapConnectivity.Reachable(editor.Scenario, start);
        Assert.Contains(editor.Scenario.Heroes[1].Position, before);

        var placed = ScatterEditor.Scatter(editor, map.Positions(0), new ScatterOptions(100));

        var after = MapConnectivity.Reachable(editor.Scenario, start);
        Assert.True(placed > 100, $"{placed} pieces");
        Assert.All(before, t => Assert.True(after.Contains(t) || editor.Scenario.ObjectCellAt(t)?.Object is Scenery, $"{t} was cut off"));
        Assert.Contains(editor.Scenario.GetObject(chest).VisitPosition, after);
        Assert.Contains(editor.Scenario.GetObject(mine).VisitPosition, after);
        Assert.Contains(editor.Scenario.Heroes[1].Position, after);
        Assert.Null(editor.Scenario.ObjectCellAt(new MapPosition(19, 14))); // the causeway's middle is the only way across

        // Without the option the same scatter closes the map.
        var closed = Editor();
        ScatterEditor.Scatter(closed, closed.Scenario.Map.Positions(0), Everywhere);
        Assert.Single(MapConnectivity.Reachable(closed.Scenario, start));
    }

    [Fact]
    public void AScatter_IsOneUndoStep_ThatPutsTheRandomNumbersBack_SoTheSameScatterComesAgain()
    {
        var editor = Editor();
        var area = new TileArea(5, 5, 24, 19, 0).Tiles().ToList();
        var before = Snapshot(editor);
        var random = editor.Scenario.RandomState;

        var placed = ScatterEditor.Scatter(editor, area);
        var after = Snapshot(editor);
        var drawn = editor.Scenario.RandomState;

        Assert.True(placed > 1);
        Assert.NotEqual(random, drawn);
        Assert.Equal(1, editor.History.Count);
        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(random, editor.Scenario.RandomState);
        Assert.True(editor.Redo());
        Assert.Equal(after, Snapshot(editor));
        Assert.Equal(drawn, editor.Scenario.RandomState);

        Assert.True(editor.Undo());
        Assert.Equal(placed, ScatterEditor.Scatter(editor, area));
        Assert.Equal(after, Snapshot(editor));
    }

    [Fact]
    public void InsideAStroke_ScattersJoinTheStroke_AndTheSelectionIsAnArea()
    {
        var editor = Editor();
        var before = Snapshot(editor);
        var random = editor.Scenario.RandomState;

        editor.BeginStroke("Scatter");
        var placed = ScatterEditor.Scatter(editor, new TileArea(5, 5, 9, 9, 0).Tiles(), Everywhere)
            + ScatterEditor.Scatter(editor, new TileArea(8, 8, 12, 12, 0).Tiles(), Everywhere);
        editor.EndStroke();

        Assert.True(placed > 2);
        Assert.Equal(1, editor.History.Count);
        Assert.Equal("Scatter", editor.History.UndoDescription);
        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(random, editor.Scenario.RandomState);

        Assert.Equal(0, ScatterEditor.ScatterSelection(editor, Everywhere));
        editor.Selecting.Set(new SelectionItems([.. new TileArea(30, 20, 32, 22, 0).Tiles()], [], [], []));
        Assert.True(ScatterEditor.ScatterSelection(editor, Everywhere) > 0);
        Assert.All(new TileArea(30, 20, 32, 22, 0).Tiles(), t => Assert.NotNull(editor.Scenario.ObjectCellAt(t)));
        Assert.Equal(9, editor.Scenario.Objects.OfType<Scenery>().Sum(s => s.OccupiedCells().Count()));
    }

    private static MapEditorSession Editor() => new(BlankScenario.Create(40, 30), copier: MapObjectCopier.Instance);

    private static string Snapshot(MapEditorSession editor) => MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));

    private static string Sceneries(MapEditorSession editor, int fromX) =>
        string.Join(';', editor.Scenario.Objects.OfType<Scenery>().Where(s => s.Position.X >= fromX).Select(s => $"{s.Definition.Id}@{s.Position.X},{s.Position.Y}"));
}
