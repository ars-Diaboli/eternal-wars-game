using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The map report (EDIT-9, ADR-266): the fairness table the Fairness panel shows and the Markdown File → Export writes.</summary>
public class MapReportTests
{
    private static readonly string Wide = new('.', 80);

    private static Core.Domain.Game.GameState TwoStarts(out Town blue)
    {
        var state = TestWorld.Create([.. Enumerable.Repeat(Wide, 10)]);
        blue = TestWorld.AddTown(state, TownIds.Haven, 0, 0, TestWorld.Blue);
        TestWorld.AddTown(state, TownIds.Haven, 72, 0, TestWorld.Red);
        TestWorld.Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 22, 6);
        TestWorld.Add<Mine>(state, BuiltInMapObjects.MineId(ResourceType.Ore), 68, 6);
        TestWorld.Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 5, 8).Amount = 1000;
        TestWorld.Add<ResourcePile>(state, BuiltInMapObjects.PileId(ResourceType.Gold), 74, 8).Amount = 2000;
        return state;
    }

    [Fact]
    public void Table_HasAColumnPerNumber_AndAMineColumnPerResourceTheMapHas()
    {
        var state = TwoStarts(out var blue);
        var fairness = new MapAnalysis(state).Fairness();

        var table = MapReport.Table(state, fairness);

        Assert.Equal(["Player", "Start", "Town", "Ore mine", "Provider", "Treasure ≤3d", "Treasure ≤7d", "Guards ≤3d", "Guards ≤7d", "Mines ≤3d"], table.Headers);
        Assert.Equal(2, table.Rows.Count);
        var row = table.Rows[0];
        Assert.Equal(table.Headers.Count, row.Count);
        Assert.Equal($"Player 1 ({state.GetPlayer(TestWorld.Blue).Name})", row[0]); // as the warnings number it
        Assert.Equal(blue.VisitPosition.ToString(), row[1]);
        Assert.Equal("2", row[3]); // as the analysis measures it
        Assert.Equal(MapReport.NoneReached, row[4]); // the map has no provider
        Assert.Equal("1,000", row[5]);
        Assert.Equal("Ore 1", row[9]);
    }

    [Fact]
    public void Markdown_ListsTheProblemsErrorsFirst_ThenTheTableAndItsWarnings()
    {
        var state = TwoStarts(out _);
        var fairness = new MapAnalysis(state).Fairness();
        var fix = new OwnerFix("give the town at (1,2,L0) to Blue", new MapObjectId(1), null, TestWorld.Blue);
        MapIssue[] issues =
        [
            new(IssueSeverity.Warning, "A warning\nover two lines") { Kind = IssueKind.Objects },
            new(IssueSeverity.Error, "An error", new MapPosition(1, 2)) { Kind = IssueKind.Players, Fix = fix },
        ];

        var markdown = MapReport.Markdown(state, fairness, issues);

        var lines = markdown.Split('\n');
        Assert.StartsWith("# Map report: ", lines[0], StringComparison.Ordinal);
        Assert.Contains("1 error(s), 1 warning(s).", lines);
        var error = Array.IndexOf(lines, "- **Error** (Players): An error (1,2,L0) Fix: give the town at (1,2,L0) to Blue.");
        var warning = Array.IndexOf(lines, "- **Warning** (Objects): A warning over two lines");
        Assert.True(error > 0 && warning > error, markdown);
        Assert.Contains("| Player | Start | Town | Ore mine | Provider | Treasure ≤3d | Treasure ≤7d | Guards ≤3d | Guards ≤7d | Mines ≤3d |", lines);
        Assert.Contains("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |", lines);
        Assert.Equal(4, lines.Count(l => l.StartsWith("| ", StringComparison.Ordinal))); // the header, the line under it, a row per start
        Assert.All(fairness.Warnings, w => Assert.Contains($"- {w}", lines));
        Assert.NotEmpty(fairness.Warnings); // 1000 against 2000 within 3 days
    }

    [Fact]
    public void Markdown_SaysWhenThereIsNoProblemAndNoStart()
    {
        var state = TestWorld.Create(Wide);
        state.Map.Name = "Bare | map";

        var markdown = MapReport.Markdown(state, new MapAnalysis(state).Fairness(), []);

        Assert.Contains("No problems found.", markdown, StringComparison.Ordinal);
        Assert.Contains("The map has no start", markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("| Player", markdown, StringComparison.Ordinal);
        Assert.StartsWith("# Map report: Bare | map\n", markdown, StringComparison.Ordinal); // a heading, not a cell: the bar stays
    }

    [Fact]
    public void Markdown_EscapesABarInATableCell()
    {
        var state = TwoStarts(out _);
        state.GetPlayer(TestWorld.Blue).Name = "Blue | Gold";

        var markdown = MapReport.Markdown(state, new MapAnalysis(state).Fairness(), []);

        Assert.Contains("| Player 1 (Blue \\| Gold) | ", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Markdown_OfOneStart_HasNothingToCompare()
    {
        var state = TestWorld.Create([.. Enumerable.Repeat(Wide, 10)]);
        TestWorld.AddTown(state, TownIds.Haven, 0, 0, TestWorld.Blue);

        var markdown = MapReport.Markdown(state, new MapAnalysis(state).Fairness(), []);

        Assert.Contains("One start: there is nothing to compare it with.", markdown, StringComparison.Ordinal);
    }
}
