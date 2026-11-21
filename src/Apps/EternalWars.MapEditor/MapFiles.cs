using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared;
using EternalWars.Godot.UI.Editor.Shell;

namespace EternalWars.MapEditor;

/// <summary>
/// The editor's map files for the <see cref="FileService"/>: the map format (<see cref="MapSerializer"/>, <see cref="MapConverter"/>) with
/// the game's content, and the autosaves with their session marker (<see cref="MapAutosaves"/>).
/// </summary>
internal sealed class MapFiles(string folder, string autosaveFolder) : IMapFiles
{
    private readonly MapAutosaves _autosaves = new(autosaveFolder);

    public string Extension => MapSerializer.FileExtension;

    public string Folder { get; } = folder;

    /// <summary>The map in the file at <paramref name="path"/> as a scenario to edit: its events are not checked, the editor's validator shows them.</summary>
    public static GameState Read(string path) => MapConverter.ToGameState(MapSerializer.Load(path), GameContent.All, checkEvents: false);

    public GameState Load(string path) => Read(path);

    public void Save(GameState map, string path) => MapSerializer.Save(MapConverter.ToDocument(map), path);

    public string? BeginSession() => _autosaves.BeginSession(Environment.ProcessId, IsRunning, DateTime.UtcNow);

    public void MarkSafe() => _autosaves.MarkSafe(DateTime.UtcNow);

    public void EndSession() => _autosaves.EndSession();

    public Action Autosave(GameState map)
    {
        var document = MapConverter.ToDocument(map);
        var now = DateTime.UtcNow;
        return () => _autosaves.Save(document, now);
    }

    public DateTime AutosaveTime(string path) => MapAutosaves.TimeOf(path)!.Value.ToLocalTime();

    /// <summary>Whether the process of that id still runs (the owner of an autosave session marker).</summary>
    public static bool IsRunning(int process)
    {
        try
        {
            using var found = System.Diagnostics.Process.GetProcessById(process);
            return !found.HasExited;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }
}
