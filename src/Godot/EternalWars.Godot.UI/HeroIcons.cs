using System.Collections.Generic;
using EternalWars.Core.Domain.Heroes;
using Godot;

namespace EternalWars.Godot.UI;

/// <summary>A hero value with its own icon in <c>res://shared/textures/ui/stats/</c>.</summary>
public enum HeroStatIcon
{
    Attack,
    Defense,
    SpellPower,
    Knowledge,
    Mana,
    Experience,
}

/// <summary>
/// The hero's painted icons: one per skill in <c>res://shared/textures/ui/skills/</c> (named by the skill id, <c>offense.melee.png</c>) and one
/// per stat in <c>res://shared/textures/ui/stats/</c>. Scaled once per size and cached, like <see cref="UiIcons"/>; a missing file gives
/// null, and callers show the text alone (skills without art yet, such as the magic schools').
/// </summary>
public static class HeroIcons
{
    /// <summary>The icon size in a skill row of the hero screen.</summary>
    public const int SkillSize = 32;

    /// <summary>The icon size before a stat's value.</summary>
    public const int StatSize = 28;

    private const string SkillFolder = "res://shared/textures/ui/skills/";
    private const string StatFolder = "res://shared/textures/ui/stats/";
    private static readonly Dictionary<(string, int), Texture2D?> Cache = [];

    /// <summary>The icon of the skill <paramref name="skillId"/>, or null if it has none yet.</summary>
    public static Texture2D? Skill(string skillId, int size = SkillSize) => Load($"{SkillFolder}{skillId}.png", size);

    /// <summary>The icon of <paramref name="stat"/>, or null if its file is missing.</summary>
    public static Texture2D? Stat(HeroStatIcon stat, int size = StatSize) => Load($"{StatFolder}{FileName(stat)}.png", size);

    /// <summary>The icon of a primary stat.</summary>
    public static HeroStatIcon Of(PrimaryStat stat) => stat switch
    {
        PrimaryStat.Attack => HeroStatIcon.Attack,
        PrimaryStat.Defense => HeroStatIcon.Defense,
        PrimaryStat.SpellPower => HeroStatIcon.SpellPower,
        _ => HeroStatIcon.Knowledge,
    };

    /// <summary>
    /// <paramref name="value"/> after <paramref name="stat"/>'s icon, with <paramref name="name"/> as the tooltip; "name value" in text when
    /// the icon is missing.
    /// </summary>
    public static Control StatBadge(HeroStatIcon stat, string name, string value)
    {
        if (Stat(stat) is not { } texture)
        {
            return new Label { Text = $"{name} {value}" };
        }

        var row = new HBoxContainer { TooltipText = name, MouseFilter = Control.MouseFilterEnum.Pass };
        row.AddThemeConstantOverride("separation", UiSpacing.Cell);
        row.AddChild(new TextureRect
        {
            Texture = texture,
            MouseFilter = Control.MouseFilterEnum.Pass,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            CustomMinimumSize = new Vector2(StatSize, StatSize),
        });
        row.AddChild(new Label { Text = value, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Pass });
        return row;
    }

    /// <summary><paramref name="control"/> after the skill's icon in a row; the control alone if the skill has no icon.</summary>
    public static Control WithSkillIcon(string skillId, Control control, int size = SkillSize)
    {
        if (Skill(skillId, size) is not { } texture)
        {
            return control;
        }

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", UiSpacing.Gap);
        row.AddChild(new TextureRect
        {
            Texture = texture,
            StretchMode = TextureRect.StretchModeEnum.KeepCentered,
            CustomMinimumSize = new Vector2(size, size),
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        });
        row.AddChild(control);
        return row;
    }

    /// <summary>Shows the skill's icon on the button, before its text.</summary>
    public static T WithSkillIcon<T>(this T button, string skillId) where T : Button
    {
        if (Skill(skillId, UiIcons.ButtonSize + 6) is { } texture)
        {
            button.Icon = texture;
        }

        return button;
    }

    /// <summary>Shows the primary stat's icon on the button, before its text.</summary>
    public static T WithStatIcon<T>(this T button, PrimaryStat stat) where T : Button
    {
        if (Stat(Of(stat), UiIcons.ButtonSize + 6) is { } texture)
        {
            button.Icon = texture;
        }

        return button;
    }

    private static Texture2D? Load(string path, int size)
    {
        if (Cache.TryGetValue((path, size), out var cached))
        {
            return cached;
        }

        var texture = UiIcons.LoadScaled(path, size);
        Cache[(path, size)] = texture;
        return texture;
    }

    private static string FileName(HeroStatIcon stat) => stat switch
    {
        HeroStatIcon.Attack => "attack",
        HeroStatIcon.Defense => "defense",
        HeroStatIcon.SpellPower => "spell_power",
        HeroStatIcon.Knowledge => "knowledge",
        HeroStatIcon.Mana => "mana",
        HeroStatIcon.Experience => "experience",
        _ => "",
    };
}
