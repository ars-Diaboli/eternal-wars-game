namespace EternalWars.Core.Domain.Settings;

/// <summary>
/// How the screens explain things (ADR-118, UI-3; ADR-154). <see cref="DetailedTooltips"/> adds the formulas and the raw numbers to the
/// information windows; off by default. Kept per computer in the settings file, like <see cref="SpeedSettings"/>; it changes no rule.
/// </summary>
public sealed record InterfaceSettings(bool DetailedTooltips = false)
{
    public static InterfaceSettings Default { get; } = new();
}
