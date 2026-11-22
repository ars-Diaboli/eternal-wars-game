using EternalWars.Core.Data.Templates;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared;
using EternalWars.Godot.UI.Editor;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// Maps from templates (ADR-288): the templates the Generate tab and the new-map wizard offer (the shipped ones and the user's valid
/// ones), "Generate from template" (ADR-087) and the template editor's "Open in map editor" (ADR-274), each opening a new map.
/// </summary>
internal sealed class GenerateComposer(EditorParts parts)
{
    /// <summary>The templates offered, read again with every map's UI.</summary>
    public IReadOnlyList<MapTemplate> Offered { get; private set; } = [];

    /// <summary>The Generate tab's last request, which the next map's tab shows again.</summary>
    public GenerateRequest? LastRequest { get; private set; }

    /// <summary>Reads the templates offered again: the shipped ones, then the user's valid ones.</summary>
    public void Reload() =>
        Offered = [.. BuiltInTemplates.All, .. UserTemplates().Select(u => u.Template).Where(t => TemplateValidator.IsValid(t))];

    /// <summary>The templates the Generate tab and the new-map wizard offer, as the panels show them.</summary>
    public List<GeneratorTemplate> GeneratorTemplates() =>
        [.. Offered.Select(t => new GeneratorTemplate(t.Name, t.Description, t.Sizes, t.MinPlayers, t.MaxHeroes, StartSlots.Of(t).Select(s => s.Name).ToList()))];

    /// <summary>
    /// "Generate from template" (ADR-087): a new map like "New", from <see cref="EditorMapGenerator"/>; a message tells how it was made, the
    /// generator's problems if every attempt had some, and how many validator issues there are. A refused request returns its reason to the panel.
    /// </summary>
    public string? Generate(GenerateRequest request)
    {
        var template = Offered[request.Template];
        EditorMap map;
        try
        {
            map = EditorMapGenerator.Generate(
                template, request.Size, request.Seed, request.Players, request.Towns, GameContent.All, request.MaxHeroes, request.Humans);
        }
        catch (ArgumentException e)
        {
            return e.Message;
        }

        LastRequest = request;
        parts.Files.OpenNew(map.Scenario, () => Describe(template, request.Size, request.Seed, map));
        return null;
    }

    /// <summary>"Open in map editor" in the template editor's preview (ADR-274): the generated map as a new map, like the Generate tab's.</summary>
    public void OpenGenerated(MapTemplate template, int size, ulong seed, EditorMap map) =>
        parts.Files.OpenNew(map.Scenario, () => Describe(template, size, seed, map));

    private void Describe(MapTemplate template, int size, ulong seed, EditorMap map)
    {
        var attempt = map.Generation.Attempt == 0 ? "" : $" (attempt {map.Generation.Attempt})";
        var problems = map.Problems.Count == 0 ? "" : $" Generator problems: {string.Join(" ", map.Problems)}";
        var issues = map.Issues.Count == 0 ? "" : $" {map.Issues.Count} validator issue(s), see Problems.";
        parts.Say($"Generated {template.Name} {size}×{size}, seed {seed}{attempt}.{problems}{issues}");
    }

    /// <summary>The readable template files in the user's templates folder.</summary>
    private static List<(string Path, MapTemplate Template)> UserTemplates()
    {
        var directory = EditorFolders.Templates;
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var found = new List<(string, MapTemplate)>();
        foreach (var file in Directory.GetFiles(directory, "*" + TemplateSerializer.FileExtension).Order(StringComparer.Ordinal))
        {
            try
            {
                found.Add((file, TemplateSerializer.Load(file)));
            }
            catch (Exception e) when (e is FormatException or NotSupportedException or IOException)
            {
                GD.PushWarning($"Skipping template {file}: {e.Message}");
            }
        }

        return found;
    }
}
