using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Creatures;

/// <summary>
/// Immutable lookup of <see cref="CreatureDefinition"/>s by id, like <c>MapObjectCatalog</c>: start from
/// <see cref="Default"/> and layer content files on top with <see cref="With"/>.
/// </summary>
public sealed class CreatureCatalog
{
    private readonly Dictionary<CreatureId, CreatureDefinition> _definitions;
    private readonly Dictionary<CreatureId, CreatureId> _baseOf = [];

    public CreatureCatalog(IEnumerable<CreatureDefinition> definitions)
    {
        _definitions = [];
        foreach (var definition in definitions)
        {
            definition.Validate();
            if (!_definitions.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException($"Duplicate creature definition '{definition.Id}'.", nameof(definitions));
            }
        }

        foreach (var definition in _definitions.Values.Where(d => d.UpgradesTo is not null))
        {
            if (!_definitions.TryGetValue(definition.UpgradesTo!.Value, out var upgrade))
            {
                throw new FormatException($"Creature '{definition.Id}' upgrades to unknown '{definition.UpgradesTo}'.");
            }

            if (upgrade.Tier != definition.Tier || upgrade.Town != definition.Town || upgrade.Category != definition.Category
                || upgrade.UpgradesTo is not null)
            {
                throw new FormatException(
                    $"Upgrade '{upgrade.Id}' of '{definition.Id}' must have the same tier, town and category and no further upgrade.");
            }

            _baseOf.TryAdd(upgrade.Id, definition.Id);
        }
    }

    public static CreatureCatalog Default { get; } = new(BuiltInCreatures.All);

    public IEnumerable<CreatureDefinition> All => _definitions.Values;

    public CreatureDefinition Get(CreatureId id) =>
        _definitions.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown creature '{id}'.");

    public bool TryGet(CreatureId id, out CreatureDefinition definition) => _definitions.TryGetValue(id, out definition!);

    public bool Contains(CreatureId id) => _definitions.ContainsKey(id);

    /// <summary>Whether the creature is the upgraded form of a line.</summary>
    public bool IsUpgrade(CreatureId id) => _baseOf.ContainsKey(id);

    /// <summary>The base creature of the creature's line: itself unless it is an upgrade.</summary>
    public CreatureId BaseOf(CreatureId id) => _baseOf.TryGetValue(id, out var line) ? line : id;

    /// <summary>Base forms (not upgrades) of a town's creatures, by tier.</summary>
    public IEnumerable<CreatureDefinition> BaseCreaturesOf(string town)
    {
        var upgrades = All.Where(d => d.UpgradesTo is not null).Select(d => d.UpgradesTo!.Value).ToHashSet();
        return All.Where(d => d.Town == town && !upgrades.Contains(d.Id)).OrderBy(d => d.Tier).ThenBy(d => d.Id);
    }

    /// <summary>Returns a new catalog where <paramref name="definitions"/> add to or replace existing ids.</summary>
    public CreatureCatalog With(IEnumerable<CreatureDefinition> definitions)
    {
        var merged = new Dictionary<CreatureId, CreatureDefinition>(_definitions);
        foreach (var definition in definitions)
        {
            merged[definition.Id] = definition;
        }

        return new CreatureCatalog(merged.Values);
    }
}
