using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The editor's overlays (ADR-264): what each tints or draws, and its legend.</summary>
public class MapOverlaysTests
{
    private const string Row = "....................";

    private static int? BandAt(MapOverlay overlay, int x, int y, int level = 0) =>
        overlay.Cells.Where(c => c.Tile == new MapPosition(x, y, level)).Select(c => (int?)c.Band).FirstOrDefault();

    private static string? TextAt(MapOverlay overlay, int x, int y) => BandAt(overlay, x, y) is { } band ? overlay.Legend[band].Text : null;

    [Fact]
    public void Passability_TintsEntrancesBlockedCellsAndTheCellsAMonsterGuards()
    {
        var state = TestWorld.Create(Row, "....~~..............", Row, Row, Row, Row, Row, Row);
        var town = TestWorld.AddTown(state, TownIds.Haven, 10, 0);
        TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 1, 1);

        var overlay = MapOverlays.Passability(state, 0);

        Assert.Equal("Entrance", TextAt(overlay, town.VisitPosition.X, town.VisitPosition.Y));
        Assert.Equal("Entrance", TextAt(overlay, 1, 1)); // the monster's own tile is where it is fought
        Assert.Equal("Blocked", TextAt(overlay, 4, 1)); // water
        var blocked = town.OccupiedCells().First(c => c.Cell == FootprintCell.Blocked).Position;
        Assert.Equal("Blocked", TextAt(overlay, blocked.X, blocked.Y));
        Assert.Equal("Guarded by a monster", TextAt(overlay, 2, 2));
        Assert.Null(TextAt(overlay, 18, 7)); // free: untinted
    }

    [Fact]
    public void Owners_TintsEveryOwnableThingAndHeroInItsOwnersColour_AndUnownedOnesNeutral()
    {
        var state = TestWorld.Create("A...................", Row, Row, Row, Row, Row, Row, Row);
        var town = TestWorld.AddTown(state, TownIds.Haven, 10, 0, TestWorld.Red);
        var mine = TestWorld.Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 3, 2);
        TestWorld.Add<MapObject>(state, BuiltInMapObjects.Sign, 18, 7);

        var overlay = MapOverlays.Owners(state, 0);

        Assert.Equal(["Blue", "Red", "Neutral"], overlay.Legend.Select(b => b.Text));
        Assert.Equal(new OverlayTint(TintKind.Player, state.GetPlayer(TestWorld.Red).Colour), overlay.Legend[1].Tint);
        Assert.Equal("Blue", TextAt(overlay, 0, 0)); // the hero
        Assert.All(town.OccupiedCells(), c => Assert.Equal("Red", TextAt(overlay, c.Position.X, c.Position.Y)));
        Assert.Equal("Neutral", TextAt(overlay, mine.VisitPosition.X, mine.VisitPosition.Y));
        Assert.Null(TextAt(overlay, 18, 7)); // a sign has no owner
    }

    [Fact]
    public void GuardStrength_SplitsTheLevelsGuardsIntoHeatBands_TheStrongestOnTop()
    {
        var state = TestWorld.Create(Row, Row, Row);
        TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 2, 1).Count = 1;
        TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 15, 1).Count = 100;

        var overlay = MapOverlays.GuardStrength(state, 0);

        Assert.Equal(MapOverlays.GuardBands, overlay.Legend.Count);
        Assert.Equal(0, BandAt(overlay, 3, 1));
        Assert.Equal(MapOverlays.GuardBands - 1, BandAt(overlay, 16, 1));
        Assert.Equal(new OverlayTint(TintKind.Heat, 100), overlay.Legend[^1].Tint);
        Assert.Null(BandAt(overlay, 9, 1));
        Assert.Empty(MapOverlays.GuardStrength(TestWorld.Create(Row), 0).Cells);
    }

    [Fact]
    public void GuardStrength_OfGuardsAllAlike_IsOneBand()
    {
        var state = TestWorld.Create(Row, Row, Row);
        TestWorld.Add<MonsterGuard>(state, BuiltInMapObjects.Monster, 5, 1).Count = 10;

        var overlay = MapOverlays.GuardStrength(state, 0);

        Assert.Single(overlay.Legend);
        Assert.Equal(new OverlayTint(TintKind.Heat, 100), overlay.Legend[0].Tint);
        Assert.All(overlay.Cells, c => Assert.Equal(0, c.Band));
    }

    [Fact]
    public void Reach_FromAStartOnTheOtherLevel_SaysSo_AndTintsWhatItReachesThere()
    {
        var state = TestWorld.CreateLevels([Row, Row], [Row, Row]);
        var start = new MapStart(TestWorld.Blue, new MapPosition(0, 0, 1), null);

        var overlay = MapOverlays.Reach(state, start, 0);

        Assert.Contains("other level", overlay.Note, StringComparison.Ordinal);
        Assert.Empty(overlay.Cells); // no gate joins the levels
        Assert.Equal(2 * Row.Length, MapOverlays.Reach(state, start, 1).Cells.Count);
    }

    [Fact]
    public void Height_ASaddleSquare_GivesTwoLinesThatDoNotCross()
    {
        var state = TestWorld.Create(Row, Row, Row);
        state.Map.SetCorner(0, 5, 1, ReliefLimits.StepsPerCell); // two opposite corners of tile (5, 1) up: a saddle at half a cell
        state.Map.SetCorner(0, 6, 2, ReliefLimits.StepsPerCell);

        var inSquare = MapOverlays.Height(state, 0).Lines
            .Where(l => l.Band == 0 && new[] { l.X1, l.X2 }.All(x => x is >= 5 and <= 6) && new[] { l.Y1, l.Y2 }.All(y => y is >= 1 and <= 2)).ToList();

        Assert.Equal(2, inSquare.Count);
        Assert.NotEqual((inSquare[0].X1, inSquare[0].Y1), (inSquare[1].X1, inSquare[1].Y1));
    }

    [Fact]
    public void Reach_TintsEachTileByItsDay_FartherOnesInOneBand_AndLeavesOutWhatNoRouteReaches()
    {
        var state = TestWorld.Create(new string('.', 110) + "~.");
        var start = new MapStart(TestWorld.Blue, new MapPosition(0, 0), null);

        var overlay = MapOverlays.Reach(state, start, 0);

        Assert.Equal(MapOverlays.ReachDays + 1, overlay.Legend.Count);
        Assert.Equal("1 day", TextAt(overlay, 0, 0)); // the start itself
        Assert.Equal("1 day", TextAt(overlay, 12, 0)); // 12 steps of 125
        Assert.Equal("2 days", TextAt(overlay, 13, 0));
        Assert.Equal($"More than {MapOverlays.ReachDays} days", TextAt(overlay, 109, 0));
        Assert.Null(TextAt(overlay, 111, 0)); // behind the water
    }

    [Fact]
    public void Starts_AreEachPlayersFirstTown_ElseItsFirstHero()
    {
        var state = TestWorld.Create("A.........B.........", Row, Row, Row, Row, Row, Row, Row);
        var town = TestWorld.AddTown(state, TownIds.Haven, 5, 1, TestWorld.Blue);

        var starts = new MapAnalysis(state).Starts();

        Assert.Equal([new MapStart(TestWorld.Blue, town.VisitPosition, TownIds.Haven), new MapStart(TestWorld.Red, new MapPosition(10, 0), TownIds.Haven)], starts); // the hero's footmen are Haven's
    }

    [Fact]
    public void Zones_WithoutAGeneratedLayout_AreTheStartZones()
    {
        var state = TestWorld.Create(Row, Row);
        state.Map.SetStartZone(new MapPosition(1, 1), TestWorld.Red);

        var overlay = MapOverlays.Zones(state, 0, null, "Not a generated map.");

        Assert.Equal("Start zone of Red", TextAt(overlay, 1, 1));
        Assert.Single(overlay.Cells);
        Assert.Equal("Not a generated map.", overlay.Note);
        Assert.StartsWith("No start zone", MapOverlays.Zones(TestWorld.Create(Row), 0, null).Note, StringComparison.Ordinal);
    }

    [Fact]
    public void GeneratedZones_AreLaidOutAgainFromTheDescription_AsTheGeneratorLaidThem()
    {
        var duel = BuiltInTemplates.Find("duel")!;
        var map = EditorMapGenerator.Generate(duel, 72, 5);
        var made = GeneratedZones.Parse(map.Scenario.Map.Description);

        Assert.Equal(new GeneratedDescription(duel.Name, 72, 5, map.Generation.Attempt, MapGenerator.Version), made);
        var reading = GeneratedZones.Read(map.Scenario, BuiltInTemplates.All);
        Assert.NotNull(reading.Zones);
        var layout = map.Generation.Map.Layout;
        Assert.Equal(layout.ZoneIds.Count, reading.Zones.Names.Count);
        Assert.All(map.Scenario.Map.Positions(0).Where(p => p.X % 7 == 0), p => Assert.Equal(layout.ZoneAt(p.X, p.Y, 0), reading.Zones.ZoneAt(p)));

        Assert.Null(reading.Zones.ZoneAt(new MapPosition(-1, 3))); // off the layout (a shifted map asks there)
        Assert.Null(reading.Zones.ZoneAt(new MapPosition(72, 3)));
        var overlay = MapOverlays.Zones(map.Scenario, 0, reading.Zones, reading.Note);
        Assert.Equal(72 * 72, overlay.Cells.Count);
        Assert.Contains(overlay.Legend, b => b.Text.Contains("start of slot 1", StringComparison.Ordinal));

        // Not found, resized, or not generated: no zones, and the note says why.
        Assert.Null(GeneratedZones.Read(map.Scenario, []).Zones);
        Assert.Null(GeneratedZones.Parse("A map of my own."));
        Assert.Null(GeneratedZones.Read(TestWorld.Create(Row), BuiltInTemplates.All).Zones);
        Assert.Equal(new GeneratedDescription("X \"Y\"", 36, 7, 2, 3),
            GeneratedZones.Parse("Generated from the template \"X \"Y\"\" at 36×36, seed 7 (attempt 2), generator version 3. More."));
    }

    [Fact]
    public void Height_IsFlatWithoutLines_AndARaisedCornerIsRingedByContours()
    {
        var state = TestWorld.Create(Row, Row, Row, Row);
        Assert.Empty(MapOverlays.Height(state, 0).Lines);
        Assert.StartsWith("The ground is flat", MapOverlays.Height(state, 0).Note, StringComparison.Ordinal);

        // One corner a cell and a half up: a ring at half a cell and one at a whole cell, through the four squares around it.
        state.Map.SetCorner(0, 5, 2, ReliefLimits.StepsPerCell * 3 / 2);

        var overlay = MapOverlays.Height(state, 0);

        var half = overlay.Lines.Where(l => l.Band == 0).ToList();
        var whole = overlay.Lines.Where(l => l.Band == 1).ToList();
        Assert.Equal(4, half.Count);
        Assert.Equal(4, whole.Count);
        Assert.All(half, l => Assert.InRange(Math.Max(Math.Abs(l.X1 - 5), Math.Abs(l.Y1 - 2)), 0.66f, 0.67f)); // 4 of 12 eighths from the top
        Assert.All(whole, l => Assert.InRange(Math.Max(Math.Abs(l.X1 - 5), Math.Abs(l.Y1 - 2)), 0.33f, 0.34f));
        Assert.Equal("From 0 to 1.5 cells high.", overlay.Note);
    }
}
