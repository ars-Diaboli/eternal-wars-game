using System.Collections.Generic;
using EternalWars.Core.Domain.Economy;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The resource icons in <c>res://shared/textures/ui/resources/</c>: one per <see cref="ResourceType"/>, plus a pile of gold for the
/// treasury (the Kingdom overview). The 256 px art is scaled down once per size and cached, like <see cref="UiIcons"/>; a missing file
/// gives null, and callers fall back to the resource's name.
/// </summary>
public static class ResourceIcons
{
    /// <summary>The icon size next to an amount in a line of text.</summary>
    public const int TextSize = 24;

    private const string Folder = "res://shared/textures/ui/resources/";
    private const string TreasuryFile = "gold_pile";
    private static readonly Dictionary<(string, int), Texture2D?> Cache = [];

    /// <summary>The icon of <paramref name="resource"/> at <paramref name="size"/> pixels, or null if its file is missing.</summary>
    public static Texture2D? Get(ResourceType resource, int size = TextSize) => Load(resource.ToString().ToLowerInvariant(), size);

    /// <summary>The pile of gold: the kingdom's treasury and income.</summary>
    public static Texture2D? Treasury(int size = UiIcons.ButtonSize) => Load(TreasuryFile, size);

    /// <summary>
    /// The icon of <paramref name="resource"/> as a control for rows and tables, with the resource's name as its tooltip; a label with the
    /// name when the icon is missing.
    /// </summary>
    public static Control Badge(ResourceType resource, int size = TextSize)
    {
        if (Get(resource, size) is not { } texture)
        {
            return new Label { Text = resource.ToString() };
        }

        return new TextureRect
        {
            Texture = texture,
            TooltipText = resource.ToString(),
            MouseFilter = Control.MouseFilterEnum.Pass,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            CustomMinimumSize = new Vector2(size, size),
        };
    }

    /// <summary>Shows <paramref name="resource"/>'s icon on the button, before its text.</summary>
    public static T WithResourceIcon<T>(this T button, ResourceType resource) where T : Button
    {
        if (Get(resource, UiIcons.ButtonSize) is { } texture)
        {
            button.Icon = texture;
        }

        return button;
    }

    /// <summary>Shows the pile of gold on the button, before its text.</summary>
    public static T WithTreasuryIcon<T>(this T button) where T : Button
    {
        if (Treasury() is { } texture)
        {
            button.Icon = texture;
        }

        return button;
    }

    private static Texture2D? Load(string file, int size)
    {
        if (Cache.TryGetValue((file, size), out var cached))
        {
            return cached;
        }

        var texture = UiIcons.LoadScaled($"{Folder}{file}.png", size);
        Cache[(file, size)] = texture;
        return texture;
    }
}
