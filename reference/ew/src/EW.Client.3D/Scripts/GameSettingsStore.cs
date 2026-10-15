using Godot;

namespace EW.Scripts;

public enum HudPanelSide
{
    Left,
    Right
}

public static class GameSettingsStore
{
    private const string SettingsPath = "user://settings.cfg";
    private const string DisplaySection = "display";
    private const string InterfaceSection = "interface";
    private const string WidthKey = "window_width";
    private const string HeightKey = "window_height";
    private const string HudPanelSideKey = "hud_panel_side";

    public static Vector2I LoadWindowSize(Vector2I fallbackSize)
    {
        ConfigFile config = new();
        Error loadError = config.Load(SettingsPath);
        if (loadError != Error.Ok)
        {
            return fallbackSize;
        }

        Variant widthValue = config.GetValue(DisplaySection, WidthKey, fallbackSize.X);
        Variant heightValue = config.GetValue(DisplaySection, HeightKey, fallbackSize.Y);

        int width = (int)widthValue;
        int height = (int)heightValue;
        if (width <= 0 || height <= 0)
        {
            return fallbackSize;
        }

        return new Vector2I(width, height);
    }

    public static Error SaveWindowSize(Vector2I windowSize)
    {
        ConfigFile config = new();
        Error loadError = config.Load(SettingsPath);
        if (loadError != Error.Ok && loadError != Error.FileNotFound)
        {
            return loadError;
        }

        config.SetValue(DisplaySection, WidthKey, windowSize.X);
        config.SetValue(DisplaySection, HeightKey, windowSize.Y);
        return config.Save(SettingsPath);
    }

    public static HudPanelSide LoadHudPanelSide(HudPanelSide fallbackSide = HudPanelSide.Left)
    {
        ConfigFile config = new();
        Error loadError = config.Load(SettingsPath);
        if (loadError != Error.Ok)
        {
            return fallbackSide;
        }

        Variant sideValue = config.GetValue(InterfaceSection, HudPanelSideKey, (int)fallbackSide);
        int sideIndex = (int)sideValue;
        return System.Enum.IsDefined(typeof(HudPanelSide), sideIndex)
            ? (HudPanelSide)sideIndex
            : fallbackSide;
    }

    public static Error SaveHudPanelSide(HudPanelSide hudPanelSide)
    {
        ConfigFile config = new();
        Error loadError = config.Load(SettingsPath);
        if (loadError != Error.Ok && loadError != Error.FileNotFound)
        {
            return loadError;
        }

        config.SetValue(InterfaceSection, HudPanelSideKey, (int)hudPanelSide);
        return config.Save(SettingsPath);
    }
}
