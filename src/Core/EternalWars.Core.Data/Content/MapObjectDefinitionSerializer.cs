using System.Text.Json;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
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
    int Amount = 0,
    LootRule? Loot = null,
    BuffRule? Buff = null,
    TrainingRule? Training = null,
    string? Becomes = null,
    string? GuardCreature = null,
    int GuardMin = 0,
    int GuardMax = 0,
    TerrainType? BattleTerrain = null,
    BankRule? Bank = null,
    IReadOnlyList<TerrainType>? Terrains = null,
    ResourceSet? Price = null,
    ObjectColour? Colour = null,
    bool Stays = false);

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
            .Select(d => new MapObjectDefinition(d.Id, d.Name, d.Kind, d.Kind == MapObjectKind.Scenery ? Footprint.Obstacle(d.Footprint) : new Footprint(d.Footprint))
            {
                Resource = d.Resource,
                DailyYield = d.DailyYield,
                Town = d.Town,
                ArtifactTier = d.ArtifactTier,
                VisitScope = d.VisitScope,
                Provider = d.Provider,
                Amount = d.Amount,
                Loot = d.Loot,
                Buff = d.Buff,
                Training = d.Training,
                Becomes = d.Becomes,
                GuardCreature = d.GuardCreature,
                GuardMin = d.GuardMin,
                GuardMax = d.GuardMax,
                BattleTerrain = d.BattleTerrain,
                Bank = d.Bank,
                Terrains = d.Terrains ?? [],
                Price = d.Price ?? ResourceSet.Empty,
                Colour = d.Colour,
                Stays = d.Stays,
            })
            .ToList();
        definitions.ForEach(d => d.Validate());
        return definitions;
    }

    public static string Serialize(IEnumerable<MapObjectDefinition> definitions) =>
        JsonSerializer.Serialize(
            definitions.Select(d => new MapObjectDefinitionDocument(d.Id, d.Name, d.Kind, d.Footprint.Rows, d.Resource, d.DailyYield, d.Town, d.ArtifactTier, d.VisitScope, d.Provider, d.Amount, d.Loot, d.Buff, d.Training, d.Becomes, d.GuardCreature, d.GuardMin, d.GuardMax, d.BattleTerrain, d.Bank, d.Terrains.Count == 0 ? null : d.Terrains,
                d.Price.IsEmpty ? null : d.Price, d.Colour, d.Stays)).ToList(),
            JsonDefaults.Options);

    public static MapObjectCatalog LoadInto(MapObjectCatalog catalog, string path) =>
        catalog.With(Deserialize(File.ReadAllText(path)));
}
