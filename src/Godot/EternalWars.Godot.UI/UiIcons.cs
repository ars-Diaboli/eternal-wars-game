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
    Info,
    Undo,
    Redo,
    ZoomIn,
    ZoomOut,
    Save,
    Load,
    Lock,
    Unlock,
    Menu,
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

    /// <summary>
    /// Shows <paramref name="icon"/> alone on the button at <paramref name="size"/> pixels (ADR-204): its text becomes the label the status
    /// bar shows on hover (<see cref="StatusLabelMeta"/>; the tooltip when <see cref="LabelsOnStatusBar"/> is off), and the name smoke scripts
    /// press it by. With <paramref name="keepText"/> the icon stands small before the text instead. The text stays if the icon is missing.
    /// </summary>
    public static T WithIcon<T>(this T button, UiIcon icon, int size = WindowSize, bool keepText = false) where T : Button
    {
        if (Get(icon, keepText ? ButtonSize : size) is not { } texture)
        {
            return button;
        }

        button.Icon = texture;
        if (keepText)
        {
            return button;
        }

        button.IconAlignment = HorizontalAlignment.Center;
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
        var label = control.HasMeta(StatusLabelMeta) ? control.GetMeta(StatusLabelMeta).AsString() : "";
        var tip = control.TooltipText;
        return label.Length == 0 ? tip : tip.Length == 0 || tip == label ? label : $"{label}: {tip}";
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

    private static string FileName(UiIcon icon) => icon switch
    {
        UiIcon.Close => "close",
        UiIcon.Confirm => "confirm",
        UiIcon.ArrowLeft => "arrow_left",
        UiIcon.ArrowRight => "arrow_right",
        UiIcon.ArrowUp => "arrow_up",
        UiIcon.ArrowDown => "arrow_down",
        UiIcon.Plus => "plus",
        UiIcon.Minus => "minus",
        UiIcon.Ban => "ban",
        UiIcon.Settings => "settings",
        UiIcon.Hourglass => "hourglass",
        UiIcon.SpellBook => "spell_book",
        UiIcon.Spells => "spells",
        UiIcon.Quests => "quests",
        UiIcon.PuzzleMap => "puzzle_map",
        UiIcon.Dig => "dig",
        UiIcon.EndTurn => "end_turn",
        UiIcon.SwitchLevel => "switch_level",
        UiIcon.Kingdom => "kingdom",
        UiIcon.Marketplace => "marketplace",
        UiIcon.ThievesGuild => "thieves_guild",
        UiIcon.Defend => "defend",
        UiIcon.Warcry => "warcry",
        UiIcon.Retreat => "retreat",
        UiIcon.Surrender => "surrender",
        UiIcon.QuickCombat => "quick_combat",
        UiIcon.AutoCombat => "auto_combat",
        UiIcon.Info => "info",
        UiIcon.Undo => "undo",
        UiIcon.Redo => "redo",
        UiIcon.ZoomIn => "zoom_in",
        UiIcon.ZoomOut => "zoom_out",
        UiIcon.Save => "save",
        UiIcon.Load => "load",
        UiIcon.Lock => "lock",
        UiIcon.Unlock => "unlock",
        UiIcon.Menu => "menu",
        _ => "",
    };
}
