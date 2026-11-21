using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The game's look (ADR-191): dark slate panels framed in bronze, in the HoMM manner, with Godot's default fonts. One set of colours that
/// the theme, the screens, the status bar and the side panel share; <see cref="Apply"/> merges it with the spacing (<see cref="UiSpacing"/>)
/// into Godot's default theme at start, so it holds on every canvas layer. The map editor uses the compact variant (EDIT-1, ADR-250): the
/// same colours with a smaller font, tighter rows and gaps and lower buttons, so more fits into a tool's panels. The editor's high contrast
/// (ADR-277, <see cref="HighContrast"/>): opaque near-black grounds, light frames, white text, and the quiet tints and disabled text
/// lightened; the colours below follow it, and <see cref="Apply"/> again switches a running UI.
/// </summary>
public static class UiTheme
{
    /// <summary>The editor's frame of a docked region: opaque, a panel's padding (a <see cref="Control.ThemeTypeVariation"/> of PanelContainer).</summary>
    public const string RegionPanel = "RegionPanel";

    /// <summary>A box over the whole editor (a question, Preferences): opaque, twice a panel's padding.</summary>
    public const string DialogPanel = "DialogPanel";

    /// <summary>A card over the map (the editor's empty state): nearly opaque, twice a panel's padding.</summary>
    public const string CardPanel = "CardPanel";

    /// <summary>A small framed tile inside a panel (the object browser's card, a picture picker's): a button's face, the quiet frame.</summary>
    public const string TilePanel = "TilePanel";

    /// <summary>An opaque page over the map (the template editor), a screen's padding.</summary>
    public const string PagePanel = "PagePanel";

    /// <summary>A quiet caption (a Label's variation): <see cref="TextQuiet"/>.</summary>
    public const string QuietLabel = "QuietLabel";

    /// <summary>Godot's own text colours that the high contrast replaces, kept to put back (null: Godot's theme had none).</summary>
    private static Dictionary<(string Name, string Type), Color?>? _godotColors;

    /// <summary>Whether the high-contrast colours are in use (the editor's preference, ADR-277).</summary>
    public static bool HighContrast { get; private set; }
    /// <summary>An opaque screen's and the status bar's ground.</summary>
    public static Color Background => HighContrast ? new(0.04f, 0.04f, 0.05f) : new(0.11f, 0.12f, 0.15f);

    /// <summary>A panel over the map: the same ground, a little of the map showing through.</summary>
    public static Color PanelBackground => HighContrast ? new(0.04f, 0.04f, 0.05f, 1f) : new(0.11f, 0.12f, 0.15f, 0.88f);

    /// <summary>A panel before a game, over the main menu's picture.</summary>
    public static Color MenuBackground => HighContrast ? new(0.03f, 0.03f, 0.04f, 0.95f) : new(0.07f, 0.075f, 0.1f, 0.85f);

    /// <summary>The bronze frame of panels and buttons.</summary>
    public static Color Border => HighContrast ? new(0.95f, 0.78f, 0.42f) : new(0.55f, 0.43f, 0.24f);

    /// <summary>A quieter frame: buttons at rest, inner borders.</summary>
    public static Color BorderDim => HighContrast ? new(0.75f, 0.62f, 0.38f) : new(0.36f, 0.3f, 0.2f);

    /// <summary>The frame of a button under the pointer or with the focus.</summary>
    public static Color BorderBright => HighContrast ? new(1f, 0.95f, 0.7f) : new(0.85f, 0.68f, 0.36f);

    /// <summary>A button's face.</summary>
    public static Color ButtonFace => HighContrast ? new(0.1f, 0.1f, 0.12f) : new(0.17f, 0.18f, 0.22f);

    /// <summary>The compact variant's font size (Godot's own is 16).</summary>
    public const int CompactFontSize = 14;

    /// <summary>The side panel's frame width, thicker than a panel's.</summary>
    public const int FrameWidth = 3;

    // Text tints (a label's or button's Modulate, a list item's colour).

    /// <summary>A caution in a line: a dwelling that closes others, a heading in the template editor's inspector.</summary>
    public static readonly Color TextCaution = new(1f, 0.85f, 0.5f);

    /// <summary>A quiet caption: a column's title in the building tree.</summary>
    public static Color TextQuiet => HighContrast ? new(0.92f, 0.92f, 0.92f) : new(0.75f, 0.75f, 0.75f);

    /// <summary>Something done: a finished quest in the quest log.</summary>
    public static Color TextDone => HighContrast ? new(0.8f, 0.8f, 0.8f) : new(0.6f, 0.6f, 0.6f);

    /// <summary>A building that is fully built.</summary>
    public static readonly Color TextComplete = new(0.6f, 1f, 0.6f);

    /// <summary>A building waiting on others (its requirements are not met).</summary>
    public static Color TextBlocked => HighContrast ? new(0.85f, 0.85f, 0.85f) : new(0.7f, 0.7f, 0.7f);

    /// <summary>A building the player cannot pay for yet.</summary>
    public static readonly Color TextUnaffordable = new(1f, 0.75f, 0.6f);

    /// <summary>Why something cannot be done (the tavern's hire refusal).</summary>
    public static readonly Color TextRefusal = new(1f, 0.6f, 0.5f);

    /// <summary>An error in a list of problems (the validator, the template editor).</summary>
    public static Color IssueError => HighContrast ? new(1f, 0.62f, 0.56f) : new(1f, 0.45f, 0.4f);

