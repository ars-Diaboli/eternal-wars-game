using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

public class MapEditorSessionTests
{
    private static MapEditorSession CreateEditor(int levels = 1) => new(BlankScenario.Create(12, 10, levels));

    [Fact]
    public void PaintTerrain_UndoRestoresPreviousTerrainAndRedoReapplies()
    {
        var editor = CreateEditor();
        var map = editor.Scenario.Map;
        map.SetTerrain(new MapPosition(5, 5), TerrainType.Sand);
        var painted = new List<TerrainPainted>();
        editor.Events.Subscribe<TerrainPainted>(painted.Add);

        Assert.Equal(9, editor.PaintTerrain(new MapPosition(5, 5), TerrainType.Snow, radius: 1));
        Assert.True(editor.Undo());

        Assert.Equal(TerrainType.Sand, map.GetTerrain(new MapPosition(5, 5)));
        Assert.Equal(TerrainType.Grass, map.GetTerrain(new MapPosition(4, 4)));
        Assert.Equal(3, painted.Count); // paint + restore grass + restore sand

        Assert.True(editor.Redo());
        Assert.Equal(TerrainType.Snow, map.GetTerrain(new MapPosition(5, 5)));
    }

    [Fact]
    public void Stroke_GroupsEditsIntoOneUndoStep()
    {
        var editor = CreateEditor();
        UndoHistoryChanged? history = null;
        editor.Events.Subscribe<UndoHistoryChanged>(e => history = e);

        editor.BeginStroke("Paint water");
        editor.PaintTerrain(new MapPosition(5, 5), TerrainType.Water);
        editor.PaintTerrain(new MapPosition(6, 5), TerrainType.Water);
        Assert.Null(history); // nothing recorded until the stroke ends
        editor.EndStroke();

        Assert.Equal(new UndoHistoryChanged("Paint water", null), history);
        editor.Undo();
        Assert.Equal(TerrainType.Grass, editor.Scenario.Map.GetTerrain(new MapPosition(5, 5)));
        Assert.Equal(TerrainType.Grass, editor.Scenario.Map.GetTerrain(new MapPosition(6, 5)));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void NewEdit_DiscardsRedo()
    {
        var editor = CreateEditor();
        editor.PaintTerrain(new MapPosition(5, 5), TerrainType.Water);
        editor.Undo();

        editor.PaintTerrain(new MapPosition(6, 6), TerrainType.Sand);

        Assert.False(editor.History.CanRedo);
    }

    [Theory]
    [InlineData(10, 9, EditResult.OutOfBounds)] // 3x2 footprint sticks out
    [InlineData(0, 0, EditResult.Overlaps)]     // covers the blue hero at (1,1)
    public void PlaceObject_RejectsInvalidPlacement(int x, int y, EditResult expected)
    {
        var editor = CreateEditor();

        Assert.Equal(expected, editor.PlaceObject(BuiltInMapObjects.MineId(ResourceType.Gold), new MapPosition(x, y), out _));
        Assert.Empty(editor.Scenario.Objects);
    }

    [Fact]
    public void PlaceObject_RejectsImpassableEntranceAndUnknownDefinition()
    {
        var editor = CreateEditor();
        editor.PaintTerrain(new MapPosition(5, 5), TerrainType.Water);

        Assert.Equal(EditResult.ImpassableTerrain, editor.PlaceObject(BuiltInMapObjects.TreasureChest, new MapPosition(5, 5), out _));
        Assert.Equal(EditResult.InvalidTarget, editor.PlaceObject("no.such.thing", new MapPosition(6, 6), out _));
    }

    [Fact]
    public void PlaceObject_ThenUndo_RemovesIt()
    {
        var editor = CreateEditor();
        var removed = new List<MapObjectRemoved>();
        editor.Events.Subscribe<MapObjectRemoved>(removed.Add);

        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.MineId(ResourceType.Ore), new MapPosition(4, 4), out var id));
        Assert.NotNull(editor.Scenario.ObjectCellAt(new MapPosition(5, 5)));
        editor.Undo();

