using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Resize, crop and shift (EDIT-8, ADR-250).</summary>
public class EditorResizeTests
{
    private static readonly PlayerId Blue = new(0);

    [Fact]
    public void Resize_Grows_AndEverythingKeepsItsPlaceFromTheAnchor()
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 8, 5);
        editor.Terrain.Paint(new MapPosition(4, 4), TerrainType.Snow);
        editor.Terrain.PaintRoad(new MapPosition(5, 4), RoadType.Cobblestone);
        editor.Terrain.PaintRiver(new MapPosition(4, 5), RiverType.Clear);
        editor.Terrain.PaintStartZone(new MapPosition(2, 2), Blue);
        Assert.True(editor.Heights.Raise(new MapPosition(20, 20), 1, 8) > 0);
        var hill = editor.Scenario.Map.GetCorner(0, 20, 20);
        var resized = new List<MapResized>();
        editor.Events.Subscribe<MapResized>(resized.Add);

        Assert.Equal(EditResult.Ok, editor.Resize(50, 40, ResizeAnchor.Centre));

        var map = editor.Scenario.Map;
        Assert.Equal((50, 40), (map.Width, map.Height));
        Assert.Equal([new MapResized(50, 40, 5, 5)], resized);
        Assert.Equal(new MapPosition(13, 10), editor.Scenario.GetObject(chest).Position);
        Assert.Equal(chest, editor.Scenario.ObjectCellAt(new MapPosition(13, 10))!.Value.Object.Id);
        Assert.Null(editor.Scenario.ObjectCellAt(new MapPosition(8, 5)));
        Assert.Equal(TerrainType.Snow, map.GetTerrain(new MapPosition(9, 9)));
        Assert.Equal(RoadType.Cobblestone, map.GetRoad(new MapPosition(10, 9)));
        Assert.Equal(RiverType.Clear, map.GetRiver(new MapPosition(9, 10)));
        Assert.Equal(Blue, map.GetStartZone(new MapPosition(7, 7)));
        Assert.Null(map.GetStartZone(new MapPosition(2, 2)));
        Assert.Equal(hill, map.GetCorner(0, 25, 25));
        Assert.Equal(new MapPosition(6, 6), editor.Scenario.GetHero(new HeroId(1)).Position);
        Assert.Equal(new HeroId(1), editor.Scenario.HeroAt(new MapPosition(6, 6))?.Id);

        // New ground is what a new map has: grass at its height.
        var blank = BlankScenario.Create(50, 40).Map;
        Assert.Equal(TerrainType.Grass, map.GetTerrain(new MapPosition(0, 0)));
        Assert.Equal(TerrainType.Grass, map.GetTerrain(new MapPosition(49, 39)));
        Assert.Equal(blank.GetCorner(0, 0, 0), map.GetCorner(0, 0, 0));
        Assert.Equal(blank.GetCorner(0, 50, 40), map.GetCorner(0, 50, 40));
        Assert.Equal(blank.GetCorner(0, 2, 20), map.GetCorner(0, 2, 20));
    }

    [Theory]
    [InlineData(ResizeAnchor.TopLeft, 0, 0)]
    [InlineData(ResizeAnchor.Top, 5, 0)]
    [InlineData(ResizeAnchor.TopRight, 10, 0)]
    [InlineData(ResizeAnchor.Left, 0, 3)]
    [InlineData(ResizeAnchor.Centre, 5, 3)]
    [InlineData(ResizeAnchor.Right, 10, 3)]
    [InlineData(ResizeAnchor.BottomLeft, 0, 6)]
    [InlineData(ResizeAnchor.Bottom, 5, 6)]
    [InlineData(ResizeAnchor.BottomRight, 10, 6)]
    public void Resize_PutsTheOldMapAtTheAnchor(ResizeAnchor anchor, int dx, int dy)
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 8, 5);

        var check = editor.CheckResize(50, 36, anchor);
        Assert.Equal((EditResult.Ok, 50, 36, dx, dy), (check.Result, check.Width, check.Height, check.Dx, check.Dy));
        Assert.False(check.LosesThings);
        Assert.Equal(EditResult.Ok, editor.Resize(50, 36, anchor));

        Assert.Equal(new MapPosition(8 + dx, 5 + dy), editor.Scenario.GetObject(chest).Position);
    }

    [Theory]
    [InlineData(41, 31)]
    [InlineData(45, 37)]
    [InlineData(46, 36)]
    public void GrowingFromTheMiddle_ThenShrinkingBack_CutsOnlyWhatWasAdded(int width, int height)
    {
        var editor = Editor();
        var corner = Place(editor, BuiltInMapObjects.TreasureChest, 0, 0);
        var far = Place(editor, BuiltInMapObjects.TreasureChest, 39, 29);
        var before = Snapshot(editor);

        Assert.Equal(EditResult.Ok, editor.Resize(width, height, ResizeAnchor.Centre));
        Assert.False(editor.CheckResize(40, 30, ResizeAnchor.Centre).LosesThings);
        Assert.Equal(EditResult.Ok, editor.Resize(40, 30, ResizeAnchor.Centre));

        Assert.Equal(new MapPosition(0, 0), editor.Scenario.GetObject(corner).Position);
        Assert.Equal(new MapPosition(39, 29), editor.Scenario.GetObject(far).Position);
        Assert.Equal(before, Snapshot(editor));
    }

    [Fact]
    public void Resize_Shrinks_RemovingWhatEndsUpOutside_InOneStep_AndUndoPutsItAllBack()
    {
        var editor = Editor();
        var inside = Place(editor, BuiltInMapObjects.TreasureChest, 20, 15);
        var left = Place(editor, BuiltInMapObjects.TreasureChest, 0, 15);
        var right = Place(editor, BuiltInMapObjects.TreasureChest, 39, 15);
        var top = Place(editor, BuiltInMapObjects.TreasureChest, 15, 0);
        var bottom = Place(editor, BuiltInMapObjects.TreasureChest, 15, 29);
        var onTheNewEdge = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5); // the first tile that stays
        var justOutside = Place(editor, BuiltInMapObjects.TreasureChest, 4, 12);
        var town = (Town)editor.Scenario.GetObject(Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 30, 8));
        Assert.Contains(town.OccupiedCells(), c => c.Position.X > 34); // the town straddles the new right edge
        Assert.Contains(town.OccupiedCells(), c => c.Position.X <= 34);
        var gateIn = Place(editor, BuiltInMapObjects.TwoWayMonolith, 12, 12);
        var gateOut = Place(editor, BuiltInMapObjects.TwoWayMonolith, 2, 2);
        Assert.Equal(EditResult.Ok, editor.Objects.LinkGates(gateIn, gateOut));
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(10, 10), "trap"));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(1, 20), "trap"));
        editor.Terrain.PaintStartZone(new MapPosition(1, 1), Blue);
        editor.Terrain.PaintRoad(new MapPosition(0, 0), RoadType.Dirt);
        editor.Selecting.Set(editor.Selecting.In(new TileArea(0, 0, 10, 10, 0)));
        editor.History.Clear();
        var before = Snapshot(editor);

        // Anchored in the middle: columns 5 … 34 and rows 5 … 24 stay.
        var check = editor.CheckResize(30, 20, ResizeAnchor.Centre);
        Assert.Equal((EditResult.Ok, -5, -5), (check.Result, check.Dx, check.Dy));
        Assert.Equal(
            new[] { left, right, top, bottom, justOutside, town.Id, gateOut }.OrderBy(id => id.Value),
            check.Objects.Select(o => o.Id).OrderBy(id => id.Value));
        Assert.Equal([new HeroId(1), new HeroId(2)], check.Heroes.Select(h => h.Id));
        Assert.Equal(1, check.EventCells);
        Assert.True(check.LosesThings);
        Assert.Equal(before, Snapshot(editor)); // a check changes nothing

        Assert.Equal(EditResult.Ok, editor.Resize(30, 20, ResizeAnchor.Centre));

        var scenario = editor.Scenario;
        Assert.Equal((30, 20), (scenario.Map.Width, scenario.Map.Height));
        Assert.Equal(new[] { inside, onTheNewEdge, gateIn }.OrderBy(id => id.Value), scenario.Objects.Select(o => o.Id).OrderBy(id => id.Value));
        Assert.Equal(new MapPosition(15, 10), scenario.GetObject(inside).Position);
        Assert.Equal(new MapPosition(0, 0), scenario.GetObject(onTheNewEdge).Position);
        Assert.Null(((TwoWayGate)scenario.GetObject(gateIn)).LinkedGate);
        Assert.Empty(scenario.Heroes);
        Assert.Equal([new EventCell(5, 5, 0, "trap")], scenario.MapEvents.Set.Cells);
        Assert.False(scenario.Map.HasStartZones);
        Assert.False(scenario.Map.HasRoads);
        Assert.True(editor.Selection.IsEmpty);
        Assert.Equal(1, editor.History.Count);
        Assert.Equal("Resize to 30 × 20", editor.History.UndoDescription);
        MapConverter.ToGameState(MapSerializer.Deserialize(Snapshot(editor))); // what is left is a map
        var after = Snapshot(editor);

        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(gateOut, ((TwoWayGate)editor.Scenario.GetObject(gateIn)).LinkedGate);
        Assert.Equal(gateIn, editor.Scenario.ObjectCellAt(new MapPosition(12, 12))!.Value.Object.Id);
        Assert.Equal(new HeroId(2), editor.Scenario.HeroAt(new MapPosition(38, 28))?.Id);

        Assert.True(editor.Redo());
        Assert.Equal(after, Snapshot(editor));
        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
    }

    [Fact]
    public void Resize_KeepsLinksIdsAndSettingsOfWhatStays()
    {
        var editor = Editor();
        var a = Place(editor, BuiltInMapObjects.TwoWayMonolith, 10, 10);
        var b = Place(editor, BuiltInMapObjects.TwoWayMonolith, 14, 10);
        Assert.Equal(EditResult.Ok, editor.Objects.LinkGates(a, b));
        var town = Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 20, 8);
        Assert.Equal(EditResult.Ok, editor.Towns.SetOwner(town, Blue));
        Assert.Equal(EditResult.Ok, editor.Towns.SetName(town, "Brightwater"));

        Assert.Equal(EditResult.Ok, editor.Resize(34, 24, ResizeAnchor.BottomRight));

        var scenario = editor.Scenario;
        Assert.Equal(b, ((TwoWayGate)scenario.GetObject(a)).LinkedGate);
        Assert.Equal(a, ((TwoWayGate)scenario.GetObject(b)).LinkedGate);
        var kept = (Town)scenario.GetObject(town);
        Assert.Equal((Blue, "Brightwater", new MapPosition(14, 2)), (kept.Owner, kept.Name, kept.Position));
    }

    [Fact]
    public void Resize_MovesTheMonstersGuardWithIt()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(8, 8), BuiltInCreatures.Wolf, 12, GuardDisposition.Hostile, true, out _));
        Assert.True(editor.Scenario.IsGuarded(new MapPosition(9, 8)));

        Assert.Equal(EditResult.Ok, editor.Shift(10, 4));

        Assert.False(editor.Scenario.IsGuarded(new MapPosition(9, 8)));
        Assert.True(editor.Scenario.IsGuarded(new MapPosition(19, 12)));
        Assert.Single(editor.Scenario.GuardsOf(new MapPosition(19, 12)));
    }

    [Fact]
    public void Resize_OnTwoLevels_FillsTheLowerOneWithSubterranean()
    {
        var editor = new MapEditorSession(BlankScenario.Create(30, 30, levels: 2), copier: MapObjectCopier.Instance);
        var below = Place(editor, BuiltInMapObjects.TreasureChest, new MapPosition(10, 10, 1));

        Assert.Equal(EditResult.Ok, editor.Resize(40, 34, ResizeAnchor.Right));

        var map = editor.Scenario.Map;
        Assert.Equal(new MapPosition(20, 12, 1), editor.Scenario.GetObject(below).Position);
        Assert.Equal(TerrainType.Grass, map.GetTerrain(new MapPosition(0, 0)));
        Assert.Equal(TerrainType.Subterranean, map.GetTerrain(new MapPosition(0, 0, 1)));
        Assert.Equal(TerrainType.Subterranean, map.GetTerrain(new MapPosition(39, 33, 1)));
        var blank = BlankScenario.Create(40, 34, levels: 2).Map;
        Assert.Equal(blank.GetCorner(1, 1, 1), map.GetCorner(1, 1, 1));
    }

    [Fact]
    public void Resize_RefusesSizesOutOfRange_AndTheSameSizeIsNoStep()
    {
        var editor = Editor();

        Assert.Equal(EditResult.InvalidValue, editor.Resize(15, 30));
        Assert.Equal(EditResult.InvalidValue, editor.Resize(40, 257));
        Assert.Equal(EditResult.InvalidValue, editor.CheckResize(300, 30).Result);
        Assert.Equal(EditResult.InvalidValue, editor.Resize(50, 30, (ResizeAnchor)99));
        Assert.Equal(EditResult.InvalidValue, editor.Shift(int.MinValue, 0));
        Assert.Equal(EditResult.Ok, editor.Resize(40, 30, ResizeAnchor.Centre));
        Assert.True(editor.CheckResize(40, 30).IsNoChange);
        Assert.Equal(EditResult.Ok, editor.Shift(0, 0));
        Assert.False(editor.History.CanUndo);

        Assert.Equal(EditResult.Ok, editor.Resize(MapSizes.MinDimension, MapSizes.MaxDimension));
        Assert.Equal((16, 256), (editor.Scenario.Map.Width, editor.Scenario.Map.Height));
    }

    [Fact]
    public void Crop_MakesTheBoxRoundTheSelectedTilesTheMap()
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 8, 6);
        var outside = Place(editor, BuiltInMapObjects.TreasureChest, 36, 6);
        editor.Terrain.Paint(new MapPosition(5, 5), TerrainType.Snow);
        Assert.Equal(EditResult.InvalidTarget, editor.Crop());

        editor.Selecting.Set(editor.Selecting.In(new TileArea(5, 5, 9, 9, 0)));
        Assert.Equal(EditResult.InvalidValue, editor.Crop()); // 5 × 5: under the smallest map

        // Two far corners are enough: the box round them counts.
        editor.Selecting.Set(new SelectionItems([new MapPosition(5, 5), new MapPosition(30, 24)], [], [], []));
        var check = editor.CheckCrop();
        Assert.Equal((EditResult.Ok, 26, 20, -5, -5), (check.Result, check.Width, check.Height, check.Dx, check.Dy));
        Assert.Equal([outside], check.Objects.Select(o => o.Id));
        Assert.Equal(EditResult.Ok, editor.Crop());

        var map = editor.Scenario.Map;
        Assert.Equal((26, 20), (map.Width, map.Height));
        Assert.Equal(TerrainType.Snow, map.GetTerrain(new MapPosition(0, 0)));
        Assert.Equal(new MapPosition(3, 1), editor.Scenario.GetObject(chest).Position);
        Assert.Null(editor.Scenario.FindObject(outside));
        Assert.Equal("Crop to 26 × 20", editor.History.UndoDescription);
        Assert.True(editor.Undo());
        Assert.Equal((40, 30), (map.Width, map.Height));
        Assert.NotNull(editor.Scenario.FindObject(outside));
    }

    [Fact]
    public void Shift_MovesTheContent_CutsWhatLeaves_AndBringsNewGroundIn()
    {
        var editor = Editor();
        var stays = Place(editor, BuiltInMapObjects.TreasureChest, 10, 10);
        var leaves = Place(editor, BuiltInMapObjects.TreasureChest, 38, 10);
        editor.Terrain.Paint(new MapPosition(0, 5), TerrainType.Snow);
        editor.Terrain.Paint(new MapPosition(39, 5), TerrainType.Sand);
        var before = Snapshot(editor);

        Assert.Equal(EditResult.InvalidValue, editor.Shift(40, 0));
        Assert.Equal(EditResult.InvalidValue, editor.Shift(0, -30));
        Assert.Equal([leaves], editor.CheckShift(3, -2).Objects.Select(o => o.Id));
        Assert.Equal(EditResult.Ok, editor.Shift(3, -2));

        var map = editor.Scenario.Map;
        Assert.Equal((40, 30), (map.Width, map.Height));
        Assert.Equal(new MapPosition(13, 8), editor.Scenario.GetObject(stays).Position);
        Assert.Null(editor.Scenario.FindObject(leaves));
        Assert.Equal(TerrainType.Snow, map.GetTerrain(new MapPosition(3, 3)));
        Assert.Equal(TerrainType.Grass, map.GetTerrain(new MapPosition(0, 3))); // new ground on the left
        Assert.DoesNotContain(map.Positions(0), p => map.GetTerrain(p) == TerrainType.Sand); // cut on the right
        Assert.DoesNotContain(editor.Scenario.Heroes, h => h.Id == new HeroId(1)); // the hero on row 1 left by the top

        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
    }

    [Fact]
    public void TheGrail_MovesWithTheMap_AndIsTakenAwayWhenTooNearTheNewEdge()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Map.SetGrail(new MapPosition(15, 15)));
        var moved = new List<GrailMoved>();
        editor.Events.Subscribe<GrailMoved>(moved.Add);

        Assert.False(editor.CheckShift(2, 1).LosesGrail);
        Assert.Equal(EditResult.Ok, editor.Shift(2, 1));
        Assert.Equal(new MapPosition(17, 16), editor.Scenario.Grail.Position);
        Assert.Equal(new GrailMoved(new MapPosition(17, 16)), moved[^1]);

        // 30 wide, anchored right: the Grail would be on column 7, nearer the edge than it may lie.
        Assert.True(editor.CheckResize(30, 30, ResizeAnchor.Right).LosesGrail);
        Assert.Equal(EditResult.Ok, editor.Resize(30, 30, ResizeAnchor.Right));
        Assert.Null(editor.Scenario.Grail.Position);

        Assert.True(editor.Undo());
        Assert.Equal(new MapPosition(17, 16), editor.Scenario.Grail.Position);
        Assert.True(editor.Undo());
        Assert.Equal(new MapPosition(15, 15), editor.Scenario.Grail.Position);
    }

    [Fact]
    public void PlacesNamedByEventActions_MoveWithTheMap()
    {
        var editor = Editor();
        var actions = new EventAction[]
        {
            new RevealAction(10, 12, 0, 3),
            new IfAction(new CompareCondition(new NumberExpr(1), CompareOp.Equal, new NumberExpr(1)), [new GiveHeroAction("haven.orrin") { X = 20, Y = 9 }])
            {
                Else = [new GiveHeroAction("haven.orrin")],
            },
        };
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("show", MapEventKind.Hero, actions)));

        Assert.Equal(EditResult.Ok, editor.Shift(4, -3));

        var moved = editor.Scenario.MapEvents.Set.Find("show")!.Actions;
        Assert.Equal(new RevealAction(14, 9, 0, 3), moved[0]);
        var branch = (IfAction)moved[1];
        Assert.Equal((24, 6), (((GiveHeroAction)branch.Then[0]).X, ((GiveHeroAction)branch.Then[0]).Y));
        Assert.Null(((GiveHeroAction)branch.Else[0]).X); // "next to the hero" stays so

        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("off the map", StringComparison.Ordinal));

        // A resize that leaves the named places off the map: the validator says so.
        Assert.Equal(EditResult.Ok, editor.Resize(16, 16));
        var warnings = MapValidator.Validate(editor.Scenario).Where(i => i.Event == "show" && i.Message.Contains("off the map", StringComparison.Ordinal)).ToList();
        Assert.Single(warnings); // the reveal at 14, 9 is still on the map
        Assert.All(warnings, w => Assert.Equal((IssueSeverity.Warning, IssueKind.Events), (w.Severity, w.Kind)));
        Assert.Contains(warnings, w => w.Message.Contains("gives a hero at 24, 6", StringComparison.Ordinal));
        Assert.True(editor.Undo());

        Assert.True(editor.Undo());
        Assert.Equal(actions[0], editor.Scenario.MapEvents.Set.Find("show")!.Actions[0]);
    }

    [Fact]
    public void EditsAfterAResize_UndoInOrder_BackToTheFirstMap()
    {
        var editor = Editor();
        var before = Snapshot(editor);

        Assert.Equal(EditResult.Ok, editor.Resize(60, 20, ResizeAnchor.Left));
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 55, 10);
        editor.Terrain.Paint(new MapPosition(58, 2), TerrainType.Snow, 2);
        Assert.True(editor.Heights.Raise(new MapPosition(50, 10), 2, 8) > 0);
        Assert.Equal(EditResult.Ok, editor.Shift(-30, 0));
        Assert.Equal(new MapPosition(25, 10), editor.Scenario.GetObject(chest).Position);
        var after = Snapshot(editor);

        while (editor.Undo())
        {
        }

        Assert.Equal(before, Snapshot(editor));
        while (editor.Redo())
        {
        }

        Assert.Equal(after, Snapshot(editor));
    }

    [Fact]
    public void ANonSquareMap_SavesLoadsAndPlays()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Resize(48, 16, ResizeAnchor.TopLeft));
        Assert.Equal(EditResult.Ok, editor.Heroes.Place(new PlayerId(1), new MapPosition(46, 14), "Far", out var far));
        Place(editor, BuiltInMapObjects.TreasureChest, 47, 15);
        editor.Terrain.PaintRoad(new MapPosition(47, 0), RoadType.Dirt);

        var document = MapSerializer.Deserialize(MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario)));
        Assert.Equal((48, 16), (document.Width, document.Height));
        var state = MapConverter.ToGameState(document);
        Assert.Equal((48, 16), (state.Map.Width, state.Map.Height));
        Assert.Equal(RoadType.Dirt, state.Map.GetRoad(new MapPosition(47, 0)));

        var session = new GameSession(state);
        session.Start();
        session.Step(new HeroId(1), Direction.East);
        Assert.Equal(new MapPosition(2, 1), session.State.GetHero(new HeroId(1)).Position);
        Assert.True(session.State.Fog.IsExplored(new PlayerId(1), new MapPosition(47, 15)));
        Assert.False(session.State.Fog.IsExplored(new PlayerId(0), new MapPosition(47, 15)));
        Assert.NotNull(new EternalWars.Core.Domain.Pathfinding.PathFinder(session.State).FindPath(far, new MapPosition(40, 3), respectFog: false));
    }

    private static MapEditorSession Editor() => new(BlankScenario.Create(40, 30), copier: MapObjectCopier.Instance);

    private static MapObjectId Place(MapEditorSession editor, string definition, int x, int y) => Place(editor, definition, new MapPosition(x, y));

    private static MapObjectId Place(MapEditorSession editor, string definition, MapPosition at)
    {
        Assert.Equal(EditResult.Ok, editor.Objects.Place(definition, at, out var id));
        return id;
    }

    /// <summary>The map file's text: what the editor saves.</summary>
    private static string Snapshot(MapEditorSession editor) => MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
}
