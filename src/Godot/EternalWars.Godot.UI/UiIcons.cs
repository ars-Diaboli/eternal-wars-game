using System.Collections.Generic;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>The stone UI icons in <c>res://shared/textures/ui/icons/</c>.</summary>
public enum UiIcon
{
    Close,
    Confirm,
    ArrowLeft,
    ArrowRight,
    ArrowUp,
    ArrowDown,
    Plus,
    Minus,
    Ban,
    Settings,
    Hourglass,
    Move,
    SpellBook,
    Spells,
    Quests,
    PuzzleMap,
    Dig,
    EndTurn,
    SwitchLevel,
    Kingdom,
    Marketplace,
    ThievesGuild,
    Defend,
    Warcry,
    Retreat,
    Surrender,
    QuickCombat,
    AutoCombat,
    Hero,
    Undo,
    Redo,
    ZoomIn,
    ZoomOut,
    Save,
    Load,
    Lock,
    Unlock,
    Menu,

    // The adventure HUD's marks (icon-prompts.md sheets 30–32): the town list, the levels, the replay bar, the route's end.
    TownCanBuild,
    TownFullyBuilt,
    LevelSurface,
    LevelUnderground,
    Pause,
    NextTurn,
    NextDay,
    SkipToResult,
    TakeOver,
    RouteAttack,
    RouteVisit,
    RoutePickUp,
    RouteEnterTown,
    RouteMeetHero,

    // The map and template editor's icons (editor-icon-prompts.md): the tools, then the template's parts, then the plain commands and options.
    ToolTerrain,
    ToolRoad,
    ToolRiver,
    ToolHeight,
    ToolStartZone,
    ToolScatter,
    ToolObject,
    ToolMonster,
    ToolGrail,
    ToolLinkGates,
    ToolStamp,
    ToolMeasure,
    ToolSelect,
    ToolMove,
    ToolErase,
    ToolFill,
    ZoneStart,
    ZoneAiStart,
    ZoneTreasure,
    ZoneJunction,
    ConnectionNormal,
    ConnectionWide,
    ConnectionGate,
    ConnectionPortal,
    GuardGarrison,
    GuardBorder,
    GuardBorderGate,
    Cut,
    Copy,
    Paste,
    Duplicate,
    Delete,
    SelectAll,
    Deselect,
    Rotate,
    Mirror,
    Crop,
    Resize,
    Link,
    New,
    SaveAs,
    Quit,
    Play,
    Help,
    CommandPalette,
    PanelLeft,
    PanelBottom,
    PanelRight,
    PanelsHide,
    Eye,
    EyeOff,
    Inspector,
    Layers,
    Events,
    History,
    Output,
    Filter,
    Error,
    Warning,
    Favourite,
    FavouriteOff,
    HeightRaise,
    HeightLower,
    HeightLevel,
    HeightSmooth,
    BrushRound,
    BrushSquare,
    Generate,
    NewSeed,
    Arrange,
    MirrorVertical,
    MirrorHorizontal,
    MirrorDiagonal,
    StrengthNone,
    StrengthWeak,
    StrengthAverage,
    StrengthStrong,
    Grid,
    TopDown,
    Focus,
    WholeMap,
    GoTo,
    Bookmark,
    Map,
    Players,
    Conditions,
    WeekEvents,
    Rumours,
    Texts,
    ToolPanel,
    Overlays,
    Fairness,
    Minimap,
    ExportReport,
    ExportPicture,
    MapTab,
    TemplateTab,
    SampleMaps,
    Find,
}

/// <summary>
/// Puts the UI icons on buttons and tabs. The 128 px art is scaled down once per size with Lanczos (the canvas has no
/// mipmaps, so letting the button shrink it looked ragged) and cached. A missing file leaves the control as it was: the
/// apps still run before <c>tools/link-shared-assets.ps1</c>.
/// </summary>
public static class UiIcons
{
    /// <summary>The icon size next to a button's text.</summary>
    public const int ButtonSize = 18;

    private const string Folder = "res://shared/textures/ui/icons/";
    private static readonly Dictionary<(UiIcon, int), Texture2D?> Cache = [];

    /// <summary>The icon at <paramref name="size"/> pixels, or null if its file is missing.</summary>
    public static Texture2D? Get(UiIcon icon, int size = ButtonSize)
    {
        if (Cache.TryGetValue((icon, size), out var cached))
        {
            return cached;
        }

        var texture = LoadScaled($"{Folder}{FileName(icon)}.png", size);
        Cache[(icon, size)] = texture;
        return texture;
    }

