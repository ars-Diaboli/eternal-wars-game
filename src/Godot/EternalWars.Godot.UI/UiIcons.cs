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

    /// <summary>
    /// Shows <paramref name="icon"/> on the button, before its text. With <paramref name="iconOnly"/> the text goes (into the
    /// tooltip when it has none), so glyph buttons such as "&lt;" become the icon; the text stays if the icon is missing.
    /// </summary>
    public static T WithIcon<T>(this T button, UiIcon icon, bool iconOnly = false) where T : Button
    {
        if (Get(icon) is not { } texture)
        {
            return button;
        }

        button.Icon = texture;
        if (iconOnly)
        {
            if (string.IsNullOrEmpty(button.TooltipText))
            {
                button.TooltipText = button.Text;
            }

            button.Text = "";
        }

        return button;
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
        _ => "",
    };
}
