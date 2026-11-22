using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The town scene's budget and the overlay's lines (the town screen plan's step 3, task 8, ADR-308).</summary>
public class TownSceneBudgetTests
{
    /// <summary>Haven's fully built greybox scene as measured at 1440p (ADR-308).</summary>
    private static readonly FrameSample Haven = new(32_722, 853, 464L << 20, 9.4, 4.0) { ShadowDrawCalls = 449 };

    [Fact]
    public void Lines_show_each_number_against_its_limit()
    {
        var lines = TownSceneBudget.Lines(Haven, BudgetTarget.ReferencePc);

        Assert.Equal(["Triangles", "Draw calls", "Video memory", "Frame time"], lines.Select(l => l.Label));
        Assert.Equal(["32,722", "853 (449 shadows)", "464 MB", "9.4 ms (CPU 9.4 ms, GPU 4.0 ms)"], lines.Select(l => l.Value));
        Assert.Equal(["1,500,000", "2,000", "1,024 MB", "16.7 ms"], lines.Select(l => l.Limit));
        Assert.All(lines, l => Assert.False(l.Over));
    }

    [Fact]
    public void The_frame_time_is_the_slower_of_the_processor_and_the_graphics_card()
    {
        var gpuBound = Haven with { CpuMilliseconds = 6, GpuMilliseconds = 20 };

        Assert.Equal(20, gpuBound.FrameMilliseconds);
        Assert.True(TownSceneBudget.Lines(gpuBound, BudgetTarget.ReferencePc)[3].Over);
    }

    [Fact]
    public void The_deck_allows_40_fps_with_the_same_counts()
    {
        var slow = Haven with { CpuMilliseconds = 20, GpuMilliseconds = 20 };

        Assert.Equal(25, TownSceneBudget.FrameMilliseconds(BudgetTarget.SteamDeck), 6);
        Assert.False(TownSceneBudget.Lines(slow, BudgetTarget.SteamDeck)[3].Over);
        Assert.True(TownSceneBudget.Lines(slow, BudgetTarget.ReferencePc)[3].Over);
    }

    [Fact]
    public void The_opening_time_shows_while_a_town_scene_does()
    {
        var lines = TownSceneBudget.Lines(Haven with { OpenMilliseconds = 15.2 }, BudgetTarget.ReferencePc);

        Assert.Equal(5, lines.Count);
        Assert.Equal(("Opened in", "15.2 ms", "1000.0 ms"), (lines[4].Label, lines[4].Value, lines[4].Limit));
        Assert.Equal(0.0152, lines[4].Share, 6);
        Assert.True(TownSceneBudget.Lines(Haven with { OpenMilliseconds = 1200 }, BudgetTarget.ReferencePc)[4].Over);
    }

    [Fact]
    public void A_number_over_its_limit_is_marked_and_the_shadows_are_left_out_when_unknown()
    {
        var heavy = new FrameSample(1_600_000, 2_100, 900L << 20, 10, 10);

        var lines = TownSceneBudget.Lines(heavy, BudgetTarget.ReferencePc);

        Assert.Equal([true, true, false, false], lines.Select(l => l.Over));
        Assert.Equal("2,100", lines[1].Value);
        Assert.Equal(
            "triangles 1,600,000 of 1,500,000 OVER; draw calls 2,100 of 2,000 OVER; video memory 900 MB of 1,024 MB; frame time 10.0 ms (CPU 10.0 ms, GPU 10.0 ms) of 16.7 ms",
            TownSceneBudget.Describe(heavy, BudgetTarget.ReferencePc));
    }
}
