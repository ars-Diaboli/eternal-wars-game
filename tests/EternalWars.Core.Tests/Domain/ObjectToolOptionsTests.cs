using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The Object tool's options (ADR-286): a random variant of the chosen entry, and the entrance snapped to a road.</summary>
public class ObjectToolOptionsTests
{
    private static readonly BrowserCatalog Browser = new(MapObjectCatalog.Default, CreatureCatalog.Default, ArtifactCatalog.Default);

    private static BrowserEntry GoldMine => Browser.Find(BuiltInMapObjects.MineId(ResourceType.Gold))!;

    [Fact]
    public void TheVariants_AreTheEntriesOfTheGroup_PlacedAlike_OfOneTier_TheChosenFirst()
    {
        var mines = Browser.VariantsOf(GoldMine);
        Assert.Same(GoldMine, mines[0]);
        Assert.Equal(MapObjectCatalog.Default.OfKind(MapObjectKind.Mine).Count(d => d.Becomes is null), mines.Count); // the working mines, no abandoned one
        Assert.All(mines, m => Assert.Equal("Mines", m.Group));
        Assert.Equal(mines.Count, mines.Distinct().Count());

        var monster = Browser.Of(BrowserCategory.Monsters).First(e => e.Order == 2);
        Assert.All(Browser.VariantsOf(monster), v => Assert.Equal((monster.Group, 2, BrowserPlacement.Monster), (v.Group, v.Order, v.Placement)));
        Assert.True(Browser.VariantsOf(monster).Count > 1);

        var tree = Browser.Of(BrowserCategory.Scenery).First(e => e.Group == "Tree");
        Assert.Equal(Browser.Of(BrowserCategory.Scenery).Count(e => e.Group == "Tree"), Browser.VariantsOf(tree).Count);

        Assert.Equal([Browser.Find(BrowserEntry.HeroId)!], Browser.VariantsOf(Browser.Find(BrowserEntry.HeroId)!)); // no group: itself
        var town = Browser.Of(BrowserCategory.Towns).First();
        Assert.Equal([town], Browser.VariantsOf(town));
    }

    [Fact]
    public void ARandomVariant_IsDrawnFromTheScenariosNumbers_AsOneStep_AndUndoPutsThemBack()
    {
        var editor = Editor();
        var mines = Browser.VariantsOf(GoldMine);

        Assert.Equal(EditResult.Ok, editor.Placing.PlaceAny(mines, new(10, 10), snapToRoad: false, out var placed, out var chosen));
        Assert.Contains(chosen!, mines);
        Assert.Equal(chosen!.Definition, editor.Scenario.GetObject(placed.Object!.Value).Definition);
        Assert.Equal(["Place a random variant of Gold mine"], editor.History.Steps);

        Assert.True(editor.Undo());
        Assert.Empty(editor.Scenario.Objects);
        Assert.Equal(EditResult.Ok, editor.Placing.PlaceAny(mines, new(10, 10), snapToRoad: false, out _, out var again));
        Assert.Same(chosen, again); // the draw was put back: the same click gives the same mine

        var drawn = new HashSet<BrowserEntry>();
        for (var x = 0; x < 9; x++)
        {
            Assert.Equal(EditResult.Ok, editor.Placing.PlaceAny(mines, new(2 + (4 * x), 20), snapToRoad: false, out _, out var each));
            drawn.Add(each!);
        }

        Assert.True(drawn.Count > 1, "nine draws gave one mine");
    }

    [Fact]
    public void WhenNoVariantFits_ThePickedEntrysRefusalIsSaid_AndNothingIsDrawn()
    {
        var editor = Editor();
        var state = editor.Scenario.Random.State;
        Assert.Equal(EditResult.OutOfBounds, editor.Placing.PlaceAny(Browser.VariantsOf(GoldMine), new(39, 29), snapToRoad: false, out var placed, out var chosen));
        Assert.Equal((default(PlacedEntry), (BrowserEntry?)null), (placed, chosen));
        Assert.Equal(state, editor.Scenario.Random.State);
        Assert.Empty(editor.History.Steps);
        Assert.Equal(EditResult.OutOfBounds, editor.Placing.Preview(Browser.VariantsOf(GoldMine), new(39, 29), snapToRoad: false).Check.Result);
    }

