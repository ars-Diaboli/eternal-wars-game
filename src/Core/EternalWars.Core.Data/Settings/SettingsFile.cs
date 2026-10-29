using System.Text.Json;
using EternalWars.Core.Domain.Settings;

namespace EternalWars.Core.Data.Settings;

/// <summary>
/// The per-computer settings file (<c>settings.json</c> in the user data folder; ADR-090, UI-1). Not part of a save: the speed settings change no
/// rule. A missing or unreadable file gives the defaults, so a broken file never stops the game from starting.
/// </summary>
public static class SettingsFile
{
    public const string FileName = "settings.json";

    public static SpeedSettings Load(string path)
    {
        try
        {
            return File.Exists(path) ? Read(File.ReadAllText(path)) : SpeedSettings.Default;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SpeedSettings.Default;
        }
    }

    public static void Save(string path, SpeedSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Write(settings));
    }

    public static string Write(SpeedSettings settings) => JsonSerializer.Serialize(new SettingsDocument(settings), JsonDefaults.Options);

    /// <summary>The settings in <paramref name="json"/>; what is missing or cannot be read keeps its default.</summary>
    public static SpeedSettings Read(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SettingsDocument>(json, JsonDefaults.Options)?.Speed ?? SpeedSettings.Default;
        }
        catch (JsonException)
        {
            return SpeedSettings.Default;
        }
    }

    /// <summary>The file's shape: one section per kind of setting, so later settings can join without moving these.</summary>
    private sealed record SettingsDocument(SpeedSettings? Speed);
}
