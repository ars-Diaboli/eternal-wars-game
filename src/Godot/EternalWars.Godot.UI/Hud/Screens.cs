using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>Shared look of the screens that cover the map (town, hero, kingdom, ...).</summary>
public static class Screens
{
    /// <summary>
    /// Gives the panel an opaque background in the bevelled bronze frame of a window (<see cref="UiFrames.Window"/>, ADR-294): Godot's default
    /// panel is translucent, so the map and the HUD showed through a screen and its text ran into theirs. Inside the frame it keeps
    /// <paramref name="padding"/> (by default a screen's, <see cref="UiSpacing.ScreenPadding"/>).
    /// </summary>
    public static void UseOpaqueBackground(this PanelContainer panel, int padding = UiSpacing.ScreenPadding)
    {
        panel.AddThemeStyleboxOverride("panel", UiFrames.Window(UiTheme.BorderBright, padding));
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
    /// The row of buttons along a window's bottom (OK, Cancel, Start, Back; ADR-204): with their text, as wide as their text, at the right.
    /// </summary>
    public static HBoxContainer ButtonRow(params Button[] buttons)
    {
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", UiSpacing.Gap);
        foreach (var button in buttons)
        {
            button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd;
            row.AddChild(button);
        }

        return row;
    }

    /// <summary>
    /// A window's close button (ADR-225): the icon-only "×" (<see cref="UiIcon.Close"/>) at the end of its title row, which a spacer pushes to
    /// the window's top-right corner. Escape still closes the window where it did.
    /// </summary>
    public static Button AddCloseButton(this HBoxContainer header, Action close)
    {
        header.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        var button = new Button { Text = "Close" }.WithIcon(UiIcon.Close);
        button.Pressed += close;
        header.AddChild(button);
        return button;
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
