using System.Text.Json;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Data.Content;

/// <summary>JSON shape of a <see cref="MapObjectDefinition"/> in content/mod files.</summary>
public sealed record MapObjectDefinitionDocument(
    string Id,
    string Name,
    MapObjectKind Kind,
    IReadOnlyList<string> Footprint,
    ResourceType? Resource = null,
    int DailyYield = 0,
    string? Town = null,
    ArtifactTier? ArtifactTier = null,
    VisitScope VisitScope = VisitScope.Unlimited,
    ProviderRule? Provider = null,
    int Amount = 0);

/// <summary>
/// Loads and saves map object definitions, e.g. <c>[{ "id": "mine.gold", "kind": "Mine", "footprint": ["BBB","BVB"], ... }]</c>.
/// Combine with <see cref="MapObjectCatalog.With"/> to extend or override the built-in catalog.
/// </summary>
public static class MapObjectDefinitionSerializer
{
    public static IReadOnlyList<MapObjectDefinition> Deserialize(string json)
    {
        var documents = JsonSerializer.Deserialize<List<MapObjectDefinitionDocument>>(json, JsonDefaults.Options)
                        ?? throw new FormatException("Definition file is empty.");
        var definitions = documents
            .Select(d => new MapObjectDefinition(d.Id, d.Name, d.Kind, new Footprint(d.Footprint))
            {
                Resource = d.Resource,
                DailyYield = d.DailyYield,
                Town = d.Town,
                ArtifactTier = d.ArtifactTier,
                VisitScope = d.VisitScope,
                Provider = d.Provider,
                Amount = d.Amount,
            })
            .ToList();
        definitions.ForEach(d => d.Validate());
        return definitions;
    }

    public static string Serialize(IEnumerable<MapObjectDefinition> definitions) =>
        JsonSerializer.Serialize(
            definitions.Select(d => new MapObjectDefinitionDocument(d.Id, d.Name, d.Kind, d.Footprint.Rows, d.Resource, d.DailyYield, d.Town, d.ArtifactTier, d.VisitScope, d.Provider, d.Amount)).ToList(),
            JsonDefaults.Options);

    public static MapObjectCatalog LoadInto(MapObjectCatalog catalog, string path) =>
        catalog.With(Deserialize(File.ReadAllText(path)));
}
