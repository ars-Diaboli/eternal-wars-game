using System.Collections.Generic;
using System.Globalization;
using EternalWars.Core.Domain.Economy;
using EternalWars.Godot.Shared.Controls;
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

    /// <summary>
    /// A cost or an income as one row: each resource's icon followed by its amount ("120 gold" in text when the icon is missing), "free" when
    /// nothing is due, then <paramref name="suffix"/> (" each", " a day") if given.
    /// </summary>
    public static HBoxContainer CostRow(ResourceSet cost, string suffix = "", int size = TextSize)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", UiSpacing.Cell);
        var any = false;
        foreach (var resource in Enum.GetValues<ResourceType>())
        {
            if (cost[resource] == 0)
            {
                continue;
            }

            any = true;
            var amount = cost[resource].ToString(CultureInfo.InvariantCulture);
            if (Get(resource, size) is { } texture)
            {
                row.AddChild(new TextureRect
                {
                    Texture = texture,
                    TooltipText = resource.ToString(),
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                    StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                    CustomMinimumSize = new Vector2(size, size),
                });
                row.AddChild(new Label { Text = amount, MouseFilter = Control.MouseFilterEnum.Ignore, VerticalAlignment = VerticalAlignment.Center });
            }
            else
            {
                row.AddChild(new Label { Text = $"{amount} {resource.ToString().ToLowerInvariant()}", MouseFilter = Control.MouseFilterEnum.Ignore });
            }
        }

        if (!any || suffix.Length > 0)
        {
            // The row's separation already spaces the suffix from the last amount.
            row.AddChild(new Label { Text = any ? suffix.TrimStart() : "free" + suffix, MouseFilter = Control.MouseFilterEnum.Ignore });
        }

        return row;
    }

    /// <summary>
    /// The player's gold on hand, as opposed to a price: the pile of gold followed by the amount ("gold 11500" in text when the icon is
    /// missing), with "Your gold" as its tooltip.
    /// </summary>
    public static HBoxContainer Balance(int gold, int size = TextSize)
    {
        var row = new HBoxContainer { TooltipText = "Your gold" };
        row.AddThemeConstantOverride("separation", UiSpacing.Cell);
        var amount = gold.ToString(CultureInfo.InvariantCulture);
        if (Treasury(size) is { } texture)
        {
            row.AddChild(new TextureRect
            {
                Texture = texture,
                MouseFilter = Control.MouseFilterEnum.Pass,
                StretchMode = TextureRect.StretchModeEnum.KeepCentered,
                CustomMinimumSize = new Vector2(size, size),
            });
            row.AddChild(new Label { Text = amount, VerticalAlignment = VerticalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Pass });
        }
        else
        {
            row.AddChild(new Label { Text = $"gold {amount}", MouseFilter = Control.MouseFilterEnum.Pass });
        }

        return row;
    }

    /// <summary>A cost in words, for tooltips and other plain text: "2500 gold, 5 ore", or "free".</summary>
    public static string Describe(ResourceSet cost)
    {
        var parts = Enum.GetValues<ResourceType>().Where(t => cost[t] != 0)
            .Select(t => $"{cost[t].ToString(CultureInfo.InvariantCulture)} {t.ToString().ToLowerInvariant()}").ToList();
        return parts.Count == 0 ? "free" : string.Join(", ", parts);
    }

    /// <summary>
    /// <paramref name="before"/>, the <see cref="CostRow"/> of <paramref name="cost"/> and <paramref name="after"/> on one line, for a sentence
    /// that names a price ("Each for Orrin: [gold] 55").
    /// </summary>
    public static HBoxContainer CostLine(string before, ResourceSet cost, string after = "")
    {
        var line = CostRow(cost, after);
        if (before.Length > 0)
        {
            var label = new Label { Text = before };
            line.AddChild(label);
            line.MoveChild(label, 0);
        }

        return line;
    }

    /// <summary>
    /// Moves the button's text into lines above a <see cref="CostRow"/> of <paramref name="cost"/>, centred inside the button, which grows to
    /// fit them. Call after setting <see cref="BaseButton.Disabled"/>: a disabled button's content is dimmed. Scripted input still names the
    /// button by its text with the cost in words on the last line (<see cref="AutoInput.LabelMeta"/>).
    /// </summary>
    public static T WithCost<T>(this T button, ResourceSet cost, string suffix = "") where T : Button
    {
        button.SetMeta(AutoInput.LabelMeta, (button.Text.Length > 0 ? button.Text + "\n" : "") + Describe(cost) + suffix);
        var content = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        content.AddThemeConstantOverride("separation", 0);
        if (button.Text.Length > 0)
        {
            content.AddChild(new Label { Text = button.Text, HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = Control.MouseFilterEnum.Ignore });
        }

        var row = CostRow(cost, suffix);
        row.Alignment = BoxContainer.AlignmentMode.Center;
        content.AddChild(row);
        if (button.Disabled)
        {
            content.Modulate = new Color(1f, 1f, 1f, 0.5f);
        }

        button.Text = "";
        var frame = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        foreach (var side in new[] { "left", "right" })
        {
            frame.AddThemeConstantOverride($"margin_{side}", UiSpacing.Gap);
        }

        foreach (var side in new[] { "top", "bottom" })
        {
            frame.AddThemeConstantOverride($"margin_{side}", UiSpacing.Cell);
        }

        frame.AddChild(content);
        button.AddChild(frame);
        frame.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        void Fit() => button.CustomMinimumSize = button.CustomMinimumSize.Max(frame.GetCombinedMinimumSize());
        frame.MinimumSizeChanged += Fit;
        Fit();
        return button;
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
