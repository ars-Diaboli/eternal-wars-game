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
    public const string Notes = "notes";
    public const string Pieces = "pieces";

    /// <summary>The columns of the shipped artifact table, in order.</summary>
    public static IReadOnlyList<string> ArtifactColumns { get; } = [Id, Name, Slot, Tier, Set, Effects, Notes, Pieces];

    /// <summary>The columns of the shipped artifact set table, in order.</summary>
    public static IReadOnlyList<string> SetColumns { get; } = [Id, Name, Effects, Notes];

    public static IReadOnlyList<ArtifactDefinition> ParseArtifacts(string text) =>
        CsvTable.Parse(text, "Artifact", [Id, Name, Slot, Tier])
            .Read(row => row.Text(Id) is { Length: > 0 } id
                ? new ArtifactDefinition(id, row.Text(Name), row.Enum<ArtifactSlot>(Slot), row.Enum<ArtifactTier>(Tier), row.Optional(Set), ReadEffects(row), row.Optional(Notes) ?? "", row.List(Pieces))
                : null)
            .OfType<ArtifactDefinition>()
            .ToList();

    public static IReadOnlyList<ArtifactSetDefinition> ParseSets(string text) =>
        CsvTable.Parse(text, "Artifact set", [Id, Name])
            .Read(row => row.Text(Id) is { Length: > 0 } id ? new ArtifactSetDefinition(id, row.Text(Name), ReadEffects(row), row.Optional(Notes) ?? "") : null)
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
    private readonly Dictionary<string, string> _relicOf = new(StringComparer.Ordinal);

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

        foreach (var relic in _artifacts.Values.Where(a => a.IsAssembled))
        {
            CheckRelic(relic);
        }
    }

    private void CheckRelic(ArtifactDefinition relic)
    {
        if (relic.Pieces.Count < 2 || relic.Pieces.Distinct(StringComparer.Ordinal).Count() != relic.Pieces.Count)
        {
            throw new FormatException($"Assembled relic '{relic.Id}' needs at least two different pieces.");
        }

        foreach (var id in relic.Pieces)
        {
            if (!_artifacts.TryGetValue(id, out var piece) || piece.IsAssembled)
            {
                throw new FormatException($"Assembled relic '{relic.Id}': '{id}' is not an ordinary artifact.");
            }

            if (!_relicOf.TryAdd(id, relic.Id))
            {
                throw new FormatException($"'{id}' is a piece of both '{_relicOf[id]}' and '{relic.Id}'.");
            }
        }

        if (_artifacts[relic.Pieces[0]].Slot != relic.Slot)
        {
            throw new FormatException($"Assembled relic '{relic.Id}' is worn in its first piece's slot ({_artifacts[relic.Pieces[0]].Slot}), not {relic.Slot}.");
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

    /// <summary>The artifacts of a tier that can drop: assembled relics never do, only their pieces (ADR-100).</summary>
    public IEnumerable<ArtifactDefinition> OfTier(ArtifactTier tier) => _artifacts.Values.Where(a => a.Tier == tier && !a.IsAssembled);

    /// <summary>The assembled relics, in id order.</summary>
    public IEnumerable<ArtifactDefinition> Relics => _artifacts.Values.Where(a => a.IsAssembled).OrderBy(a => a.Id, StringComparer.Ordinal);

    /// <summary>The assembled relic an artifact is a piece of, or null.</summary>
    public string? RelicOf(string piece) => _relicOf.GetValueOrDefault(piece);

    /// <summary>The slots an artifact blocks besides its own: an assembled relic's other pieces' slots (none for an ordinary one).</summary>
    public IReadOnlyList<ArtifactSlot> BlockedSlots(ArtifactDefinition artifact) => [.. artifact.Pieces.Skip(1).Select(p => Get(p).Slot)];

    /// <summary>What wearing the artifact does: its effects, and an assembled relic's pieces' effects too.</summary>
    public IEnumerable<ArtifactEffect> EffectsOf(ArtifactDefinition artifact) =>
        artifact.Effects.Concat(artifact.Pieces.SelectMany(p => TryGet(p, out var piece) ? piece.Effects : []));
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
