using System.Text.Json;
using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Core.Data.Content;

/// <summary>
/// Loads and saves creature definitions, e.g.
/// <c>[{ "id": "neutral.wolf", "name": "Wolf", "tier": 1, "size": "Small", "attack": 6, ... }]</c>.
/// Combine with <see cref="CreatureCatalog.With"/> to extend or override the built-in creatures.
/// </summary>
public static class CreatureDefinitionSerializer
{
    /// <summary>Derived values (IsRanged, Value, …) are not data, so they are left out of the files.</summary>
    private static readonly JsonSerializerOptions Options = new(JsonDefaults.Options) { IgnoreReadOnlyProperties = true };

    public static IReadOnlyList<CreatureDefinition> Deserialize(string json)
    {
        var definitions = JsonSerializer.Deserialize<List<CreatureDefinition>>(json, Options)
                          ?? throw new FormatException("Creature file is empty.");
        definitions.ForEach(d => d.Validate());
        return definitions;
    }

    public static string Serialize(IEnumerable<CreatureDefinition> definitions) =>
        JsonSerializer.Serialize(definitions.ToList(), Options);

    public static CreatureCatalog LoadInto(CreatureCatalog catalog, string path) =>
        catalog.With(Deserialize(File.ReadAllText(path)));
}
