using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// The editor's own settings file (EDIT-1, ADR-250), in the user data folder: the workspace's layout and the recent files. One object
/// holds the file for everything that keeps something in it; every change is written at once.
/// </summary>
public sealed class EditorSettingsFile
{
    public const string FileName = "editor-settings.cfg";

    private readonly ConfigFile _file = new();
    private readonly string? _path;

    /// <summary>Reads <paramref name="path"/>; null: the defaults, and nothing is kept.</summary>
    public EditorSettingsFile(string? path)
    {
        _path = path;
        if (path is not null && File.Exists(path) && _file.Load(path) != Error.Ok)
        {
            GD.PushWarning($"Could not read {path}: the editor's default settings are used.");
            _file.Clear();
        }
    }

    public Variant Get(string section, string key, Variant fallback) => _file.GetValue(section, key, fallback);

    public void Set(string section, string key, Variant value)
    {
        _file.SetValue(section, key, value);
        if (_path is not null && _file.Save(_path) != Error.Ok)
        {
            GD.PushWarning($"Could not keep the editor's settings in {_path}.");
        }
    }
}
