using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The game's look (ADR-191): dark slate panels framed in bronze, in the HoMM manner, with Godot's default fonts. One set of colours that
/// the theme, the screens, the status bar and the side panel share; <see cref="Apply"/> merges it with the spacing (<see cref="UiSpacing"/>)
/// into Godot's default theme at start, so it holds on every canvas layer. The map editor uses the compact variant (EDIT-1, ADR-250): the
/// same colours with a smaller font, tighter rows and gaps and lower buttons, so more fits into a tool's panels.
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

    /// <summary>The compact variant's font size (Godot's own is 16).</summary>
    public const int CompactFontSize = 14;

    /// <summary>The side panel's frame width, thicker than a panel's.</summary>
    public const int FrameWidth = 3;

    // Text tints (a label's or button's Modulate, a list item's colour).

    /// <summary>A caution in a line: a dwelling that closes others, a heading in the template editor's inspector.</summary>
    public static readonly Color TextCaution = new(1f, 0.85f, 0.5f);

    /// <summary>A quiet caption: a column's title in the building tree.</summary>
    public static readonly Color TextQuiet = new(0.75f, 0.75f, 0.75f);

    /// <summary>Something done: a finished quest in the quest log.</summary>
    public static readonly Color TextDone = new(0.6f, 0.6f, 0.6f);

    /// <summary>A building that is fully built.</summary>
    public static readonly Color TextComplete = new(0.6f, 1f, 0.6f);

    /// <summary>A building waiting on others (its requirements are not met).</summary>
    public static readonly Color TextBlocked = new(0.7f, 0.7f, 0.7f);

    /// <summary>A building the player cannot pay for yet.</summary>
    public static readonly Color TextUnaffordable = new(1f, 0.75f, 0.6f);

    /// <summary>Why something cannot be done (the tavern's hire refusal).</summary>
    public static readonly Color TextRefusal = new(1f, 0.6f, 0.5f);

    /// <summary>An error in a list of problems (the validator, the template editor).</summary>
    public static readonly Color IssueError = new(1f, 0.45f, 0.4f);

    /// <summary>A warning in a list of problems (the validator, the template editor).</summary>
    public static readonly Color IssueWarning = new(1f, 0.85f, 0.4f);

    // Sizes of the town and hero screens and the editor's inspector (the least width of a label, a field or a slot).

    /// <summary>A wrapping text line on the town and hero screens (status, stats, skill descriptions).</summary>
    public const int ScreenTextWidth = 600;

    /// <summary>A labelled row on the town screen: the governor, a caravan's dwelling or provider.</summary>
    public const int TownRowWidth = 360;

    /// <summary>A dwelling's line and the blacksmith's caption on the town screen.</summary>
    public const int TownLineWidth = 240;

    /// <summary>A column of the town's building tree.</summary>
    public const int BuildingColumnWidth = 170;

    /// <summary>The name before a row of army slots, and the tavern's caption, on the town screen.</summary>
    public const int ArmyNameWidth = 110;

    /// <summary>A stack's slot on the town screen.</summary>
    public static readonly Vector2 TownSlotSize = new(86, 48);

    /// <summary>A stack's slot on the hero screen.</summary>
    public static readonly Vector2 HeroSlotSize = new(96, 56);

    /// <summary>A slot of the paper doll on the hero screen.</summary>
    public const int DollSlotWidth = 190;

    /// <summary>A free text in the inspector: a sign's or a box's message.</summary>
    public const int InspectorTextWidth = 260;

    /// <summary>A sphinx's riddle field in the inspector, and its caption.</summary>
    public const int InspectorFieldWidth = 280;

    /// <inheritdoc cref="InspectorFieldWidth"/>
    public const int InspectorFieldLabelWidth = 90;

    /// <summary>A town's name in the inspector.</summary>
    public const int InspectorNameWidth = 160;

    /// <summary>A wrapping note in the inspector (the arena place).</summary>
    public const int InspectorNoteWidth = 300;

    // The chat over the map in a LAN game (ADR-194): its place in the bottom-left corner, above the status bar.

    /// <summary>The map chat's distance from the screen's left edge.</summary>
    public const int MapChatLeft = 8;

    /// <summary>The map chat's size.</summary>
    public static readonly Vector2 MapChatSize = new(380, 150);

    /// <summary>The map chat's distance from the screen's bottom edge: the status bar is below it.</summary>
    public const int MapChatBottom = 40;

    /// <summary>The map chat's lines' see-through ground.</summary>
    public static readonly Color MapChatBackground = new(Background, 0.55f);

    /// <summary>A flat box of <paramref name="background"/> framed in <paramref name="border"/>, <paramref name="padding"/> inside.</summary>
    public static StyleBoxFlat Box(Color background, Color border, int padding, int borderWidth = 1)
    {
        var style = new StyleBoxFlat { BgColor = background, BorderColor = border };
        style.SetBorderWidthAll(borderWidth);
        style.SetContentMarginAll(padding);
        return style;
    }

    /// <summary>The spacing (<see cref="UiSpacing.CreateTheme"/>) with the colours: panels and buttons. <paramref name="compact"/>: the editor's variant.</summary>
    public static Theme CreateTheme(bool compact = false)
    {
        var theme = UiSpacing.CreateTheme(compact);
        theme.SetStylebox("panel", "PanelContainer", Box(PanelBackground, Border, compact ? UiSpacing.CompactPadding : UiSpacing.Padding));
        StyleBoxFlat ButtonBox(Color face, Color border) => ButtonStyle(face, border, compact);
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

    /// <summary>
    /// Merges the theme into Godot's default one, which every control falls back to. Call it once, before the UI is built.
    /// <paramref name="compact"/>: the editor's variant, with its smaller font.
    /// </summary>
    public static void Apply(bool compact = false)
    {
        var theme = ThemeDB.GetDefaultTheme();
        theme.MergeWith(CreateTheme(compact));
        if (compact)
        {
            theme.DefaultFontSize = CompactFontSize;
        }
    }

    private static StyleBoxFlat ButtonStyle(Color face, Color border, bool compact)
    {
        var style = Box(face, border, 0);
        style.ContentMarginLeft = style.ContentMarginRight = compact ? UiSpacing.CompactGap : UiSpacing.Gap;
        style.ContentMarginTop = style.ContentMarginBottom = compact ? 2 : 4;
        style.SetCornerRadiusAll(2);
        return style;
    }
}
