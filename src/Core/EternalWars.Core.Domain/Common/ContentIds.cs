namespace EternalWars.Core.Domain.Common;

/// <summary>
/// The id of a content definition (ADR-243): a creature, spell, artifact or building of the content tables. Each kind is a type of its own, so one
/// kind cannot be passed where another is wanted. Files and messages write it as its plain string (the format does not change); a string turns
/// into an id without a cast (the tables, the files and the literals in code), an id into a string only through <see cref="Value"/>.
/// </summary>
public interface IContentId<TSelf> : IComparable<TSelf>
    where TSelf : struct, IContentId<TSelf>
{
    /// <summary>The id as written in the content tables and files; empty for a default value.</summary>
    string Value { get; }

    static abstract TSelf From(string value);
}

/// <summary>A creature's id (<see cref="Creatures.CreatureDefinition.Id"/>), such as <c>haven.pikeman</c>.</summary>
public readonly record struct CreatureId : IContentId<CreatureId>
{
    private readonly string? _value;

    public CreatureId(string value) => _value = value ?? throw new ArgumentNullException(nameof(value));

    public string Value => _value ?? "";

    public static CreatureId From(string value) => new(value);

    public static implicit operator CreatureId(string value) => new(value);

    public int CompareTo(CreatureId other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;
}

/// <summary>A spell's id (<see cref="Magic.SpellDefinition.Id"/>), such as <c>light.bless</c>.</summary>
public readonly record struct SpellId : IContentId<SpellId>
{
    private readonly string? _value;

    public SpellId(string value) => _value = value ?? throw new ArgumentNullException(nameof(value));

    public string Value => _value ?? "";

    public static SpellId From(string value) => new(value);

    public static implicit operator SpellId(string value) => new(value);

    public int CompareTo(SpellId other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;
}

/// <summary>An artifact's id (<see cref="Artifacts.ArtifactDefinition.Id"/>).</summary>
public readonly record struct ArtifactId : IContentId<ArtifactId>
{
    private readonly string? _value;

    public ArtifactId(string value) => _value = value ?? throw new ArgumentNullException(nameof(value));

    public string Value => _value ?? "";

    public static ArtifactId From(string value) => new(value);

    public static implicit operator ArtifactId(string value) => new(value);

    public int CompareTo(ArtifactId other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;
}

/// <summary>A town building's id (the keys of <see cref="Towns.BuildingCatalog"/>), such as <c>town_hall</c>.</summary>
public readonly record struct BuildingId : IContentId<BuildingId>
{
    private readonly string? _value;

    public BuildingId(string value) => _value = value ?? throw new ArgumentNullException(nameof(value));

    public string Value => _value ?? "";

    public static BuildingId From(string value) => new(value);

    public static implicit operator BuildingId(string value) => new(value);

    public int CompareTo(BuildingId other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value;
}
