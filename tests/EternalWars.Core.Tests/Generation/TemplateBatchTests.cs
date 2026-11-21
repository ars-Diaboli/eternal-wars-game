using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Visibility;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The template editor's preview picture and Generate many (ADR-274): <see cref="ZonePicture"/> and <see cref="TemplateBatch"/>.</summary>
public sealed class TemplateBatchTests
{
    private static readonly MapTemplate Template = TemplateEditorSession.Blank();

    [Fact]
    public async Task ABatch_EqualsSingleRuns_InSeedOrder()
    {
        var reported = new System.Collections.Concurrent.ConcurrentBag<ulong>();
        var batch = await TemplateBatch.RunAsync(Template, 72, 40, count: 4, parallel: 3, done: m => reported.Add(m.Seed), cancel: TestContext.Current.CancellationToken);

        Assert.Equal([40UL, 41UL, 42UL, 43UL], batch.Select(m => m.Seed));
        Assert.Equal([40UL, 41UL, 42UL, 43UL], reported.Order());
        foreach (var map in batch)
        {
            var single = TemplateBatch.Check(Template, 72, map.Seed);
            Assert.Equal(GameStateDigest.Compute(single.Map.Scenario), GameStateDigest.Compute(map.Map.Scenario));
            Assert.Equal((single.Attempt, single.Passed, single.SameTwice), (map.Attempt, map.Passed, map.SameTwice));
            Assert.Equal(single.Failures, map.Failures);
            Assert.Equal(single.Starts, map.Starts);
        }
    }

    [Fact]
    public void Check_GeneratesEverySlotAsAPlayer_AndPassesAFairTemplate()
    {
        var map = TemplateBatch.Check(Template, 72, 7);

        Assert.True(map.SameTwice);
        Assert.Empty(map.Unreachable);
        Assert.Equal(map.Passed, map.Failures.Count == 0);
        Assert.Equal(Template.MaxPlayers, map.Map.Scenario.Players.Count);
        Assert.Equal(2, map.Starts.Count);
        Assert.Equal(GameStateDigest.Compute(TemplateBatch.Generate(Template, 72, 7).Scenario), GameStateDigest.Compute(map.Map.Scenario));
    }

    [Fact]
    public async Task Cancelling_StopsStartingMaps()
    {
        using var cancel = new CancellationTokenSource();
        var done = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TemplateBatch.RunAsync(Template, 72, 1, count: 6, parallel: 1, done: _ =>
        {
            Interlocked.Increment(ref done);
            cancel.Cancel();
        }, cancel: cancel.Token));
        Assert.Equal(1, done);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TemplateBatch.RunAsync(Template, 72, 1, count: 2, cancel: new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task ABatch_RefusesWhatCannotBeGenerated_UpFront()
    {
        var broken = Template with { Connections = [] };
        await Assert.ThrowsAsync<ArgumentException>(() => TemplateBatch.RunAsync(broken, 72, 1, count: 1, cancel: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => TemplateBatch.RunAsync(Template, 50, 1, count: 1, cancel: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => TemplateBatch.RunAsync(Template, 72, 1, count: 0, cancel: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => TemplateBatch.RunAsync(Template, 72, 1, count: TemplateBatch.MaxCount + 1, cancel: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => TemplateBatch.RunAsync(Template, 72, 1, count: 1, parallel: 0, cancel: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void TheSeeds_CountUpFromTheFirst_WrappingPastTheLargest()
    {
        Assert.Equal([ulong.MaxValue, 0UL, 1UL], TemplateBatch.Seeds(ulong.MaxValue, 3));
    }

    [Fact]
    public void Imbalance_AndTheSummary_AreTheStartsSpreadOverTheirMean()
    {
        var maps = new[] { TemplateBatch.Check(Template, 72, 3), TemplateBatch.Check(Template, 72, 4) };
        foreach (var map in maps)
        {
            var values = map.Starts.Select(s => (double)s.TreasureValue).ToList();
            var expected = values.Average() <= 0 ? 0 : (values.Max() - values.Min()) * 100 / values.Average();
            Assert.Equal(expected, map.Imbalance(FairnessMeasure.TreasureValue), 6);
        }

        var summary = TemplateBatch.Summarize(maps);
        Assert.Equal((2, maps.Count(m => m.Passed)), (summary.Maps, summary.Passed));
        var guards = maps.Select(m => m.Imbalance(FairnessMeasure.GuardPower)).ToList();
        Assert.Equal(new Spread(guards.Min(), guards.Average(), guards.Max()), summary.Imbalance[FairnessMeasure.GuardPower]);
        Assert.Equal(default, TemplateBatch.Summarize([]).Attempts);
    }

    [Fact]
    public void ThePicture_ShowsTheMinimapsTiles_TheZonesBorders_AndALabelInEachZone()
    {
        var map = TemplateBatch.Generate(Template, 72, 11);
        var picture = ZonePicture.Of(map);
        var layout = map.Generation.Map.Layout;

        Assert.Equal((72, 72, 1), (picture.Width, picture.Height, picture.Levels));
        Assert.Equal(layout.ZoneIds, picture.ZoneIds);
        var cells = Minimap.Build(map.Scenario, null, 0);
        for (var y = 0; y < 72; y++)
        {
            for (var x = 0; x < 72; x++)
            {
                Assert.Equal(cells[(y * 72) + x], picture.Cell(x, y, 0));
                Assert.Equal(layout.ZoneAt(x, y, 0), picture.Zone(x, y, 0));
                Assert.Equal(x < 71 && layout.ZoneAt(x + 1, y, 0) != layout.ZoneAt(x, y, 0), picture.BorderRight(x, y, 0));
                Assert.Equal(y < 71 && layout.ZoneAt(x, y + 1, 0) != layout.ZoneAt(x, y, 0), picture.BorderBelow(x, y, 0));
            }
        }

        Assert.Equal(Template.Zones.Select(z => z.Id), picture.Labels(0).Select(l => l.Id));
        Assert.All(picture.Labels(0), l => Assert.Equal(l.Id, layout.IdAt(l.X, l.Y, 0)));
    }

    [Fact]
    public void ThePicture_RefusesALayoutOfAnotherSize()
    {
        var map = TemplateBatch.Generate(Template, 72, 11);
        var other = TemplateBatch.Generate(Template, 96, 11);
        Assert.Throws<ArgumentException>(() => ZonePicture.Of(map.Scenario, other.Generation.Map.Layout));
    }
}
