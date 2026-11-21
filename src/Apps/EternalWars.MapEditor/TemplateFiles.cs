using EternalWars.Core.Data.Templates;
using EternalWars.Core.Generation;
using EternalWars.Godot.UI.Editor.Shell;

namespace EternalWars.MapEditor;

/// <summary>
/// The editor's template files for the <see cref="TemplateFileService"/>: the template format (<see cref="TemplateSerializer"/>), the shipped
/// templates (<see cref="BuiltInTemplates"/>), and the autosaves with their session marker (<see cref="TemplateAutosaves"/>).
/// </summary>
internal sealed class TemplateFiles(string folder, string autosaveFolder) : ITemplateFiles
{
    private readonly TemplateAutosaves _autosaves = new(autosaveFolder);

    public string Extension => TemplateSerializer.FileExtension;

    public string Folder { get; } = folder;

    public IReadOnlyList<MapTemplate> Shipped => BuiltInTemplates.All;

    public MapTemplate Load(string path) => TemplateSerializer.Load(path);

    public void Save(MapTemplate template, string path) => TemplateSerializer.Save(template, path);

    public string? BeginSession() => _autosaves.BeginSession(Environment.ProcessId, MapFiles.IsRunning, DateTime.UtcNow);

    public void MarkSafe() => _autosaves.MarkSafe(DateTime.UtcNow);

    public void EndSession() => _autosaves.EndSession();

    public Action Autosave(MapTemplate template)
    {
        var now = DateTime.UtcNow;
        return () => _autosaves.Save(template, now);
    }

    public DateTime AutosaveTime(string path) => TemplateAutosaves.TimeOf(path)!.Value.ToLocalTime();
}