        Assert.Empty(editor.Scenario.Objects);
        Assert.Null(editor.Scenario.ObjectCellAt(new MapPosition(5, 5)));
        Assert.Equal([new MapObjectRemoved(id)], removed);
    }

    [Fact]
    public void RemovingLinkedGate_UnlinksPartner_UndoRestoresBoth()
    {
        var editor = CreateEditor(levels: 2);
        editor.PlaceObject(BuiltInMapObjects.SubterraneanGate, new MapPosition(4, 4), out var surface);
        editor.PlaceObject(BuiltInMapObjects.SubterraneanGate, new MapPosition(4, 4, 1), out var underground);
        editor.LinkGates(surface, underground);

        editor.RemoveObject(surface);
        Assert.Null(Gate(editor, underground).LinkedGate);

        editor.Undo();
        Assert.Equal(underground, Gate(editor, surface).LinkedGate);
        Assert.Equal(surface, Gate(editor, underground).LinkedGate);
    }

    [Fact]
    public void LinkGates_RelinksAndUndoRestoresPreviousLinks()
    {
        var editor = CreateEditor(levels: 2);
        editor.PlaceObject(BuiltInMapObjects.SubterraneanGate, new MapPosition(4, 4), out var a);
        editor.PlaceObject(BuiltInMapObjects.SubterraneanGate, new MapPosition(4, 4, 1), out var b);
        editor.PlaceObject(BuiltInMapObjects.SubterraneanGate, new MapPosition(8, 4, 1), out var c);
        editor.LinkGates(a, b);

        editor.LinkGates(a, c);
        Assert.Null(Gate(editor, b).LinkedGate);
        Assert.Equal(c, Gate(editor, a).LinkedGate);

        editor.Undo();
        Assert.Equal(b, Gate(editor, a).LinkedGate);
        Assert.Equal(a, Gate(editor, b).LinkedGate);
        Assert.Null(Gate(editor, c).LinkedGate);
        Assert.Equal(EditResult.InvalidTarget, editor.LinkGates(a, a));
    }

    [Fact]
    public void PlaceHero_ValidatesTileAndOwner()
    {
        var editor = CreateEditor();
        editor.PaintTerrain(new MapPosition(5, 5), TerrainType.Water);
        editor.PlaceObject(BuiltInMapObjects.TreasureChest, new MapPosition(6, 6), out _);

        Assert.Equal(EditResult.ImpassableTerrain, editor.PlaceHero(new PlayerId(0), new MapPosition(5, 5), "Wet", out _));
        Assert.Equal(EditResult.Overlaps, editor.PlaceHero(new PlayerId(0), new MapPosition(6, 6), "Chesty", out _));
        Assert.Equal(EditResult.UnknownPlayer, editor.PlaceHero(new PlayerId(7), new MapPosition(3, 3), "Ghost", out _));
        Assert.Equal(EditResult.Ok, editor.PlaceHero(new PlayerId(0), new MapPosition(3, 3), "Valid", out var id));
        Assert.Equal(new HeroId(3), id);

        editor.Undo();
        Assert.DoesNotContain(editor.Scenario.Heroes, h => h.Id == id);
    }

    [Fact]
    public void PlaceHero_InTheEntranceOfItsOwnTown_IsAllowed_AndValid()
    {
        var editor = CreateEditor();
        Assert.Equal(EditResult.Ok, editor.PlaceObject(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(3, 3), out var townId));
        var entrance = editor.Scenario.GetObject(townId).VisitPosition;

        Assert.Equal(EditResult.Overlaps, editor.PlaceHero(new PlayerId(0), entrance, "Neutral town", out _));
        editor.SetTownOwner(townId, new PlayerId(0));
        Assert.Equal(EditResult.Overlaps, editor.PlaceHero(new PlayerId(1), entrance, "Enemy town", out _));
        Assert.Equal(EditResult.Ok, editor.PlaceHero(new PlayerId(0), entrance, "Home", out _));
        Assert.DoesNotContain(MapValidator.Validate(editor.Scenario), i => i.Message.Contains("inside an object", StringComparison.Ordinal));

        editor.SetTownOwner(townId, new PlayerId(1));
        Assert.Contains(MapValidator.Validate(editor.Scenario), i => i.Message == "Hero Home stands inside an object.");
    }

    [Fact]
    public void PlayerSlots_OnlyUnusedLastSlotCanBeRemoved()
    {
        var editor = CreateEditor();

        Assert.Equal(EditResult.PlayerInUse, editor.RemoveLastPlayer()); // Red owns a hero
        Assert.Equal(EditResult.Ok, editor.AddPlayer("Green", isHuman: false, out var green));
        Assert.Equal(new PlayerId(2), green);
        editor.PlaceObject(BuiltInMapObjects.MineId(ResourceType.Gold), new MapPosition(4, 4), out var mine);
        editor.SetMineOwner(mine, green);
        Assert.Equal(EditResult.PlayerInUse, editor.RemoveLastPlayer()); // Green owns a mine

        editor.SetMineOwner(mine, null);
        Assert.Equal(EditResult.Ok, editor.RemoveLastPlayer());
        Assert.Equal(2, editor.Scenario.Players.Count);

        editor.Undo();
        Assert.Equal("Green", editor.Scenario.Players[2].Name);
    }

    [Fact]
    public void PlayerSlots_AreCappedAtEight()
    {
        var editor = CreateEditor();
        for (var i = 2; i < GameState.MaxPlayers; i++)
        {
            Assert.Equal(EditResult.Ok, editor.AddPlayer($"P{i}", isHuman: false, out _));
        }

        Assert.Equal(EditResult.TooManyPlayers, editor.AddPlayer("Ninth", isHuman: false, out _));
        Assert.Equal(8, editor.Scenario.Players.Count);
    }

    [Fact]
    public void MapSizes_ArePresetsUpTo256()
    {
        Assert.Equal([48, 72, 96, 128, 160, 192, 256], MapSizes.Standard);
        Assert.NotNull(BlankScenario.Create(256, 256, levels: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AdventureMap(257, 48));
    }

    [Fact]
    public void MapProperties_AreUndoable()
    {
        var editor = CreateEditor();

        editor.SetMapProperties("Ashen Fields", "Two lords, one valley.");
        Assert.Equal("Ashen Fields", editor.Scenario.Map.Name);

        editor.Undo();
        Assert.Equal(("Untitled", ""), (editor.Scenario.Map.Name, editor.Scenario.Map.Description));
    }

    [Fact]
    public void Validator_ReportsHeroOnWaterAndUnlinkedGate()
    {
        var editor = CreateEditor(levels: 2);
        editor.PaintTerrain(new MapPosition(1, 1), TerrainType.Water); // under the blue hero
        editor.PlaceObject(BuiltInMapObjects.SubterraneanGate, new MapPosition(4, 4), out _);

        var issues = MapValidator.Validate(editor.Scenario);

        Assert.Contains(issues, i => i is { Severity: IssueSeverity.Error, Position: { X: 1, Y: 1 } });
        Assert.Contains(issues, i => i.Severity == IssueSeverity.Warning && i.Message.Contains("not linked"));
    }

    [Fact]
    public void DemoScenario_HasNoValidationErrors()
    {
        var issues = MapValidator.Validate(DemoScenario.Create());

        Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
    }

    private static TwoWayGate Gate(MapEditorSession editor, MapObjectId id) => (TwoWayGate)editor.Scenario.GetObject(id);
}