    /// <summary>A warning in a list of problems (the validator, the template editor).</summary>
    public static Color IssueWarning => HighContrast ? new(1f, 0.92f, 0.55f) : new(1f, 0.85f, 0.4f);

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
        foreach (var (variation, style) in new[]
        {
            (RegionPanel, Box(new Color(PanelBackground, 1), Border, UiSpacing.Padding)),
            (DialogPanel, Box(new Color(PanelBackground, 1), Border, UiSpacing.Padding * 2)),
            (CardPanel, Box(new Color(PanelBackground, 0.94f), Border, UiSpacing.Padding * 2)),
            (TilePanel, Box(ButtonFace, BorderDim, UiSpacing.Padding)),
            (PagePanel, Box(Background, Border, UiSpacing.ScreenPadding)),
        })
        {
            theme.SetTypeVariation(variation, "PanelContainer");
            theme.SetStylebox("panel", variation, style);
        }

        theme.SetTypeVariation(QuietLabel, "Label");
        theme.SetColor("font_color", QuietLabel, TextQuiet);
        if (HighContrast)
        {
            foreach (var (name, type, colour) in HighContrastText())
            {
                theme.SetColor(name, type, colour);
            }
        }

        StyleBoxFlat ButtonBox(Color face, Color border) => ButtonStyle(face, border, compact);
        foreach (var type in new[] { "Button", "OptionButton", "MenuButton" })
        {
            theme.SetStylebox("normal", type, ButtonBox(ButtonFace, BorderDim));
            theme.SetStylebox("hover", type, ButtonBox(ButtonFace.Lightened(0.08f), BorderBright));
            theme.SetStylebox("pressed", type, ButtonBox(ButtonFace.Darkened(0.3f), BorderBright));
            theme.SetStylebox("hover_pressed", type, ButtonBox(ButtonFace.Darkened(0.2f), BorderBright));
            theme.SetStylebox("disabled", type, ButtonBox(ButtonFace.Darkened(0.35f), BorderDim.Darkened(0.4f)));
            var focus = new StyleBoxFlat { DrawCenter = false, BorderColor = BorderBright };
            focus.SetBorderWidthAll(HighContrast ? 2 : 1);
            theme.SetStylebox("focus", type, focus);
        }

        return theme;
    }

    /// <summary>
    /// Merges the theme into Godot's default one, which every control falls back to: before the UI is built, and again to switch a running
    /// UI to or from <paramref name="highContrast"/> (every other item is set in both looks; the text colours only the high contrast sets go
    /// back to Godot's own). <paramref name="compact"/>: the editor's variant, with its smaller font.
    /// </summary>
    public static void Apply(bool compact = false, bool highContrast = false)
    {
        var theme = ThemeDB.GetDefaultTheme();
        _godotColors ??= HighContrastText().ToDictionary(c => (c.Name, c.Type), c => theme.HasColor(c.Name, c.Type) ? theme.GetColor(c.Name, c.Type) : (Color?)null);
        if (!highContrast)
        {
            var godot = new Theme(); // one merge, so the running UI hears of one change rather than one a colour
            foreach (var ((name, type), colour) in _godotColors)
            {
                if (colour is { } own)
                {
                    godot.SetColor(name, type, own);
                }
                else if (theme.HasColor(name, type))
                {
                    theme.ClearColor(name, type);
                }
            }

            theme.MergeWith(godot);
        }

        HighContrast = highContrast;
        theme.MergeWith(CreateTheme(compact));
        if (compact)
        {
            theme.DefaultFontSize = CompactFontSize;
        }
    }

    /// <summary>The high contrast's text colours: white, the disabled and the placeholder text light grey rather than faded.</summary>
    private static IEnumerable<(string Name, string Type, Color Colour)> HighContrastText()
    {
        var white = Colors.White;
        var disabled = new Color(0.74f, 0.74f, 0.74f);
        foreach (var type in new[] { "Label", "Button", "OptionButton", "MenuButton", "CheckBox", "CheckButton", "LinkButton", "LineEdit", "TextEdit", "Tree", "ItemList", "PopupMenu", "MenuBar" })
        {
            yield return ("font_color", type, white);
        }

        foreach (var type in new[] { "Button", "OptionButton", "MenuButton", "CheckBox", "CheckButton", "MenuBar" })
        {
            yield return ("font_hover_color", type, white);
            yield return ("font_pressed_color", type, white);
            yield return ("font_focus_color", type, white);
            yield return ("font_hover_pressed_color", type, white);
            yield return ("font_disabled_color", type, disabled);
        }

        yield return ("font_disabled_color", "PopupMenu", disabled);
        yield return ("font_hover_color", "PopupMenu", white);
        yield return ("font_accelerator_color", "PopupMenu", disabled);
        yield return ("font_separator_color", "PopupMenu", disabled);
        yield return ("font_readonly_color", "TextEdit", disabled);
        yield return ("font_hover_color", "LinkButton", white);
        yield return ("font_pressed_color", "LinkButton", white);
        yield return ("font_hovered_color", "Tree", white);
        yield return ("font_hovered_color", "ItemList", white);
        yield return ("font_uneditable_color", "LineEdit", disabled);
        yield return ("font_placeholder_color", "LineEdit", disabled);
        yield return ("font_placeholder_color", "TextEdit", disabled);
        yield return ("font_selected_color", "Tree", white);
        yield return ("font_selected_color", "ItemList", white);
        foreach (var type in new[] { "TabBar", "TabContainer" })
        {
            yield return ("font_selected_color", type, white);
            yield return ("font_hovered_color", type, white);
            yield return ("font_unselected_color", type, new Color(0.88f, 0.88f, 0.88f));
            yield return ("font_disabled_color", type, disabled);
        }

        yield return ("default_color", "RichTextLabel", white);
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
