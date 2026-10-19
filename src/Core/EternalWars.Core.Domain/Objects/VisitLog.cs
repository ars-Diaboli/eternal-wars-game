using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Objects;

/// <summary>How often a map object gives its reward (ADR-068); set on its <see cref="MapObjectDefinition"/>.</summary>
public enum VisitScope
{
    /// <summary>Every visit counts (mines, gates, marketplaces, piles that vanish anyway): the default.</summary>
    Unlimited,

    /// <summary>Once a week per object: the first visit of a week collects, any later one that week gets nothing (Water Wheel, Windmill).</summary>
    Weekly,

    /// <summary>Once per hero: every hero may collect once (Learning Stone, Arena).</summary>
    OncePerHero,

    /// <summary>Only the very first visit collects; later visitors get nothing, or only a penalty (Corpse, Warrior's Tomb).</summary>
    FirstVisitor,
}

/// <summary>
/// What a map object remembers about the rewards taken from it (ADR-068). Only domain services record visits. The log
/// starts empty in every game and is not part of the map format: saves replay the commands, which rebuild it.
/// </summary>
public sealed class VisitLog
{
    private readonly SortedSet<int> _heroes = [];

    /// <summary>The <see cref="GameDate.AbsoluteWeek"/> of the last collection, or null if nobody collected yet.</summary>
    public int? CollectedWeek { get; private set; }

    /// <summary>Heroes that collected here.</summary>
    public IEnumerable<HeroId> Heroes => _heroes.Select(h => new HeroId(h));

    /// <summary>Whether anyone collected here.</summary>
    public bool IsSpent => CollectedWeek is not null;

    /// <summary>Whether <paramref name="hero"/> may collect the reward today under <paramref name="scope"/>.</summary>
    public bool CanCollect(VisitScope scope, HeroId hero, GameDate date) => scope switch
    {
        VisitScope.Weekly => CollectedWeek != date.AbsoluteWeek,
        VisitScope.OncePerHero => !_heroes.Contains(hero.Value),
        VisitScope.FirstVisitor => !IsSpent,
        _ => true,
    };

    internal void Record(HeroId hero, GameDate date)
    {
        _heroes.Add(hero.Value);
        CollectedWeek = date.AbsoluteWeek;
    }

    public override string ToString() => IsSpent ? $"week {CollectedWeek} heroes {string.Join(',', _heroes)}" : "";
}
