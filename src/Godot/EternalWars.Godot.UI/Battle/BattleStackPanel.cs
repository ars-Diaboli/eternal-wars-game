using System.Globalization;
using EternalWars.Core.Domain.Battles;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// The battle HUD's stack panel (ADR-314), bottom left in a bevelled panel rimmed in the stack's side's colour: the stack's portrait, its name
/// and count, then its numbers as the battle uses them now (<see cref="StackPanel"/>): Attack, Defense, damage, the top creature's health,
/// speed, initiative, shots for a shooter, morale and luck, each an icon (its name while the icon sheet is missing) with the number, named on
/// the status bar; and its marks (defending, native terrain, the spells on it) as small icons, the enemy's spells tinted red, each named on
/// the status bar. A war machine shows the numbers it has. The narrow form (<see cref="Compact"/>) keeps Attack, Defense and health. Redrawn only when what it shows changes.
/// </summary>
public partial class BattleStackPanel : PanelContainer
{
    /// <summary>The panel's width.</summary>
    public const int Width = 340;

    /// <summary>The panel's width in the narrow form (<see cref="Compact"/>).</summary>
    public const int CompactWidth = 240;

    /// <summary>The portrait's side.</summary>
    public const int PortraitSize = 72;

    private const int MarkSize = 20;

    private static readonly Color HostileTint = new(1f, 0.55f, 0.5f);

