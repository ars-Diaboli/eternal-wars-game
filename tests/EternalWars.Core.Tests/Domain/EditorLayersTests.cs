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

/// <summary>The editor's layers (ADR-262): what is hidden or locked cannot be selected or erased.</summary>
public class EditorLayersTests
{
    [Fact]
    public void Layers_PublishOnlyWhenTheyChange_AndAreNoUndoStep()
    {
        var (editor, _) = Map();
        editor.History.Clear();
        editor.MarkSaved();
        var changes = new List<MapLayersChanged>();
        editor.Events.Subscribe<MapLayersChanged>(changes.Add);

        Assert.True(editor.Layers.Show(MapLayer.Scenery, false));
        Assert.False(editor.Layers.Show(MapLayer.Scenery, false));
        Assert.True(editor.Layers.Lock(MapLayer.Monsters | MapLayer.Heroes, true));
        Assert.True(editor.Layers.Lock(MapLayer.Heroes, false));

        Assert.Equal(
            [
                new MapLayersChanged(MapLayer.Scenery, MapLayer.None),
                new MapLayersChanged(MapLayer.Scenery, MapLayer.Monsters | MapLayer.Heroes),
                new MapLayersChanged(MapLayer.Scenery, MapLayer.Monsters),
            ],
            changes);
        Assert.False(editor.Layers.IsShown(MapLayer.Scenery));
        Assert.True(editor.Layers.IsLocked(MapLayer.Monsters));
        Assert.False(editor.Layers.IsLocked(MapLayer.Monsters | MapLayer.Heroes));
        Assert.Equal(MapLayer.All & ~(MapLayer.Scenery | MapLayer.Monsters), editor.Layers.Editable);
        Assert.False(editor.History.CanUndo);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void TheTerrainAndTheStartZones_OnlyHide_TheyDoNotLock()
    {
        var (editor, _) = Map();

        Assert.False(editor.Layers.Lock(MapLayer.Terrain | MapLayer.StartZones, true));
        Assert.True(editor.Layers.Lock(MapLayer.All, true));

        Assert.Equal(MapLayers.Lockable, editor.Layers.Locked);
        Assert.False(editor.Layers.IsLocked(MapLayer.Terrain));
        Assert.True(editor.Layers.Show(MapLayer.Terrain | MapLayer.StartZones, false));
        Assert.Equal(MapLayer.None, editor.Layers.Editable);
    }

    [Fact]
    public void ALockedOrHiddenKind_IsNotFound_NorSelected()
    {
        var (editor, things) = Map();
        var area = new TileArea(4, 4, 30, 6, 0);
        editor.Layers.Lock(MapLayer.Monsters | MapLayer.Events, true);
        editor.Layers.Show(MapLayer.Heroes, false);

        var found = editor.Selecting.In(area);
        Assert.Equal([things.Chest, things.Scenery, things.Town], found.Objects.OrderBy(o => o.Value));
        Assert.Empty(found.Heroes);
        Assert.Empty(found.Events);
        Assert.NotEmpty(found.Tiles); // tiles are no kind: they stay selectable
        Assert.True(editor.Selecting.At(new MapPosition(7, 5)).IsEmpty);
        Assert.True(editor.Selecting.At(new MapPosition(15, 5)).IsEmpty); // the hidden hero
        Assert.Empty(editor.Selecting.OfKind(things.Monster, area).Objects);
        Assert.DoesNotContain(things.Monster, editor.Selecting.Everything(0).Objects);
        Assert.Empty(editor.Selecting.Everything(0).Heroes);

        // Asked for by id (a jump link, Go to): left out too.
        Assert.False(editor.Selecting.Set(new SelectionItems([], [things.Monster], [things.Hero], ["trap"])));
        Assert.True(editor.Selection.IsEmpty);
    }

    [Fact]
    public void Locking_LetsGoOfWhatIsSelectedOfThatKind_AndUnlockingSelectsNothingAgain()
    {
        var (editor, things) = Map();
        editor.Selecting.Set(new SelectionItems([new MapPosition(1, 1)], [things.Chest, things.Monster], [things.Hero], ["trap"]));
        var changes = 0;
        editor.Events.Subscribe<SelectionChanged>(_ => changes++);

        editor.Layers.Lock(MapLayer.Monsters | MapLayer.Heroes, true);
        editor.Layers.Show(MapLayer.Events, false);

        Assert.Equal([things.Chest], editor.Selection.Objects);
        Assert.Empty(editor.Selection.Heroes);
        Assert.Empty(editor.Selection.Events);
        Assert.Equal([new MapPosition(1, 1)], editor.Selection.Tiles);
        Assert.Equal(2, changes);

        editor.Layers.Lock(MapLayer.All, false);
        editor.Layers.Show(MapLayer.All, true);
        Assert.Equal([things.Chest], editor.Selection.Objects);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void TheEraser_LeavesWhatIsLockedOrHidden_UnderTheBrushAndInTheSelection()
    {
        var (editor, things) = Map();
        var tiles = new TileArea(4, 4, 30, 6, 0).Tiles().ToList();
        editor.Layers.Lock(MapLayer.Monsters | MapLayer.Roads | MapLayer.Events, true);
        editor.Layers.Show(MapLayer.Scenery | MapLayer.Heroes, false);

        // The chest, the town (one cell under the tiles) and the river tile go; the rest stays.
        Assert.Equal(3, editor.Erasing.Erase(tiles, EraseKinds.All));
        Assert.Equal([things.Monster, things.Scenery], editor.Scenario.Objects.Select(o => o.Id).OrderBy(o => o.Value));
        Assert.Equal(RoadType.Dirt, editor.Scenario.Map.GetRoad(new MapPosition(11, 5)));
        Assert.Equal(RiverType.None, editor.Scenario.Map.GetRiver(new MapPosition(13, 5)));
        Assert.Single(editor.Scenario.MapEvents.Set.Cells);
        Assert.Contains(editor.Scenario.Heroes, h => h.Id == things.Hero);

        // The selection holds tiles only (no locked thing can be in it): over them, the same kinds stay.
        editor.Selecting.Set(editor.Selecting.In(new TileArea(4, 4, 30, 6, 0)));
        Assert.Equal(0, editor.Erasing.EraseSelection(EraseKinds.All));

        editor.Layers.Lock(MapLayer.All, false);
        editor.Layers.Show(MapLayer.All, true);
        Assert.Equal(6, editor.Erasing.Erase(tiles, EraseKinds.All)); // the monster, the scenery, two road tiles, the event cell, the hero
    }

    [Fact]
    public void WhatWorksOnTheSelection_LeavesALockedKind_ThatABoxDrewOver()
    {
        var (editor, things) = Map();
        editor.Layers.Lock(MapLayer.Monsters | MapLayer.Scenery, true);
        var area = new TileArea(4, 4, 30, 6, 0);

        Assert.True(editor.Selecting.Set(editor.Selecting.In(area)));
        Assert.False(editor.Selecting.Add(SelectionItems.Of(things.Monster)));
        Assert.DoesNotContain(things.Monster, editor.Selection.Objects);
        Assert.Equal(EditResult.InvalidTarget, editor.Selecting.Rotate()); // the scenery is not selected
        var cut = editor.Fragments.Cut()!;
        Assert.DoesNotContain(cut.Objects, o => o.Id == things.Monster || o.Id == things.Scenery);
        Assert.True(editor.Undo());

        editor.Selecting.Set(editor.Selecting.In(area));
        Assert.True(editor.Selecting.Delete() > 0);
        Assert.Equal([things.Monster, things.Scenery], editor.Scenario.Objects.Select(o => o.Id).OrderBy(o => o.Value));
    }

    [Fact]
    public void ALockedRoad_IsNotPastedOver_NorCopied()
    {
        var (editor, _) = Map();
        var road = new MapPosition(11, 5);
        editor.Selecting.Set(editor.Selecting.In(new TileArea(1, 1, 2, 1, 0))); // plain ground: a paste of it would clear a road
        var plain = editor.Fragments.Copy()!;
        editor.Layers.Lock(MapLayer.Roads, true);

        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(plain, road));
        Assert.Equal(RoadType.Dirt, editor.Scenario.Map.GetRoad(road));

        editor.Selecting.Set(editor.Selecting.In(new TileArea(11, 5, 12, 5, 0)));
        Assert.All(editor.Fragments.Copy()!.Tiles, t => Assert.Equal(RoadType.None, t.Road)); // not taken while locked

        editor.Layers.Lock(MapLayer.Roads, false);
        Assert.Equal(RoadType.Dirt, editor.Fragments.Copy()!.Tiles[0].Road);
        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(plain, road));
        Assert.Equal(RoadType.None, editor.Scenario.Map.GetRoad(road));
    }

