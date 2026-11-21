using System.Text.Json.Nodes;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Scenery rotation and mirroring (EDIT-6, ADR-252).</summary>
public class SceneryOrientationTests
{
    private const string Wall3 = "scenery.wall.h3";
    private const string Wall4 = "scenery.wall.h4";
    private const string Diagonal = "scenery.wall.d2";

    [Fact]
    public void AFootprint_Oriented_IsMirroredThenTurnedClockwise()
    {
        var shape = Footprint.Obstacle(["BB.", "..B"]);

        Assert.Same(shape, shape.Oriented(default));
        Assert.Equal([".B", ".B", "B."], shape.Oriented(new Orientation(1)).Rows);
        Assert.Equal(["B..", ".BB"], shape.Oriented(new Orientation(2)).Rows);
        Assert.Equal([".B", "B.", "B."], shape.Oriented(new Orientation(3)).Rows);
        Assert.Equal([".BB", "B.."], shape.Oriented(new Orientation(0, mirrored: true)).Rows);
        Assert.Equal(["B.", ".B", ".B"], shape.Oriented(new Orientation(1, mirrored: true)).Rows);
        Assert.Same(shape.Oriented(new Orientation(1)), shape.Oriented(new Orientation(1)));
        Assert.False(shape.Oriented(new Orientation(1)).HasVisit);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Orientation(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Orientation(-1));
    }

    [Fact]
    public void RotatedAndFlipped_AreWhatTheyDoToTheShapeAsItLies()
    {
        var shape = Footprint.Obstacle(["BB.", "..B", "..."]);
        var all = Enumerable.Range(0, 4).SelectMany(t => new[] { new Orientation(t), new Orientation(t, mirrored: true) }).ToList();

        Assert.Equal(8, all.Select(o => string.Join('/', shape.Oriented(o).Rows)).Distinct().Count());
        Assert.All(all, o =>
        {
            var lying = shape.Oriented(o).Rows;
            Assert.Equal(lying.Select(r => new string([.. r.Reverse()])), shape.Oriented(o.Flipped()).Rows);
            Assert.Equal(Footprint.Obstacle(lying).Oriented(new Orientation(1)).Rows, shape.Oriented(o.Rotated()).Rows);
            Assert.Equal(o, o.Flipped().Flipped());
            Assert.Equal(o, o.Rotated().Rotated().Rotated().Rotated());
        });
    }

    [Fact]
    public void Rotate_TurnsAPieceRoundTheMiddleOfItsBox_AndFourTurnsBringItBack()
    {
        var editor = Editor();
        var wall = Place(editor, Wall3, 10, 10);
        var moved = new List<MapObjectMoved>();
        editor.Events.Subscribe<MapObjectMoved>(moved.Add);

        Assert.Equal(EditResult.Ok, editor.Objects.Rotate([wall]));

        var piece = (Scenery)editor.Scenario.GetObject(wall);
        Assert.Equal(new Orientation(1), piece.Orientation);
        Assert.Equal(new MapPosition(11, 9), piece.Position);
        Assert.Equal([new MapPosition(11, 9), new MapPosition(11, 10), new MapPosition(11, 11)], Cells(piece));
        Assert.Equal([new MapObjectMoved(wall, new MapPosition(10, 10), new MapPosition(11, 9))], moved);
        Assert.Null(editor.Scenario.ObjectCellAt(new MapPosition(10, 10)));
        Assert.Null(editor.Scenario.ObjectCellAt(new MapPosition(12, 10)));
        Assert.All(Cells(piece), c => Assert.Equal(((MapObject)piece, FootprintCell.Blocked), editor.Scenario.ObjectCellAt(c)!.Value));
        Assert.Equal("Rotate Castle wall", editor.History.UndoDescription);

        for (var turn = 0; turn < 3; turn++)
        {
            Assert.Equal(EditResult.Ok, editor.Objects.Rotate([wall]));
        }

        Assert.Equal(default, piece.Orientation);
        Assert.Equal([new MapPosition(10, 10), new MapPosition(11, 10), new MapPosition(12, 10)], Cells(piece));
    }

