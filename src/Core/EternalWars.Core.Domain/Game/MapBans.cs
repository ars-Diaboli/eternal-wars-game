namespace EternalWars.Core.Domain.Game;

/// <summary>
/// The heroes, artifacts, spells and skills a map bans (ADR-126, HoMM3 and HotA; map format v35). Every pool and roll skips them: the
/// taverns, prisons and hero placeholders, the artifact rolls (pickups, chests, banks, merchants, loot, sphinxes, battle loot), the guild and spell
/// building draws, shrines, level-ups and witch huts. What the author places by hand is kept (the validator warns); a banned skill a hero starts
/// with stays but is never offered; a ban that would empty a pool leaves that pool whole (<see cref="Allowed{T}"/>). Ids are kept sorted.
/// </summary>
public sealed record MapBans
{
    public static MapBans None { get; } = new();

    public IReadOnlyList<string> Heroes { get; init; } = [];

    public IReadOnlyList<string> Artifacts { get; init; } = [];

    public IReadOnlyList<string> Spells { get; init; } = [];

    public IReadOnlyList<string> Skills { get; init; } = [];

    public bool IsEmpty => Heroes.Count == 0 && Artifacts.Count == 0 && Spells.Count == 0 && Skills.Count == 0;

    public bool BansHero(string id) => Heroes.Contains(id, StringComparer.Ordinal);

    public bool BansArtifact(string id) => Artifacts.Contains(id, StringComparer.Ordinal);

    public bool BansSpell(string id) => Spells.Contains(id, StringComparer.Ordinal);

    public bool BansSkill(string id) => Skills.Contains(id, StringComparer.Ordinal);

    /// <summary>The same bans with <paramref name="id"/> of a kind banned or allowed; the list stays sorted and without repeats.</summary>
    public MapBans With(MapBanKind kind, string id, bool banned)
    {
        IReadOnlyList<string> Change(IReadOnlyList<string> ids) =>
            (banned ? ids.Append(id) : ids.Where(i => i != id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        return kind switch
        {
            MapBanKind.Hero => this with { Heroes = Change(Heroes) },
            MapBanKind.Artifact => this with { Artifacts = Change(Artifacts) },
            MapBanKind.Spell => this with { Spells = Change(Spells) },
            _ => this with { Skills = Change(Skills) },
        };
    }

    /// <summary>The ids of a kind.</summary>
    public IReadOnlyList<string> Of(MapBanKind kind) => kind switch
    {
        MapBanKind.Hero => Heroes,
        MapBanKind.Artifact => Artifacts,
        MapBanKind.Spell => Spells,
        _ => Skills,
    };

    /// <summary>The pool without what is banned; the whole pool when the bans would leave nothing (ADR-126).</summary>
    public static IReadOnlyList<T> Allowed<T>(IReadOnlyList<T> pool, Func<T, bool> banned)
    {
        var allowed = pool.Where(p => !banned(p)).ToList();
        return allowed.Count > 0 ? allowed : pool;
    }

    public bool Equals(MapBans? other) => other is not null && Heroes.SequenceEqual(other.Heroes) && Artifacts.SequenceEqual(other.Artifacts)
        && Spells.SequenceEqual(other.Spells) && Skills.SequenceEqual(other.Skills);

    public override int GetHashCode() => HashCode.Combine(Heroes.Count, Artifacts.Count, Spells.Count, Skills.Count);
}

/// <summary>What a map ban names.</summary>
public enum MapBanKind
{
    Hero,
    Artifact,
    Spell,
    Skill,
}
