using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The selected hero's card at the bottom (ADR-309): the portrait as tall as the card, so its margin to the frame is the same all round (a
/// click opens the hero screen), two thin bars beside it (movement green, experience to the next level gold), then three rows: the level and
/// name ("LV 3 | Orrin"), the four primary stats, and morale and luck as icons with a signed value and the mana left as an icon and a number. The HUD hides it while no own hero is
/// selected and stretches it to the bottom row's height.
/// </summary>
public partial class SelectedHeroCard : HBoxContainer
{
    /// <summary>The size the portrait's picture is loaded at; it is drawn as tall as the card.</summary>
    public const int PortraitSize = 128;

    /// <summary>The card's width, whatever the hero's name and numbers, so the army strip beside it keeps its place.</summary>
    public const int Width = 360;

    private const int BarWidth = 6;

    private readonly Button _portrait = new() { Text = "Hero screen", FocusMode = FocusModeEnum.None };
    private readonly GaugeBar _movement = new() { Colour = GaugeBar.Movement, CustomMinimumSize = new Vector2(BarWidth, 0), MouseFilter = MouseFilterEnum.Pass };
    private readonly GaugeBar _experience = new() { Colour = GaugeBar.Experience, CustomMinimumSize = new Vector2(BarWidth, 0), MouseFilter = MouseFilterEnum.Pass };
    private readonly Label _name = new() { MouseFilter = MouseFilterEnum.Pass, ClipText = true };
    private readonly HBoxContainer _stats = new();
    private readonly HBoxContainer _spirits = new();
    private HeroId? _hero;

    public SelectedHeroCard()
    {
        Name = "HeroCard";
        CustomMinimumSize = new Vector2(Width, 0);
        AddThemeConstantOverride("separation", UiSpacing.Gap);
        _portrait.WithIcon(HeroIcons.PortraitOrPlaceholder(null, "?", PortraitSize), PortraitSize);
        _portrait.ExpandIcon = true;
        _portrait.CustomMinimumSize = Vector2.Zero; // a square as tall as the card (Resized)
        _portrait.SizeFlagsVertical = SizeFlags.Fill;
        UiFrames.Bevel(_portrait, BevelKind.Slot, padding: 2); // the portrait sunk into the card's panel (ADR-310)
        Resized += () => _portrait.CustomMinimumSize = new Vector2(Size.Y, Size.Y);
        _portrait.Pressed += () =>
        {
            if (_hero is { } hero)
            {
                HeroScreenRequested?.Invoke(hero);
            }
        };
        AddChild(_portrait);

        var bars = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        bars.AddThemeConstantOverride("separation", UiSpacing.Cell);
        foreach (var bar in new[] { _movement, _experience })
        {
            bar.SizeFlagsVertical = SizeFlags.ExpandFill;
            bars.AddChild(bar);
        }

        AddChild(bars);

        var lines = new VBoxContainer { Alignment = AlignmentMode.Center, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        lines.AddThemeConstantOverride("separation", UiSpacing.Cell);
        _name.AddThemeFontSizeOverride("font_size", 18);
        lines.AddChild(_name);
        foreach (var row in new[] { _stats, _spirits })
        {
            row.AddThemeConstantOverride("separation", UiSpacing.Gap);
            lines.AddChild(row);
        }

        AddChild(lines);
    }

    /// <summary>The portrait was clicked: the hero screen of this hero.</summary>
    public event Action<HeroId>? HeroScreenRequested;

    /// <summary>The hero shown; null while hidden.</summary>
    public HeroId? Hero => _hero;

    /// <summary>The name line ("LV 3 | Orrin"), for the smoke checks.</summary>
    public string Title => _name.Text;

    /// <summary>Shows <paramref name="card"/>; a <paramref name="refusal"/> greys the portrait (the replay viewer) and says why.</summary>
    public void Show(HeroCard card, string? refusal)
    {
        _hero = card.Hero;
        _portrait.Icon = HeroIcons.PortraitOrPlaceholder(card.Portrait, card.Name, PortraitSize);
        _portrait.SetMeta(UiIcons.StatusLabelMeta, $"{card.Name}: hero screen");
        _portrait.Disabled = refusal is not null;
        _portrait.TooltipText = refusal ?? "";
        _movement.Gauge = card.Movement;
        _experience.Gauge = card.Experience;
        _movement.TooltipText = $"Movement {card.Movement.Value} / {card.Movement.Max}";
        _experience.TooltipText = "Experience to the next level";
        _name.Text = $"LV {card.Level} | {card.Name}";

        foreach (var child in _stats.GetChildren().Concat(_spirits.GetChildren()))
        {
            child.GetParent().RemoveChild(child);
            child.QueueFree();
        }

        _stats.AddChild(HeroIcons.StatBadge(HeroStatIcon.Attack, "Attack", card.Attack.ToString()));
        _stats.AddChild(HeroIcons.StatBadge(HeroStatIcon.Defense, "Defence", card.Defense.ToString()));
        _stats.AddChild(HeroIcons.StatBadge(HeroStatIcon.SpellPower, "Spell power", card.SpellPower.ToString()));
        _stats.AddChild(HeroIcons.StatBadge(HeroStatIcon.Knowledge, "Knowledge", card.Knowledge.ToString()));
        _spirits.AddChild(HeroIcons.StatBadge(HeroStatIcon.Morale, "Morale", HeroCard.Signed(card.Morale)));
        _spirits.AddChild(HeroIcons.StatBadge(HeroStatIcon.Luck, "Luck", HeroCard.Signed(card.Luck)));
        var mana = HeroIcons.StatBadge(HeroStatIcon.Mana, "Mana", card.Mana.Value.ToString());
        mana.TooltipText = $"Mana {card.Mana.Value} / {card.Mana.Max}";
        _spirits.AddChild(mana);
    }

    /// <summary>No hero: the card is hidden by the HUD.</summary>
    public void Clear() => _hero = null;
}
