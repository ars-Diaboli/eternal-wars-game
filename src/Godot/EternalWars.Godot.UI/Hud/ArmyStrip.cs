using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The selected hero's army at the bottom, beside its card (ADR-309): always <see cref="Army.SlotCount"/> slots, empty ones dim. A stack shows
/// the creature's portrait and the count; its name goes to the status bar on hover. A click on a stack opens the hero screen, a right click
/// the creature's information window. The HUD hides it while no own hero is selected. Unlike the screens' <see cref="Widgets.ArmyBar"/> it
/// moves nothing.
/// </summary>
public partial class ArmyStrip : HBoxContainer
{
    /// <summary>A slot's side.</summary>
    public const int SlotSize = 56;

    private readonly Button[] _slots = new Button[Army.SlotCount];
    private HeroId? _hero;
    private bool _opens;
    private IReadOnlyList<CreatureStack?> _stacks = [];

    public ArmyStrip()
    {
        Name = "ArmyStrip";
        AddThemeConstantOverride("separation", UiSpacing.Cell);
        for (var i = 0; i < _slots.Length; i++)
        {
            var slot = i;
            var button = new Button
            {
                ThemeTypeVariation = UiTheme.SlotButton, CustomMinimumSize = new Vector2(SlotSize, SlotSize), FocusMode = FocusModeEnum.None,
                IconAlignment = HorizontalAlignment.Center, ExpandIcon = true, SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            var count = new Label
            {
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, MouseFilter = MouseFilterEnum.Ignore,
            };
            count.AddThemeFontSizeOverride("font_size", 13);
            count.AddThemeConstantOverride("outline_size", 4);
            count.AddThemeColorOverride("font_outline_color", Colors.Black);
            button.AddChild(count);
            count.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            count.OffsetRight = count.OffsetBottom = -3;
            button.Pressed += () =>
            {
                if (_opens && _hero is { } hero && slot < _stacks.Count && _stacks[slot] is not null)
                {
                    HeroScreenRequested?.Invoke(hero);
                }
            };
            button.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } click && _hero is { } hero
                    && slot < _stacks.Count && _stacks[slot] is not null)
                {
                    StackInfoRequested?.Invoke(hero, slot, click.GlobalPosition);
                    button.AcceptEvent();
                }
            };
            _slots[i] = button;
            AddChild(button);
        }
    }

    /// <summary>A stack was clicked: the hero screen.</summary>
    public event Action<HeroId>? HeroScreenRequested;

    /// <summary>A stack was right-clicked: its information window, at the pointer.</summary>
    public event Action<HeroId, int, Vector2>? StackInfoRequested;

    /// <summary>The slots, for the smoke checks.</summary>
    public IReadOnlyList<Button> Slots => _slots;

    /// <summary>
    /// Shows <paramref name="card"/>'s army; <paramref name="refusal"/> (the replay viewer, another player's turn) keeps the clicks from
    /// opening the hero screen, while a right click still opens the information window.
    /// </summary>
    public void Show(GameState state, HeroCard card, string? refusal)
    {
        _hero = card.Hero;
        _opens = refusal is null;
        _stacks = card.Slots;
        for (var i = 0; i < _slots.Length; i++)
        {
            var button = _slots[i];
            var stack = i < card.Slots.Count ? card.Slots[i] : null;
            var count = (Label)button.GetChild(0);
            if (stack is null)
            {
                button.Icon = null;
                count.Text = "";
                button.Modulate = new Color(1, 1, 1, 0.45f);
                button.SetMeta(UiIcons.StatusLabelMeta, "Empty slot");
                button.TooltipText = "";
                continue;
            }

            var name = state.Creatures.Get(stack.Creature).Name;
            button.Icon = CreatureIcons.Get(stack.Creature.Value, SlotSize - 8);
            count.Text = stack.Count.ToString();
            button.Modulate = Colors.White;
            button.SetMeta(UiIcons.StatusLabelMeta, $"{stack.Count} {name}");
            button.TooltipText = refusal is null ? "Click: hero screen · right click: information" : $"{refusal} · right click: information";
        }
    }
}
