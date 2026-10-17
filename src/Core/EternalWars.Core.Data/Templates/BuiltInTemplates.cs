using EternalWars.Core.Generation;

namespace EternalWars.Core.Data.Templates;

/// <summary>The templates that ship with the game (embedded files in <c>data/templates</c>).</summary>
public static class BuiltInTemplates
{
    private const string Prefix = "EternalWars.Templates.";

    /// <summary>Every shipped template, by file name order.</summary>
    public static IReadOnlyList<MapTemplate> All { get; } = typeof(BuiltInTemplates).Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(TemplateSerializer.FileExtension, StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .Select(Read)
        .ToList();

    public static MapTemplate? Find(string name) => All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));

    private static MapTemplate Read(string resource)
    {
        using var stream = typeof(BuiltInTemplates).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return TemplateSerializer.Deserialize(reader.ReadToEnd());
    }
}
