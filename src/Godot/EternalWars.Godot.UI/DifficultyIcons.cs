using System.Collections.Generic;
using EternalWars.Core.Domain.Game;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>
/// The difficulty levels' chess pieces (ADR-225; icon-prompts.md sheet 29: <c>difficulty_easy.png</c> … <c>difficulty_master.png</c> in the UI
/// icons): on the new-game screen's difficulty row, the victory screen and the leaderboard. A missing picture leaves the level's name.
/// </summary>
public static class DifficultyIcons
{
    private const string Folder = "res://shared/textures/ui/icons/";
    private static readonly Dictionary<(Difficulty, int), Texture2D?> Cache = [];

    /// <summary>The level's piece at <paramref name="size"/> pixels, or null when its file is missing.</summary>
    public static Texture2D? Get(Difficulty level, int size = UiIcons.WindowSize)
    {
        if (!Cache.TryGetValue((level, size), out var texture))
        {
            texture = UiIcons.LoadScaled($"{Folder}difficulty_{level.ToString().ToLowerInvariant()}.png", size);
            Cache[(level, size)] = texture;
        }

        return texture;
    }

    /// <summary>The level's piece before its name (the leaderboard's cell), in <paramref name="color"/>; just the name when the piece is missing.</summary>
    public static Control Titled(Difficulty level, Color? color = null)
    {
        var label = new Label { Text = level.ToString(), VerticalAlignment = VerticalAlignment.Center, TooltipText = DifficultyText.Describe(level) };
        label.MouseFilter = Control.MouseFilterEnum.Pass;
        if (color is { } font)
        {
            label.AddThemeColorOverride("font_color", font);
        }

        if (Get(level, UiIcons.StatusBarSize) is null)
        {
            return label;
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiSpacing.Gap);
        row.AddChild(Badge(level));
        row.AddChild(label);
        return row;
    }

    /// <summary>The level's piece with its description as the tooltip (and so on the status bar); the level's name when the piece is missing.</summary>
    public static Control Badge(Difficulty level, int size = UiIcons.StatusBarSize)
    {
        var tip = DifficultyText.Describe(level);
        if (Get(level, size) is not { } texture)
        {
            return new Label { Text = level.ToString(), TooltipText = tip, MouseFilter = Control.MouseFilterEnum.Pass };
        }

        return new TextureRect
        {
            Texture = texture,
            TooltipText = tip,
            MouseFilter = Control.MouseFilterEnum.Pass,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            CustomMinimumSize = new Vector2(size, size),
        };
    }
}
