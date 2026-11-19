namespace EternalWars.Core.Domain.Heroes;

/// <summary>
/// How many times one game's heroes have had their <see cref="Hero.Position"/> set. Per game, so games running side by side (tests,
/// <c>sim game --runs</c>) do not make each other's hero index and route caches stale. Safe to read on another thread than the one moving.
/// </summary>
internal sealed class HeroMoveCounter
{
    private long _count;

    public long Count => Volatile.Read(ref _count);

    public void Add() => Interlocked.Increment(ref _count);
}