    /// <summary>The image at <paramref name="path"/> scaled to <paramref name="size"/> pixels square, or null if the file is missing.</summary>
    internal static Texture2D? LoadScaled(string path, int size)
    {
        if (!ResourceLoader.Exists(path) || GD.Load<Texture2D>(path)?.GetImage() is not { } image)
        {
            return null;
        }

        if (image.IsCompressed())
        {
            image.Decompress();
        }

        image.Resize(size, size, Image.Interpolation.Lanczos);
        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>An icon button's size in a bar or panel: the side panel's actions, the battle bar, the turn block (ADR-204).</summary>
    public const int BarSize = 48;

    /// <summary>An icon button's size inside a window or a row (ADR-204).</summary>
    public const int WindowSize = 32;

    /// <summary>An icon button's size on the status bar, whose line is <see cref="Hud.StatusBar.Height"/> tall.</summary>
    public const int StatusBarSize = 20;

    /// <summary>
    /// The metadata holding an icon-only button's label (its text and hotkey), which the <see cref="Hud.StatusBar"/> shows while the pointer is
    /// over it (ADR-204).
    /// </summary>
    public const string StatusLabelMeta = "status_label";

    /// <summary>
    /// Whether the app shows an icon-only button's label on a status bar (the game, ADR-204); false (the map editor, which has none) puts the
    /// label into the button's tooltip instead. The app sets it before it builds its controls.
    /// </summary>
    public static bool LabelsOnStatusBar { get; set; } = true;

    /// <summary>The room round an icon-only button's icon, on each side: the button is the icon plus twice this, square.</summary>
    public const int IconPadding = 6;

    /// <summary>
    /// Shows <paramref name="icon"/> alone on a square button at <paramref name="size"/> pixels (ADR-204), which keeps its size in any
    /// container: its text becomes the label the status bar shows on hover (<see cref="StatusLabelMeta"/>; the tooltip when
    /// <see cref="LabelsOnStatusBar"/> is off), and the name smoke scripts press it by. With <paramref name="keepText"/> (the buttons along a
    /// window's bottom: OK, Cancel, Start, Back) the icon stands small before the text instead. The text stays if the icon is missing.
    /// </summary>
    public static T WithIcon<T>(this T button, UiIcon icon, int size = WindowSize, bool keepText = false) where T : Button =>
        Get(icon, keepText ? ButtonSize : size) is { } texture ? button.WithIcon(texture, size, keepText) : button;

    /// <summary>
    /// <see cref="WithIcon{T}(T, UiIcon, int, bool)"/> with a picture that is no <see cref="UiIcon"/> (a placeholder), drawn at
    /// <paramref name="size"/> pixels already.
    /// </summary>
    public static T WithIcon<T>(this T button, Texture2D texture, int size, bool keepText = false) where T : Button
    {
        button.Icon = texture;
        if (keepText)
        {
            return button;
        }

        button.IconAlignment = HorizontalAlignment.Center;
        var side = size + (2 * IconPadding);
        button.CustomMinimumSize = new Vector2(side, side);
        button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin; // square, never stretched by its row or column
        button.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        Label(button, button.Text);
        return button;
    }

    private static void Label(Button button, string label)
    {
        button.Text = "";
        button.SetMeta(StatusLabelMeta, label);
        button.SetMeta(Shared.Controls.AutoInput.LabelMeta, label);
        if (!LabelsOnStatusBar && string.IsNullOrEmpty(button.TooltipText))
        {
            button.TooltipText = label;
        }
    }

    /// <summary>
    /// Sets what a button says: an icon-only button's label (<see cref="WithIcon"/>), else its text. Code that renames a button after it was
    /// made ("Start anyway", the surrender's price) goes through here, so an icon-only button keeps showing only its icon.
    /// </summary>
    public static void SetLabel(this Button button, string label)
    {
        if (button.HasMeta(StatusLabelMeta))
        {
            Label(button, label);
        }
        else
        {
            button.Text = label;
        }
    }

    /// <summary>What the status bar says while the pointer is over <paramref name="control"/>: an icon-only button's label, then its tooltip.</summary>
    public static string StatusText(Control control)
    {
        var (label, tip) = StatusParts(control);
        return label.Length == 0 ? tip : tip.Length == 0 ? label : $"{label}: {tip}";
    }

    /// <summary>
    /// <paramref name="control"/>'s status-bar label (an icon-only button's, <see cref="StatusLabelMeta"/>) and its tooltip: the status bar's
    /// subject and detail (ADR-309). A tooltip that only repeats the label is left out; either may be empty.
    /// </summary>
    public static (string Label, string Tip) StatusParts(Control control)
    {
        var label = control.HasMeta(StatusLabelMeta) ? control.GetMeta(StatusLabelMeta).AsString() : "";
        var tip = control.TooltipText;
        return (label, tip == label ? "" : tip);
    }

    /// <summary><paramref name="text"/> after <paramref name="icon"/>, for a heading that is no button; just the label if the icon is missing.</summary>
    public static Control IconLabel(UiIcon icon, string text)
    {
        var label = new Label { Text = text };
        if (Get(icon) is not { } texture)
        {
            return label;
        }

        var row = new HBoxContainer();
        row.AddChild(new TextureRect { Texture = texture, StretchMode = TextureRect.StretchModeEnum.KeepCentered });
        row.AddChild(label);
        return row;
    }

    /// <summary>Shows <paramref name="icon"/> on a tab's title.</summary>
    public static void WithTabIcon(this TabContainer tabs, int index, UiIcon icon)
    {
        if (Get(icon, 16) is { } texture)
        {
            tabs.SetTabIcon(index, texture);
        }
    }

    /// <summary>The icon's file name: its name in snake case (<see cref="UiIcon.SpellBook"/>: <c>spell_book</c>).</summary>
    public static string FileName(UiIcon icon)
    {
        var name = icon.ToString();
        var file = new System.Text.StringBuilder(name.Length + 4);
        for (var i = 0; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && i > 0)
            {
                file.Append('_');
            }

            file.Append(char.ToLowerInvariant(name[i]));
        }

        return file.ToString();
    }
}
