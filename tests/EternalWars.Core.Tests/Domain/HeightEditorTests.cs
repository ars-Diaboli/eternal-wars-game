using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The editor's height tools (ADR-182 g): the brush, the limits, Auto height, levelling on placement, undo.</summary>
public class HeightEditorTests
{
    /// <summary>Grass, the blank map's terrain, stands one cell width high.</summary>
    private const int Grass = 8;

    private static readonly MapPosition Middle = new(10, 10);

    private static MapEditorSession Editor() => TestEditor.Blank(24, 24);

    private static int[] Corners(MapEditorSession editor, int level = 0)
    {
        var map = editor.Scenario.Map;
        return [.. Enumerable.Range(0, (map.Width + 1) * (map.Height + 1)).Select(i => map.GetCorner(level, i % (map.Width + 1), i / (map.Width + 1)))];
    }

    private static int Corner(MapEditorSession editor, int cx, int cy) => editor.Scenario.Map.GetCorner(0, cx, cy);

    [Fact]
    public void Raise_LiftsTheTilesCornersByTheStrength_FadingOutsideTheBrush()
    {
        var editor = Editor();
        var events = new List<GroundHeightsChanged>();
        editor.Events.Subscribe<GroundHeightsChanged>(events.Add);

        var changed = editor.Heights.Raise(Middle, 0, 4);

        foreach (var (cx, cy) in new[] { (10, 10), (11, 10), (10, 11), (11, 11) })
        {
            Assert.Equal(Grass + 4, Corner(editor, cx, cy));
        }

        Assert.InRange(Corner(editor, 9, 10), Grass, Grass + 3); // the fade: less than the full strength
        Assert.Equal(Grass, Corner(editor, 7, 10)); // beyond the brush
        Assert.Equal(changed, Corners(editor).Count(h => h != Grass));
        var e = Assert.Single(events);
        Assert.Equal(0, e.Level);
        Assert.True(e.Area.Contains(10, 10) && e.Area.Contains(11, 11));
        Assert.Equal("Raise ground", editor.History.UndoDescription);
    }

    [Fact]
    public void TheFalloff_IsHowFarTheStrengthFades_PastTheFullCircle()
    {
        var hard = Editor();
        var soft = Editor();
        var usual = Editor();
        var plain = Editor();

        // Radius 1: the full circle reaches 1.75 tiles from the tile's centre (10.5, 10.5).
        Assert.True(hard.Heights.Raise(Middle, 1, 8, falloff: 0) > 0);
        Assert.True(soft.Heights.Raise(Middle, 1, 8, falloff: 4) > 0);
        Assert.True(usual.Heights.Raise(Middle, 1, 8, falloff: HeightEditor.DefaultFalloff) > 0);
        Assert.True(plain.Heights.Raise(Middle, 1, 8) > 0);

        Assert.Equal(Corners(usual), Corners(plain)); // one tile unless told
        // No falloff: a step at the rim, every corner inside raised the whole strength, none outside.
        Assert.Equal(Grass + 8, Corner(hard, 12, 11)); // 1.58 from the centre
        Assert.Equal(Grass, Corner(hard, 13, 10)); // 2.55
        Assert.All(Corners(hard), h => Assert.True(h is Grass or Grass + 8));

        // A wide falloff reaches further and fades by smaller steps: corner by corner along a row, no step larger than half the strength.
        Assert.True(Corner(soft, 14, 10) > Grass); // 3.54 from the centre: within 1.75 + 4
        Assert.Equal(Grass, Corner(usual, 14, 10));
        Assert.Equal(Grass, Corner(soft, 17, 10)); // 6.52: past it
        var row = Enumerable.Range(10, 8).Select(x => Corner(soft, x, 10)).ToList();
        Assert.All(row.Zip(row.Skip(1)), p => Assert.InRange(p.First - p.Second, 0, 4));
        Assert.True(Corners(soft).Count(h => h != Grass) > Corners(usual).Count(h => h != Grass));
    }

