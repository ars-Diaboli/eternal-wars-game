using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Generation;

/// <summary>
/// Independent random streams for the steps of the generator pipeline. Every step asks for its own stream by name
/// (<c>"terrain"</c>, <c>"towns"</c>, …), derived from the map seed, so changing or adding one step never shifts the
/// numbers another step draws: a map stays reproducible from its seed while the generator is tuned step by step.
/// Names are hashed with FNV-1a, which is fixed (unlike <see cref="string.GetHashCode()"/>), so streams are identical on every platform.
/// </summary>
public sealed class SeedStreams(ulong seed)
{
    public ulong Seed { get; } = seed;

    /// <summary>A fresh generator for the named stream; asking twice gives two generators with the same sequence.</summary>
    public GameRandom For(string name) => For(name, 0);

    /// <summary>A fresh generator for the named stream of one numbered item (a zone, a player), so items do not depend on each other's draws.</summary>
    public GameRandom For(string name, int index)
    {
        var mix = new GameRandom(Seed ^ Fnv1a(name) ^ ((ulong)(uint)index * 0x9E3779B97F4A7C15UL));
        mix.NextUInt64(); // one round of mixing so close seeds and indices do not give close sequences
        return new GameRandom(mix.NextUInt64());
    }

    private static ulong Fnv1a(string text)
    {
        var hash = 14695981039346656037UL;
        foreach (var c in text)
        {
            hash = (hash ^ c) * 1099511628211UL;
        }

        return hash;
    }
}
