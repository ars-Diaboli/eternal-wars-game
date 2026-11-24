namespace EternalWars.Core.Domain.Settings;

/// <summary>The screen edge a bar runs along.</summary>
public enum ScreenEdge
{
    Top,
    Bottom,
}

/// <summary>A side of the screen.</summary>
public enum ScreenSide
{
    Right,
    Left,
}

/// <summary>
/// Where the screens put their parts (ADR-118, UI-3; ADR-156; ADR-309): the battle's initiative bar along the top or the bottom, and whether the
/// adventure map's HUD is the default arrangement or its mirror, left and right swapped (<see cref="HudArrangement"/>). Kept per computer in the
/// settings file, like <see cref="SpeedSettings"/>; it changes no rule.
/// </summary>
public sealed record LayoutSettings(ScreenEdge InitiativeBar = ScreenEdge.Top, bool Mirrored = false)
{
    public static LayoutSettings Default { get; } = new();
}
