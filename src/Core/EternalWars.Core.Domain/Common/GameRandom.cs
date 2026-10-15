namespace EternalWars.Core.Domain.Common;

/// <summary>
/// The game's only source of randomness: a seeded, serializable SplitMix64 generator. Its whole state is
/// one <see cref="ulong"/>, so the same seed and the same commands give the same game on every platform
/// (replays, saves, tests, AI simulation). Never use <see cref="System.Random"/> in rules.
/// </summary>
public sealed class GameRandom(ulong state)
{
    /// <summary>The complete generator state; persist it to resume the sequence exactly.</summary>
    public ulong State { get; private set; } = state;

    public ulong NextUInt64()
    {
        State += 0x9E3779B97F4A7C15UL;
        var z = State;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    /// <summary>Uniform in [0, <paramref name="maxExclusive"/>) (Lemire's multiply-high, no modulo bias worth noting).</summary>
    public int Next(int maxExclusive)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxExclusive, 1);
        return (int)(((UInt128)NextUInt64() * (ulong)maxExclusive) >> 64);
    }

    /// <summary>Uniform in [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public int NextInclusive(int min, int max)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(max, min);
        return min + Next(max - min + 1);
    }

    /// <summary>An independent generator seeded from this one (e.g. one per battle).</summary>
    public GameRandom Fork() => new(NextUInt64());
}
