using System.Globalization;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// A row of creature portraits with their counts in the corner (ADR-314): a side's losses on the result and auto-combat windows, the undead
/// Necromancy raised. Each portrait is named on the status bar; "nothing" when the row is empty.
/// </summary>
public static class BattleLossRow
{
    /// <summary>A portrait's side.</summary>
    public const int PortraitSize = 48;

    /// <summary>
    /// <paramref name="title"/> over the portraits of <paramref name="creatures"/> (creature id, name, count), in a column; the creature's name
    /// stands in for a missing portrait.
    /// </summary>
    public static Control Of(string title, IEnumerable<(string Id, string Name, int Count)> creatures)
    {
        var box = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", UiSpacing.Cell);
        box.AddChild(new Label { Text = title });
        var row = new HFlowContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("h_separation", UiSpacing.Cell);
        row.AddThemeConstantOverride("v_separation", UiSpacing.Cell);
        foreach (var (id, name, count) in creatures)
        {
            row.AddChild(Portrait(id, name, count));
        }

        if (row.GetChildCount() == 0)
        {
            var none = new Label { Text = "nothing" };
            none.AddThemeColorOverride("font_color", UiTheme.TextQuiet);
            row.AddChild(none);
        }

        box.AddChild(row);
        return box;
    }

    private static Control Portrait(string id, string name, int count)
    {
        var slot = new PanelContainer { CustomMinimumSize = new Vector2(PortraitSize, PortraitSize), MouseFilter = Control.MouseFilterEnum.Pass };
        slot.AddThemeStyleboxOverride("panel", UiFrames.Themed(BevelKind.Slot, 2));
        slot.SetMeta(UiIcons.StatusLabelMeta, $"{name} ×{count.ToString(CultureInfo.InvariantCulture)}");
        var portrait = CreatureIcons.Get(id, PortraitSize);
        if (portrait is not null)
        {
            slot.AddChild(new TextureRect
            {
                Texture = portrait, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = Control.MouseFilterEnum.Ignore,
            });
        }

        var label = new Label
        {
            Text = portrait is null ? $"{name}\n{count.ToString(CultureInfo.InvariantCulture)}" : count.ToString(CultureInfo.InvariantCulture),
            HorizontalAlignment = portrait is null ? HorizontalAlignment.Center : HorizontalAlignment.Right,
            VerticalAlignment = portrait is null ? VerticalAlignment.Center : VerticalAlignment.Bottom,
            ClipText = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeConstantOverride("outline_size", 4);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        slot.AddChild(label);
        return slot;
    }
}
