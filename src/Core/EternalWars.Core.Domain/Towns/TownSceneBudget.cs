using System.Globalization;

namespace EternalWars.Core.Domain.Towns;

/// <summary>One frame's rendering numbers, as the budget overlay reads them from the renderer (ADR-308).</summary>
/// <param name="Triangles">Triangles drawn in the frame, shadow passes included.</param>
/// <param name="DrawCalls">Draw calls in the frame, shadow passes included.</param>
/// <param name="VideoMemoryBytes">Video memory in use by the game.</param>
/// <param name="CpuMilliseconds">The frame's time on the processor: its game logic and the renderer's work.</param>
/// <param name="GpuMilliseconds">The frame's time on the graphics card.</param>
public sealed record FrameSample(long Triangles, long DrawCalls, long VideoMemoryBytes, double CpuMilliseconds, double GpuMilliseconds)
{
    /// <summary>The part of <see cref="DrawCalls"/> that draws the shadow maps; null when not known.</summary>
    public long? ShadowDrawCalls { get; init; }

    /// <summary>How long the town scene shown took to open (ADR-306); null when no town scene shows.</summary>
    public double? OpenMilliseconds { get; init; }

    /// <summary>What sets the frame rate: the slower of the processor and the graphics card.</summary>
    public double FrameMilliseconds => Math.Max(CpuMilliseconds, GpuMilliseconds);
}

/// <summary>Where the town scene is measured: the reference PC at 1440p, or the Steam Deck at its own 1280 × 800 with the Deck preset.</summary>
public enum BudgetTarget
{
    /// <summary>The reference PC at 2560 × 1440, 60 fps.</summary>
    ReferencePc,

    /// <summary>The Steam Deck at 1280 × 800, 40 fps.</summary>
    SteamDeck,
}

/// <summary>One line of the overlay: a number, its limit and the share of it used.</summary>
public sealed record BudgetLine(string Label, string Value, string Limit, double Share)
{
    /// <summary>Over the budget.</summary>
    public bool Over => Share > 1;
}

/// <summary>
/// The town scene's budget at full build (ADR-308, the town screen plan's rendering section): triangles, draw calls, video memory and frame time,
/// the whole frame counted as the renderer counts it (the scene's depth pass, its colour pass and its four shadow cascades, the bars over it,
/// and the game's video memory as a whole). The same scene runs on both targets, so the counts are the same; only the frame time differs (60
/// fps on the reference PC, 40 on the Deck). The fully built greybox towns measured against it are step 3's baseline; the art of step 4 gets
/// what is left, each model its share.
/// </summary>
public static class TownSceneBudget
{
    /// <summary>Triangles in a frame.</summary>
    public const long Triangles = 1_500_000;

    /// <summary>Draw calls in a frame, the depth pass and the shadow cascades included (the greybox towns draw 850 to 1,450).</summary>
    public const long DrawCalls = 2_000;

    /// <summary>The game's video memory in use while the town scene shows, in bytes: 1 GiB.</summary>
    public const long VideoMemoryBytes = 1L << 30;

    /// <summary>The time the town scene may take to open (TOWN-23, ADR-306).</summary>
    public const double OpenMilliseconds = 1000;

    /// <summary>The frame time a target's frame rate allows: 60 fps on the reference PC, 40 on the Deck.</summary>
    public static double FrameMilliseconds(BudgetTarget target) => 1000.0 / (target == BudgetTarget.SteamDeck ? 40 : 60);

    /// <summary>
    /// The overlay's lines for a frame: triangles, draw calls, video memory, frame time (with the processor's and the graphics card's), and the
    /// town scene's opening time while one shows.
    /// </summary>
    public static IReadOnlyList<BudgetLine> Lines(FrameSample sample, BudgetTarget target)
    {
        ArgumentNullException.ThrowIfNull(sample);
        var frame = FrameMilliseconds(target);
        List<BudgetLine> lines =
        [
            new("Triangles", Count(sample.Triangles), Count(Triangles), (double)sample.Triangles / Triangles),
            new(
                "Draw calls", sample.ShadowDrawCalls is { } shadows ? $"{Count(sample.DrawCalls)} ({Count(shadows)} shadows)" : Count(sample.DrawCalls),
                Count(DrawCalls), (double)sample.DrawCalls / DrawCalls),
            new("Video memory", Megabytes(sample.VideoMemoryBytes), Megabytes(VideoMemoryBytes), (double)sample.VideoMemoryBytes / VideoMemoryBytes),
            new(
                "Frame time",
                $"{Milliseconds(sample.FrameMilliseconds)} (CPU {Milliseconds(sample.CpuMilliseconds)}, GPU {Milliseconds(sample.GpuMilliseconds)})",
                Milliseconds(frame), sample.FrameMilliseconds / frame),
        ];
        if (sample.OpenMilliseconds is { } open)
        {
            lines.Add(new("Opened in", Milliseconds(open), Milliseconds(OpenMilliseconds), open / OpenMilliseconds));
        }

        return lines;
    }

    /// <summary>A frame's numbers on one line, for a log or a report.</summary>
    public static string Describe(FrameSample sample, BudgetTarget target) =>
        string.Join("; ", Lines(sample, target).Select(l => $"{l.Label.ToLowerInvariant()} {l.Value} of {l.Limit}{(l.Over ? " OVER" : "")}"));

    private static string Count(long value) => value.ToString("N0", CultureInfo.InvariantCulture);

    private static string Megabytes(long bytes) => $"{(bytes / (1024.0 * 1024.0)).ToString("N0", CultureInfo.InvariantCulture)} MB";

    private static string Milliseconds(double value) => $"{value.ToString("0.0", CultureInfo.InvariantCulture)} ms";
}
