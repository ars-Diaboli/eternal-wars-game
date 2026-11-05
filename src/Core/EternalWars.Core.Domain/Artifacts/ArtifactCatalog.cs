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
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ArtifactDefinition> _written = new(StringComparer.Ordinal);

    /// <summary>
    /// Between a scroll's id and the spell written on it (ADR-136): <c>spell-scroll-1:light.bless</c>. The catalog holds one blank scroll per spell
    /// level; a written one is looked up by this id and made from its level's scroll.
    /// </summary>
    public const char SpellSeparator = ':';

    /// <summary>The highest spell level whose scrolls the artifact rolls give; level 4 scrolls are only placed by hand (ADR-136).</summary>
    public const int MaxRolledScrollLevel = 3;

    /// <summary>The id of a scroll with a spell written on it: <c>spell-scroll-1:light.bless</c>.</summary>
    public static string WithSpell(string scroll, string spell) => $"{BaseOf(scroll)}{SpellSeparator}{spell}";

    /// <summary>The catalog row of an artifact id: a written scroll's blank scroll, otherwise the id itself.</summary>
    public static string BaseOf(string id) => id.IndexOf(SpellSeparator, StringComparison.Ordinal) is var at and >= 0 ? id[..at] : id;

    /// <param name="artifacts">The artifact rows.</param>
    /// <param name="sets">The set rows.</param>
    /// <param name="spells">The spells the scrolls can hold; the shipped table when null.</param>
    public ArtifactCatalog(IEnumerable<ArtifactDefinition> artifacts, IEnumerable<ArtifactSetDefinition>? sets = null, Magic.SpellCatalog? spells = null)
    {
        Spells = spells ?? Magic.SpellCatalog.Default;
        foreach (var set in sets ?? [])
        {
            if (!_sets.TryAdd(set.Id, set))
            {
                throw new FormatException($"Duplicate artifact set '{set.Id}'.");
            }
        }

        foreach (var artifact in artifacts)
        {
            if (artifact.Id.Contains(SpellSeparator, StringComparison.Ordinal) || !_artifacts.TryAdd(artifact.Id, artifact))
            {
                throw new FormatException($"Duplicate artifact '{artifact.Id}', or a '{SpellSeparator}' in its id.");
            }

            if (artifact.Set is { } set && !_sets.ContainsKey(set))
            {
                throw new FormatException($"Artifact '{artifact.Id}' is in the unknown set '{set}'.");
            }

            if (artifact.IsScroll && (artifact.Set is not null || artifact.IsAssembled || artifact.Effects.Count != 1
                                      || artifact.ScrollLevel is < 1 or > Creatures.SpellLevels.Max))
            {
                throw new FormatException(
                    $"Scroll '{artifact.Id}' has only its 'scroll <level>' effect, a spell level from 1 to {Creatures.SpellLevels.Max}, and no set or pieces.");
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

        if (_relicOf.Keys.FirstOrDefault(p => _artifacts[p].IsScroll) is { } piece)
        {
            throw new FormatException($"The scroll '{piece}' cannot be a piece of an assembled relic.");
        }
    }

    /// <summary>The spells scrolls can hold.</summary>
    public Magic.SpellCatalog Spells { get; }

    /// <summary>The spells a scroll of the level can hold, by id.</summary>
    public IReadOnlyList<string> ScrollSpells(int level) =>
        Spells.All.Where(s => s.Level == level).Select(s => s.Id).Order(StringComparer.Ordinal).ToList();

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

    /// <summary>Whether the id is an artifact: a catalog row, or a scroll with a spell of its level written on it.</summary>
    public bool Contains(string id) => TryGet(id, out _);

    /// <summary>The artifact of an id, a written scroll (<c>spell-scroll-1:light.bless</c>) too.</summary>
    public bool TryGet(string id, out ArtifactDefinition artifact)
    {
        if (_artifacts.TryGetValue(id, out artifact!))
        {
            return true;
        }

        var at = id.IndexOf(SpellSeparator, StringComparison.Ordinal);
        if (at < 0 || !_artifacts.TryGetValue(id[..at], out var blank) || !blank.IsScroll
            || !Spells.TryGet(id[(at + 1)..], out var spell) || spell.Level != blank.ScrollLevel)
        {
            artifact = null!;
            return false;
        }

        artifact = _written.GetOrAdd(id, _ => blank with
        {
            Id = id,
            Name = $"Scroll of {spell.Name}",
            Effects = [new ArtifactEffect(ArtifactEffectKind.Scroll, spell.Level, spell.Id)],
            Spell = spell.Id,
        });
        return true;
    }

    public ArtifactDefinition Get(string id) =>
        TryGet(id, out var artifact) ? artifact : throw new KeyNotFoundException($"Unknown artifact '{id}'.");

    public ArtifactSetDefinition GetSet(string id) =>
        _sets.TryGetValue(id, out var set) ? set : throw new KeyNotFoundException($"Unknown artifact set '{id}'.");

    public IEnumerable<ArtifactDefinition> PiecesOf(string set) => _artifacts.Values.Where(a => a.Set == set);

    /// <summary>
    /// The artifacts of a tier that can drop: assembled relics never do, only their pieces (ADR-100); nor do the scrolls of level 4 spells, which
    /// only the map's author places (ADR-136).
    /// </summary>
    public IEnumerable<ArtifactDefinition> OfTier(ArtifactTier tier) =>
        _artifacts.Values.Where(a => a.Tier == tier && !a.IsAssembled && !(a.ScrollLevel > MaxRolledScrollLevel));

    /// <summary>The blank spell scrolls, one per spell level, in id order.</summary>
    public IEnumerable<ArtifactDefinition> Scrolls => _artifacts.Values.Where(a => a.IsScroll).OrderBy(a => a.Id, StringComparer.Ordinal);

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

/// <summary>
/// How an artifact roll picks from a tier's pool (ADR-136): the tier's spell scroll takes <see cref="ScrollPercent"/> of the rolls when the pool has
/// it and other artifacts; the others are equally likely. The roll gives a blank scroll; the caller writes its spell.
/// </summary>
public static class ArtifactRolls
{
    /// <summary>The share of a tier's rolls that gives a scroll, in percent (draft number).</summary>
    public const int ScrollPercent = 5;

    /// <summary>One artifact of a non-empty <paramref name="pool"/>, drawn from <paramref name="random"/>.</summary>
    public static ArtifactDefinition Pick(IReadOnlyList<ArtifactDefinition> pool, Common.GameRandom random)
    {
        var scrolls = pool.Where(a => a.IsScroll).ToList();
        var others = pool.Where(a => !a.IsScroll).ToList();
        var part = scrolls.Count == 0 ? others
            : others.Count == 0 ? scrolls
            : random.Next(100) < ScrollPercent ? scrolls : others;
        return part[random.Next(part.Count)];
    }
}