    [Fact]
    public void AnEvenPiece_AlsoComesBackAfterFourTurns()
    {
        var editor = Editor();
        var wall = Place(editor, Wall4, 10, 10);
        var before = Snapshot(editor);

        Assert.Equal(EditResult.Ok, editor.Objects.Rotate([wall]));
        Assert.Equal([9, 10, 11, 12], Cells(editor.Scenario.GetObject(wall)).Select(c => c.Y).Order());
        for (var turn = 0; turn < 3; turn++)
        {
            Assert.Equal(EditResult.Ok, editor.Objects.Rotate([wall]));
        }

        Assert.Equal(before, Snapshot(editor));
    }

    [Fact]
    public void Mirror_FlipsAPieceInItsBox()
    {
        var editor = Editor();
        var wall = Place(editor, Diagonal, 20, 10);

        Assert.Equal(EditResult.Ok, editor.Objects.Mirror([wall]));

        var piece = (Scenery)editor.Scenario.GetObject(wall);
        Assert.Equal(new Orientation(0, mirrored: true), piece.Orientation);
        Assert.Equal(new MapPosition(20, 10), piece.Position);
        Assert.Equal([new MapPosition(21, 10), new MapPosition(20, 11)], Cells(piece));
        Assert.Equal("Mirror Castle wall", editor.History.UndoDescription);

        // Mirrored after a turn: the piece as it lies is flipped left to right.
        Assert.Equal(EditResult.Ok, editor.Objects.Rotate([wall]));
        Assert.Equal([new MapPosition(20, 10), new MapPosition(21, 11)], Cells(piece));
        Assert.Equal(EditResult.Ok, editor.Objects.Mirror([wall]));
        Assert.Equal([new MapPosition(21, 10), new MapPosition(20, 11)], Cells(piece));
        Assert.Equal(new Orientation(3, mirrored: false), piece.Orientation);
    }

    [Fact]
    public void ATurn_IsRefused_WhereTheTurnedShapeDoesNotFit_AndNothingChanges()
    {
        var editor = Editor();
        var wall = Place(editor, Wall3, 10, 10);
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 11, 11);
        var atEdge = Place(editor, Wall3, 20, 0);
        var free = Place(editor, Wall3, 30, 20);
        var before = Snapshot(editor);
        var steps = editor.History.Count;

        Assert.Equal(EditResult.Overlaps, editor.Objects.Rotate([wall]));
        Assert.Equal(EditResult.OutOfBounds, editor.Objects.Rotate([atEdge]));
        Assert.Equal(EditResult.Overlaps, editor.Objects.Rotate([free, wall])); // all or none
        Assert.Equal(EditResult.InvalidTarget, editor.Objects.Rotate([chest]));
        Assert.Equal(EditResult.InvalidTarget, editor.Objects.Rotate([free, chest]));
        Assert.Equal(EditResult.InvalidTarget, editor.Objects.Mirror([new MapObjectId(999)]));
        Assert.Equal(EditResult.InvalidTarget, editor.Objects.Rotate([]));

        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(steps, editor.History.Count);

        var check = editor.Objects.CheckTurn([wall]);
        Assert.Equal(EditResult.Overlaps, check.Result);
        Assert.Equal([new MoveCell(new MapPosition(11, 9), true), new MoveCell(new MapPosition(11, 10), true), new MoveCell(new MapPosition(11, 11), false)], check.Cells);

