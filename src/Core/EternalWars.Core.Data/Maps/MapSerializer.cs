using System.Text.Json;

namespace EternalWars.Core.Data.Maps;

public static class MapSerializer
{
    public const string FileExtension = ".ewmap.json";

    public static string Serialize(MapDocument document) => JsonSerializer.Serialize(document, JsonDefaults.Options);

    /// <summary>Reads a map file of any version: an older one is upgraded to the current format first (<see cref="MapMigrations"/>, ADR-174).</summary>
    public static MapDocument Deserialize(string json) =>
        (MapMigrations.Upgrade(json) is { } upgraded
            ? upgraded.Deserialize<MapDocument>(JsonDefaults.Options)
            : JsonSerializer.Deserialize<MapDocument>(json, JsonDefaults.Options))
        ?? throw new FormatException("Map file is empty.");

    public static void Save(MapDocument document, string path) => File.WriteAllText(path, Serialize(document));

    public static MapDocument Load(string path) => Deserialize(File.ReadAllText(path));
}
