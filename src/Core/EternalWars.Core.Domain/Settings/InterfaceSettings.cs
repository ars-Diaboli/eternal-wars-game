namespace EternalWars.Core.Domain.Settings;

/// <summary>
/// How the screens explain and show things (ADR-118, UI-3; ADR-154; ADR-176). <see cref="DetailedTooltips"/> adds the formulas and the raw
/// numbers to the information windows; off by default. <see cref="Fog"/> is how the fog of war looks on the adventure map. Kept per computer
/// in the settings file, like <see cref="SpeedSettings"/>; it changes no rule.
/// </summary>
public sealed record InterfaceSettings(bool DetailedTooltips = false, FogLook Fog = FogLook.Dark)
{
    public static InterfaceSettings Default { get; } = new();
}

/// <summary>How the fog of war is drawn (ADR-176). Both hide the same tiles; only the picture differs.</summary>
public enum FogLook
{
    /// <summary>A starry void with drifting nebulae: the map floats in space.</summary>
    Dark,

    /// <summary>Thick white clouds drifting over the hidden land.</summary>
    Clouds,
}
