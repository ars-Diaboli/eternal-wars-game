using System.Globalization;
using System.Text;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Analysis;

/// <summary>The fairness numbers as a table (<see cref="MapReport.Table"/>): a header per column and a row of texts per start.</summary>
public sealed record FairnessTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// The map report (EDIT-9, ADR-266): the fairness numbers of <see cref="MapAnalysis.Fairness"/> as a table, which the editor's Fairness panel
/// shows, and the whole report as Markdown (<see cref="Markdown"/>) with the map's problems, which File → Export writes.
/// </summary>
public static class MapReport
{
    /// <summary>What a number the analysis could not find reads as: nothing of the kind is reached from the start.</summary>
    public const string NoneReached = "—";

    /// <summary>
    /// One row per start: the player, the start tile, the days to the nearest town, mine of each resource the map has and resource provider,
    /// the treasure and the guards within <see cref="MapAnalysis.NearDays"/> and <see cref="MapAnalysis.FarDays"/> days, and the mines within
    /// <see cref="MapAnalysis.NearDays"/> days (the start town's surroundings).
    /// </summary>
    public static FairnessTable Table(GameState state, FairnessAnalysis fairness)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(fairness);
        var resources = fairness.Starts.SelectMany(s => s.DaysToMine.Keys).Distinct().Order().ToList();
        var headers = new List<string> { "Player", "Start", "Town" };
        headers.AddRange(resources.Select(r => $"{r} mine"));
        headers.AddRange(["Provider", $"Treasure ≤{MapAnalysis.NearDays}d", $"Treasure ≤{MapAnalysis.FarDays}d", $"Guards ≤{MapAnalysis.NearDays}d",
            $"Guards ≤{MapAnalysis.FarDays}d", $"Mines ≤{MapAnalysis.NearDays}d"]);

        var rows = fairness.Starts.Select(s =>
        {
            var row = new List<string> { PlayerName(state, s), s.Start.ToString(), Days(s.DaysToTown) };
            row.AddRange(resources.Select(r => Days(s.DaysToMine.GetValueOrDefault(r))));
            var mines = s.MinesWithin3.Where(m => m.Value > 0).OrderBy(m => m.Key).Select(m => $"{m.Key} {m.Value}").ToList();
            row.AddRange([Days(s.DaysToProvider), Number(s.TreasureWithin3), Number(s.TreasureWithin7), Number(s.GuardsWithin3), Number(s.GuardsWithin7),
                mines.Count == 0 ? "none" : string.Join(", ", mines)]);
            return (IReadOnlyList<string>)row;
        }).ToList();
        return new FairnessTable(headers, rows);
    }

    /// <summary>
    /// The report as Markdown: the map's name and size, its problems (<paramref name="issues"/>, errors first, in the validator's order), the
    /// fairness table and its warnings. The numbers' units and ranges are explained under the table.
    /// </summary>
    public static string Markdown(GameState state, FairnessAnalysis fairness, IReadOnlyList<MapIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(fairness);
        ArgumentNullException.ThrowIfNull(issues);
        var map = state.Map;
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"# Map report: {Line(map.Name)}\n\n");
        text.Append(CultureInfo.InvariantCulture, $"{map.Width} × {map.Height} tiles, {(map.Levels > 1 ? $"{map.Levels} levels" : "1 level")}, {state.Players.Count()} player(s).\n\n");

        var errors = issues.Count(i => i.Severity == IssueSeverity.Error);
        text.Append("## Problems\n\n");
        if (issues.Count == 0)
        {
            text.Append("No problems found.\n\n");
        }
        else
        {
            text.Append(CultureInfo.InvariantCulture, $"{errors} error(s), {issues.Count - errors} warning(s).\n\n");
            foreach (var issue in issues.OrderBy(i => i.Severity == IssueSeverity.Error ? 0 : 1))
            {
                var where = issue.Position is { } p ? $" {p}" : "";
                var fix = issue.Fix is { } f ? $" Fix: {Line(f.Text)}." : "";
                text.Append(CultureInfo.InvariantCulture, $"- **{issue.Severity}** ({issue.Kind}): {Line(issue.Message)}{where}{fix}\n");
            }

            text.Append('\n');
        }

        text.Append("## Fairness\n\n");
        if (fairness.Starts.Count == 0)
        {
            text.Append("The map has no start: no player has a town or a hero on it.\n");
            return text.ToString();
        }

        var table = Table(state, fairness);
        text.Append(CultureInfo.InvariantCulture, $"| {string.Join(" | ", table.Headers.Select(Cell))} |\n");
        text.Append(CultureInfo.InvariantCulture, $"|{string.Concat(table.Headers.Select(_ => " --- |"))}\n");
        foreach (var row in table.Rows)
        {
            text.Append(CultureInfo.InvariantCulture, $"| {string.Join(" | ", row.Select(Cell))} |\n");
        }

        text.Append(CultureInfo.InvariantCulture, $"\n{Legend}\n\n");
        text.Append("### Warnings\n\n");
        if (fairness.Warnings.Count == 0)
        {
            text.Append(fairness.Starts.Count == 1
                ? "One start: there is nothing to compare it with.\n"
                : $"No start is more than {MapAnalysis.WarningPercent} % from the starts' mean.\n");
        }
        else
        {
            foreach (var warning in fairness.Warnings)
            {
                text.Append(CultureInfo.InvariantCulture, $"- {Line(warning)}\n");
            }
        }

        return text.ToString();
    }

    /// <summary>What the table's columns mean, under it in the report and as the panel's note.</summary>
    public static string Legend =>
        $"Days are a scout's days from the start (its town's entrance, else its hero) to the nearest one of a kind, {NoneReached} where none is reached. "
        + $"Treasure is in gold, guards are army power, counted within {MapAnalysis.NearDays} and {MapAnalysis.FarDays} days. "
        + $"A warning names each number more than {MapAnalysis.WarningPercent} % from the starts' mean.";

    /// <summary>"Player 1 (Blue)": the number the warnings name the player by, and its name.</summary>
    private static string PlayerName(GameState state, StartAnalysis start) =>
        state.Players.FirstOrDefault(p => p.Id == start.Player)?.Name is { Length: > 0 } name
            ? $"Player {start.Player.Value + 1} ({name})"
            : $"Player {start.Player.Value + 1}";

    private static string Days(int? days) => days is { } d ? d.ToString(CultureInfo.InvariantCulture) : NoneReached;

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>A text on one line: a line break would end a list item or a table row.</summary>
    private static string Line(string text) => text.ReplaceLineEndings(" ");

    /// <summary>A table cell: a bar would end the cell.</summary>
    private static string Cell(string text) => Line(text).Replace("|", "\\|", StringComparison.Ordinal);
}
