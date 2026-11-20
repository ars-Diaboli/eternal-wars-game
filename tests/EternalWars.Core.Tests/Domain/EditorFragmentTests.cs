using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Fragments and the clipboard, brushes, fill and erase by kind (EDIT-1, EDIT-4, ADR-250).</summary>
public class EditorFragmentTests
{
    // Fragments -------------------------------------------------------------------------------------------------

    [Fact]
    public void Copy_TakesTheTilesTheirCornersAndTheObjectsWithTheirSettings_AndChangesNothing()
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 6, 6);
        Assert.Equal(EditResult.Ok, editor.Contents.SetChestValues(chest, new ChestValues(1500, 1000)));
        editor.Terrain.Paint(new MapPosition(5, 5), TerrainType.Snow);
        editor.Terrain.PaintRoad(new MapPosition(6, 5), RoadType.Cobblestone);
        editor.Terrain.PaintRiver(new MapPosition(5, 6), RiverType.Clear);
        editor.History.Clear();
        var before = Snapshot(editor);

        editor.Selecting.Set(editor.Selecting.In(new TileArea(5, 5, 6, 6, 0)));
        var fragment = editor.Fragments.Copy()!;

        Assert.Equal((2, 2), (fragment.Width, fragment.Height));
        Assert.Equal(
            [
                new FragmentTile(0, 0, TerrainType.Snow, RoadType.None, RiverType.None),
                new FragmentTile(1, 0, TerrainType.Grass, RoadType.Cobblestone, RiverType.None),
                new FragmentTile(0, 1, TerrainType.Grass, RoadType.None, RiverType.Clear),
                new FragmentTile(1, 1, TerrainType.Grass, RoadType.None, RiverType.None),
            ],
            fragment.Tiles);
        Assert.Equal(9, fragment.Corners.Count);
        Assert.All(fragment.Corners, c => Assert.Equal(editor.Scenario.Map.GetCorner(0, 5 + c.X, 5 + c.Y), c.Height));
        var copy = Assert.IsType<TreasureChest>(Assert.Single(fragment.Objects));
        Assert.Equal(new MapPosition(1, 1), copy.Position);
        Assert.Equal(new ChestValues(1500, 1000), copy.FixedValues);
        Assert.NotSame(editor.Scenario.GetObject(chest), copy);
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Copy_OfNothing_IsNull_AndHeroesAreLeftOut()
    {
        var editor = Editor();
        Assert.Null(editor.Fragments.Copy());

        editor.Selecting.Set(SelectionItems.Of(new HeroId(1)));
        Assert.Null(editor.Fragments.Copy());
        Assert.Null(editor.Fragments.Cut());
        Assert.Equal(EditResult.InvalidTarget, editor.Fragments.DuplicateAt(new MapPosition(5, 5)));
    }

    [Fact]
    public void CutThenPasteInPlace_GivesTheSameMap()
    {
        var editor = RichMap();
        var before = Snapshot(editor);

        editor.Selecting.Set(editor.Selecting.In(new TileArea(0, 0, 39, 29, 0), SelectionFilter.All & ~SelectionFilter.Heroes));
        var fragment = editor.Fragments.Cut()!;
        Assert.Empty(editor.Scenario.Objects);
        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(0, 0)));

        Assert.Equal(before, Snapshot(editor));
    }

    [Fact]
    public void CopyThenPasteTheTilesInPlace_ChangesNothing_AndIsNoUndoStep()
    {
        var editor = RichMap();
        editor.History.Clear();
        var before = Snapshot(editor);

        editor.Selecting.Set(editor.Selecting.In(new TileArea(0, 0, 39, 29, 0)));
        var fragment = editor.Fragments.Copy()!;
        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(0, 0), FragmentLayers.All & ~FragmentLayers.Objects));

        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void Paste_GivesNewIds_KeepsLinksInside_AndDropsLinksToOutside()
    {
        var editor = Editor();
        var a = Place(editor, BuiltInMapObjects.TwoWayMonolith, 5, 5);
        var b = Place(editor, BuiltInMapObjects.TwoWayMonolith, 7, 5);
        var entrance = Place(editor, BuiltInMapObjects.OneWayMonolithEntrance, 9, 5);
        var exit = Place(editor, BuiltInMapObjects.OneWayMonolithExit, 20, 5); // outside the copied area
        var lone = Place(editor, BuiltInMapObjects.TwoWayMonolith, 11, 5);
        var far = Place(editor, BuiltInMapObjects.TwoWayMonolith, 22, 5); // outside too
        Assert.Equal(EditResult.Ok, editor.Objects.LinkGates(a, b));
        Assert.Equal(EditResult.Ok, editor.Objects.SetGateExit(entrance, exit));
        Assert.Equal(EditResult.Ok, editor.Objects.LinkGates(lone, far));

        editor.Selecting.Set(editor.Selecting.In(new TileArea(5, 5, 11, 5, 0)));
        var fragment = editor.Fragments.Copy()!;
        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(5, 15)));

        var old = new[] { a, b, entrance, exit, lone, far };
        var pasted = editor.Scenario.Objects.Where(o => !old.Contains(o.Id)).ToList();
        Assert.Equal(4, pasted.Count);
        Assert.Equal(editor.Scenario.Objects.Count, editor.Scenario.Objects.Select(o => o.Id).Distinct().Count());
        var newA = Assert.IsType<TwoWayGate>(editor.Scenario.ObjectCellAt(new MapPosition(5, 15))!.Value.Object);
        var newB = Assert.IsType<TwoWayGate>(editor.Scenario.ObjectCellAt(new MapPosition(7, 15))!.Value.Object);
        Assert.Equal(newB.Id, newA.LinkedGate);
        Assert.Equal(newA.Id, newB.LinkedGate);
        Assert.Null(Assert.IsType<OneWayGateEntrance>(editor.Scenario.ObjectCellAt(new MapPosition(9, 15))!.Value.Object).Exit);
        Assert.Null(Assert.IsType<TwoWayGate>(editor.Scenario.ObjectCellAt(new MapPosition(11, 15))!.Value.Object).LinkedGate);

        // The originals keep their links, and the pasted things are what is selected.
        Assert.Equal(b, ((TwoWayGate)editor.Scenario.GetObject(a)).LinkedGate);
        Assert.Equal(exit, ((OneWayGateEntrance)editor.Scenario.GetObject(entrance)).Exit);
        Assert.Equal(far, ((TwoWayGate)editor.Scenario.GetObject(lone)).LinkedGate);
        Assert.Equal(pasted.Select(o => o.Id).OrderBy(i => i.Value), editor.Selection.Objects.OrderBy(i => i.Value));
        Assert.Equal(7, editor.Selection.Tiles.Count);
    }

    [Fact]
    public void Paste_IsOneUndoStep_AndIdsNeverRepeat()
    {
        var editor = RichMap();
        editor.History.Clear();
        var before = Snapshot(editor);
        editor.Selecting.Set(editor.Selecting.In(new TileArea(4, 4, 12, 9, 0)));
        var fragment = editor.Fragments.Copy()!;
        var steps = 0;
        editor.Events.Subscribe<UndoHistoryChanged>(_ => steps++);

        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(4, 15)));
        var once = Snapshot(editor);
        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(20, 15)));

        Assert.Equal(2, steps);
        Assert.Equal(2, editor.History.Count);
        Assert.Equal("Paste", editor.History.UndoDescription);
        Assert.Equal(editor.Scenario.Objects.Count, editor.Scenario.Objects.Select(o => o.Id).Distinct().Count());
        Assert.Equal(TerrainType.Snow, editor.Scenario.Map.GetTerrain(new MapPosition(4, 15)));
        Assert.Equal(RoadType.Cobblestone, editor.Scenario.Map.GetRoad(new MapPosition(5, 15)));
        Assert.Equal(RiverType.Clear, editor.Scenario.Map.GetRiver(new MapPosition(4, 16)));

        Assert.True(editor.Undo());
        Assert.Equal(once, Snapshot(editor));
        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
        Assert.True(editor.Redo());
        Assert.Equal(once, Snapshot(editor));
    }

    [Fact]
    public void Paste_BringsTheHeights_AndAutoHeightDoesNotShapeTheGroundThen()
    {
        var editor = Editor();
        Assert.True(editor.Heights.Raise(new MapPosition(5, 5), 1, 8) > 0);
        editor.Terrain.Paint(new MapPosition(5, 5), TerrainType.Snow);
        editor.Selecting.Set(editor.Selecting.In(new TileArea(3, 3, 7, 7, 0)));
        var fragment = editor.Fragments.Copy()!;
        var source = Heights(editor);

        Assert.True(editor.Heights.AutoHeight);
        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(23, 13)));

        var map = editor.Scenario.Map;
        var after = Heights(editor);
        for (var y = 0; y <= map.Height; y++)
        {
            for (var x = 0; x <= map.Width; x++)
            {
                var inside = x is >= 23 and <= 28 && y is >= 13 and <= 18;
                Assert.Equal(inside ? source[((y - 10) * (map.Width + 1)) + x - 20] : source[(y * (map.Width + 1)) + x], after[(y * (map.Width + 1)) + x]);
            }
        }
    }

    [Fact]
    public void Paste_OfSomeLayers_PutsOnlyThose()
    {
        var editor = RichMap();
        editor.Selecting.Set(editor.Selecting.In(new TileArea(4, 4, 12, 9, 0)));
        var fragment = editor.Fragments.Copy()!;
        var objects = editor.Scenario.Objects.Count;

        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(4, 15), FragmentLayers.Roads));

        Assert.Equal(objects, editor.Scenario.Objects.Count);
        Assert.Equal(TerrainType.Grass, editor.Scenario.Map.GetTerrain(new MapPosition(4, 15)));
        Assert.Equal(RoadType.Cobblestone, editor.Scenario.Map.GetRoad(new MapPosition(5, 15)));
        Assert.Equal(RiverType.None, editor.Scenario.Map.GetRiver(new MapPosition(4, 16)));
    }

    [Fact]
    public void Paste_IsRefusedWhole_WhenAnObjectDoesNotFit()
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        editor.Terrain.Paint(new MapPosition(4, 5), TerrainType.Snow);
        Place(editor, BuiltInMapObjects.Sign, 21, 15);
        editor.Terrain.Paint(new MapPosition(30, 20), TerrainType.Water);
        editor.Selecting.Set(editor.Selecting.In(new TileArea(4, 5, 5, 5, 0)));
        var fragment = editor.Fragments.Copy()!;
        editor.History.Clear();
        var before = Snapshot(editor);

        var onSign = editor.Fragments.CheckPaste(fragment, new MapPosition(20, 15));
        Assert.Equal(EditResult.Overlaps, onSign.Result);
        Assert.Equal([new MoveCell(new MapPosition(20, 15), true), new MoveCell(new MapPosition(21, 15), false)], onSign.Cells);
        Assert.Equal(EditResult.Overlaps, editor.Fragments.Paste(fragment, new MapPosition(20, 15)));
        Assert.Equal(EditResult.Overlaps, editor.Fragments.Paste(fragment, new MapPosition(0, 1))); // on the blue hero
        Assert.Equal(EditResult.OutOfBounds, editor.Fragments.Paste(fragment, new MapPosition(39, 15)));

        // The chest's entrance on water: refused unless the paste brings its own terrain.
        Assert.Equal(EditResult.ImpassableTerrain, editor.Fragments.Paste(fragment, new MapPosition(29, 20), FragmentLayers.Objects));
        Assert.Equal(before, Snapshot(editor));
        Assert.False(editor.History.CanUndo);
        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(29, 20)));
        Assert.Equal(TerrainType.Grass, editor.Scenario.Map.GetTerrain(new MapPosition(30, 20)));
        Assert.NotEqual(chest, editor.Scenario.ObjectCellAt(new MapPosition(30, 20))!.Value.Object.Id);
    }

    [Fact]
    public void Paste_LeavesOutTheTilesOffTheMap()
    {
        var editor = Editor();
        editor.Terrain.Paint(new MapPosition(5, 5), TerrainType.Snow, radius: 1);
        editor.Selecting.Set(editor.Selecting.In(new TileArea(4, 4, 6, 6, 0)));
        var fragment = editor.Fragments.Copy()!;

        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(38, 28)));

        Assert.Equal(TerrainType.Snow, editor.Scenario.Map.GetTerrain(new MapPosition(39, 29)));
        Assert.Equal(4, editor.Selection.Tiles.Count);
    }

    [Fact]
    public void Paste_KeepsThePlacementLimits()
    {
        var editor = Editor();
        for (var i = 0; i < GrailRules.MaxObelisks - 1; i++)
        {
            Place(editor, BuiltInMapObjects.Obelisk, 2 + (i % 12 * 3), 3 + (i / 12 * 3));
        }

        editor.Selecting.Set(editor.Selecting.In(new TileArea(2, 3, 6, 3, 0)));
        var two = editor.Fragments.Copy()!;
        Assert.Equal(2, two.Objects.Count);

        Assert.Equal(EditResult.TooManyObelisks, editor.Fragments.Paste(two, new MapPosition(2, 20)));
    }

    [Fact]
    public void AFragment_PastesIntoAnotherMap()
    {
        var first = Editor();
        var sign = Place(first, BuiltInMapObjects.Sign, 5, 5);
        Assert.Equal(EditResult.Ok, first.Contents.SetSignMessage(sign, "Here be dragons."));
        first.Selecting.Set(SelectionItems.Of(sign));
        var fragment = first.Fragments.Copy()!;

        var second = Editor();
        Place(second, BuiltInMapObjects.TreasureChest, 3, 3);
        Assert.Equal(EditResult.Ok, second.Fragments.Paste(fragment, new MapPosition(10, 10)));

        var pasted = Assert.IsType<SignPost>(second.Scenario.ObjectCellAt(new MapPosition(10, 10))!.Value.Object);
        Assert.Equal("Here be dragons.", pasted.Message);
        Assert.Equal(new MapObjectId(2), pasted.Id);
        Assert.Single(first.Scenario.Objects);
    }

    [Fact]
    public void APastedObject_LosesTheOwnerTheEventAndTheQuestTheMapDoesNotHave()
    {
        var first = Editor();
        Assert.Equal(EditResult.Ok, first.Players.Add("Green", false, out var green));
        var town = Place(first, BuiltInMapObjects.TownId(TownIds.Haven), 20, 5);
        Assert.Equal(EditResult.Ok, first.Towns.SetOwner(town, green));
        var box = Place(first, BuiltInMapObjects.PandorasBox, 5, 5);
        var hut = Place(first, BuiltInMapObjects.SeerHut, 8, 5);
        Assert.Equal(EditResult.Ok, first.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, first.MapEvents.Add(new MapEventDefinition("errand", MapEventKind.Quest, []) { Stages = [QuestForms.Level(2, "Grow up.")] }));
        Assert.Equal(EditResult.Ok, first.Contents.SetPandorasBoxEvent(box, "trap"));
        Assert.Equal(EditResult.Ok, first.Contents.SetQuestSiteQuest(hut, "errand"));
        first.Selecting.Set(new SelectionItems([], [town, box, hut], [], []));
        var fragment = first.Fragments.Copy()!;

        // In its own map: the owner and the event stay, the quest is the first hut's alone.
        Assert.Equal(EditResult.Ok, first.Fragments.Paste(fragment, new MapPosition(5, 15)));
        var copies = first.Scenario.Objects.Where(o => first.Selection.Contains(o.Id)).ToList();
        Assert.Equal(green, copies.OfType<Town>().Single().Owner);
        Assert.Equal("trap", copies.OfType<PandorasBox>().Single().Event);
        Assert.Null(copies.OfType<QuestSite>().Single().Quest);
        Assert.Equal("errand", ((QuestSite)first.Scenario.GetObject(hut)).Quest);

        // In a map with two players and no events: all three are taken from the copies; the map loads as a game.
        var second = Editor();
        Assert.Equal(EditResult.Ok, second.Fragments.Paste(fragment, new MapPosition(5, 15)));
        Assert.Null(second.Scenario.ObjectsOf<Town>().Single().Owner);
        Assert.Null(second.Scenario.ObjectsOf<PandorasBox>().Single().Event);
        Assert.Null(second.Scenario.ObjectsOf<QuestSite>().Single().Quest);
        MapConverter.ToGameState(MapConverter.ToDocument(second.Scenario));
    }

    [Fact]
    public void ACutQuestObject_KeepsItsQuestWhenPastedBack()
    {
        var editor = Editor();
        var hut = Place(editor, BuiltInMapObjects.SeerHut, 8, 5);
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("errand", MapEventKind.Quest, []) { Stages = [QuestForms.Level(2, "Grow up.")] }));
        Assert.Equal(EditResult.Ok, editor.Contents.SetQuestSiteQuest(hut, "errand"));
        editor.Selecting.Set(SelectionItems.Of(hut));

        var fragment = editor.Fragments.Cut()!;
        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(20, 20)));

        Assert.Equal("errand", editor.Scenario.ObjectsOf<QuestSite>().Single().Quest);
    }

    [Fact]
    public void PastedHeights_LeaveTheGroundOfObjectsThereLevel()
    {
        var editor = Editor();
        editor.Heights.AutoHeight = false;
        Assert.True(editor.Heights.Raise(new MapPosition(6, 22), 2, 8) > 0);
        Assert.True(editor.Heights.Raise(new MapPosition(9, 24), 1, 8) > 0);
        var town = (Town)editor.Scenario.GetObject(Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 20, 5));
        editor.Selecting.Set(editor.Selecting.In(new TileArea(2, 18, 13, 28, 0)));
        var hills = editor.Fragments.Copy()!;
        var flat = Heights(editor);

        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(hills, new MapPosition(17, 1), FragmentLayers.Heights));

        var pasted = Heights(editor);
        Assert.NotEqual(flat, pasted);
        var map = editor.Scenario.Map;
        var under = town.OccupiedCells()
            .SelectMany(c => new[] { (c.Position.X, c.Position.Y), (c.Position.X + 1, c.Position.Y), (c.Position.X, c.Position.Y + 1), (c.Position.X + 1, c.Position.Y + 1) })
            .Select(c => map.GetCorner(0, c.Item1, c.Item2))
            .Distinct();
        Assert.Single(under); // the hills came down round the town, and its own ground is flat again
        Assert.True(editor.Undo());
        Assert.Equal(flat, Heights(editor));
    }

    [Fact]
    public void AFragment_PastesOnTheLevelOfItsNewPlace()
    {
        var editor = new MapEditorSession(BlankScenario.Create(30, 30, levels: 2), copier: MapObjectCopier.Instance);
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        editor.Terrain.PaintRoad(new MapPosition(4, 5), RoadType.Dirt);
        editor.Selecting.Set(editor.Selecting.In(new TileArea(4, 5, 5, 5, 0)));
        var fragment = editor.Fragments.Copy()!;

        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(10, 10, 1)));

        Assert.Equal(RoadType.Dirt, editor.Scenario.Map.GetRoad(new MapPosition(10, 10, 1)));
        Assert.Equal(RoadType.None, editor.Scenario.Map.GetRoad(new MapPosition(10, 10)));
        var copy = editor.Scenario.ObjectCellAt(new MapPosition(11, 10, 1))!.Value.Object;
        Assert.NotEqual(chest, copy.Id);
        Assert.All(editor.Selection.Tiles, t => Assert.Equal(1, t.Level));
    }

    [Fact]
    public void Cut_RemovesTheObjectsAsOneStep_AndUndoPutsThemAndTheirLinksBack()
    {
        var editor = RichMap();
        editor.History.Clear();
        var before = Snapshot(editor);
        editor.Selecting.Set(editor.Selecting.In(new TileArea(4, 4, 12, 9, 0)));
        var count = editor.Scenario.Objects.Count;
        var changes = 0;
        editor.Events.Subscribe<SelectionChanged>(_ => changes++);

        var fragment = editor.Fragments.Cut()!;

        Assert.True(fragment.Objects.Count > 1);
        Assert.Equal(count - fragment.Objects.Count, editor.Scenario.Objects.Count);
        Assert.Equal(1, editor.History.Count);
        Assert.Equal(1, changes);
        Assert.Empty(editor.Selection.Objects);
        Assert.Equal(TerrainType.Snow, editor.Scenario.Map.GetTerrain(new MapPosition(4, 4))); // the ground stays
        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
    }

    [Fact]
    public void DuplicateAt_PastesACopyOfTheSelection_AsOneStep()
    {
        var editor = RichMap();
        editor.History.Clear();
        var town = editor.Scenario.ObjectsOf<Town>().Single();
        editor.Selecting.Set(SelectionItems.Of(town.Id));

        Assert.Equal(EditResult.Ok, editor.Fragments.DuplicateAt(new MapPosition(20, 18)));

        Assert.Equal(1, editor.History.Count);
        Assert.Equal("Duplicate", editor.History.UndoDescription);
        var copy = Assert.Single(editor.Scenario.ObjectsOf<Town>(), t => t != town);
        Assert.Equal("Brightwater", copy.Name);
        Assert.Equal(town.Owner, copy.Owner);
        Assert.Equal([copy.Id], editor.Selection.Objects);
        var cells = copy.OccupiedCells().Select(c => c.Position).ToList();
        Assert.Equal((20, 18), (cells.Min(c => c.X), cells.Min(c => c.Y))); // the box round the copy starts where it was asked for
    }

    [Fact]
    public void ASessionWithoutACopier_RefusesToCopyObjects()
    {
        var editor = new MapEditorSession(BlankScenario.Create(20, 20));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TreasureChest, new MapPosition(5, 5), out var chest));
        editor.Selecting.Set(SelectionItems.Of(chest));

        Assert.Throws<InvalidOperationException>(() => editor.Fragments.Copy());
    }

    // Brushes ---------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1, BrushShape.Square, 1)]
    [InlineData(1, BrushShape.Round, 1)]
    [InlineData(2, BrushShape.Square, 4)]
    [InlineData(2, BrushShape.Round, 4)]
    [InlineData(3, BrushShape.Square, 9)]
    [InlineData(3, BrushShape.Round, 5)]
    [InlineData(4, BrushShape.Round, 12)]
    [InlineData(5, BrushShape.Round, 21)]
    [InlineData(9, BrushShape.Square, 81)]
    [InlineData(9, BrushShape.Round, 61)]
    public void ABrush_CoversItsTiles(int size, BrushShape shape, int tiles)
    {
        var center = new MapPosition(20, 20, 1);
        var covered = new Brush(size, shape).Tiles(center).ToList();

        Assert.Equal(tiles, covered.Count);
        Assert.Equal(tiles, covered.Distinct().Count());
        Assert.Contains(center, covered);
        Assert.All(covered, t => Assert.Equal(1, t.Level));
        Assert.Equal(20 - ((size - 1) / 2), covered.Min(t => t.X));
        Assert.Equal(20 + (size / 2), covered.Max(t => t.X));

        // A round brush is the same turned a quarter and mirrored.
        var offsets = covered.Select(t => (X: t.X - covered.Min(c => c.X), Y: t.Y - covered.Min(c => c.Y))).ToHashSet();
        Assert.All(offsets, o => Assert.Contains((o.Y, o.X), offsets));
        Assert.All(offsets, o => Assert.Contains((size - 1 - o.X, o.Y), offsets));
    }

    [Fact]
    public void ALine_HasNoGaps_AndBothEnds()
    {
        Assert.Equal([new MapPosition(3, 3)], Brush.LineTiles(new MapPosition(3, 3), new MapPosition(3, 3)));
        Assert.Equal(
            [new MapPosition(2, 2), new MapPosition(3, 3), new MapPosition(4, 4)],
            Brush.LineTiles(new MapPosition(2, 2), new MapPosition(4, 4)));
        Assert.Equal(
            [new MapPosition(5, 1), new MapPosition(4, 1), new MapPosition(3, 2), new MapPosition(2, 2), new MapPosition(1, 3), new MapPosition(0, 3)],
            Brush.LineTiles(new MapPosition(5, 1), new MapPosition(0, 3)));

        var steep = Brush.LineTiles(new MapPosition(7, 2, 1), new MapPosition(9, 12, 1)).ToList();
        Assert.Equal(11, steep.Count);
        Assert.Equal(new MapPosition(9, 12, 1), steep[^1]);
        Assert.All(steep.Zip(steep.Skip(1)), pair => Assert.True(Math.Abs(pair.First.X - pair.Second.X) <= 1 && pair.Second.Y - pair.First.Y == 1));
    }

    [Fact]
    public void ALineWithABrush_PaintsEveryTileOnce_AsOneStep()
    {
        var editor = Editor();
        var brush = new Brush(3, BrushShape.Round);

        var tiles = brush.Line(new MapPosition(10, 10), new MapPosition(14, 10));
        Assert.Equal(17, tiles.Count); // five pluses in a row: 5 + 4 × 3
        Assert.Equal(17, editor.Terrain.Paint(tiles, TerrainType.Sand));

        Assert.Equal(1, editor.History.Count);
        Assert.Equal(TerrainType.Sand, editor.Scenario.Map.GetTerrain(new MapPosition(15, 10)));
        Assert.Equal(TerrainType.Grass, editor.Scenario.Map.GetTerrain(new MapPosition(15, 11)));
        Assert.Equal(0, editor.Terrain.Paint(new Brush(5).Tiles(new MapPosition(-9, -9)), TerrainType.Sand)); // off the map: nothing
    }

    [Fact]
    public void Fill_TakesTheJoinedAreaOfOneTerrain_NotAcrossCorners_AsOneStep()
    {
        var editor = new MapEditorSession(BlankScenario.Create(20, 20, levels: 2));
        editor.Heights.AutoHeight = false;

        // A ring of rock round (5..9, 5..9), open only at a corner-to-corner gap.
        var ring = new TileArea(4, 4, 10, 10, 0).Tiles().Where(t => t.X is 4 or 10 || t.Y is 4 or 10).ToList();
        editor.Terrain.Paint(ring, TerrainType.Rock);
        editor.Terrain.Paint(new MapPosition(10, 10), TerrainType.Grass); // the ring's corner: its neighbours inside and outside touch only at corners
        editor.History.Clear();

        Assert.Equal(25, editor.Terrain.Fill(new MapPosition(7, 7), TerrainType.Swamp));

        var map = editor.Scenario.Map;
        Assert.All(new TileArea(5, 5, 9, 9, 0).Tiles(), t => Assert.Equal(TerrainType.Swamp, map.GetTerrain(t)));
        Assert.Equal(TerrainType.Grass, map.GetTerrain(new MapPosition(10, 10)));
        Assert.Equal(TerrainType.Grass, map.GetTerrain(new MapPosition(11, 11)));
        Assert.Equal(TerrainType.Subterranean, map.GetTerrain(new MapPosition(7, 7, 1)));
        Assert.Equal(1, editor.History.Count);
        Assert.Equal("Fill Swamp", editor.History.UndoDescription);

        Assert.Equal(0, editor.Terrain.Fill(new MapPosition(7, 7), TerrainType.Swamp));
        Assert.Equal(0, editor.Terrain.Fill(new MapPosition(-1, 7), TerrainType.Swamp));
        Assert.Equal(1, editor.History.Count);
        Assert.True(editor.Undo());
        Assert.All(new TileArea(5, 5, 9, 9, 0).Tiles(), t => Assert.Equal(TerrainType.Grass, map.GetTerrain(t)));

        // The outside is one area too: everything but the ring and what it closes in.
        Assert.Equal((20 * 20) - 25 - 23, editor.Terrain.Fill(new MapPosition(0, 0), TerrainType.Sand));
        Assert.Equal(TerrainType.Grass, map.GetTerrain(new MapPosition(7, 7)));
    }

    // Erase by kind ---------------------------------------------------------------------------------------------

    [Fact]
    public void Erase_TakesOnlyTheKindsAsked_UnderTheTiles()
    {
        var (editor, things) = ErasableMap();
        var tiles = new TileArea(4, 4, 30, 6, 0).Tiles().ToList();

        Assert.Equal(1, editor.Erasing.Erase(tiles, EraseKinds.Monsters));
        Assert.Null(editor.Scenario.Objects.FirstOrDefault(o => o.Id == things.Monster));
        Assert.Equal(1, editor.Erasing.Erase(tiles, EraseKinds.Scenery));
        Assert.Equal(2, editor.Erasing.Erase(tiles, EraseKinds.Roads));
        Assert.Equal(1, editor.Erasing.Erase(tiles, EraseKinds.Rivers));
        Assert.Equal(1, editor.Erasing.Erase(tiles, EraseKinds.Events));
        Assert.Empty(editor.Scenario.MapEvents.Set.Cells);
        Assert.NotNull(editor.Scenario.MapEvents.Set.Find("trap")); // the event itself stays
        Assert.Equal(1, editor.Erasing.Erase(tiles, EraseKinds.Heroes));
        Assert.Equal(0, editor.Erasing.Erase(tiles, EraseKinds.Heroes | EraseKinds.Monsters | EraseKinds.Scenery | EraseKinds.Roads | EraseKinds.Rivers | EraseKinds.Events));

        // The town has one cell under the tiles: it goes whole.
        Assert.Equal([things.Chest, things.Town], editor.Scenario.Objects.Select(o => o.Id));
        Assert.Equal(2, editor.Erasing.Erase(tiles, EraseKinds.Objects));
        Assert.Empty(editor.Scenario.Objects);
    }

    [Fact]
    public void Erase_IsOneUndoStep_AndJoinsAnOpenStroke()
    {
        var (editor, _) = ErasableMap();
        editor.History.Clear();
        var before = Snapshot(editor);

        Assert.Equal(9, editor.Erasing.Erase(new TileArea(4, 4, 30, 6, 0).Tiles(), EraseKinds.All));
        Assert.Equal(1, editor.History.Count);
        Assert.Equal("Erase", editor.History.UndoDescription);
        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));

        editor.BeginStroke("Erase stroke");
        editor.Erasing.Erase([new MapPosition(5, 5)], EraseKinds.All);
        editor.Erasing.Erase([new MapPosition(7, 5)], EraseKinds.All);
        editor.EndStroke();
        Assert.Equal(1, editor.History.Count);
        Assert.Equal("Erase stroke", editor.History.UndoDescription);
        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));

        Assert.Equal(0, editor.Erasing.Erase([new MapPosition(30, 25), new MapPosition(-3, 0)], EraseKinds.All));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void EraseSelection_TakesTheSelectedThings_TheSelectedEventsCells_AndWhatIsOnTheSelectedTiles()
    {
        var (editor, things) = ErasableMap();
        editor.History.Clear();
        var before = Snapshot(editor);
        editor.Selecting.Set(new SelectionItems([new MapPosition(11, 5), new MapPosition(12, 5)], [things.Chest, things.Monster], [], ["trap"]));
        var changes = 0;
        editor.Events.Subscribe<SelectionChanged>(_ => changes++);

        Assert.Equal(1, editor.Erasing.EraseSelection(EraseKinds.Monsters));
        Assert.Equal([things.Chest], editor.Selection.Objects);
        Assert.Equal(1, changes);

        // The chest, the two road tiles and the event's cell (it is not on a selected tile).
        Assert.Equal(4, editor.Erasing.EraseSelection(EraseKinds.Objects | EraseKinds.Roads | EraseKinds.Events));
        Assert.Empty(editor.Scenario.MapEvents.Set.Cells);
        Assert.Equal(RoadType.None, editor.Scenario.Map.GetRoad(new MapPosition(11, 5)));
        Assert.Equal(2, editor.History.Count);
        Assert.Equal("Erase in the selection", editor.History.UndoDescription);

        Assert.True(editor.Undo());
        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(0, editor.Erasing.EraseSelection(EraseKinds.None));
    }

    [Fact]
    public void ErasingAGate_UnlinksItsPartner()
    {
        var editor = Editor();
        var a = Place(editor, BuiltInMapObjects.TwoWayMonolith, 5, 5);
        var b = Place(editor, BuiltInMapObjects.TwoWayMonolith, 9, 5);
        Assert.Equal(EditResult.Ok, editor.Objects.LinkGates(a, b));

        Assert.Equal(1, editor.Erasing.Erase([new MapPosition(5, 5)], EraseKinds.Objects));

        Assert.Null(((TwoWayGate)editor.Scenario.GetObject(b)).LinkedGate);
        Assert.True(editor.Undo());
        Assert.Equal(a, ((TwoWayGate)editor.Scenario.GetObject(b)).LinkedGate);
    }

    // Helpers ---------------------------------------------------------------------------------------------------

    private static MapEditorSession Editor() => new(BlankScenario.Create(40, 30), copier: MapObjectCopier.Instance);

    /// <summary>Terrain, a road, a river, raised ground, a named town with an owner, a chest with fixed values, linked gates and a monster.</summary>
    private static MapEditorSession RichMap()
    {
        var editor = Editor();
        editor.Terrain.Paint(new MapPosition(4, 4), TerrainType.Snow);
        editor.Terrain.PaintRoad(new MapPosition(5, 4), RoadType.Cobblestone);
        editor.Terrain.PaintRiver(new MapPosition(4, 5), RiverType.Clear);
        Assert.True(editor.Heights.Raise(new MapPosition(6, 6), 1, 6) > 0);
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 8, 5);
        Assert.Equal(EditResult.Ok, editor.Contents.SetChestValues(chest, new ChestValues(1500, 1000)));
        var a = Place(editor, BuiltInMapObjects.TwoWayMonolith, 10, 5);
        var b = Place(editor, BuiltInMapObjects.TwoWayMonolith, 12, 5);
        Assert.Equal(EditResult.Ok, editor.Objects.LinkGates(a, b));
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(8, 8), BuiltInCreatures.Wolf, 12, GuardDisposition.Hostile, true, out _));
        var town = Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 20, 5);
        Assert.Equal(EditResult.Ok, editor.Towns.SetOwner(town, new PlayerId(1)));
        Assert.Equal(EditResult.Ok, editor.Towns.SetName(town, "Brightwater"));
        return editor;
    }

    private sealed record Things(MapObjectId Chest, MapObjectId Monster, MapObjectId Scenery, MapObjectId Town);

    /// <summary>In rows 4 to 6: a chest, a monster, scenery, a road of two tiles, a river tile, an event cell, a hero, and a town's top row.</summary>
    private static (MapEditorSession Editor, Things Things) ErasableMap()
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(7, 5), BuiltInCreatures.Wolf, 12, GuardDisposition.Hostile, true, out var monster));
        var scenery = Place(editor, editor.Catalog.All.First(d => d.Kind == MapObjectKind.Scenery && d.Footprint.OccupiedCells.Count == 1).Id, 9, 5);
        editor.Terrain.PaintRoad([new MapPosition(11, 5), new MapPosition(12, 5)], RoadType.Dirt);
        editor.Terrain.PaintRiver([new MapPosition(13, 5)], RiverType.Clear);
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(14, 5), "trap"));
        Assert.Equal(EditResult.Ok, editor.Heroes.Place(new PlayerId(0), new MapPosition(15, 5), "Orrin", out _));
        var town = Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 20, 6);
        Assert.Contains(editor.Scenario.GetObject(town).OccupiedCells(), c => c.Position.Y == 6 && c.Position.X <= 30);
        return (editor, new Things(chest, monster, scenery, town));
    }

    private static MapObjectId Place(MapEditorSession editor, string definition, int x, int y)
    {
        Assert.Equal(EditResult.Ok, editor.Objects.Place(definition, new MapPosition(x, y), out var id));
        return id;
    }

    /// <summary>The map file's text: what the editor saves.</summary>
    private static string Snapshot(MapEditorSession editor) => MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));

    private static List<int> Heights(MapEditorSession editor)
    {
        var map = editor.Scenario.Map;
        var heights = new List<int>();
        for (var y = 0; y <= map.Height; y++)
        {
            for (var x = 0; x <= map.Width; x++)
            {
                heights.Add(map.GetCorner(0, x, y));
            }
        }

        return heights;
    }
}