    [Fact]
    public void TheFalloff_ShapesEveryBrush_AndIsHeldToItsRange()
    {
        // Corner (9, 10) is 1.58 tiles from Middle's centre: past a radius-0 brush's full circle (0.75), within a falloff of 3.
        foreach (var falloff in new[] { 0, 3 })
        {
            var lower = Editor();
            lower.Heights.Lower(Middle, 0, 8, falloff);
            var level = Editor();
            level.Heights.Level(Middle, 0, 8, Grass + 8, falloff);
            var smooth = Editor();
            smooth.Heights.Raise(Middle, 0, 8, falloff: 0); // a spike on the tile's four corners
            smooth.Heights.Smooth(new MapPosition(7, 10), 0, 4, falloff); // (9, 10) beside the spike, 1.58 from this tile's centre

            var reached = falloff > 0;
            Assert.Equal(reached, Corner(lower, 9, 10) < Grass);
            Assert.Equal(reached, Corner(level, 9, 10) > Grass);
            Assert.Equal(reached, Corner(smooth, 9, 10) > Grass);
        }

        var editor = Editor();
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Heights.Raise(Middle, 0, 2, falloff: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Heights.Raise(Middle, 0, 2, falloff: HeightEditor.MaxFalloff + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Heights.Smooth(Middle, 0, 2, falloff: HeightEditor.MaxFalloff + 1));
    }

    [Fact]
    public void ALargerRadius_ReachesFurther()
    {
        var small = Editor();
        var large = Editor();

        Assert.True(large.Heights.Raise(Middle, 3, 2) > small.Heights.Raise(Middle, 0, 2));
        Assert.Equal(Grass + 2, Corner(large, 13, 10));
        Assert.Equal(Grass, Corner(small, 13, 10));
    }

    [Fact]
    public void WalkableGround_StaysBetweenZeroAndFour_AndAToolAtTheLimitRecordsNothing()
    {
        var editor = Editor();
        for (var i = 0; i < 6; i++)
        {
            editor.Heights.Raise(Middle, 0, HeightEditor.MaxStrength);
        }

        Assert.Equal(ReliefLimits.WalkableMax, Corner(editor, 10, 10));
        editor.History.Clear();
        Assert.Equal(0, editor.Heights.Raise(Middle, 0, 1)); // at the top; the fade is too faint to move the ring
        Assert.False(editor.History.CanUndo);

        for (var i = 0; i < 10; i++)
        {
            editor.Heights.Lower(Middle, 0, HeightEditor.MaxStrength);
        }

        Assert.Equal(0, Corner(editor, 10, 10));
        editor.History.Clear();
        Assert.Equal(0, editor.Heights.Lower(Middle, 0, 1));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void TheShoreline_StaysAtZero_WhatTheToolsDo()
    {
        var editor = Editor();
        editor.Heights.AutoHeight = false;
        editor.Terrain.Paint(Middle, TerrainType.Water);
        editor.Heights.FromTerrain();

        editor.Heights.Raise(Middle, 1, HeightEditor.MaxStrength);

        Assert.Equal(0, Corner(editor, 10, 10)); // shared by the water tile and grass
        Assert.Equal(0, Corner(editor, 11, 11));
    }

    [Fact]
    public void Level_MovesTheGroundTowardsTheTarget_BySteps()
    {
        var editor = Editor();
        editor.Heights.Raise(Middle, 1, 8);
        var target = editor.Heights.HeightAt(new MapPosition(2, 2));
        Assert.Equal(Grass, target);

        editor.Heights.Level(Middle, 1, 3, target);
        Assert.Equal(Grass + 5, Corner(editor, 10, 10));
        editor.Heights.Level(Middle, 1, 8, target);
        Assert.Equal(Grass, Corner(editor, 10, 10));
        Assert.Equal(0, editor.Heights.Level(Middle, 0, 8, target));
    }

    [Fact]
    public void Smooth_PullsASpikeTowardsItsNeighbours()
    {
        var editor = Editor();
        editor.Heights.Raise(Middle, 0, 8);
        var peak = Corners(editor).Max();

        Assert.True(editor.Heights.Smooth(Middle, 0, 4) > 0);

        Assert.True(Corners(editor).Max() < peak);
        Assert.True(Corner(editor, 10, 10) > Grass);
    }

    [Fact]
    public void FromTerrain_UnderTheBrush_GivesTheTerrainsHeightThere_AndLeavesTheRest()
    {
        var editor = Editor();
        editor.Heights.Raise(Middle, 1, 8);
        editor.Heights.Raise(new MapPosition(3, 3), 0, 8);

        editor.Heights.FromTerrain(Middle, 3);

        Assert.Equal(Grass, Corner(editor, 10, 10));
        Assert.Equal(Grass + 8, Corner(editor, 3, 3));
        Assert.Equal("Heights from terrain", editor.History.UndoDescription);
    }

    [Fact]
    public void FromTerrain_OnAnArea_MatchesTheWholeLevel_Inside_AndKeepsTheOutside()
    {
        var map = new AdventureMap(20, 16);
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                map.SetTerrain(new MapPosition(x, y), ((x + (2 * y)) % 5) switch { 0 => TerrainType.Snow, 1 => TerrainType.Water, 2 => TerrainType.Rock, 3 => TerrainType.Sand, _ => TerrainType.Grass });
            }
        }

        var whole = new AdventureMap(20, 16);
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                whole.SetTerrain(new MapPosition(x, y), map.GetTerrain(new MapPosition(x, y)));
            }
        }

        ReliefShaper.FromTerrain(whole, 0);
        var area = new CornerArea(1, 2, 9, 7);
        ReliefShaper.FromTerrain(map, 0, area);

        for (var cy = 0; cy <= 16; cy++)
        {
            for (var cx = 0; cx <= 20; cx++)
            {
                Assert.Equal(area.Contains(cx, cy) ? whole.GetCorner(0, cx, cy) : 0, map.GetCorner(0, cx, cy));
            }
        }
    }

    [Fact]
    public void PaintingTerrain_WithAutoHeight_ShapesTheGround_AndOneUndoTakesBackBoth()
    {
        var editor = Editor();
        var before = Corners(editor);

        editor.Terrain.Paint(Middle, TerrainType.Snow, radius: 2);

        Assert.True(Corner(editor, 10, 10) > Grass);
        Assert.Equal(Grass, Corner(editor, 0, 0)); // far away
        Assert.Equal("Paint Snow", editor.History.UndoDescription);
        Assert.True(editor.Undo());
        Assert.Equal(before, Corners(editor));
        Assert.Equal(TerrainType.Grass, editor.Scenario.Map.GetTerrain(Middle));
        Assert.False(editor.History.CanUndo);
    }

    [Fact]
    public void PaintingTheUnderground_WithAutoHeight_ShapesOnlyItsLevel_AndUndoAndRedoGiveBackEachCorner()
    {
        var editor = TestEditor.Blank(24, 24, levels: 2);
        var (surface, underground) = (Corners(editor), Corners(editor, 1));
        var events = new List<GroundHeightsChanged>();
        editor.Events.Subscribe<GroundHeightsChanged>(events.Add);

        editor.Terrain.Paint(Middle with { Level = 1 }, TerrainType.Snow, radius: 1);
        var shaped = Corners(editor, 1);

        Assert.Equal(surface, Corners(editor));
        Assert.NotEqual(underground, shaped);
        Assert.Equal(0, editor.Scenario.Map.GetCorner(1, 0, 0) - underground[0]); // far away: as it was
        var e = Assert.Single(events);
        Assert.Equal(1, e.Level);
        Assert.True(e.Area.Contains(10, 10) && e.Area.Contains(11, 11));
        Assert.True(editor.Undo());
        Assert.Equal(underground, Corners(editor, 1));
        Assert.True(editor.Redo());
        Assert.Equal(shaped, Corners(editor, 1));
    }

    [Fact]
    public void PaintingTerrain_WithoutAutoHeight_LeavesTheGround()
    {
        var editor = Editor();
        var before = Corners(editor);
        editor.Heights.AutoHeight = false;

        editor.Terrain.Paint(Middle, TerrainType.Snow, radius: 2);

        Assert.Equal(before, Corners(editor));
        Assert.True(new MapEditorSession(editor.Scenario).Heights.AutoHeight); // on by default
    }

    [Fact]
    public void PaintingNearATown_KeepsItsGroundLevel()
    {
        var editor = Editor();
        var town = Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(8, 8));
        var level = editor.Heights.HeightAt(town.VisitPosition);

        editor.Terrain.Paint(town.VisitPosition.Offset(-4, 1), TerrainType.Highlands, radius: 1); // beside the entrance, off the footprint

        AssertFlat(editor, town, editor.Heights.HeightAt(town.VisitPosition));
        Assert.NotEqual(level, editor.Heights.HeightAt(town.VisitPosition)); // levelled again at the entrance's new height
    }

    [Fact]
    public void PlacingATownOnASlope_LevelsItsGround_AndUndoRestoresTheSlope()
    {
        var editor = Editor();
        editor.Heights.AutoHeight = false;
        for (var x = 0; x < 24; x++)
        {
            editor.Heights.Raise(new MapPosition(x, 12), 0, x % 8 + 1);
        }

        var slope = Corners(editor);
        var town = Place(editor, BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(8, 9));

        AssertFlat(editor, town, editor.Heights.HeightAt(town.VisitPosition));
        Assert.NotEqual(slope, Corners(editor));
        Assert.True(editor.Undo());
        Assert.Equal(slope, Corners(editor));
        Assert.Empty(editor.Scenario.ObjectsOf<Town>());
        Assert.True(editor.Redo());
        AssertFlat(editor, editor.Scenario.ObjectsOf<Town>().Single(), editor.Heights.HeightAt(town.VisitPosition));
    }

    [Fact]
    public void PlacingAnObject_KeepsANeighboursFootprint()
    {
        var editor = Editor();
        editor.Heights.Raise(new MapPosition(6, 6), 3, 8);
        var mine = Place(editor, BuiltInMapObjects.MineId(ResourceType.Gold), new MapPosition(5, 5)); // tiles 5–7, corners 5–8
        var mineLevel = editor.Heights.HeightAt(mine.VisitPosition);
        var before = Corners(editor);

        Place(editor, BuiltInMapObjects.MineId(ResourceType.Ore), new MapPosition(9, 5)); // corners 9–12; its ring (8) is the gold mine's edge

        Assert.NotEqual(before, Corners(editor));
        AssertFlat(editor, mine, mineLevel);
    }

    [Fact]
    public void OneTileObjects_DoNotLevelTheGround()
    {
        var editor = Editor();
        editor.Heights.Raise(Middle, 0, 5);
        var before = Corners(editor);

        Place(editor, BuiltInMapObjects.TreasureChest, Middle);

        Assert.Equal(before, Corners(editor));
    }

    private static MapObject Place(MapEditorSession editor, string definition, MapPosition at)
    {
        Assert.Equal(EditResult.Ok, editor.Objects.Place(definition, at, out var id));
        return editor.Scenario.GetObject(id);
    }

    private static void AssertFlat(MapEditorSession editor, MapObject mapObject, int height)
    {
        foreach (var tile in mapObject.OccupiedCells().Select(c => c.Position).Append(mapObject.VisitPosition))
        {
            for (var dy = 0; dy <= 1; dy++)
            {
                for (var dx = 0; dx <= 1; dx++)
                {
                    Assert.Equal(height, Corner(editor, tile.X + dx, tile.Y + dy));
                }
            }
        }
    }
}
