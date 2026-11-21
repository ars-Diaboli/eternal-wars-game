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

/// <summary>The editor's selection, and deleting and moving what is selected (EDIT-1, ADR-250).</summary>
public class EditorSelectionTests
{
    private static readonly PlayerId Blue = new(0);
    private static readonly PlayerId Red = new(1);

    [Fact]
    public void Selecting_PublishesOnlyWhenTheSelectionChanges_AndIsNoUndoStep()
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        editor.History.Clear();
        editor.MarkSaved();
        var changes = 0;
        editor.Events.Subscribe<SelectionChanged>(_ => changes++);

        Assert.True(editor.Selecting.Set(SelectionItems.Of(chest)));
        Assert.False(editor.Selecting.Set(SelectionItems.Of(chest)));
        Assert.True(editor.Selecting.Add(SelectionItems.Empty with { Tiles = [new MapPosition(1, 1)] }));
        Assert.False(editor.Selecting.Add(SelectionItems.Of(chest)));
        Assert.True(editor.Selecting.Remove(SelectionItems.Of(chest)));
        Assert.False(editor.Selecting.Remove(SelectionItems.Of(chest)));
        Assert.True(editor.Selecting.Clear());
        Assert.False(editor.Selecting.Clear());

        Assert.Equal(4, changes);
        Assert.True(editor.Selection.IsEmpty);
        Assert.False(editor.History.CanUndo);
        Assert.False(editor.IsDirty);
    }

    [Fact]
    public void Set_LeavesOutWhatIsNotOnTheMap()
    {
        var editor = Editor();

        editor.Selecting.Set(new SelectionItems([new MapPosition(-1, 0), new MapPosition(2, 2)], [new MapObjectId(999)], [new HeroId(999)], ["nothing"]));

        Assert.Equal([new MapPosition(2, 2)], editor.Selection.Tiles);
        Assert.Empty(editor.Selection.Objects);
        Assert.Empty(editor.Selection.Heroes);
        Assert.Empty(editor.Selection.Events);
    }

    [Fact]
    public void In_FindsTheTilesAndTheThingsWithACellInTheArea_ThroughTheFilter()
    {
        var editor = Editor();
        var town = Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 10, 10);
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(6, 5), BuiltInCreatures.Wolf, 10, GuardDisposition.Aggressive, false, out var monster));
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(7, 5), "trap"));
        var hero = editor.Scenario.Heroes[0];
        var area = TileArea.Between(new MapPosition(7, 6), new MapPosition(5, 4)); // any two corners

        var all = editor.Selecting.In(area);
        Assert.Equal(9, all.Tiles.Count);
        Assert.Equal([chest, monster], all.Objects.OrderBy(o => o.Value));
        Assert.Equal(["trap"], all.Events);
        Assert.Empty(all.Heroes);

        Assert.Equal([monster], editor.Selecting.In(area, SelectionFilter.Monsters).Objects);
        Assert.Empty(editor.Selecting.In(area, SelectionFilter.Monsters).Events);
        Assert.Equal([chest], editor.Selecting.In(area, SelectionFilter.Objects | SelectionFilter.Events).Objects);

        // One cell of the town's footprint in the area is enough; the area's tiles off the map are left out.
        var corner = editor.Scenario.GetObject(town).OccupiedCells().First().Position;
        Assert.Equal([town], editor.Selecting.In(TileArea.Between(corner, corner)).Objects);
        Assert.Equal(4, editor.Selecting.In(TileArea.Between(new MapPosition(-3, -3), new MapPosition(1, 1))).Tiles.Count);
        Assert.Equal([hero.Id], editor.Selecting.In(TileArea.Between(hero.Position, hero.Position)).Heroes);
    }

    [Fact]
    public void At_PicksTheHeroThenTheObjectThenTheEvents()
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(7, 5), "trap"));
        var hero = editor.Scenario.Heroes[0];

        Assert.Equal([hero.Id], editor.Selecting.At(hero.Position).Heroes);
        Assert.True(editor.Selecting.At(hero.Position, SelectionFilter.Objects).IsEmpty);
        Assert.Equal([chest], editor.Selecting.At(new MapPosition(5, 5)).Objects);
        Assert.True(editor.Selecting.At(new MapPosition(5, 5), SelectionFilter.Scenery | SelectionFilter.Monsters).IsEmpty);
        Assert.Equal(["trap"], editor.Selecting.At(new MapPosition(7, 5)).Events);
        Assert.True(editor.Selecting.At(new MapPosition(8, 8)).IsEmpty);
    }

    [Fact]
    public void OfKind_FindsEveryObjectOfThatDefinitionInTheArea()
    {
        var editor = Editor();
        var first = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        var second = Place(editor, BuiltInMapObjects.TreasureChest, 8, 5);
        Place(editor, BuiltInMapObjects.TreasureChest, 30, 20); // out of view
        Place(editor, BuiltInMapObjects.Sign, 6, 6);

        var found = editor.Selecting.OfKind(first, TileArea.Between(new MapPosition(0, 0), new MapPosition(12, 12)));

        Assert.Equal([first, second], found.Objects.OrderBy(o => o.Value));
    }

    [Fact]
    public void Everything_SelectsTheThingsOfOneLevel_ThroughTheFilter()
    {
        var editor = new MapEditorSession(BlankScenario.Create(20, 20, levels: 2));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TreasureChest, new MapPosition(3, 3, 1), out var below));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TreasureChest, new MapPosition(3, 3), out var above));

        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("deep", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(5, 5, 1), "deep"));

        var all = editor.Selecting.Everything(0);

        Assert.Equal([above], all.Objects);
        Assert.Equal(editor.Scenario.Heroes.Where(h => h.Position.Level == 0).Select(h => h.Id), all.Heroes);
        Assert.Empty(all.Tiles);
        Assert.Empty(all.Events);
        Assert.Equal([below], editor.Selecting.Everything(1).Objects);
        Assert.Equal(["deep"], editor.Selecting.Everything(1).Events);
        Assert.Empty(editor.Selecting.Everything(0, SelectionFilter.Heroes).Objects);
    }

    [Fact]
    public void AThingThatLeavesTheMap_LeavesTheSelection()
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        var sign = Place(editor, BuiltInMapObjects.Sign, 6, 6);
        var hero = editor.Scenario.Heroes[0].Id;
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        editor.Selecting.Set(new SelectionItems([], [chest, sign], [hero], ["trap"]));
        var changes = 0;
        editor.Events.Subscribe<SelectionChanged>(_ => changes++);

        Assert.Equal(EditResult.Ok, editor.Objects.Remove(chest));
        Assert.True(editor.Undo()); // the chest's removal is undone: it comes back unselected
        Assert.True(editor.Undo()); // the event's adding is undone
        Assert.Equal(EditResult.Ok, editor.Heroes.Remove(hero));

        Assert.Equal([sign], editor.Selection.Objects);
        Assert.Empty(editor.Selection.Heroes);
        Assert.Empty(editor.Selection.Events);
        Assert.Equal(3, changes);
    }

    [Fact]
    public void Delete_RemovesTheSelectedObjectsAndHeroesAsOneStep()
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        var sign = Place(editor, BuiltInMapObjects.Sign, 6, 6);
        var hero = editor.Scenario.Heroes[0].Id;
        var heroes = editor.Scenario.Heroes.Count;
        editor.History.Clear();
        editor.Selecting.Set(new SelectionItems([new MapPosition(1, 1)], [chest, sign], [hero], []));

        var changes = 0;
        editor.Events.Subscribe<SelectionChanged>(_ => changes++);

        Assert.Equal(3, editor.Selecting.Delete());
        Assert.Equal(1, changes); // one change for the whole deletion

        Assert.Equal(1, editor.History.Count);
        Assert.Equal("Delete 3 things", editor.History.UndoDescription);
        Assert.Null(editor.Scenario.ObjectCellAt(new MapPosition(5, 5)));
        Assert.Equal(heroes - 1, editor.Scenario.Heroes.Count);
        Assert.Equal([new MapPosition(1, 1)], editor.Selection.Tiles); // tiles stay selected, the things are gone from it
        Assert.False(editor.Selection.HasThings);

        Assert.True(editor.Undo());
        Assert.Equal(chest, editor.Scenario.ObjectCellAt(new MapPosition(5, 5))!.Value.Object.Id);
        Assert.Equal(sign, editor.Scenario.ObjectCellAt(new MapPosition(6, 6))!.Value.Object.Id);
        Assert.Equal(heroes, editor.Scenario.Heroes.Count);
        Assert.Equal(0, editor.Selecting.Delete()); // nothing selected: no step
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void MoveObject_AndItsUndo_RestoreTheHeightsLinksAndOwners()
    {
        var editor = Editor();
        var townId = Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 3, 3);
        Assert.Equal(EditResult.Ok, editor.Towns.SetOwner(townId, Red));
        var gateA = Place(editor, BuiltInMapObjects.TwoWayMonolith, 20, 3);
        var gateB = Place(editor, BuiltInMapObjects.TwoWayMonolith, 24, 3);
        Assert.Equal(EditResult.Ok, editor.Objects.LinkGates(gateA, gateB));
        Assert.True(editor.Heights.Raise(new MapPosition(22, 18), 3, 8) > 0); // a hill where the town goes
        editor.History.Clear();
        var town = (Town)editor.Scenario.GetObject(townId);
        var order = editor.Scenario.Objects.Select(o => o.Id).ToList();
        var before = Heights(editor);
        var oldCell = town.OccupiedCells().First().Position;
        var moves = new List<MapObjectMoved>();
        editor.Events.Subscribe<MapObjectMoved>(moves.Add);

        Assert.Equal(EditResult.Ok, editor.Objects.Move(townId, new MapPosition(20, 16)));
        Assert.Equal(EditResult.Ok, editor.Objects.Move(gateA, new MapPosition(30, 5)));

        Assert.Equal(new MapPosition(20, 16), town.Position);
        Assert.Equal(Red, town.Owner);
        Assert.Same(town, editor.Scenario.ObjectCellAt(town.VisitPosition)!.Value.Object);
        var newCell = town.OccupiedCells().First().Position;
        Assert.Equal(oldCell.Offset(17, 13), newCell);
        Assert.Null(editor.Scenario.ObjectCellAt(oldCell));
        Assert.Equal(order, editor.Scenario.Objects.Select(o => o.Id)); // the order rules visit objects in stays
        Assert.NotEqual(before, Heights(editor)); // the hill under the town was levelled
        Assert.Equal(gateB, ((TwoWayGate)editor.Scenario.GetObject(gateA)).LinkedGate);
        Assert.Equal(gateA, ((TwoWayGate)editor.Scenario.GetObject(gateB)).LinkedGate);
        Assert.Equal([new MapObjectMoved(townId, new MapPosition(3, 3), new MapPosition(20, 16)), new MapObjectMoved(gateA, new MapPosition(20, 3), new MapPosition(30, 5))], moves);
        Assert.Equal(2, editor.History.Count);

        Assert.True(editor.Undo());
        Assert.True(editor.Undo());

        Assert.Equal(new MapPosition(3, 3), town.Position);
        Assert.Equal(Red, town.Owner);
        Assert.Same(town, editor.Scenario.ObjectCellAt(oldCell)!.Value.Object);
        Assert.Null(editor.Scenario.ObjectCellAt(newCell));
        Assert.Equal(before, Heights(editor));
        Assert.Equal(order, editor.Scenario.Objects.Select(o => o.Id));
        Assert.Equal(gateB, ((TwoWayGate)editor.Scenario.GetObject(gateA)).LinkedGate);
        Assert.Equal(new MapPosition(20, 3), editor.Scenario.GetObject(gateA).Position);

        Assert.True(editor.Redo());
        Assert.Equal(new MapPosition(20, 16), town.Position);
    }

    [Fact]
    public void Move_IsRefusedWhereAPlacementWouldBe()
    {
        var editor = Editor();
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        Place(editor, BuiltInMapObjects.Sign, 8, 5);
        editor.Scenario.Map.SetTerrain(new MapPosition(5, 8), TerrainType.Water);
        var hero = editor.Scenario.Heroes[0];
        editor.History.Clear();

        Assert.Equal(EditResult.OutOfBounds, editor.Objects.Move(chest, new MapPosition(-1, 5)));
        Assert.Equal(EditResult.Overlaps, editor.Objects.Move(chest, new MapPosition(8, 5)));
        Assert.Equal(EditResult.Overlaps, editor.Objects.Move(chest, hero.Position));
        Assert.Equal(EditResult.ImpassableTerrain, editor.Objects.Move(chest, new MapPosition(5, 8)));
        Assert.Equal(EditResult.InvalidValue, editor.Objects.Move(chest, new MapPosition(6, 6, 1)));
        Assert.Equal(EditResult.InvalidTarget, editor.Objects.Move(new MapObjectId(999), new MapPosition(6, 6)));
        Assert.Equal(EditResult.Overlaps, editor.Heroes.Move(hero.Id, new MapPosition(5, 5)));
        Assert.Equal(EditResult.ImpassableTerrain, editor.Heroes.Move(hero.Id, new MapPosition(5, 8)));
        Assert.Equal(EditResult.Ok, editor.Objects.Move(chest, new MapPosition(5, 5))); // where it is: nothing to do

        Assert.Equal(new MapPosition(5, 5), editor.Scenario.GetObject(chest).Position);
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void MoveHero_ToItsOwnTownsEntranceOnly()
    {
        var editor = Editor();
        var mine = (Town)editor.Scenario.GetObject(Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 3, 10));
        var theirs = (Town)editor.Scenario.GetObject(Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 20, 10));
        var hero = editor.Scenario.Heroes.First(h => h.Owner == Blue);
        Assert.Equal(EditResult.Ok, editor.Towns.SetOwner(mine.Id, Blue));
        Assert.Equal(EditResult.Ok, editor.Towns.SetOwner(theirs.Id, Red));
        var from = hero.Position;
        var moves = new List<HeroRepositioned>();
        editor.Events.Subscribe<HeroRepositioned>(moves.Add);

        Assert.Equal(EditResult.Overlaps, editor.Heroes.Move(hero.Id, theirs.VisitPosition));
        Assert.Equal(EditResult.Ok, editor.Heroes.Move(hero.Id, mine.VisitPosition));

        Assert.Equal(mine.VisitPosition, hero.Position);
        Assert.Same(hero, editor.Scenario.HeroAt(mine.VisitPosition));
        Assert.Null(editor.Scenario.HeroAt(from));
        Assert.Equal([new HeroRepositioned(hero.Id, from, mine.VisitPosition)], moves);

        Assert.True(editor.Undo());
        Assert.Equal(from, hero.Position);
        Assert.Same(hero, editor.Scenario.HeroAt(from));
    }

    [Fact]
    public void MoveSelection_MovesEverythingAsOneStep_AndWhatMovesAlongIsNotInTheWay()
    {
        var editor = Editor();
        var left = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        var right = Place(editor, BuiltInMapObjects.TreasureChest, 6, 5);
        var hero = editor.Scenario.Heroes[0];
        var heroFrom = hero.Position;
        editor.History.Clear();
        editor.Selecting.Set(new SelectionItems([new MapPosition(5, 5)], [left, right], [hero.Id], []));

        // One tile to the right: the left chest lands where the right one was.
        Assert.Equal(EditResult.Ok, editor.Selecting.CheckMove(1, 0).Result);
        Assert.Equal(EditResult.Ok, editor.Selecting.Move(1, 0));

        Assert.Equal(new MapPosition(6, 5), editor.Scenario.GetObject(left).Position);
        Assert.Equal(new MapPosition(7, 5), editor.Scenario.GetObject(right).Position);
        Assert.Equal(heroFrom.Offset(1, 0), hero.Position);
        Assert.Equal(1, editor.History.Count);
        Assert.Equal("Move 3 things", editor.History.UndoDescription);
        Assert.Equal([left, right], editor.Selection.Objects.OrderBy(o => o.Value)); // the things stay selected, the tiles are let go
        Assert.Equal([hero.Id], editor.Selection.Heroes);
        Assert.Empty(editor.Selection.Tiles);

        Assert.True(editor.Undo());
        Assert.Equal(new MapPosition(5, 5), editor.Scenario.GetObject(left).Position);
        Assert.Equal(new MapPosition(6, 5), editor.Scenario.GetObject(right).Position);
        Assert.Equal(heroFrom, hero.Position);
    }

    [Fact]
    public void CheckMove_TellsEveryCellAndWhichDoNotFit()
    {
        var editor = Editor();
        var left = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        var right = Place(editor, BuiltInMapObjects.TreasureChest, 6, 5);
        Place(editor, BuiltInMapObjects.Sign, 8, 5);
        editor.Selecting.Set(new SelectionItems([], [left, right], [], []));

        var check = editor.Selecting.CheckMove(2, 0);

        Assert.Equal(EditResult.Overlaps, check.Result);
        Assert.Equal([new MoveCell(new MapPosition(7, 5), true), new MoveCell(new MapPosition(8, 5), false)], check.Cells);
        Assert.Equal(EditResult.Overlaps, editor.Selecting.Move(2, 0));
        Assert.Equal(new MapPosition(5, 5), editor.Scenario.GetObject(left).Position);

        editor.Selecting.Clear();
        Assert.Equal(EditResult.InvalidTarget, editor.Selecting.Move(1, 0));
    }

    [Fact]
    public void MoveSelection_KeepsTheObjectsOrder_TheSelectionAndTheGuardZones_ThroughUndoAndRedo()
    {
        var editor = new MapEditorSession(BlankScenario.Create(40, 30, levels: 2));
        var first = Place(editor, BuiltInMapObjects.TreasureChest, 5, 5);
        Place(editor, BuiltInMapObjects.Sign, 20, 20); // stays, between the moved ones in the order
        Assert.Equal(EditResult.Ok, editor.Monsters.Place(new MapPosition(7, 5), BuiltInCreatures.Wolf, 10, GuardDisposition.Aggressive, false, out var monster));
        Assert.Equal(EditResult.Ok, editor.Objects.Place(BuiltInMapObjects.TreasureChest, new MapPosition(9, 9, 1), out var below));
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        var order = editor.Scenario.Objects.Select(o => o.Id).ToList();
        editor.Selecting.Set(new SelectionItems([], [first, monster, below], [], ["trap"]));
        var changes = 0;
        editor.Events.Subscribe<SelectionChanged>(_ => changes++);

        Assert.Equal(EditResult.Ok, editor.Selecting.Move(3, 2));

        Assert.Equal(new MapPosition(12, 11, 1), editor.Scenario.GetObject(below).Position); // on its own level
        Assert.Equal(order, editor.Scenario.Objects.Select(o => o.Id));
        Assert.True(editor.Scenario.IsGuarded(new MapPosition(10, 8))); // beside the monster's new tile
        Assert.False(editor.Scenario.IsGuarded(new MapPosition(7, 6)));
        Assert.Empty(editor.Selection.Events); // events are let go, the things stay selected
        Assert.Equal(1, changes);

        Assert.True(editor.Undo());
        Assert.Equal(order, editor.Scenario.Objects.Select(o => o.Id));
        Assert.True(editor.Scenario.IsGuarded(new MapPosition(7, 6)));
        Assert.False(editor.Scenario.IsGuarded(new MapPosition(10, 8)));
        Assert.True(editor.Redo());
        Assert.Equal(order, editor.Scenario.Objects.Select(o => o.Id));
        Assert.Equal(new MapPosition(8, 7), editor.Scenario.GetObject(first).Position);
        Assert.Equal([first, monster, below], editor.Selection.Objects.OrderBy(o => o.Value)); // undo and redo leave the selection
        Assert.Equal(1, changes);
    }

    [Fact]
    public void ATownAndTheHeroOnItsEntrance_MoveTogether_ButTheTownAloneNotOntoAHero()
    {
        var editor = Editor();
        var town = (Town)editor.Scenario.GetObject(Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 10, 10));
        var hero = editor.Scenario.Heroes.First(h => h.Owner == Blue);
        var other = editor.Scenario.Heroes.First(h => h.Owner == Red);
        Assert.Equal(EditResult.Ok, editor.Towns.SetOwner(town.Id, Blue));
        Assert.Equal(EditResult.Ok, editor.Heroes.Move(hero.Id, town.VisitPosition));
        Assert.Equal(EditResult.Ok, editor.Heroes.Move(other.Id, town.VisitPosition.Offset(5, 0)));

        editor.Selecting.Set(new SelectionItems([], [town.Id], [hero.Id], []));
        Assert.Equal(EditResult.Ok, editor.Selecting.Move(0, 6));
        Assert.Equal(town.VisitPosition, hero.Position);
        Assert.Same(hero, editor.Scenario.HeroAt(town.VisitPosition));

        // The town alone: its entrance would land on the other player's hero.
        Assert.Equal(EditResult.Overlaps, editor.Objects.Move(town.Id, town.Position.Offset(5, -6)));
    }

    [Fact]
    public void ARenamedEvent_LeavesTheSelection()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        editor.Selecting.Set(SelectionItems.Empty with { Events = ["trap"] });

        Assert.Equal(EditResult.Ok, editor.MapEvents.Replace("trap", new MapEventDefinition("snare", MapEventKind.Hero, [])));

        Assert.Empty(editor.Selection.Events);
    }

    [Fact]
    public void Cells_AreTheTilesAndEveryCellOfTheSelectedThings_EachOnce()
    {
        var editor = Editor();
        var town = Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), 10, 10);
        Assert.Equal(EditResult.Ok, editor.MapEvents.Add(new MapEventDefinition("trap", MapEventKind.Hero, [])));
        Assert.Equal(EditResult.Ok, editor.MapEvents.SetCell(new MapPosition(30, 5), "trap"));
        var hero = editor.Scenario.Heroes[0];
        var townCells = editor.Scenario.GetObject(town).OccupiedCells().Select(c => c.Position).ToList();
        Assert.True(editor.Selecting.Set(new SelectionItems([new MapPosition(2, 2), townCells[0]], [town], [hero.Id], ["trap"])));

        var cells = editor.Selection.Cells(editor.Scenario);

        Assert.Equal(cells.Count, cells.Distinct().Count());
        Assert.Equal(
            [new MapPosition(2, 2), hero.Position, new MapPosition(30, 5), .. townCells],
            cells.OrderBy(c => c == new MapPosition(2, 2) ? 0 : c == hero.Position ? 1 : c == new MapPosition(30, 5) ? 2 : 3 + townCells.IndexOf(c)));
        Assert.Empty(new MapSelection().Cells(editor.Scenario));
    }

    private static MapEditorSession Editor() => new(BlankScenario.Create(40, 30));

    private static MapObjectId Place(MapEditorSession editor, string definition, int x, int y)
    {
        Assert.Equal(EditResult.Ok, editor.Objects.Place(definition, new MapPosition(x, y), out var id));
        return id;
    }

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
