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

/// <summary>How the hero and town lists run: down a side column, or as rows along the bottom of the screen.</summary>
public enum ListOrientation
{
    Vertical,
    Horizontal,
}

/// <summary>
/// Where the screens put their parts (ADR-118, UI-3; ADR-156): the battle's initiative bar, the adventure map's minimap corner, the hero
/// and town lists and the action panel's side. Kept per computer in the settings file, like <see cref="SpeedSettings"/>; it changes no rule.
/// </summary>
public sealed record LayoutSettings(
    ScreenEdge InitiativeBar = ScreenEdge.Top,
    ScreenCorner Minimap = ScreenCorner.TopRight,
    ListOrientation Lists = ListOrientation.Vertical,
    ScreenSide ActionPanel = ScreenSide.Right)
{
    public static LayoutSettings Default { get; } = new();
}
