namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// Immutable lookup of <see cref="MapObjectDefinition"/>s by id. Start from <see cref="Default"/>
/// (built-in content) and layer data files or mods on top with <see cref="With"/>.
/// </summary>
public sealed class MapObjectCatalog
{
    private readonly Dictionary<string, MapObjectDefinition> _definitions;

    public MapObjectCatalog(IEnumerable<MapObjectDefinition> definitions)
    {
        _definitions = new Dictionary<string, MapObjectDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            definition.Validate();
            if (!_definitions.TryAdd(definition.Id, definition))
            {
                throw new ArgumentException($"Duplicate map object definition '{definition.Id}'.", nameof(definitions));
            }
        }
    }

    public static MapObjectCatalog Default { get; } = new(BuiltInMapObjects.All);

    public IEnumerable<MapObjectDefinition> All => _definitions.Values;

    public MapObjectDefinition Get(string id) =>
        _definitions.TryGetValue(id, out var definition)
            ? definition
            : throw new KeyNotFoundException($"Unknown map object definition '{id}'.");

    public bool TryGet(string id, out MapObjectDefinition definition) => _definitions.TryGetValue(id, out definition!);

    public IEnumerable<MapObjectDefinition> OfKind(MapObjectKind kind) => All.Where(d => d.Kind == kind);

    /// <summary>Returns a new catalog where <paramref name="definitions"/> add to or replace existing ids.</summary>
    public MapObjectCatalog With(IEnumerable<MapObjectDefinition> definitions)
    {
        var merged = new Dictionary<string, MapObjectDefinition>(_definitions, StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            merged[definition.Id] = definition;
        }

        return new MapObjectCatalog(merged.Values);
    }
}
