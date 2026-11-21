using System.Globalization;

namespace EternalWars.Core.Domain.Editing;

/// <summary>Frame times over a stretch of frames, in milliseconds (ADR-278).</summary>
/// <param name="Frames">How many frames were timed.</param>
/// <param name="MeanMs">The mean frame time.</param>
/// <param name="P95Ms">The frame time 95 % of the frames stay within.</param>
/// <param name="WorstMs">The longest frame.</param>
public sealed record FrameStats(int Frames, double MeanMs, double P95Ms, double WorstMs)
{
    public static FrameStats None { get; } = new(0, 0, 0, 0);

    /// <summary>The frames per second the mean frame time gives (0 when nothing was timed).</summary>
    public double MeanFps => MeanMs > 0 ? 1000 / MeanMs : 0;

    public static FrameStats Of(IReadOnlyCollection<double> milliseconds)
    {
        if (milliseconds.Count == 0)
        {
            return None;
        }

        var sorted = milliseconds.Order().ToArray();
        var p95 = sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * 0.95) - 1)];
        return new FrameStats(sorted.Length, sorted.Average(), p95, sorted[^1]);
    }
}

/// <summary>
/// The map editor's timing on a large map (ADR-278): opening it, painting the terrain and the validation after an edit, against the budgets of
/// the editor plan: open within <see cref="OpenBudgetMs"/>, paint at <see cref="PaintFps"/> frames a second, and no frame while the map is
/// validated longer than <see cref="StallMs"/>.
/// </summary>
/// <param name="Map">What was timed (its size and levels).</param>
/// <param name="OpenMs">From the open command to the end of the first frame that shows the map.</param>
/// <param name="ReadMs">Reading the file into a game state, a part of <paramref name="OpenMs"/>.</param>
/// <param name="Idle">The frames with nothing happening, the map in view.</param>
/// <param name="Painting">The frames of a terrain brush stroke.</param>
/// <param name="Validating">The frames from the end of the stroke until the problems are shown.</param>
/// <param name="SnapshotMs">The validation's part on the main thread: copying the map.</param>
/// <param name="ValidateMs">The validation's part on a task: checking the copy.</param>
/// <param name="ValidationFinished">Whether the Problems panel showed the problems after the stroke before the probe gave up waiting.</param>
public sealed record EditorTimings(
    string Map, double OpenMs, double ReadMs, FrameStats Idle, FrameStats Painting, FrameStats Validating, double SnapshotMs, double ValidateMs,
    bool ValidationFinished = true)
{
    public const double OpenBudgetMs = 3000;
    public const double PaintFps = 60;
    public const double StallMs = 50;

    /// <summary>The budgets missed, in words; empty when every one is met.</summary>
    public IReadOnlyList<string> Misses()
    {
        var misses = new List<string>();
        if (OpenMs > OpenBudgetMs)
        {
            misses.Add(Invariant($"opening took {OpenMs:0} ms, over {OpenBudgetMs:0} ms"));
        }

        if (Painting.Frames == 0 || Painting.MeanFps < PaintFps)
        {
            misses.Add(Invariant($"painting ran at {Painting.MeanFps:0.#} fps, under {PaintFps:0}"));
        }

        if (!ValidationFinished)
        {
            misses.Add("the Problems panel never showed the problems after the stroke");
        }

        if (Validating.WorstMs > StallMs || SnapshotMs > StallMs)
        {
            misses.Add(Invariant($"validation stalled a frame for {Math.Max(Validating.WorstMs, SnapshotMs):0} ms, over {StallMs:0} ms"));
        }

        return misses;
    }

    /// <summary>The report: one <c>name value</c> line a number, then the misses (or that every budget is met).</summary>
    public string Text()
    {
        static string Frames(string name, FrameStats f) =>
            Invariant($"{name,-12}{f.MeanFps,8:0.0} fps   mean {f.MeanMs,6:0.0} ms   p95 {f.P95Ms,6:0.0} ms   worst {f.WorstMs,6:0.0} ms   ({f.Frames} frames)");

        var lines = new List<string>
        {
            $"map         {Map}",
            Invariant($"open        {OpenMs,8:0} ms (reading {ReadMs:0} ms; budget {OpenBudgetMs:0} ms)"),
            Frames("idle", Idle),
            Frames("painting", Painting),
            Frames("validating", Validating),
            Invariant($"validation  {SnapshotMs,8:0.0} ms copying on the main thread, {ValidateMs:0} ms checking on a task (stall budget {StallMs:0} ms)"),
        };
        var misses = Misses();
        lines.AddRange(misses.Count == 0 ? ["every budget met"] : misses.Select(m => $"MISSED: {m}"));
        return string.Join('\n', lines);
    }

    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