    [Fact]
    public void ADrawnVariant_SuitsTheTerrain_ThePickedOneNeedNot()
    {
        var tile = new MapPosition(10, 10);
        var probe = Editor();

        // A scenery entry and a variant of it that does not suit lava, with a cell of the first that the second does not take.
        var (picked, other, cell) = Browser.Of(BrowserCategory.Scenery)
            .SelectMany(p => Browser.VariantsOf(p).Skip(1).Where(o => !o.Suits(TerrainType.Lava)).Select(o => (p, o)))
            .Select(pair => (pair.p, pair.o, Cells: Cells(probe, pair.p, tile).Except(Cells(probe, pair.o, tile)).ToList()))
            .Where(t => t.Cells.Count > 0)
            .Select(t => (t.p, t.o, t.Cells[0]))
            .First();
        IReadOnlyList<BrowserEntry> variants = [picked, other];

        foreach (var lava in new[] { true, false })
        {
            var editor = Editor();
            if (lava)
            {
                editor.Terrain.Paint(tile, TerrainType.Lava, radius: 5);
            }

            Assert.Equal(EditResult.Ok, editor.Placing.Place(Browser.Find(BuiltInMapObjects.TreasureChest)!, cell, out _)); // the picked one no longer fits
            var preview = editor.Placing.Preview(variants, tile, snapToRoad: false);
            var placed = editor.Placing.PlaceAny(variants, tile, snapToRoad: false, out _, out var chosen);
            if (lava)
            {
                Assert.Equal((picked, false), (preview.Entry, preview.Check.Result == EditResult.Ok)); // the other is no tree for lava
                Assert.Equal((false, (BrowserEntry?)null), (placed == EditResult.Ok, chosen));
            }
            else
            {
                Assert.Equal((other, EditResult.Ok), (preview.Entry, preview.Check.Result));
                Assert.Equal((EditResult.Ok, other), (placed, chosen));
            }
        }

        static IEnumerable<MapPosition> Cells(MapEditorSession editor, BrowserEntry entry, MapPosition at) =>
            editor.Placing.Check(entry, at).Cells.Select(c => c.Position);
    }

    [Fact]
    public void Snapping_PrefersTheEntranceOnTheRoad_ToBesideIt_AtOneDistance_AndKeepsToTheMap()
    {
        var editor = Editor();
        editor.Terrain.PaintRoad([new MapPosition(23, 7)], RoadType.Dirt);
        Assert.Equal(EditResult.Ok, editor.Placing.Place(Browser.Find(BuiltInMapObjects.TreasureChest)!, new(22, 5), out _)); // where pointed: taken
        // One tile down puts the entrance on the road; one right puts it beside the road: as near, and the road wins over the top row.
        Assert.Equal(new MapPosition(22, 6), editor.Placing.Spot(GoldMine, new(22, 5), snapToRoad: true));

        editor.Terrain.PaintRoad(Enumerable.Range(0, 30).Select(y => new MapPosition(0, y)), RoadType.Dirt);
        // The entrance on the road (x 0) would put the mine's left column off the map: beside the road it is.
        Assert.Equal(new MapPosition(0, 20), editor.Placing.Spot(GoldMine, new(2, 20), snapToRoad: true));
    }

