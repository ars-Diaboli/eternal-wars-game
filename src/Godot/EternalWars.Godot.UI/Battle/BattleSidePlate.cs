using System.Globalization;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Cursors;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// A side's plate in the battle HUD (ADR-314), the attacker's top left and the defender's top right, mirrored: the hero's portrait rimmed in the
/// side's colour (a side without a hero shows its strongest stack), the side's name, and for a side with a caster its mana as a thin blue bar
/// and "14 / 24". A click on the portrait opens the hero's information window (<see cref="PortraitPressed"/>). The spell book's button stands
/// beside the plate of the side the book is for (<see cref="ShowBook"/>).
/// </summary>
public partial class BattleSidePlate : PanelContainer
{
    /// <summary>The portrait's side.</summary>
    public const int PortraitSize = 56;

    private const int BookSize = 32;

    private readonly Button _portrait = new() { Text = "Hero", FocusMode = FocusModeEnum.None, ExpandIcon = true };
    private readonly Label _name = new() { VerticalAlignment = VerticalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
    private readonly GaugeBar _mana = new() { Colour = GaugeBar.Mana, Upright = false, CustomMinimumSize = new Vector2(120, 6), MouseFilter = MouseFilterEnum.Pass };
    private readonly Label _manaText = new() { MouseFilter = MouseFilterEnum.Pass };
    private readonly HBoxContainer _manaRow = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Button _book = new Button { Text = BattleHotkeys.Of(BattleHotkeyAction.SpellBook).Hint, FocusMode = FocusModeEnum.None, Visible = false }
        .WithIcon(UiIcon.SpellBook, BookSize);

    private readonly BevelFrame _rim;

    public BattleSidePlate(BattleSide side)
    {
        Side = side;
        Name = $"{side}Plate";
        MouseFilter = MouseFilterEnum.Ignore;
        UiFrames.Bevel(this, BevelKind.Panel, padding: UiSpacing.Padding);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", UiSpacing.Gap);
        AddChild(row);

        _portrait.WithIcon(HeroIcons.PortraitOrPlaceholder(null, "?", PortraitSize), PortraitSize);
        _portrait.ExpandIcon = true;
        _portrait.CustomMinimumSize = new Vector2(PortraitSize, PortraitSize);
        _rim = UiFrames.Bevel(_portrait, BevelKind.Slot, padding: 2);
        _portrait.Pressed += () => PortraitPressed?.Invoke();

        var lines = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
        lines.AddThemeConstantOverride("separation", UiSpacing.Cell);
        _name.AddThemeFontSizeOverride("font_size", 17);
        _name.HorizontalAlignment = side == BattleSide.Attacker ? HorizontalAlignment.Left : HorizontalAlignment.Right;
        lines.AddChild(_name);
        _manaRow.AddThemeConstantOverride("separation", UiSpacing.Gap);
        _mana.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _manaRow.Alignment = side == BattleSide.Attacker ? BoxContainer.AlignmentMode.Begin : BoxContainer.AlignmentMode.End;
        _manaRow.AddChild(_mana);
        _manaRow.AddChild(_manaText);
        lines.AddChild(_manaRow);

        var parts = side == BattleSide.Attacker ? new Control[] { _portrait, lines, _book } : [_book, lines, _portrait]; // mirrored: the portrait at the edge
        UiFrames.Bevel(_book, BevelKind.Button, padding: UiIcons.IconPadding);
        _book.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _book.Pressed += () => BookPressed?.Invoke();
        foreach (var part in parts)
        {
            row.AddChild(part);
        }
    }

    /// <summary>The side this plate shows.</summary>
    public BattleSide Side { get; }

    /// <summary>The portrait was clicked: the hero's information window.</summary>
    public event Action? PortraitPressed;

    /// <summary>The spell book's button beside the plate was pressed.</summary>
    public event Action? BookPressed;

    /// <summary>The side's name as shown, for the smoke checks.</summary>
    public string Title => _name.Text;

    /// <summary>The mana as shown ("14 / 24"), empty for a side without a caster.</summary>
    public string Mana => _manaRow.Visible ? _manaText.Text : "";

    /// <summary>Shows the side in <paramref name="battle"/>: <paramref name="name"/>, rimmed in <paramref name="colour"/>.</summary>
    public void Show(GameState state, BattleState battle, string name, Color colour)
    {
        var participant = battle.Participant(Side);
        var hero = participant.Hero is { } id ? state.FindHero(id) : null;
        _name.Text = name;
        _rim.Tint(colour, 0.7f);
        if (hero is not null)
        {
            _portrait.Icon = HeroIcons.PortraitOrPlaceholder(hero.PortraitHero, hero.Name, PortraitSize);
            _portrait.SetMeta(UiIcons.StatusLabelMeta, hero.Name);
            _portrait.TooltipText = "The hero's information window";
            _portrait.Disabled = false;
        }
        else
        {
            // No hero: the side's strongest stack stands for it.
            var strongest = battle.Units.Where(u => u.Side == Side && u.IsAlive && !u.IsMachine).MaxBy(u => (long)u.Count * u.Creature.Health);
            _portrait.Icon = (strongest is null ? null : CreatureIcons.Of(strongest, PortraitSize)) ?? HeroIcons.PortraitOrPlaceholder(null, name, PortraitSize);
            _portrait.SetMeta(UiIcons.StatusLabelMeta, name);
            _portrait.TooltipText = "No hero leads this side";
            _portrait.Disabled = true;
        }

        _manaRow.Visible = participant.Caster is not null;
        if (participant.Caster is not null)
        {
            var mana = battle.ManaOf(Side);
            var max = Math.Max(hero?.MaxMana ?? mana, 1);
            _mana.Gauge = new Gauge(mana, max);
            _manaText.Text = $"{mana.ToString(CultureInfo.InvariantCulture)} / {max.ToString(CultureInfo.InvariantCulture)}";
            _mana.TooltipText = _manaText.TooltipText = $"Mana {_manaText.Text}";
        }
    }

    /// <summary>Shows the spell book's button beside the plate (the book is this side's), greyed with <paramref name="refusal"/>; hidden when <paramref name="shown"/> is false.</summary>
    public void ShowBook(bool shown, string? refusal)
    {
        _book.Visible = shown;
        _book.Disabled = refusal is not null;
        _book.TooltipText = refusal ?? "The battle spells and warcries";
    }
}
