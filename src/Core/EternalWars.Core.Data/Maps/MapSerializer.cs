using System.Text.Json;

namespace EternalWars.Core.Data.Maps;

public static class MapSerializer
{
    public const string FileExtension = ".ewmap.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string Serialize(MapDocument document) => JsonSerializer.Serialize(document, Options);

    public static MapDocument Deserialize(string json) =>
        JsonSerializer.Deserialize<MapDocument>(json, Options)
        ?? throw new FormatException("Map file is empty.");

    public static void Save(MapDocument document, string path) => File.WriteAllText(path, Serialize(document));

    public static MapDocument Load(string path) => Deserialize(File.ReadAllText(path));
}