        // A hero stands in the way as an object does.
        var hero = editor.Scenario.Heroes[0];
        Assert.Equal(EditResult.Ok, editor.Heroes.Move(hero.Id, new MapPosition(31, 21)));
        Assert.Equal(EditResult.Overlaps, editor.Objects.Rotate([free]));
    }

    [Fact]
    public void PiecesTurnedTogether_DoNotStandInEachOthersWay_WhereTheyWere()
    {
        var editor = Editor();
        var top = Place(editor, Wall3, 10, 9);
        var bottom = Place(editor, Wall3, 10, 11);
        var middle = Place(editor, Wall3, 10, 10);

        // Alone the middle one would turn onto the other two.
        Assert.Equal(EditResult.Overlaps, editor.Objects.Rotate([middle]));

        // Together they would all come to lie in the same column.
        Assert.Equal(EditResult.Overlaps, editor.Objects.Rotate([top, middle, bottom]));

        Assert.Equal(EditResult.Ok, editor.Objects.Remove(top));
        Assert.Equal(EditResult.Ok, editor.Objects.Remove(bottom));
        var left = Place(editor, "scenery.wall.v3", 9, 9);
        Assert.Equal(EditResult.Overlaps, editor.Objects.Rotate([left]));

        // Each takes the cells the other leaves.
        Assert.Equal(EditResult.Ok, editor.Objects.Rotate([left, middle]));
        Assert.Equal([new MapPosition(8, 10), new MapPosition(9, 10), new MapPosition(10, 10)], Cells(editor.Scenario.GetObject(left)));
        Assert.Equal([new MapPosition(11, 9), new MapPosition(11, 10), new MapPosition(11, 11)], Cells(editor.Scenario.GetObject(middle)));
        Assert.Equal("Rotate 2 pieces of scenery", editor.History.UndoDescription);
    }

    [Fact]
    public void ATurn_IsOneUndoStep_AndThePieceKeepsItsPlaceInTheOrder()
    {
        var editor = Editor();
        var wall = Place(editor, Wall3, 10, 10);
        var diagonal = Place(editor, Diagonal, 20, 10);
        Place(editor, BuiltInMapObjects.TreasureChest, 30, 10);
        var order = editor.Scenario.Objects.Select(o => o.Id).ToList();
        var before = Snapshot(editor);
        var steps = editor.History.Count;

        Assert.Equal(EditResult.Ok, editor.Objects.Rotate([diagonal, wall]));
        Assert.Equal(EditResult.Ok, editor.Objects.Mirror([wall, diagonal]));
        var after = Snapshot(editor);

        Assert.Equal(steps + 2, editor.History.Count);
        Assert.Equal(order, editor.Scenario.Objects.Select(o => o.Id));
        Assert.NotEqual(before, after);
        Assert.True(editor.Undo());
        Assert.True(editor.Undo());
        Assert.Equal(before, Snapshot(editor));
        Assert.Equal(order, editor.Scenario.Objects.Select(o => o.Id));
        Assert.True(editor.Redo());
        Assert.True(editor.Redo());
        Assert.Equal(after, Snapshot(editor));
    }

    [Fact]
    public void TheSelection_TurnsItsScenery_AndLeavesTheRestAsItIs()
    {
        var editor = Editor();
        var wall = Place(editor, Wall3, 10, 10);
        var chest = Place(editor, BuiltInMapObjects.TreasureChest, 30, 10);
        var hero = editor.Scenario.Heroes[0];
        editor.Selecting.Set(new SelectionItems([new MapPosition(3, 3)], [wall, chest], [hero.Id], []));

        Assert.Equal(EditResult.Ok, editor.Selecting.CheckTurn().Result);
        Assert.Equal(EditResult.Ok, editor.Selecting.Rotate());
        Assert.Equal(EditResult.Ok, editor.Selecting.Mirror());

        Assert.Equal(new Orientation(3, mirrored: true), ((Scenery)editor.Scenario.GetObject(wall)).Orientation);
        Assert.Equal(new MapPosition(30, 10), editor.Scenario.GetObject(chest).Position);
        Assert.True(editor.Selection.Contains(wall));
        Assert.True(editor.Selection.Contains(chest));
        Assert.True(editor.Selection.Contains(hero.Id));

        editor.Selecting.Set(new SelectionItems([], [chest], [], []));
        Assert.Equal(EditResult.InvalidTarget, editor.Selecting.Rotate());
        Assert.Equal(EditResult.InvalidTarget, editor.Selecting.Mirror());
        Assert.Equal(EditResult.InvalidTarget, editor.Selecting.CheckTurn(mirror: true).Result);
    }

    [Fact]
    public void ATurnedPiece_SavesAndLoads_BlocksWhereItLies_AndIsCopiedAsItLies()
    {
        var editor = Editor();
        var wall = Place(editor, Wall3, 10, 10);
        var diagonal = Place(editor, Diagonal, 20, 10);
        var plain = Place(editor, Wall3, 30, 20);
        Assert.Equal(EditResult.Ok, editor.Objects.Rotate([wall]));
        Assert.Equal(EditResult.Ok, editor.Objects.Mirror([diagonal]));

        var json = Snapshot(editor);
        var loaded = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(new Orientation(1), ((Scenery)loaded.GetObject(wall)).Orientation);
        Assert.Equal(new Orientation(0, mirrored: true), ((Scenery)loaded.GetObject(diagonal)).Orientation);
        Assert.Equal(default, ((Scenery)loaded.GetObject(plain)).Orientation);
        Assert.Equal(Cells(editor.Scenario.GetObject(wall)), Cells(loaded.GetObject(wall)));
        Assert.Equal(json, MapSerializer.Serialize(MapConverter.ToDocument(loaded)));
        Assert.NotNull(loaded.ObjectCellAt(new MapPosition(11, 11)));
        Assert.Null(loaded.ObjectCellAt(new MapPosition(10, 10)));

        // The file says only what is not the default.
        var objects = JsonNode.Parse(json)!["objects"]!.AsArray().ToDictionary(o => (int)o!["id"]!, o => o!.AsObject());
        Assert.Equal(1, (int)objects[wall.Value]["turns"]!);
        Assert.False(objects[wall.Value].ContainsKey("mirrored"));
        Assert.True((bool)objects[diagonal.Value]["mirrored"]!);
        Assert.False(objects[diagonal.Value].ContainsKey("turns"));
        Assert.False(objects[plain.Value].ContainsKey("turns") || objects[plain.Value].ContainsKey("mirrored"));

        // What a player remembers of it, the minimap and the game's digest follow the lying shape.
        var known = KnownObject.Of(loaded.GetObject(wall));
        Assert.Equal(Cells(loaded.GetObject(wall)), known.Cells());
        Assert.NotEqual(GameStateDigest.Describe(BlankWith(Wall3, 11, 9)), GameStateDigest.Describe(BlankWith(Wall3, 11, 9, new Orientation(1))));

        // A copy (paste, stamp) lies as its original.
        editor.Selecting.Set(new SelectionItems([], [wall], [], []));
        var fragment = editor.Fragments.Copy()!;
        Assert.Equal(EditResult.Ok, editor.Fragments.Paste(fragment, new MapPosition(5, 20)));
        var copy = editor.Scenario.Objects.OfType<Scenery>().Last();
        Assert.Equal(new Orientation(1), copy.Orientation);
        Assert.Equal(3, Cells(copy).Select(c => c.Y).Distinct().Count());
    }

    [Fact]
    public void TheMapFile_RefusesATurnThatIsNoQuarterTurn_AndAnOlderFilesSceneryIsNotTurned()
    {
        var editor = Editor();
        var wall = Place(editor, Wall3, 10, 10);
        var node = JsonNode.Parse(Snapshot(editor))!.AsObject();
        var scenery = node["objects"]!.AsArray().Single(o => (int)o!["id"]! == wall.Value)!.AsObject();

        scenery["turns"] = 4;
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(MapSerializer.Deserialize(node.ToJsonString())));
        scenery["turns"] = -1;
        Assert.Throws<FormatException>(() => MapConverter.ToGameState(MapSerializer.Deserialize(node.ToJsonString())));

        // Turned so that it would leave the map: refused as any object that does not fit.
        scenery["turns"] = 1;
        scenery["x"] = 38;
        scenery["y"] = 28;
        Assert.ThrowsAny<Exception>(() => MapConverter.ToGameState(MapSerializer.Deserialize(node.ToJsonString())));

        // Format 42 knew no orientation: whatever such a file says under these names, its scenery lies as drawn.
        scenery["x"] = 10;
        scenery["y"] = 10;
        scenery["turns"] = 2;
        scenery["mirrored"] = true;
        node["formatVersion"] = 42;
        var old = MapSerializer.Deserialize(node.ToJsonString());
        Assert.Equal(MapDocument.CurrentFormatVersion, old.FormatVersion);
        Assert.Equal(default, ((Scenery)MapConverter.ToGameState(old).GetObject(wall)).Orientation);
        Assert.True(MapDocument.CurrentFormatVersion >= 43);
    }

    private static MapEditorSession Editor() => new(BlankScenario.Create(40, 30), copier: MapObjectCopier.Instance);

    private static GameState BlankWith(string definition, int x, int y, Orientation orientation = default)
    {
        var state = BlankScenario.Create(40, 30);
        state.AddObject(new Scenery(new MapObjectId(50), MapObjectCatalog.Default.Get(definition), new MapPosition(x, y), orientation));
        return state;
    }

    private static MapObjectId Place(MapEditorSession editor, string definition, int x, int y)
    {
        Assert.Equal(EditResult.Ok, editor.Objects.Place(definition, new MapPosition(x, y), out var id));
        return id;
    }

    private static List<MapPosition> Cells(MapObject mapObject) => [.. mapObject.OccupiedCells().Select(c => c.Position)];

    private static string Snapshot(MapEditorSession editor) => MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
}
