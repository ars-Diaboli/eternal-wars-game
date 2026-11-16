using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The game's look (ADR-191): dark slate panels framed in bronze, in the HoMM manner, with Godot's default fonts. One set of colours that
/// the theme, the screens, the status bar and the side panel share; <see cref="Apply"/> merges it with the spacing (<see cref="UiSpacing"/>)
/// into Godot's default theme at start, so it holds on every canvas layer.
/// </summary>
public static class UiTheme
{
    /// <summary>An opaque screen's and the status bar's ground.</summary>
    public static readonly Color Background = new(0.11f, 0.12f, 0.15f);

    /// <summary>A panel over the map: the same ground, a little of the map showing through.</summary>
    public static readonly Color PanelBackground = new(0.11f, 0.12f, 0.15f, 0.88f);

    /// <summary>A panel before a game, over the main menu's picture.</summary>
    public static readonly Color MenuBackground = new(0.07f, 0.075f, 0.1f, 0.85f);

    /// <summary>The bronze frame of panels and buttons.</summary>
    public static readonly Color Border = new(0.55f, 0.43f, 0.24f);

    /// <summary>A quieter frame: buttons at rest, inner borders.</summary>
    public static readonly Color BorderDim = new(0.36f, 0.3f, 0.2f);

    /// <summary>The frame of a button under the pointer or with the focus.</summary>
    public static readonly Color BorderBright = new(0.85f, 0.68f, 0.36f);

    /// <summary>A button's face.</summary>
    public static readonly Color ButtonFace = new(0.17f, 0.18f, 0.22f);

    /// <summary>The side panel's frame width, thicker than a panel's.</summary>
    public const int FrameWidth = 3;

    /// <summary>A flat box of <paramref name="background"/> framed in <paramref name="border"/>, <paramref name="padding"/> inside.</summary>
    public static StyleBoxFlat Box(Color background, Color border, int padding, int borderWidth = 1)
    {
        var style = new StyleBoxFlat { BgColor = background, BorderColor = border };
        style.SetBorderWidthAll(borderWidth);
        style.SetContentMarginAll(padding);
        return style;
    }

    /// <summary>The spacing (<see cref="UiSpacing.CreateTheme"/>) with the colours: panels and buttons.</summary>
    public static Theme CreateTheme()
    {
        var theme = UiSpacing.CreateTheme();
        theme.SetStylebox("panel", "PanelContainer", Box(PanelBackground, Border, UiSpacing.Padding));
        foreach (var type in new[] { "Button", "OptionButton", "MenuButton" })
        {
            theme.SetStylebox("normal", type, ButtonBox(ButtonFace, BorderDim));
            theme.SetStylebox("hover", type, ButtonBox(ButtonFace.Lightened(0.08f), BorderBright));
            theme.SetStylebox("pressed", type, ButtonBox(ButtonFace.Darkened(0.3f), BorderBright));
            theme.SetStylebox("hover_pressed", type, ButtonBox(ButtonFace.Darkened(0.2f), BorderBright));
            theme.SetStylebox("disabled", type, ButtonBox(ButtonFace.Darkened(0.35f), BorderDim.Darkened(0.4f)));
            var focus = new StyleBoxFlat { DrawCenter = false, BorderColor = BorderBright };
            focus.SetBorderWidthAll(1);
            theme.SetStylebox("focus", type, focus);
        }

        return theme;
    }

    /// <summary>Merges the theme into Godot's default one, which every control falls back to. Call it once, before the UI is built.</summary>
    public static void Apply() => ThemeDB.GetDefaultTheme().MergeWith(CreateTheme());

    private static StyleBoxFlat ButtonBox(Color face, Color border)
    {
        var style = Box(face, border, 0);
        style.ContentMarginLeft = style.ContentMarginRight = UiSpacing.Gap;
        style.ContentMarginTop = style.ContentMarginBottom = 4;
        style.SetCornerRadiusAll(2);
        return style;
    }
}