    private sealed record Things(MapObjectId Chest, MapObjectId Monster, MapObjectId Scenery, MapObjectId Town, HeroId Hero);

    /// <summary>In rows 4 to 6: a chest, a monster, scenery, a road of two tiles, a river tile, an event cell, a hero, and a town's top row.</summary>
    private static (MapEditorSession Editor, Things Things) Map()
    {
        var editor = new MapEditorSession(BlankScenario.Create(40, 30), copier: MapObjectCopier.Instance);
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(7, 5), BuiltInCreatures.Wolf, 12, GuardDisposition.Hostile, true, out var monster));
        var scenery = Place(editor, editor.Catalog.All.First(d => d.Kind == MapObjectKind.Scenery && d.Footprint.OccupiedCells.Count == 1).Id, 9, 5);
        editor.Terrain.PaintRoad([new MapPosition(11, 5), new MapPosition(12, 5)], RoadType.Dirt);
        editor.Terrain.PaintRiver([new MapPosition(13, 5)], RiverType.Clear);
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(14, 5), "trap"));
        Assert.Equal(EditResult.Ok, editor.Heroes.Place(new PlayerId(0), new MapPosition(15, 5), "Orrin", out var hero));
        var town = Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 20, 6);
        return (editor, new Things(chest, monster, scenery, town, hero));
    }

    private static MapObjectId Place(MapEditorSession editor, string definition, int x, int y)
    {
        Assert.Equal(EditResult.Ok, editor.Objects.Place(definition, new MapPosition(x, y), out var id));
        return id;
    }
}
