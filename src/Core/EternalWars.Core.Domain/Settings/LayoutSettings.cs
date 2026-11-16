namespace EternalWars.Core.Domain.Settings;

/// <summary>The screen edge a bar runs along.</summary>
public enum ScreenEdge
{
    Top,
    Bottom,
}

/// <summary>A corner of the screen.</summary>
public enum ScreenCorner
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft,
}

/// <summary>A side of the screen.</summary>
public enum ScreenSide
{
    Right,
    Left,
}

/// <summary>
/// The HoMM-style side panel (ADR-191): one framed column down a side of the adventure map, from the top to the status bar, holding the
/// minimap, the hero and town lists and the action panel; or off, and the parts stand free where the other settings put them.
/// </summary>
public enum SidePanel
{
    Right,
    Left,
    Off,
}

/// <summary>How the hero and town lists run: down a side column, or as rows along the bottom of the screen.</summary>
public enum ListOrientation
{
    Vertical,
    Horizontal,
}

/// <summary>
/// Where the screens put their parts (ADR-118, UI-3; ADR-156): the battle's initiative bar, the adventure map's minimap corner, the hero
/// and town lists and the action panel's side, unless the <see cref="SidePanel"/> holds them all (ADR-191). Kept per computer in the settings file, like <see cref="SpeedSettings"/>; it changes no rule.
/// </summary>
public sealed record LayoutSettings(
    ScreenEdge InitiativeBar = ScreenEdge.Top,
    ScreenCorner Minimap = ScreenCorner.TopRight,
    ListOrientation Lists = ListOrientation.Vertical,
    ScreenSide ActionPanel = ScreenSide.Right,
    SidePanel SidePanel = SidePanel.Right)
{
    public static LayoutSettings Default { get; } = new();
}
