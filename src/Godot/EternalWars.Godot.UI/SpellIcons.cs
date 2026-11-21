using System.Collections.Generic;
using EternalWars.Core.Domain.Common;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The spells' painted icons in <c>res://shared/textures/ui/spells/</c>, named by the spell id (<c>light.bless.png</c>; spell-icon-prompts.md):
/// on the spell buttons of the spell book, the hero screen and the battle, and in the editor's spell pickers. Scaled once per size and cached,
/// like <see cref="UiIcons"/>; a spell without a picture yet keeps its text alone.
/// </summary>
public static class SpellIcons
{
    /// <summary>The icon size before a spell button's text.</summary>
    public const int ButtonSize = 28;

    private const string Folder = "res://shared/textures/ui/spells/";
    private static readonly Dictionary<(string, int), Texture2D?> Cache = [];

    /// <summary>The icon of <paramref name="spell"/> at <paramref name="size"/> pixels, or null if it has none yet.</summary>
    public static Texture2D? Get(SpellId spell, int size = ButtonSize)
    {
        if (!Cache.TryGetValue((spell.Value, size), out var texture))
        {
            texture = UiIcons.LoadScaled($"{Folder}{spell.Value}.png", size);
            Cache[(spell.Value, size)] = texture;
        }

        return texture;
    }

    /// <summary>Shows the icon of <paramref name="spell"/> on the button, before its text; the button is unchanged when the spell has none.</summary>
    public static T WithSpellIcon<T>(this T button, SpellId spell) where T : Button
    {
        if (Get(spell) is { } texture)
        {
            button.Icon = texture;
            button.AddThemeConstantOverride("icon_max_width", ButtonSize);
        }

        return button;
    }
}
