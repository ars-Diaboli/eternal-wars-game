using System.Collections.Generic;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The faction heraldry in <c>res://shared/textures/ui/heraldry/</c> (docs/content/icon-prompts.md, "Heraldry"): per town type
/// (<c>haven</c>, <c>swamp-bestiary</c>) and per group (<c>group_dawn-covenant</c>) a painted crest, <c>crest_&lt;id&gt;.png</c>, and its
/// figure alone, <c>charge_&lt;id&gt;.png</c>. Scaled once per size and cached, like <see cref="UiIcons"/>; a missing file gives null, and
/// callers keep the faction's name alone.
/// </summary>
public static class Heraldry
{
    /// <summary>A crest's size before a name in a row or list.</summary>
    public const int RowSize = 24;

    /// <summary>A crest's size beside a screen's or a card's title.</summary>
    public const int TitleSize = 48;

    private const string Folder = "res://shared/textures/ui/heraldry/";
    private static readonly Dictionary<(string, int), Texture2D?> Cache = [];

    /// <summary>The crest of the town type or group <paramref name="id"/> at <paramref name="size"/> pixels, or null.</summary>
    public static Texture2D? Crest(string? id, int size = RowSize) => Load("crest_", id, size);

    /// <summary>The charge (the crest's figure alone) of <paramref name="id"/> at <paramref name="size"/> pixels, or null.</summary>
    public static Texture2D? Charge(string? id, int size = RowSize) => Load("charge_", id, size);

    /// <summary>
    /// The crest of <paramref name="townType"/> as a control, with the faction's name as its tooltip; a label with the name when the crest is
    /// missing.
    /// </summary>
    public static Control Badge(string townType, int size = RowSize)
    {
        if (Crest(townType, size) is not { } texture)
        {
            return new Label { Text = townType };
        }

        return new TextureRect
        {
            Texture = texture,
            TooltipText = townType,
            MouseFilter = Control.MouseFilterEnum.Pass,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            CustomMinimumSize = new Vector2(size, size),
        };
    }

    /// <summary>The crest of <paramref name="townType"/> before <paramref name="text"/>, for a title or a line; just the label without the crest.</summary>
    public static Control Titled(string townType, string text, int size = RowSize, Color? color = null)
    {
        var label = new Label { Text = text, VerticalAlignment = VerticalAlignment.Center };
        if (color is { } font)
        {
            label.AddThemeColorOverride("font_color", font);
        }

        if (Crest(townType, size) is null)
        {
            return label;
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiSpacing.Gap);
        row.AddChild(Badge(townType, size));
        row.AddChild(label);
        return row;
    }

    /// <summary>Shows <paramref name="townType"/>'s crest on the button, before its text.</summary>
    public static T WithCrest<T>(this T button, string townType, int size = RowSize) where T : Button
    {
        if (Crest(townType, size) is { } texture)
        {
            button.Icon = texture;
        }

        return button;
    }

    /// <summary>Adds <paramref name="townType"/> to the choice with its crest.</summary>
    public static void AddFaction(this OptionButton choice, string townType, string? text = null)
    {
        if (Crest(townType, UiIcons.ButtonSize) is { } texture)
        {
            choice.AddIconItem(texture, text ?? townType);
        }
        else
        {
            choice.AddItem(text ?? townType);
        }
    }

    private static Texture2D? Load(string prefix, string? id, int size)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var file = prefix + id;
        if (Cache.TryGetValue((file, size), out var cached))
        {
            return cached;
        }

        var texture = UiIcons.LoadScaled($"{Folder}{file}.png", size);
        Cache[(file, size)] = texture;
        return texture;
    }
}
