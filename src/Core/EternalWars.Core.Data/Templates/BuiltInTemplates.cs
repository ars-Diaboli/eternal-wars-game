using EternalWars.Core.Generation;

namespace EternalWars.Core.Data.Templates;

/// <summary>The templates that ship with the game (embedded files in <c>data/templates</c>).</summary>
public static class BuiltInTemplates
{
    private const string Prefix = "EternalWars.Templates.";

    private static readonly IReadOnlyList<(string Key, MapTemplate Template)> Entries = typeof(BuiltInTemplates).Assembly.GetManifestResourceNames()
        .Where(n => n.StartsWith(Prefix, StringComparison.Ordinal) && n.EndsWith(TemplateSerializer.FileExtension, StringComparison.Ordinal))
        .Order(StringComparer.Ordinal)
        .Select(n => (n[Prefix.Length..^TemplateSerializer.FileExtension.Length], Read(n)))
        .ToList();

    /// <summary>Every shipped template, by file name order.</summary>
    public static IReadOnlyList<MapTemplate> All { get; } = Entries.Select(e => e.Template).ToList();

    /// <summary>The file name of each shipped template without the extension (<c>duel</c>, <c>2v2</c>, …), in the order of <see cref="All"/>.</summary>
    public static IReadOnlyList<string> Keys { get; } = Entries.Select(e => e.Key).ToList();

    /// <summary>The shipped template with this name or file key (<see cref="Keys"/>), ignoring case.</summary>
    public static MapTemplate? Find(string name) =>
        All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? Entries.FirstOrDefault(e => string.Equals(e.Key, name, StringComparison.OrdinalIgnoreCase)).Template;

    private static MapTemplate Read(string resource)
    {
        using var stream = typeof(BuiltInTemplates).Assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return TemplateSerializer.Deserialize(reader.ReadToEnd());
    }
}
