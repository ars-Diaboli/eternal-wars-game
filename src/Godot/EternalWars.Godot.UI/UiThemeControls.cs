using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The rest of the game's look (ADR-294; not the editor's compact variant): styles for the Godot controls the screens use beside panels and buttons (tabs, check boxes, text fields,
/// scroll bars, sliders, lists, menus, tooltips, separators, progress bars, embedded windows), the heading and title labels in the heading face,
/// and the slot buttons of armies. Panels, slots, lists, text fields, tooltips and pop-ups are framed by the shader (ADR-310,
/// <see cref="UiFrames.Themed"/>); the rest is drawn flat in its colours, a near-black face and a grey rim (<see cref="Rim"/>).
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
    private static Color FieldBackground => HighContrast ? new(0f, 0f, 0f) : new(0.04f, 0.042f, 0.047f);

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
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            // Sunk in, lit under the pointer by the frame itself (ADR-310).
            theme.SetStylebox(state, SlotButton, UiFrames.Themed(BevelKind.Slot, 2, flat: UiFrames.Slot(lit: state is "hover" or "pressed" or "hover_pressed")));
        }

        theme.SetTypeVariation(BarPanel, "PanelContainer");
        theme.SetStylebox("panel", BarPanel, UiFrames.Themed(BevelKind.Panel, padding, flat: Box(Background, Border, padding)));

        // Tabs: the chosen tab joins the panel below it.
        foreach (var type in new[] { "TabContainer", "TabBar" })
        {
            theme.SetStylebox("tab_selected", type, Tab(Face.Lightened(0.06f), Rim));
            theme.SetStylebox("tab_unselected", type, Tab(Face, RimDim));
            theme.SetStylebox("tab_hovered", type, Tab(Face.Lightened(0.04f), RimBright));
            theme.SetStylebox("tab_disabled", type, Tab(Face.Darkened(0.4f), RimDim.Darkened(0.4f)));
            theme.SetStylebox("tab_focus", type, Focus());
        }

        theme.SetStylebox("panel", "TabContainer", Rounded(Box(Face.Lightened(0.06f), Rim, padding)));

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
            var field = Field(BorderDim);
            theme.SetStylebox("normal", type, UiFrames.Themed(BevelKind.Slot, (int)field.ContentMarginTop, (int)field.ContentMarginLeft, flat: field));
            theme.SetStylebox("focus", type, HighContrast ? Field(BorderBright) : Rounded(new StyleBoxFlat { DrawCenter = false, BorderColor = RimBright }, width: 1));
            theme.SetStylebox("read_only", type, Field(BorderDim.Darkened(0.4f)));
        }

        // Scroll bars: a thin groove, a bronze grabber.
        foreach (var type in new[] { "VScrollBar", "HScrollBar" })
        {
            theme.SetStylebox("scroll", type, Margined(Box(FieldBackground, FieldBackground, 0), 4));
            theme.SetStylebox("scroll_focus", type, Margined(Box(FieldBackground, RimDim, 0), 4));
            theme.SetStylebox("grabber", type, Grabber(RimDim.Lightened(0.15f)));
            theme.SetStylebox("grabber_highlight", type, Grabber(Rim));
            theme.SetStylebox("grabber_pressed", type, Grabber(RimBright));
        }

        // Sliders: a groove, the lit part bronze, a disc to drag.
        foreach (var type in new[] { "HSlider", "VSlider" })
        {
            theme.SetStylebox("slider", type, Margined(Box(FieldBackground, RimDim, 0), 3));
            theme.SetStylebox("grabber_area", type, Margined(Box(Rim, Rim, 0), 3));
            theme.SetStylebox("grabber_area_highlight", type, Margined(Box(RimBright, RimBright, 0), 3));
            theme.SetIcon("grabber", type, UiFrames.Grabber(false));
            theme.SetIcon("grabber_highlight", type, UiFrames.Grabber(true));
            theme.SetIcon("grabber_disabled", type, UiFrames.Grabber(false));
        }

        // Lists, trees and menus.
        foreach (var type in new[] { "ItemList", "Tree" })
        {
            theme.SetStylebox("panel", type, UiFrames.Themed(BevelKind.Slot, padding, flat: Box(FieldBackground, BorderDim, padding)));
            theme.SetStylebox("focus", type, Focus());
            theme.SetStylebox("selected", type, Rounded(Box(Face.Lightened(0.1f), Rim, 2)));
            theme.SetStylebox("selected_focus", type, Rounded(Box(Face.Lightened(0.14f), RimBright, 2)));
            theme.SetStylebox("cursor", type, Focus());
            theme.SetStylebox("cursor_unfocused", type, new StyleBoxEmpty());
        }

        theme.SetStylebox("hovered", "ItemList", Rounded(Box(Face.Lightened(0.05f), RimDim, 2)));
        theme.SetStylebox("panel", "PopupMenu", Rounded(Box(Face, Rim, padding)));
        theme.SetStylebox("hover", "PopupMenu", Rounded(Box(Face.Lightened(0.08f), RimBright, 2)));
        theme.SetStylebox("panel", "PopupPanel", UiFrames.Themed(BevelKind.Panel, padding, flat: Box(Background, Border, padding)));
        theme.SetStylebox("panel", "TooltipPanel", HighContrast
            ? Box(FieldBackground, Border, padding)
            : UiFrames.Themed(BevelKind.Panel, UiFrames.Tooltip(padding), padding));
        theme.SetColor("font_color", "TooltipLabel", HighContrast ? Colors.White : new Color(0.92f, 0.9f, 0.85f));

        // Separators, progress bars and the windows Godot embeds (dialogs).
        theme.SetStylebox("separator", "HSeparator", new StyleBoxLine { Color = RimDim, Thickness = 1 });
        theme.SetStylebox("separator", "VSeparator", new StyleBoxLine { Color = RimDim, Thickness = 1, Vertical = true });
        theme.SetStylebox("background", "ProgressBar", Rounded(Box(FieldBackground, RimDim, 0), radius: 3));
        theme.SetStylebox("fill", "ProgressBar", Rounded(Box(Rim, RimBright, 0), radius: 3));
        theme.SetStylebox("embedded_border", "Window", Rounded(Box(Face, Rim, padding, FrameWidth)));
        theme.SetStylebox("embedded_unfocused_border", "Window", Rounded(Box(Face, RimDim, padding, FrameWidth)));
        theme.SetStylebox("panel", "AcceptDialog", UiFrames.Themed(BevelKind.Panel, padding, flat: Box(Background, Border, padding)));
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

    /// <summary>The shader frames' corners on a flat box (<paramref name="radius"/>, 6 px), <paramref name="width"/> its rim; square in high contrast.</summary>
    private static StyleBoxFlat Rounded(StyleBoxFlat style, int radius = 6, int? width = null)
    {
        if (!HighContrast)
        {
            style.SetCornerRadiusAll(radius);
        }

        if (width is { } w)
        {
            style.SetBorderWidthAll(w);
        }

        return style;
    }

    private static StyleBoxFlat Margined(StyleBoxFlat style, int margin)
    {
        style.SetContentMarginAll(margin);
        return style;
    }

    private static StyleBoxFlat Focus()
    {
        var focus = new StyleBoxFlat { DrawCenter = false, BorderColor = RimBright };
        focus.SetBorderWidthAll(HighContrast ? 2 : 1);
        return focus;
    }
}
