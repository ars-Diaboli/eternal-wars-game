using EternalWars.Core.Data.Settings;
using EternalWars.Godot.Shared;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// Where the editor keeps its files: the user data folder, or the folder of the settings file the launch arguments name (the smoke
/// scenarios run side by side, each with its own settings, maps folder and autosaves).
/// </summary>
internal static class EditorFolders
{
    private const string MapsDirectory = "user://maps";

    /// <summary>The user's templates (ADR-070: they live in the user data folder): where the template editor saves, and the Generate tab looks.</summary>
    private const string TemplatesDirectory = "user://templates";

    /// <summary>The editor's settings file (only the window mode is used from it), or the one the launch arguments name.</summary>
    public static string SettingsPath =>
        LaunchArguments.SettingsPath ?? Path.Combine(ProjectSettings.GlobalizePath("user://"), SettingsFile.FileName);

    /// <summary>The folder of the editor's own settings, maps and autosaves.</summary>
    public static string Data =>
        LaunchArguments.SettingsPath is { } settings ? Path.GetDirectoryName(Path.GetFullPath(settings))! : ProjectSettings.GlobalizePath("user://");

    public static string Maps =>
        LaunchArguments.SettingsPath is null ? ProjectSettings.GlobalizePath(MapsDirectory) : Path.Combine(Data, "maps");

    /// <summary>Where the browser keeps the pictures it rendered (ADR-254); deleting the folder only costs rendering them again.</summary>
    public static string Thumbnails =>
        LaunchArguments.SettingsPath is null ? ProjectSettings.GlobalizePath("user://cache/thumbs") : Path.Combine(Data, "cache", "thumbs");

    /// <summary>Where "Save selection as stamp" writes and the browser's stamp list reads (ADR-250).</summary>
    public static string Stamps =>
        LaunchArguments.SettingsPath is null ? ProjectSettings.GlobalizePath("user://stamps") : Path.Combine(Data, "stamps");

    public static string Autosave =>
        LaunchArguments.SettingsPath is null ? Path.Combine(ProjectSettings.GlobalizePath(MapsDirectory), ".autosave") : Path.Combine(Data, "autosave");

    public static string Templates =>
        LaunchArguments.SettingsPath is null ? ProjectSettings.GlobalizePath(TemplatesDirectory) : Path.Combine(Data, "templates");

    public static string TemplateAutosave =>
        LaunchArguments.SettingsPath is null ? Path.Combine(Templates, ".autosave") : Path.Combine(Data, "template-autosave");

    /// <summary>Where the template editor keeps the user's zone presets (ADR-273).</summary>
    public static string TemplatePresets =>
        LaunchArguments.SettingsPath is null ? ProjectSettings.GlobalizePath("user://template-presets") : Path.Combine(Data, "template-presets");
}
