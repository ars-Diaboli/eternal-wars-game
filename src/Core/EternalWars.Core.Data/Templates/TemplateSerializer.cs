using System.Text.Json;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Data.Templates;

/// <summary>Reads and writes <c>*.ewtemplate.json</c> (the model is <see cref="MapTemplate"/>, same JSON conventions as maps).</summary>
public static class TemplateSerializer
{
    public const string FileExtension = ".ewtemplate.json";

    /// <summary>Writes the template in the current format (an older one read in is saved upgraded).</summary>
    public static string Serialize(MapTemplate template) =>
        JsonSerializer.Serialize(template with { FormatVersion = MapTemplate.CurrentFormatVersion }, JsonDefaults.Options);

    /// <summary>Throws <see cref="FormatException"/> for a broken file and <see cref="NotSupportedException"/> for one from a newer game.</summary>
    public static MapTemplate Deserialize(string json)
    {
        MapTemplate template;
        try
        {
            template = JsonSerializer.Deserialize<MapTemplate>(json, JsonDefaults.Options) ?? throw new FormatException("Template file is empty.");
        }
        catch (JsonException e)
        {
            throw new FormatException($"Template file is not valid: {e.Message}", e);
        }

        return template.FormatVersion > MapTemplate.CurrentFormatVersion
            ? throw new NotSupportedException($"Template format v{template.FormatVersion} is newer than supported v{MapTemplate.CurrentFormatVersion}.")
            : template;
    }

    public static void Save(MapTemplate template, string path) => File.WriteAllText(path, Serialize(template));

    public static MapTemplate Load(string path) => Deserialize(File.ReadAllText(path));
}