    private readonly TextureRect _portrait = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        CustomMinimumSize = new Vector2(PortraitSize, PortraitSize),
        MouseFilter = MouseFilterEnum.Ignore,
    };

    private readonly Label _name = new() { ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, MouseFilter = MouseFilterEnum.Ignore };
    private readonly GridContainer _numbers = new() { Columns = 4, MouseFilter = MouseFilterEnum.Ignore };
    private readonly HFlowContainer _marks = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly HBoxContainer _content = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly BevelFrame _frame;
    private string? _shown;

    public BattleStackPanel()
    {
        Name = "StackPanel";
        MouseFilter = MouseFilterEnum.Ignore;
        CustomMinimumSize = new Vector2(Width, 0);
        _frame = UiFrames.Bevel(this, BevelKind.Panel, padding: UiSpacing.Padding);
        _content.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(_content);

        var portrait = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkBegin };
        UiFrames.Bevel(portrait, BevelKind.Slot, padding: 2);
        portrait.AddChild(_portrait);
        _content.AddChild(portrait);

        var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", UiSpacing.Cell);
        _name.AddThemeFontSizeOverride("font_size", 17);
        lines.AddChild(_name);
        _numbers.AddThemeConstantOverride("h_separation", UiSpacing.Gap);
        _numbers.AddThemeConstantOverride("v_separation", 2);
        lines.AddChild(_numbers);
        _marks.AddThemeConstantOverride("h_separation", UiSpacing.Cell);
        lines.AddChild(_marks);
        _content.AddChild(lines);
    }

    /// <summary>
    /// The narrow form (ADR-314): the portrait, the name and count, Attack, Defense and health only, without the marks, in a narrower panel.
    /// </summary>
    public bool Compact
    {
        get => _compact;
        set
        {
            if (_compact == value)
            {
                return;
            }

            _compact = value;
            _shown = null; // redrawn at the next show
            CustomMinimumSize = new Vector2(value ? CompactWidth : Width, 0);
            _numbers.Columns = value ? 3 : 4;
        }
    }

    private bool _compact;

    /// <summary>The name line shown ("Swordsmen ×7"), empty while the panel is blank: for the smoke checks.</summary>
    public string Title => _content.Visible ? _name.Text : "";

    /// <summary>Shows <paramref name="panel"/> (null: blank), the portrait <paramref name="portrait"/>, rimmed in <paramref name="side"/>'s colour.</summary>
    public void Show(StackPanel? panel, Texture2D? portrait, Color side)
    {
        var key = panel is null ? null : Key(panel);
        if (key is not null && key == _shown)
        {
            return;
        }

        _shown = key;
        _content.Visible = panel is not null;
        _numbers.ClearChildren(detach: true);
        _marks.ClearChildren(detach: true);
        if (panel is null)
        {
            _frame.Tint(null);
            return;
        }

        _frame.Tint(side, 0.55f);
        _portrait.Texture = portrait;
        _portrait.FlipH = panel.Side == BattleSide.Defender; // facing its enemy, as on the timeline
        _name.Text = panel.IsMachine ? panel.Name : $"{panel.Name} ×{panel.Count}";

        Number(HeroStatIcon.Attack, "Att", "Attack", panel.Attack);
        Number(HeroStatIcon.Defense, "Def", "Defense", panel.Defense);
        if (_compact)
        {
            Number(null, "HP", "Health of the top creature", $"{Text(panel.Health)}/{Text(panel.MaxHealth)}");
            _marks.Visible = false;
            return;
        }

        if (panel.MinDamage is { } low)
        {
            Number(null, "Dmg", "Damage", low == panel.MaxDamage ? Text(low) : $"{Text(low)}–{Text(panel.MaxDamage ?? low)}");
        }

        Number(null, "HP", "Health of the top creature", $"{Text(panel.Health)}/{Text(panel.MaxHealth)}");
        Number(null, "Spd", "Speed", panel.Speed);
        Number(null, "Ini", "Initiative", panel.Initiative);
        if (panel.UnlimitedShots)
        {
            Number(null, "Shots", "Shots: unlimited (an ammo cart)", "∞");
        }
        else
        {
            Number(null, "Shots", "Shots left", panel.Shots);
        }

        if (!panel.IsMachine)
        {
            Number(HeroStatIcon.Morale, "Mor", panel.Morale is null ? "Morale: none (the creature has none)" : "Morale", panel.Morale is { } morale ? Signed(morale) : "–");
            Number(HeroStatIcon.Luck, "Luck", "Luck", panel.Luck is { } luck ? Signed(luck) : null);
        }

        foreach (var mark in panel.Marks)
        {
            _marks.AddChild(Mark(mark));
        }

        _marks.Visible = panel.Marks.Count > 0;
    }

    /// <summary>What the panel shows, to skip a redraw that would change nothing.</summary>
    private static string Key(StackPanel panel) =>
        string.Join('|', panel with { Marks = [] }, string.Join(';', panel.Marks.Select(m => $"{m.Kind}:{m.Name}:{m.Friendly}")));

    private void Number(HeroStatIcon? icon, string shortName, string name, int? value)
    {
        if (value is { } shown)
        {
            Number(icon, shortName, name, Text(shown));
        }
    }

    private void Number(HeroStatIcon? icon, string shortName, string name, string? value)
    {
        if (value is null)
        {
            return;
        }

        var badge = new HBoxContainer { TooltipText = name, MouseFilter = MouseFilterEnum.Pass };
        badge.SetMeta(UiIcons.StatusLabelMeta, name);
        badge.AddThemeConstantOverride("separation", 2);
        if (icon is { } stat && HeroIcons.Stat(stat, MarkSize) is { } texture)
        {
            badge.AddChild(new TextureRect
            {
                Texture = texture, StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(MarkSize, MarkSize),
                MouseFilter = MouseFilterEnum.Pass,
            });
        }
        else
        {
            var label = new Label { Text = shortName, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Pass };
            label.AddThemeColorOverride("font_color", UiTheme.TextQuiet);
            badge.AddChild(label);
        }

        badge.AddChild(new Label { Text = value, VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Pass });
        _numbers.AddChild(badge);
    }

    private static Control Mark(StackMark mark)
    {
        var texture = mark.Kind switch
        {
            StackMarkKind.Defending => UiIcons.Get(UiIcon.Defend, MarkSize),
            StackMarkKind.NativeTerrain => UiIcons.Get(UiIcon.Favourite, MarkSize),
            _ => (mark.Spell is { } spell ? SpellIcons.Get(spell, MarkSize) : null) ?? UiIcons.Get(UiIcon.Spells, MarkSize),
        };
        var name = mark.Friendly ? mark.Name : $"{mark.Name} (the enemy's spell)";
        Control shown = texture is null
            ? new Label { Text = mark.Name.Split(':')[0] }
            : new TextureRect { Texture = texture, StretchMode = TextureRect.StretchModeEnum.KeepCentered, CustomMinimumSize = new Vector2(MarkSize, MarkSize) };
        shown.MouseFilter = MouseFilterEnum.Pass;
        shown.TooltipText = name;
        shown.SetMeta(UiIcons.StatusLabelMeta, mark.Name.Split(':')[0]);
        if (!mark.Friendly)
        {
            shown.Modulate = HostileTint;
        }

        return shown;
    }

    private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Signed(int value) => value > 0 ? $"+{Text(value)}" : Text(value);
}
