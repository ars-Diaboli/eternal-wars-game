using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The one set of margins and gaps every panel uses (the spacing pass, ADR-118, ADR-163). <see cref="CreateTheme"/> turns them into theme
/// constants that each app merges into the default theme at start, so containers space their children alike without overrides of their own; the fonts,
/// colours and boxes stay Godot's default look. Code that places controls itself (the HUD's corners, the battle HUD, the information window)
/// uses the same numbers.
/// </summary>
public static class UiSpacing
{
    /// <summary>Between the cells of a grid or a flow and between slots: army slots, paper-doll slots, the backpack.</summary>
    public const int Cell = 4;

    /// <summary>Between the rows of a panel.</summary>
    public const int Row = 6;

    /// <summary>Between controls side by side in a row.</summary>
    public const int Gap = 8;

    /// <summary>Above a section's heading, so a panel's parts read as groups.</summary>
    public const int Section = 16;

    /// <summary>Inside a panel's border.</summary>
    public const int Padding = 8;

    /// <summary>Inside the border of a screen or window over the map (town, hero, kingdom, menu, ...).</summary>
    public const int ScreenPadding = 16;

    /// <summary>From the window's edge to the HUD parts, and between them.</summary>
    public const int ScreenEdge = 8;

    /// <summary>
    /// The spacing as a theme: containers' separations and panels' inner margins. Everything else falls back to Godot's default theme. The
    /// panel box is the default one with margins added, so panels look as before.
    /// </summary>
    public static Theme CreateTheme()
    {
        var theme = new Theme();
        theme.SetConstant("separation", "BoxContainer", Row);
        theme.SetConstant("separation", "VBoxContainer", Row);
        theme.SetConstant("separation", "HBoxContainer", Gap);
        foreach (var type in new[] { "GridContainer", "FlowContainer", "HFlowContainer", "VFlowContainer" })
        {
            theme.SetConstant("h_separation", type, Cell);
            theme.SetConstant("v_separation", type, Cell);
        }

        theme.SetConstant("separation", "HSplitContainer", Gap);
        theme.SetConstant("separation", "VSplitContainer", Gap);
        if (ThemeDB.GetDefaultTheme().GetStylebox("panel", "PanelContainer").Duplicate() is StyleBox panel)
        {
            panel.SetContentMarginAll(Padding);
            theme.SetStylebox("panel", "PanelContainer", panel);
        }

        return theme;
    }

    /// <summary>
    /// Merges the spacing into Godot's default theme, the one every control falls back to, so it holds on every canvas layer (a theme on the
    /// root window does not reach controls under a canvas layer). Call it once, before the UI is built.
    /// </summary>
    public static void Apply() => ThemeDB.GetDefaultTheme().MergeWith(CreateTheme());

    /// <summary>Spaces a row of slots (an army) like the cells of a grid, <see cref="Cell"/> apart, as the paper doll beside it.</summary>
    public static T WithCellGap<T>(this T box) where T : BoxContainer
    {
        box.AddThemeConstantOverride("separation", Cell);
        return box;
    }

    /// <summary>A section's heading: <see cref="Section"/> of room above it, then the text (same font as the rest).</summary>
    public static Label AddHeading(this BoxContainer box, string text)
    {
        box.AddChild(Space(Section - Row));
        var label = new Label { Text = text };
        box.AddChild(label);
        return label;
    }

    /// <summary>Room of <paramref name="height"/> pixels in a vertical box, between rows that belong to different groups.</summary>
    public static Control Space(float height) => new() { CustomMinimumSize = new Vector2(0, height), MouseFilter = Control.MouseFilterEnum.Ignore };
}
