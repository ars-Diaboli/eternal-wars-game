using System.Text.Json;
using EternalWars.Core.Domain.Settings;

namespace EternalWars.Core.Data.Settings;

/// <summary>
/// The per-computer settings file (<c>settings.json</c> in the user data folder; ADR-090, UI-1; ADR-154; ADR-156). Not part of a save: the
/// speed, interface and layout settings change no rule. A missing or unreadable file gives the defaults, so a broken file never stops the
/// game from starting. A layout written before ADR-309 (corners, lists along the bottom, a side panel) loads as the default arrangement or its
/// mirror; the interface's UI scale is brought into its range.
/// </summary>
public static class SettingsFile
{
    public const string FileName = "settings.json";

    public static SpeedSettings Load(string path) => Document(path).Speed ?? SpeedSettings.Default;

    public static InterfaceSettings LoadInterface(string path) => InterfaceOf(Document(path));

    public static LayoutSettings LoadLayout(string path) => LayoutOf(Document(path));

    public static void Save(string path, SpeedSettings speed, InterfaceSettings? ui = null, LayoutSettings? layout = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, Write(speed, ui, layout));
    }

    public static string Write(SpeedSettings speed, InterfaceSettings? ui = null, LayoutSettings? layout = null) =>
        JsonSerializer.Serialize(new SettingsDocument(speed, ui ?? InterfaceSettings.Default, LayoutJson.Of(layout ?? LayoutSettings.Default)), JsonDefaults.Options);

    /// <summary>The speed settings in <paramref name="json"/>; what is missing or cannot be read keeps its default.</summary>
    public static SpeedSettings Read(string json) => Parse(json).Speed ?? SpeedSettings.Default;

    /// <summary>The interface settings in <paramref name="json"/>; what is missing or cannot be read keeps its default.</summary>
    public static InterfaceSettings ReadInterface(string json) => InterfaceOf(Parse(json));

    /// <summary>The layout settings in <paramref name="json"/>; what is missing or cannot be read keeps its default.</summary>
    public static LayoutSettings ReadLayout(string json) => LayoutOf(Parse(json));

    private static InterfaceSettings InterfaceOf(SettingsDocument document) => (document.Interface ?? InterfaceSettings.Default).Normalized();

    private static LayoutSettings LayoutOf(SettingsDocument document) => document.Layout?.ToSettings() ?? LayoutSettings.Default;

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
    private sealed record SettingsDocument(SpeedSettings? Speed, InterfaceSettings? Interface, LayoutJson? Layout)
    {
        public static SettingsDocument Empty { get; } = new(null, null, null);
    }

    /// <summary>
    /// The layout section: what <see cref="LayoutSettings"/> holds, and read only, the older choices (ADR-156, ADR-191) that say whether an older
    /// file's HUD stood on the left: its side panel on the left, or the panel off and the minimap in a left corner. A missing side panel was
    /// the panel on the right. Read as they come, so a value the older enums took (a name in any case, or a number) never fails the file.
    /// </summary>
    private sealed record LayoutJson(
        ScreenEdge InitiativeBar = ScreenEdge.Bottom, bool? Mirrored = null, JsonElement? SidePanel = null, JsonElement? Minimap = null)
    {
        public static LayoutJson Of(LayoutSettings layout) => new(layout.InitiativeBar, layout.Mirrored);

        public LayoutSettings ToSettings() => new(InitiativeBar, Mirrored ?? WasOnTheLeft());

        // The older enums' order: SidePanel Right, Left, Off; ScreenCorner TopRight, TopLeft, BottomRight, BottomLeft.
        private bool WasOnTheLeft() => Old(SidePanel, "RIGHT", "LEFT", "OFF") switch
        {
            "LEFT" => true,
            "OFF" => Old(Minimap, "TOPRIGHT", "TOPLEFT", "BOTTOMRIGHT", "BOTTOMLEFT") is "TOPLEFT" or "BOTTOMLEFT",
            _ => false,
        };

        /// <summary>An older enum value as its upper-case name, written as a name or as its number; null for anything else.</summary>
        private static string? Old(JsonElement? value, params string[] names) => value switch
        {
            { ValueKind: JsonValueKind.String } text => text.GetString()?.ToUpperInvariant(),
            { ValueKind: JsonValueKind.Number } number when number.TryGetInt32(out var i) && i >= 0 && i < names.Length => names[i],
            _ => null,
        };
    }
}
