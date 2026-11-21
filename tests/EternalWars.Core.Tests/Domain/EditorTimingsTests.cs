using EternalWars.Core.Domain.Editing;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The editor's timing report (ADR-278): frame statistics and the budgets of the editor plan.</summary>
public class EditorTimingsTests
{
    private static readonly FrameStats Smooth = FrameStats.Of([10, 10, 10, 10]);

    private static EditorTimings Timings(double openMs = 1200, FrameStats? painting = null, FrameStats? validating = null, double snapshotMs = 20) =>
        new("test map", openMs, 200, Smooth, painting ?? Smooth, validating ?? Smooth, snapshotMs, 90);

    [Fact]
    public void FrameStats_GiveTheMeanThe95thPercentileAndTheWorst()
    {
        var stats = FrameStats.Of([.. Enumerable.Repeat(10.0, 95), .. Enumerable.Repeat(40.0, 4), 100]);

        Assert.Equal(100, stats.Frames);
        Assert.Equal(12.1, stats.MeanMs, 6);
        Assert.Equal(10, stats.P95Ms);
        Assert.Equal(100, stats.WorstMs);
        Assert.Equal(1000 / 12.1, stats.MeanFps, 6);
    }

    [Fact]
    public void FrameStats_OfNothing_AreNone()
    {
        Assert.Equal(FrameStats.None, FrameStats.Of([]));
        Assert.Equal(0, FrameStats.None.MeanFps);
    }

    [Fact]
    public void WithinEveryBudget_NothingIsMissed()
    {
        var timings = Timings();

        Assert.Empty(timings.Misses());
        Assert.EndsWith("every budget met", timings.Text());
    }

    [Theory]
    [InlineData(3001, 10, 10, 20, "opening took 3001 ms, over 3000 ms")]
    [InlineData(1000, 17, 10, 20, "painting ran at 58.8 fps, under 60")]
    [InlineData(1000, 10, 51, 20, "validation stalled a frame for 51 ms, over 50 ms")]
    [InlineData(1000, 10, 10, 60, "validation stalled a frame for 60 ms, over 50 ms")]
    public void EachBudget_IsReportedWhenMissed(double openMs, double paintMs, double validatingMs, double snapshotMs, string miss)
    {
        var timings = Timings(openMs, FrameStats.Of([paintMs]), FrameStats.Of([validatingMs]), snapshotMs);

        Assert.Equal(miss, Assert.Single(timings.Misses()));
        Assert.Contains($"MISSED: {miss}", timings.Text(), StringComparison.Ordinal);
    }

    [Fact]
    public void AValidationThatNeverFinished_IsAMiss() =>
        Assert.Equal(
            "the Problems panel never showed the problems after the stroke",
            Assert.Single((Timings() with { ValidationFinished = false }).Misses()));

    [Fact]
    public void NoPaintedFrames_MissThePaintingBudget() =>
        Assert.Equal("painting ran at 0 fps, under 60", Assert.Single(Timings(painting: FrameStats.None).Misses()));
}
