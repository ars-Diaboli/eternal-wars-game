using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Settings;

/// <summary>
/// How the screens explain and show things (ADR-118, UI-3; ADR-154; ADR-176). <see cref="DetailedTooltips"/> adds the formulas and the raw
/// numbers to the information windows; off by default. <see cref="Fog"/> is how the fog of war looks on the adventure map;
/// <see cref="ShowGrid"/> draws the tile grid over its ground (ADR-183; off by default). <see cref="Window"/> is whether the game fills the
/// screen (ADR-186; a window by default). <see cref="MapSort"/>, <see cref="MapSortReversed"/> and <see cref="MapSizeFilter"/> are how the
/// new-game screen's map lists are sorted and filtered (ADR-204; by name, every size, by default). <see cref="UiScalePercent"/> is the size of
/// the game's UI (ADR-309; 100 % by default; <see cref="ScaleFor"/> lowers it on a small window). Kept per computer in the settings file, like <see cref="SpeedSettings"/>; it changes no rule.
/// </summary>
public sealed record InterfaceSettings(
    bool DetailedTooltips = false,
    FogLook Fog = FogLook.Dark,
    bool ShowGrid = false,
    WindowMode Window = WindowMode.Windowed,
    MapSort MapSort = MapSort.Name,
    bool MapSortReversed = false,
    int MapSizeFilter = MapListView.AllSizes,
    int UiScalePercent = 100)
{
    public const int MinUiScale = 100;
    public const int MaxUiScale = 200;

    /// <summary>The UI scales the game offers, in percent (ADR-309).</summary>
    public static IReadOnlyList<int> UiScales { get; } = [100, 150, 200];

    /// <summary>The smallest screen, in the UI's own pixels, the adventure HUD fits on whole (ADR-309): a UI scale never leaves less.</summary>
    public const int MinScaledWidth = 960, MinScaledHeight = 540;

    public static InterfaceSettings Default { get; } = new();

    /// <summary>
    /// The factor the UI is drawn at in a window of <paramref name="width"/> by <paramref name="height"/> pixels: the chosen scale (normalized),
    /// lowered as far as needed to leave the UI <see cref="MinScaledWidth"/> by <see cref="MinScaledHeight"/>, never below 1.
    /// </summary>
    public float ScaleFor(int width, int height)
    {
        var chosen = Normalized().UiScalePercent / 100f;
        var fits = Math.Min((float)width / MinScaledWidth, (float)height / MinScaledHeight);
        return Math.Max(1f, Math.Min(chosen, fits));
    }

    /// <summary>
    /// The same settings with the UI scale inside its range, as the map editor's preferences are, then on the nearest scale offered (the
    /// smaller of two as near).
    /// </summary>
    public InterfaceSettings Normalized()
    {
        var scale = Math.Clamp(UiScalePercent, MinUiScale, MaxUiScale);
        return this with { UiScalePercent = UiScales.MinBy(s => Math.Abs(s - scale)) };
    }

    /// <summary>These settings with the other window mode (Alt+Enter).</summary>
    public InterfaceSettings WithWindowToggled() =>
        this with { Window = Window == WindowMode.Fullscreen ? WindowMode.Windowed : WindowMode.Fullscreen };

    /// <summary>How the map lists are sorted and filtered (a size that is no preset shows every map).</summary>
    public MapListView MapList() => new MapListView(MapSort, MapSortReversed).Filtered(MapSizeFilter);

    /// <summary>These settings with the map lists sorted and filtered as <paramref name="view"/>.</summary>
    public InterfaceSettings WithMapList(MapListView view) =>
        this with { MapSort = view.Sort, MapSortReversed = view.Reversed, MapSizeFilter = view.Size };
}

/// <summary>How the game's window shows (ADR-186).</summary>
public enum WindowMode
{
    /// <summary>A window on the desktop.</summary>
    Windowed,

    /// <summary>The whole screen, borderless at the screen's own size.</summary>
    Fullscreen,
}

/// <summary>How the fog of war is drawn (ADR-176). Both hide the same tiles; only the picture differs.</summary>
public enum FogLook
{
    /// <summary>A starry void with drifting nebulae: the map floats in space.</summary>
    Dark,

    /// <summary>Thick white clouds drifting over the hidden land.</summary>
    Clouds,
}