    [Fact]
    public void ARightClick_SelectsTheThingUnderThePointer_UnlessItIsSelected()
    {
        var editor = Editor();
        Assert.Equal(EditResult.Ok, editor.Placing.Place(Browser.Find(BuiltInMapObjects.TreasureChest)!, new(5, 5), out var chest));
        Assert.Equal(EditResult.Ok, editor.Placing.Place(Browser.Find(BuiltInMapObjects.TreasureChest)!, new(8, 5), out var second));

        Assert.False(editor.Selecting.PickUnlessSelected(new MapPosition(12, 12))); // open ground: the selection stays
        Assert.True(editor.Selecting.PickUnlessSelected(new MapPosition(5, 5)));
        Assert.Equal([chest.Object!.Value], editor.Selection.Objects);

        editor.Selecting.Set(SelectionItems.Empty with { Objects = [chest.Object.Value, second.Object!.Value] });
        Assert.False(editor.Selecting.PickUnlessSelected(new MapPosition(8, 5))); // selected already: the menu is for both
        Assert.Equal(2, editor.Selection.Objects.Count);
        Assert.False(editor.Selecting.PickUnlessSelected(new MapPosition(5, 5), SelectionFilter.All & ~SelectionFilter.Objects));
    }

    [Fact]
    public void Snapping_MovesTheEntrance_OntoOrBesideTheNearestRoad_WhereTheObjectFits()
    {
        var editor = Editor();
        editor.Terrain.PaintRoad(Enumerable.Range(0, 40).Select(x => new MapPosition(x, 12)), RoadType.Dirt);
        var mine = GoldMine; // BBB over BVB: the entrance is one right of and one below the top-left tile

        Assert.Equal(new MapPosition(10, 8), editor.Placing.Spot(mine, new(10, 8), snapToRoad: false));
        Assert.Equal(new MapPosition(10, 10), editor.Placing.Spot(mine, new(10, 8), snapToRoad: true)); // the entrance (11, 11) beside the road
        Assert.Equal(new MapPosition(10, 2), editor.Placing.Spot(mine, new(10, 2), snapToRoad: true)); // no road within reach: it stays
        Assert.Equal(new MapPosition(10, 11), editor.Placing.Spot(mine, new(10, 11), snapToRoad: true)); // the entrance on the road: as good as it gets

        Assert.Equal(EditResult.Ok, editor.Placing.Place(Browser.Find(BuiltInMapObjects.TreasureChest)!, new(11, 11), out _));
        Assert.Equal(new MapPosition(8, 10), editor.Placing.Spot(mine, new(10, 8), snapToRoad: true)); // the nearer spots take the chest's tile

        Assert.Equal(EditResult.Ok, editor.Placing.PlaceAny([mine], new(10, 8), snapToRoad: true, out var placed, out _));
        Assert.Equal(new MapPosition(8, 10), editor.Scenario.GetObject(placed.Object!.Value).Position);

        var hero = Browser.Find(BrowserEntry.HeroId)!;
        Assert.Equal(new MapPosition(10, 8), editor.Placing.Spot(hero, new(10, 8), snapToRoad: true)); // no entrance: no snapping
        var rock = Browser.Find("scenery.rock.small")!;
        Assert.Equal(new MapPosition(10, 8), editor.Placing.Spot(rock, new(10, 8), snapToRoad: true));
    }

    [Fact]
    public void ThePreview_IsWhatTheClickPlaces()
    {
        var editor = Editor();
        editor.Terrain.PaintRoad(Enumerable.Range(0, 40).Select(x => new MapPosition(x, 12)), RoadType.Dirt);
        var before = snapshot(editor);
        var preview = editor.Placing.Preview([GoldMine], new(10, 8), snapToRoad: true);
        Assert.Equal((GoldMine, new MapPosition(10, 10), EditResult.Ok), (preview.Entry, preview.At, preview.Check.Result));
        Assert.Contains(preview.Check.Cells, c => c.Position == new MapPosition(11, 11));

        Assert.Equal(before, snapshot(editor)); // a preview changes nothing
        Assert.Equal(EditResult.Ok, editor.Placing.PlaceAny([GoldMine], new(10, 8), snapToRoad: true, out var placed, out _));
        Assert.Equal(preview.At, editor.Scenario.GetObject(placed.Object!.Value).Position);

        static string snapshot(MapEditorSession editor) => MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
    }

    private static MapEditorSession Editor() => new(BlankScenario.Create(40, 30), copier: MapObjectCopier.Instance);
}
