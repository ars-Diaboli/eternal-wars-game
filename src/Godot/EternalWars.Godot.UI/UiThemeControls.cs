using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The rest of the game's look (ADR-294; not the editor's compact variant): styles for the Godot controls the screens use beside panels and buttons (tabs, check boxes, text fields,
/// scroll bars, sliders, lists, menus, tooltips, separators, progress bars, embedded windows), the heading and title labels in the heading face,
/// and the slot buttons of armies. The frames are <see cref="UiFrames"/>' (the painted tooltip and divider where they exist).
/// </summary>
public static partial class UiTheme
{
    /// <summary>A section's heading (a Label's variation): the heading face at <see cref="UiFonts.HeadingSize"/>.</summary>
    public const string HeadingLabel = "HeadingLabel";

    /// <summary>A window's or screen's title (a Label's variation): the heading face at <see cref="UiFonts.TitleSize"/>.</summary>
    public const string TitleLabel = "TitleLabel";

    /// <summary>An army slot (a Button's variation): the sunken slot frame, lit under the pointer and when picked.</summary>
    public const string SlotButton = "SlotButton";

    /// <summary>The bars along a screen's top and bottom (a PanelContainer's variation): opaque, a bronze line toward the content.</summary>
    public const string BarPanel = "BarPanel";

    /// <summary>The tooltip's and the field's ground: a shade darker than a panel.</summary>
    private static Color FieldBackground => HighContrast ? new(0f, 0f, 0f) : new(0.07f, 0.075f, 0.095f);

    /// <summary>Adds the styles of every other control to <paramref name="theme"/>.</summary>
    private static void AddControlStyles(Theme theme)
    {
        var padding = UiSpacing.Padding;

        // Headings and titles in the heading face.
        theme.SetTypeVariation(HeadingLabel, "Label");
        theme.SetTypeVariation(TitleLabel, "Label");
        theme.SetFontSize("font_size", HeadingLabel, UiFonts.HeadingSize);
        theme.SetFontSize("font_size", TitleLabel, UiFonts.TitleSize);
        theme.SetColor("font_color", HeadingLabel, HighContrast ? Colors.White : new Color(0.93f, 0.86f, 0.7f));
        theme.SetColor("font_color", TitleLabel, HighContrast ? Colors.White : new Color(0.96f, 0.9f, 0.74f));
        if (UiFonts.Heading is { } heading)
        {
            theme.SetFont("font", HeadingLabel, heading);
            theme.SetFont("font", TitleLabel, heading);
        }

        // Army slots.
        theme.SetTypeVariation(SlotButton, "Button");
        theme.SetStylebox("normal", SlotButton, UiFrames.Slot());
        theme.SetStylebox("hover", SlotButton, UiFrames.Slot(lit: true));
        theme.SetStylebox("pressed", SlotButton, UiFrames.Slot(lit: true));
        theme.SetStylebox("hover_pressed", SlotButton, UiFrames.Slot(lit: true));
        theme.SetStylebox("disabled", SlotButton, UiFrames.Slot());
        theme.SetTypeVariation(BarPanel, "PanelContainer");
        theme.SetStylebox("panel", BarPanel, Box(Background, Border, padding));

        // Tabs: the chosen tab joins the panel below it.
        foreach (var type in new[] { "TabContainer", "TabBar" })
        {
            theme.SetStylebox("tab_selected", type, Tab(PanelBackground.Lightened(0.06f) with { A = 1 }, Border));
            theme.SetStylebox("tab_unselected", type, Tab(ButtonFace.Darkened(0.2f), BorderDim));
            theme.SetStylebox("tab_hovered", type, Tab(ButtonFace, BorderBright));
            theme.SetStylebox("tab_disabled", type, Tab(ButtonFace.Darkened(0.4f), BorderDim.Darkened(0.4f)));
            theme.SetStylebox("tab_focus", type, Focus());
        }

        theme.SetStylebox("panel", "TabContainer", Box(PanelBackground with { A = 1 }, Border, padding));

        // Check boxes and radio buttons: a bronze mark, no button face.
        foreach (var type in new[] { "CheckBox", "CheckButton" })
        {
            foreach (var state in new[] { "normal", "pressed", "hover", "hover_pressed", "disabled" })
            {
                theme.SetStylebox(state, type, new StyleBoxEmpty { ContentMarginLeft = 2, ContentMarginRight = 2, ContentMarginTop = 2, ContentMarginBottom = 2 });
            }

            theme.SetStylebox("focus", type, Focus());
        }

        theme.SetIcon("checked", "CheckBox", UiFrames.CheckMark(true));
        theme.SetIcon("unchecked", "CheckBox", UiFrames.CheckMark(false));
        theme.SetIcon("checked_disabled", "CheckBox", UiFrames.CheckMark(true, disabled: true));
        theme.SetIcon("unchecked_disabled", "CheckBox", UiFrames.CheckMark(false, disabled: true));
        theme.SetIcon("radio_checked", "CheckBox", UiFrames.RadioMark(true));
        theme.SetIcon("radio_unchecked", "CheckBox", UiFrames.RadioMark(false));

        // Text fields.
        foreach (var type in new[] { "LineEdit", "TextEdit" })
        {
            theme.SetStylebox("normal", type, Field(BorderDim));
            theme.SetStylebox("focus", type, Field(BorderBright));
            theme.SetStylebox("read_only", type, Field(BorderDim.Darkened(0.4f)));
        }

        // Scroll bars: a thin groove, a bronze grabber.
        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            theme.SetStylebox("scroll", type, Margined(Box(FieldBackground, FieldBackground, 0), 4));
            theme.SetStylebox("scroll_focus", type, Margined(Box(FieldBackground, BorderDim, 0), 4));
            theme.SetStylebox("grabber", type, Grabber(BorderDim));
            theme.SetStylebox("grabber_highlight", type, Grabber(Border));
            theme.SetStylebox("grabber_pressed", type, Grabber(BorderBright));
        }

        // Sliders: a groove, the lit part bronze, a disc to drag.
        foreach (var type in new[] { "HSlider", "VSlider" })
        {
            theme.SetStylebox("slider", type, Margined(Box(FieldBackground, BorderDim, 0), 3));
            theme.SetStylebox("grabber_area", type, Margined(Box(BorderDim, BorderDim, 0), 3));
            theme.SetStylebox("grabber_area_highlight", type, Margined(Box(Border, Border, 0), 3));
            theme.SetIcon("grabber", type, UiFrames.Grabber(false));
            theme.SetIcon("grabber_highlight", type, UiFrames.Grabber(true));
            theme.SetIcon("grabber_disabled", type, UiFrames.Grabber(false));
        }

        // Lists, trees and menus.
        foreach (var type in new[] { "ItemList", "Tree" })
        {
            theme.SetStylebox("panel", type, Box(FieldBackground, BorderDim, padding));
            theme.SetStylebox("focus", type, Focus());
            theme.SetStylebox("selected", type, Box(ButtonFace.Lightened(0.1f), Border, 2));
            theme.SetStylebox("selected_focus", type, Box(ButtonFace.Lightened(0.14f), BorderBright, 2));
            theme.SetStylebox("cursor", type, Focus());
            theme.SetStylebox("cursor_unfocused", type, new StyleBoxEmpty());
        }

        theme.SetStylebox("hovered", "ItemList", Box(ButtonFace, BorderDim, 2));
        theme.SetStylebox("panel", "PopupMenu", Box(Background, Border, padding));
        theme.SetStylebox("hover", "PopupMenu", Box(ButtonFace.Lightened(0.08f), BorderBright, 2));
        theme.SetStylebox("panel", "PopupPanel", Box(Background, Border, padding));
        theme.SetStylebox("panel", "TooltipPanel", UiFrames.Tooltip(padding) ?? (StyleBox)Box(FieldBackground with { A = 0.97f }, Border, padding));
        theme.SetColor("font_color", "TooltipLabel", HighContrast ? Colors.White : new Color(0.92f, 0.9f, 0.85f));

        // Separators, progress bars and the windows Godot embeds (dialogs).
        theme.SetStylebox("separator", "HSeparator", UiFrames.Divider() ?? (StyleBox)new StyleBoxLine { Color = BorderDim, Thickness = 1 });
        theme.SetStylebox("separator", "VSeparator", new StyleBoxLine { Color = BorderDim, Thickness = 1, Vertical = true });
        theme.SetStylebox("background", "ProgressBar", Box(FieldBackground, BorderDim, 0));
        theme.SetStylebox("fill", "ProgressBar", Box(Border.Darkened(0.1f), Border, 0));
        theme.SetStylebox("embedded_border", "Window", Box(Background, Border, padding, FrameWidth));
        theme.SetStylebox("embedded_unfocused_border", "Window", Box(Background, BorderDim, padding, FrameWidth));
        theme.SetStylebox("panel", "AcceptDialog", Box(Background, Border, padding));
    }

    private static StyleBoxFlat Tab(Color face, Color border)
    {
        var style = Box(face, border, 0);
        style.BorderWidthBottom = 0;
        style.ContentMarginLeft = style.ContentMarginRight = UiSpacing.Gap + 4;
        style.ContentMarginTop = style.ContentMarginBottom = 5;
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = 3;
        return style;
    }

    private static StyleBoxFlat Field(Color border)
    {
        var style = Box(FieldBackground, border, 0);
        style.ContentMarginLeft = style.ContentMarginRight = 6;
        style.ContentMarginTop = style.ContentMarginBottom = 4;
        return style;
    }

    private static StyleBoxFlat Grabber(Color color)
    {
        var style = Box(color, color, 0);
        style.SetCornerRadiusAll(3);
        style.SetContentMarginAll(4);
        return style;
    }

    private static StyleBoxFlat Margined(StyleBoxFlat style, int margin)
    {
        style.SetContentMarginAll(margin);
        return style;
    }

    private static StyleBoxFlat Focus()
    {
        var focus = new StyleBoxFlat { DrawCenter = false, BorderColor = BorderBright };
        focus.SetBorderWidthAll(HighContrast ? 2 : 1);
        return focus;
    }
}
