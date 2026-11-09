using System.Text.Json;
using EternalWars.Core.Domain.Settings;

namespace EternalWars.Core.Data.Settings;

/// <summary>
/// The per-computer settings file (<c>settings.json</c> in the user data folder; ADR-090, UI-1; ADR-154; ADR-156). Not part of a save: the
/// speed, interface and layout settings change no rule. A missing or unreadable file gives the defaults, so a broken file never stops the
/// game from starting.
/// </summary>
public static class SettingsFile
{
    public const string FileName = "settings.json";

    public static SpeedSettings Load(string path) => Document(path).Speed ?? SpeedSettings.Default;

    public static InterfaceSettings LoadInterface(string path) => Document(path).Interface ?? InterfaceSettings.Default;

    public static LayoutSettings LoadLayout(string path) => Document(path).Layout ?? LayoutSettings.Default;

    public static void Save(string path, SpeedSettings speed, InterfaceSettings? ui = null, LayoutSettings? layout = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Write(speed, ui, layout));
    }

    public static string Write(SpeedSettings speed, InterfaceSettings? ui = null, LayoutSettings? layout = null) =>
        JsonSerializer.Serialize(new SettingsDocument(speed, ui ?? InterfaceSettings.Default, layout ?? LayoutSettings.Default), JsonDefaults.Options);

    /// <summary>The speed settings in <paramref name="json"/>; what is missing or cannot be read keeps its default.</summary>
    public static SpeedSettings Read(string json) => Parse(json).Speed ?? SpeedSettings.Default;

    /// <summary>The interface settings in <paramref name="json"/>; what is missing or cannot be read keeps its default.</summary>
    public static InterfaceSettings ReadInterface(string json) => Parse(json).Interface ?? InterfaceSettings.Default;

    /// <summary>The layout settings in <paramref name="json"/>; what is missing or cannot be read keeps its default.</summary>
    public static LayoutSettings ReadLayout(string json) => Parse(json).Layout ?? LayoutSettings.Default;

    private static SettingsDocument Document(string path)
    {
        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : SettingsDocument.Empty;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SettingsDocument.Empty;
        }
    }

    private static SettingsDocument Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SettingsDocument>(json, JsonDefaults.Options) ?? SettingsDocument.Empty;
        }
        catch (JsonException)
        {
            return SettingsDocument.Empty;
        }
    }

    /// <summary>The file's shape: one section per kind of setting, so later settings can join without moving these.</summary>
    private sealed record SettingsDocument(SpeedSettings? Speed, InterfaceSettings? Interface, LayoutSettings? Layout)
    {
        public static SettingsDocument Empty { get; } = new(null, null, null);
    }
}
