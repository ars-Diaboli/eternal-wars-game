using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>Shared look of the screens that cover the map (town, hero, kingdom, ...).</summary>
public static class Screens
{
    /// <summary>
    /// Gives the panel an opaque background: Godot's default panel is translucent, so the map and the HUD showed through a
    /// screen and its text ran into theirs. Inside the border it keeps <paramref name="padding"/> (by default a screen's,
    /// <see cref="UiSpacing.ScreenPadding"/>).
    /// </summary>
    public static void UseOpaqueBackground(this PanelContainer panel, int padding = UiSpacing.ScreenPadding)
    {
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTheme.Background, UiTheme.Border, padding));
    }

    /// <summary>
    /// The panel of a screen before a game, over the <see cref="Setup.MenuBackground"/> picture: dark enough to read, a little of the picture
    /// showing through, with a screen's padding.
    /// </summary>
    public static void UseMenuPanel(this PanelContainer panel)
    {
        panel.AddThemeStyleboxOverride("panel", UiTheme.Box(UiTheme.MenuBackground, UiTheme.Border, UiSpacing.ScreenPadding));
    }

    /// <summary>
    /// An opaque panel held in the middle of the screen. It grows to both sides as its content grows; with only the Center
    /// preset it grew right and down from the middle, past the screen's edge.
    /// </summary>
    public static void CenterAsScreen(this PanelContainer panel)
    {
        panel.UseOpaqueBackground();
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical = Control.GrowDirection.Both;
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center);
    }
}
