using System.Globalization;
using EternalWars.Core.Domain.Battles;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// The battle HUD's stack panel (ADR-314), bottom left in a bevelled panel rimmed in the stack's side's colour, as wide as the adventure HUD's
/// hero card (<see cref="SelectedHeroCard.Width"/>): the stack's portrait, a square as tall as the panel like the hero card's; the name and
/// count, its marks at the right of that line (defending, native terrain, the spells on it, the enemy's tinted red); then its numbers as the
/// battle uses them now (<see cref="StackPanel"/>) in two rows of matching columns: damage, Attack, speed, morale and, for a shooter, shots;
/// the top creature's health, Defense, initiative, luck and, for a creature caster, its casts left. Each number is an icon
/// (<see cref="HeroStatIcon"/>, <see cref="StackStatIcon"/>; its short name while the file is missing) with its value, each number and mark
/// named on the status bar. A war machine shows the numbers it has. The narrow form (<see cref="Compact"/>) keeps damage, Attack, health and
/// Defense, without the marks. Redrawn only when what it shows changes.
/// </summary>
public partial class BattleStackPanel : PanelContainer
{
    /// <summary>The panel's width: the adventure HUD's hero card's.</summary>
    public const int Width = SelectedHeroCard.Width;

    /// <summary>The panel's width in the narrow form (<see cref="Compact"/>).</summary>
    public const int CompactWidth = 240;

    /// <summary>The size the portrait is loaded at (it is shown as tall as the panel).</summary>
    public const int PortraitSize = SelectedHeroCard.PortraitSize;

    private const int MarkSize = 20;

    private static readonly Color HostileTint = new(1f, 0.55f, 0.5f);

    private readonly TextureRect _portrait = new()
    {
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
        StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = MouseFilterEnum.Ignore,
    };

    private readonly PanelContainer _portraitSlot = new() { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.Fill };
    private readonly Label _name = new()
    {
        ClipText = true, TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis, SizeFlagsHorizontal = SizeFlags.ExpandFill,
        MouseFilter = MouseFilterEnum.Ignore,
    };

    private readonly GridContainer _numbers = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly HBoxContainer _marks = new() { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkCenter };
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

        UiFrames.Bevel(_portraitSlot, BevelKind.Slot, padding: 2);
        _portraitSlot.AddChild(_portrait);
        _content.AddChild(_portraitSlot);
        _content.Resized += () => _portraitSlot.CustomMinimumSize = new Vector2(_content.Size.Y, _content.Size.Y); // a square as tall as the panel

