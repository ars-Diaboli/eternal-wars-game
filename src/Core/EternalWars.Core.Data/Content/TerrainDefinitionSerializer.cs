using System.Text.Json;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Data.Content;

/// <summary>JSON shape of a <see cref="TerrainDefinition"/>.</summary>
public sealed record TerrainDefinitionDocument(TerrainType Terrain, TerrainClass Class, int Cost, IReadOnlyList<string>? NativeTowns = null);

/// <summary>
/// Loads and saves terrain rules, e.g. <c>[{ "terrain": "Swamp", "class": "Native", "cost": 175, "nativeTowns": ["swamp-bestiary"] }]</c>.
/// Combine with <see cref="TerrainCatalog.With"/> to override built-in rules.
/// </summary>
public static class TerrainDefinitionSerializer
{
    public static IReadOnlyList<TerrainDefinition> Deserialize(string json)
    {
        var documents = JsonSerializer.Deserialize<List<TerrainDefinitionDocument>>(json, JsonDefaults.Options)
                        ?? throw new FormatException("Terrain file is empty.");
        var definitions = documents.Select(d => new TerrainDefinition(d.Terrain, d.Class, d.Cost, d.NativeTowns ?? [])).ToList();
        definitions.ForEach(d => d.Validate());
        return definitions;
    }

    public static string Serialize(IEnumerable<TerrainDefinition> definitions) =>
        JsonSerializer.Serialize(
            definitions.Select(d => new TerrainDefinitionDocument(d.Terrain, d.Class, d.Cost, d.NativeTowns)).ToList(),
            JsonDefaults.Options);

    public static TerrainCatalog LoadInto(TerrainCatalog catalog, string path) =>
        catalog.With(Deserialize(File.ReadAllText(path)));
}
