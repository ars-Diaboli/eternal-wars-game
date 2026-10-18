using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Artifacts;

/// <summary>
/// The artifact tables as CSV (<c>data/artifacts.csv</c> and <c>data/artifact_sets.csv</c>), same conventions as the creature
/// table: <c>id, name, slot, tier, set, effects, notes</c> and <c>id, name, effects, notes</c>; effects are separated by
/// <c>;</c> (<see cref="ArtifactEffect.Parse"/>).
/// </summary>
public static class ArtifactTable
{
    public const string Id = "id";
    public const string Name = "name";
    public const string Slot = "slot";
    public const string Tier = "tier";
    public const string Set = "set";
    public const string Effects = "effects";

    public static IReadOnlyList<ArtifactDefinition> ParseArtifacts(string text) =>
        CsvTable.Parse(text, "Artifact", [Id, Name, Slot, Tier])
            .Read(row => row.Text(Id) is { Length: > 0 } id
                ? new ArtifactDefinition(id, row.Text(Name), row.Enum<ArtifactSlot>(Slot), row.Enum<ArtifactTier>(Tier), row.Optional(Set), ReadEffects(row), row.Optional("notes") ?? "")
                : null)
            .OfType<ArtifactDefinition>()
            .ToList();

    public static IReadOnlyList<ArtifactSetDefinition> ParseSets(string text) =>
        CsvTable.Parse(text, "Artifact set", [Id, Name])
            .Read(row => row.Text(Id) is { Length: > 0 } id ? new ArtifactSetDefinition(id, row.Text(Name), ReadEffects(row), row.Optional("notes") ?? "") : null)
            .OfType<ArtifactSetDefinition>()
            .ToList();

    private static IReadOnlyList<ArtifactEffect> ReadEffects(CsvTable.Row row)
    {
        try
        {
            return row.List(Effects).Select(ArtifactEffect.Parse).ToList();
        }
        catch (FormatException e)
        {
            throw row.Error(Effects, e.Message);
        }
    }
}

/// <summary>The artifacts of a game by id, with their sets. Start from <see cref="Default"/> (the embedded tables) or a designer's copy.</summary>
public sealed class ArtifactCatalog
{
    private readonly Dictionary<string, ArtifactDefinition> _artifacts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ArtifactSetDefinition> _sets = new(StringComparer.Ordinal);

    public ArtifactCatalog(IEnumerable<ArtifactDefinition> artifacts, IEnumerable<ArtifactSetDefinition>? sets = null)
    {
        foreach (var set in sets ?? [])
        {
            if (!_sets.TryAdd(set.Id, set))
            {
                throw new FormatException($"Duplicate artifact set '{set.Id}'.");
            }
        }

        foreach (var artifact in artifacts)
        {
            if (!_artifacts.TryAdd(artifact.Id, artifact))
            {
                throw new FormatException($"Duplicate artifact '{artifact.Id}'.");
            }

            if (artifact.Set is { } set && !_sets.ContainsKey(set))
            {
                throw new FormatException($"Artifact '{artifact.Id}' is in the unknown set '{set}'.");
            }
        }

        foreach (var set in _sets.Values.Where(s => PiecesOf(s.Id).Count() < 2))
        {
            throw new FormatException($"Artifact set '{set.Id}' needs at least two pieces.");
        }
    }

    public static ArtifactCatalog Default { get; } = new(
        ArtifactTable.ParseArtifacts(BuiltInArtifacts.EmbeddedTable(BuiltInArtifacts.ArtifactsResource)),
        ArtifactTable.ParseSets(BuiltInArtifacts.EmbeddedTable(BuiltInArtifacts.SetsResource)));

    public IEnumerable<ArtifactDefinition> All => _artifacts.Values;

    public IEnumerable<ArtifactSetDefinition> Sets => _sets.Values;

    public bool Contains(string id) => _artifacts.ContainsKey(id);

    public bool TryGet(string id, out ArtifactDefinition artifact) => _artifacts.TryGetValue(id, out artifact!);

    public ArtifactDefinition Get(string id) =>
        _artifacts.TryGetValue(id, out var artifact) ? artifact : throw new KeyNotFoundException($"Unknown artifact '{id}'.");

    public ArtifactSetDefinition GetSet(string id) =>
        _sets.TryGetValue(id, out var set) ? set : throw new KeyNotFoundException($"Unknown artifact set '{id}'.");

    public IEnumerable<ArtifactDefinition> PiecesOf(string set) => _artifacts.Values.Where(a => a.Set == set);

    public IEnumerable<ArtifactDefinition> OfTier(ArtifactTier tier) => _artifacts.Values.Where(a => a.Tier == tier);
}

/// <summary>The shipped artifact tables, embedded at build time (<c>data/artifacts.csv</c>, <c>data/artifact_sets.csv</c>).</summary>
public static class BuiltInArtifacts
{
    public const string ArtifactsResource = "EternalWars.Artifacts.csv";
    public const string SetsResource = "EternalWars.ArtifactSets.csv";

    public static string EmbeddedTable(string resource)
    {
        using var stream = typeof(BuiltInArtifacts).Assembly.GetManifestResourceStream(resource)
                           ?? throw new InvalidOperationException($"The artifact table '{resource}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
