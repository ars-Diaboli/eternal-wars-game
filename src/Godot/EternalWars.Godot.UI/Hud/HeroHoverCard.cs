using EternalWars.Godot.UI.Widgets;
using EternalWars.Core.Domain.Heroes;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The card beside the hero list while the pointer is over an entry (ADR-309): the hero's name, movement and mana as numbers, the four
/// primary stats and the army's seven slots (empty ones dim). It stands on its own (<see cref="Control.TopLevel"/>), next to the entry,
/// towards the middle of the screen, and lets the mouse through.
/// </summary>
public partial class HeroHoverCard : PanelContainer
{
    private const int SlotSize = 36;

    private readonly VBoxContainer _rows = new() { MouseFilter = MouseFilterEnum.Ignore };

    public HeroHoverCard()
    {
        Name = "HeroCard";
        TopLevel = true;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        _rows.AddThemeConstantOverride("separation", UiSpacing.Cell);
        AddChild(_rows);
    }

    /// <summary>The name on the card now; empty while it is hidden.</summary>
    public string HeroName { get; private set; } = "";

    /// <summary>Shows <paramref name="card"/> beside <paramref name="entry"/> (a global rectangle): to its right, or its left when <paramref name="toLeft"/>.</summary>
    public void Show(HeroCard card, Rect2 entry, bool toLeft)
    {
        Fill(card);
        HeroName = card.Name;
        Visible = true;
        ResetSize();
        var size = GetCombinedMinimumSize();
        var x = toLeft ? entry.Position.X - size.X - UiSpacing.Gap : entry.End.X + UiSpacing.Gap;
        var screen = GetViewportRect().Size;
        var y = Math.Clamp(entry.Position.Y, 0, Math.Max(0, screen.Y - size.Y));
        GlobalPosition = new Vector2(x, y);
    }

    /// <summary>Hides the card.</summary>
    public void Leave()
    {
        Visible = false;
        HeroName = "";
    }

    private void Fill(HeroCard card)
    {
        foreach (var child in _rows.GetChildren())
        {
            _rows.RemoveChild(child);
            child.QueueFree();
        }

        var name = new Label { Text = $"{card.Name}, level {card.Level}", MouseFilter = MouseFilterEnum.Ignore };
        name.AddThemeFontSizeOverride("font_size", 18);
        _rows.AddChild(name);
        _rows.AddChild(new Label
        {
            Text = $"Movement {card.Movement.Value} / {card.Movement.Max}    Mana {card.Mana.Value} / {card.Mana.Max}",
            MouseFilter = MouseFilterEnum.Ignore,
        });

        var stats = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        stats.AddThemeConstantOverride("separation", UiSpacing.Section);
        stats.AddChild(Ignore(HeroIcons.StatBadge(HeroStatIcon.Attack, "Attack", card.Attack.ToString())));
        stats.AddChild(Ignore(HeroIcons.StatBadge(HeroStatIcon.Defense, "Defence", card.Defense.ToString())));
        stats.AddChild(Ignore(HeroIcons.StatBadge(HeroStatIcon.SpellPower, "Spell power", card.SpellPower.ToString())));
        stats.AddChild(Ignore(HeroIcons.StatBadge(HeroStatIcon.Knowledge, "Knowledge", card.Knowledge.ToString())));
        _rows.AddChild(stats);

        var army = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        army.AddThemeConstantOverride("separation", UiSpacing.Cell);
        foreach (var stack in card.Slots)
        {
            army.AddChild(Slot(stack));
        }

        _rows.AddChild(army);
    }

    /// <summary>One slot of the army: the creature's portrait with its count at the bottom right, or a dim empty frame.</summary>
    private static Control Slot(Core.Domain.Armies.CreatureStack? stack)
    {
        var slot = new PanelContainer { CustomMinimumSize = new Vector2(SlotSize, SlotSize), MouseFilter = MouseFilterEnum.Ignore };
        slot.AddThemeStyleboxOverride("panel", UiFrames.Themed(BevelKind.Slot, 0, flat: UiTheme.Box(UiTheme.SideBarBackground, stack is null ? UiTheme.BorderDim : UiTheme.Border, 0)));
        if (stack is null)
        {
            slot.Modulate = new Color(1, 1, 1, 0.5f);
            return slot;
        }

        if (CreatureIcons.Get(stack.Creature.Value, SlotSize) is { } portrait)
        {
            slot.AddChild(new TextureRect { Texture = portrait, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, MouseFilter = MouseFilterEnum.Ignore });
        }

        var count = new Label
        {
            Text = stack.Count.ToString(),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        count.AddThemeFontSizeOverride("font_size", 12);
        count.AddThemeConstantOverride("outline_size", 4);
        count.AddThemeColorOverride("font_outline_color", Colors.Black);
        slot.AddChild(count);
        return slot;
    }

    private static Control Ignore(Control control)
    {
        control.MouseFilter = MouseFilterEnum.Ignore;
        foreach (var child in control.GetChildren())
        {
            if (child is Control part)
            {
                part.MouseFilter = MouseFilterEnum.Ignore;
            }
        }

        return control;
    }
}