        var lines = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", UiSpacing.Cell);
        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", UiSpacing.Gap);
        _name.AddThemeFontSizeOverride("font_size", 17);
        header.AddChild(_name);
        _marks.AddThemeConstantOverride("separation", UiSpacing.Cell);
        header.AddChild(_marks);
        lines.AddChild(header);
        _numbers.AddThemeConstantOverride("h_separation", UiSpacing.Gap);
        _numbers.AddThemeConstantOverride("v_separation", 2);
        lines.AddChild(_numbers);
        _content.AddChild(lines);
    }

    /// <summary>The narrow form (ADR-314): the portrait, the name and count, damage, Attack, health and Defense, without the marks, in a narrower panel.</summary>
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

        var damage = panel.MinDamage is { } low
            ? Badge(StackStatIcon.Damage, "Dmg", "Damage", low == panel.MaxDamage ? Text(low) : $"{Text(low)}–{Text(panel.MaxDamage ?? low)}")
            : null;
        var attack = Badge(HeroStatIcon.Attack, "Att", "Attack", panel.Attack);
        var health = Badge(StackStatIcon.Health, "HP", "Health of the top creature", $"{Text(panel.Health)}/{Text(panel.MaxHealth)}");
        var defense = Badge(HeroStatIcon.Defense, "Def", "Defense", panel.Defense);
        if (_compact)
        {
            Rows([damage, attack], [health, defense]);
            _marks.Visible = false;
            return;
        }

        var shots = panel.UnlimitedShots
            ? Badge(StackStatIcon.Shots, "Shots", "Shots: unlimited (an ammo cart)", "∞")
            : Badge(StackStatIcon.Shots, "Shots", "Shots left", panel.Shots);
        var morale = panel.IsMachine ? null
            : Badge(HeroStatIcon.Morale, "Mor", panel.Morale is null ? "Morale: none (the creature has none)" : "Morale", panel.Morale is { } m ? Signed(m) : "–");
        var luck = panel.IsMachine ? null : Badge(HeroStatIcon.Luck, "Luck", "Luck", panel.Luck is { } l ? Signed(l) : null);
        Rows(
            [damage, attack, Badge(StackStatIcon.Speed, "Spd", "Speed", panel.Speed), morale, shots],
            [health, defense, Badge(StackStatIcon.Initiative, "Ini", "Initiative", panel.Initiative), luck,
                Badge(HeroStatIcon.Mana, "Casts", "Spell casts left this battle", panel.Casts)]);

        foreach (var mark in panel.Marks)
        {
            _marks.AddChild(Mark(mark));
        }

        _marks.Visible = panel.Marks.Count > 0;
    }

    /// <summary>
    /// The two rows of numbers in matching columns: a column empty in both rows is left out, one empty in one row keeps its place.
    /// </summary>
    private void Rows(Control?[] top, Control?[] bottom)
    {
        var columns = Enumerable.Range(0, top.Length).Where(i => top[i] is not null || bottom[i] is not null).ToList();
        _numbers.Columns = Math.Max(1, columns.Count);
        foreach (var row in new[] { top, bottom })
        {
            foreach (var i in columns)
            {
                _numbers.AddChild(row[i] ?? new Control { MouseFilter = MouseFilterEnum.Ignore });
            }
        }
    }

    /// <summary>What the panel shows, to skip a redraw that would change nothing.</summary>
    private static string Key(StackPanel panel) =>
        string.Join('|', panel with { Marks = [] }, string.Join(';', panel.Marks.Select(m => $"{m.Kind}:{m.Name}:{m.Friendly}")));

    private static Control? Badge(HeroStatIcon icon, string shortName, string name, int? value) =>
        value is { } shown ? Badge(HeroIcons.Stat(icon, MarkSize), shortName, name, Text(shown)) : null;

    private static Control? Badge(HeroStatIcon icon, string shortName, string name, string? value) =>
        value is null ? null : Badge(HeroIcons.Stat(icon, MarkSize), shortName, name, value);

    private static Control? Badge(StackStatIcon icon, string shortName, string name, int? value) =>
        value is { } shown ? Badge(HeroIcons.Stat(icon, MarkSize), shortName, name, Text(shown)) : null;

    private static Control? Badge(StackStatIcon icon, string shortName, string name, string? value) =>
        value is null ? null : Badge(HeroIcons.Stat(icon, MarkSize), shortName, name, value);

    /// <summary>A number's badge: its icon (else its short name, quiet) and its value, named on the status bar.</summary>
    private static Control Badge(Texture2D? icon, string shortName, string name, string value)
    {
        var badge = new HBoxContainer { TooltipText = name, MouseFilter = MouseFilterEnum.Pass };
        badge.SetMeta(UiIcons.StatusLabelMeta, name);
        badge.AddThemeConstantOverride("separation", 2);
        if (icon is { } texture)
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
        return badge;
    }

    private static Control Mark(StackMark mark)
    {
        var texture = mark.Kind switch
        {
            StackMarkKind.Defending => UiIcons.Get(UiIcon.Defend, MarkSize),
            StackMarkKind.NativeTerrain => HeroIcons.Stat(StackStatIcon.NativeTerrain, MarkSize) ?? UiIcons.Get(UiIcon.Favourite, MarkSize),
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
