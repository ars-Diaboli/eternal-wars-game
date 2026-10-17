using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The named random streams of the map generator (Phase 4).</summary>
public class SeedStreamsTests
{
    private static int[] Draw(EternalWars.Core.Domain.Common.GameRandom random) => Enumerable.Range(0, 8).Select(_ => random.Next(1000)).ToArray();

    [Fact]
    public void ASeedAndAName_AlwaysGiveTheSameSequence()
    {
        Assert.Equal(Draw(new SeedStreams(42).For("terrain")), Draw(new SeedStreams(42).For("terrain")));
        Assert.Equal(Draw(new SeedStreams(42).For("zone", 3)), Draw(new SeedStreams(42).For("zone", 3)));
    }

    [Fact]
    public void DifferentNamesIndicesAndSeeds_GiveDifferentSequences()
    {
        var streams = new SeedStreams(42);

        Assert.NotEqual(Draw(streams.For("terrain")), Draw(streams.For("towns")));
        Assert.NotEqual(Draw(streams.For("zone", 0)), Draw(streams.For("zone", 1)));
        Assert.NotEqual(Draw(new SeedStreams(1).For("terrain")), Draw(new SeedStreams(2).For("terrain")));
    }

    [Fact]
    public void AStreamDoesNotDependOnWhatOtherStreamsDrew()
    {
        var streams = new SeedStreams(7);
        var before = Draw(streams.For("monsters"));

        Draw(streams.For("terrain")); // another step runs first and draws a lot
        Draw(streams.For("terrain"));

        Assert.Equal(before, Draw(streams.For("monsters")));
    }

    [Fact]
    public void TheStreamsAreFixedForAllTime_SoSharedSeedsKeepTheirMaps()
    {
        // Changing these numbers breaks every shared map seed: only do it together with a generator version bump.
        Assert.Equal([225, 558, 252, 913], Draw(new SeedStreams(12345).For("pin")).Take(4));
    }
}
