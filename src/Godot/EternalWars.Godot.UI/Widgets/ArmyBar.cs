using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Widgets;

/// <summary>A stack's place in an <see cref="ArmyBar"/>: the army (the screen's key: a hero id, a town army) and its slot.</summary>
public sealed record ArmySlot(object Army, int Slot);

/// <summary>
/// What the army bars of one screen do (ADR-294), given by the screen: the commands for a move, a spread, an upgrade and the heroes' swap, the
/// information sheet of a stack, and how commands run. The bars of one group drag and pick between each other; a stack picked by a click is
/// the group's, so a click on another bar's slot moves it there (the keyboard's and the controller's way, beside dragging).
/// </summary>
public sealed class ArmyBarGroup
{
    /// <summary>The command that moves <c>count</c> creatures (null: the whole stack) from one slot to another, or null when it does nothing.</summary>
    public required Func<ArmySlot, ArmySlot, int?, GameCommand?> Move { get; init; }

    /// <summary>Runs commands in order, stopping at the first refused one.</summary>
    public required Action<IReadOnlyList<GameCommand>> Run { get; init; }

    /// <summary>The stack's information sheet (the "Detailed tooltips" setting given), opened by a double click and a right click.</summary>
    public required Func<ArmySlot, bool, InfoSheet?> Info { get; init; }

    /// <summary>The commands that spread a stack over its army's empty slots (Alt + click); none when the screen has no spread.</summary>
    public Func<ArmySlot, IReadOnlyList<GameCommand>>? Spread { get; init; }

    /// <summary>A stack the town can upgrade: the command and the badge's tooltip with the price; null for none.</summary>
    public Func<ArmySlot, (GameCommand Command, string Tooltip)?>? Upgrade { get; init; }

    /// <summary>A hero's portrait dragged onto the other bar: the command that swaps or moves the heroes, or null when it cannot.</summary>
    public Func<object, object, GameCommand?>? MoveHero { get; init; }

    /// <summary>The stack picked by a click, if any.</summary>
    public ArmySlot? Picked
    {
        get;
        set
        {
            field = value;
            PickChanged?.Invoke();
        }
    }

    /// <summary>The picked stack changed: the bars show it.</summary>
    public event Action? PickChanged;
}

/// <summary>
/// An army in a row (ADR-294): a hero's portrait (an empty frame without a hero) and seven slots with the creature's portrait and the count.
/// Dragging a stack onto a slot moves it, merges it with the same creature or swaps it with another; Shift + drag asks how many to split off
/// (<see cref="SliderDialog"/>); Ctrl + drag moves one creature; Alt + click spreads the stack over the empty slots; a double click or a right
/// click opens the creature's information. A stack the town can upgrade carries an arrow badge with the price in its tooltip. A click picks a
/// stack and a click on another slot moves it there (Shift: split), so the keyboard and the controller do all of it too. Dragging the hero's
/// portrait onto the other bar of the group swaps or moves the heroes. Used by the town, hero and exchange screens; what each move does is
/// the screen's (<see cref="ArmyBarGroup"/>).
/// </summary>
public partial class ArmyBar : HBoxContainer
{
    /// <summary>A slot's size: the creature's portrait with its count.</summary>
    public static readonly Vector2 SlotSize = new(72, 72);

    private const int PortraitSize = 60;

    private readonly ArmyBarGroup _group;
    private readonly object _army;
    private readonly Button _hero = new() { ThemeTypeVariation = UiTheme.SlotButton, CustomMinimumSize = SlotSize, ClipContents = true };
    private string? _heroName;
    private readonly Button[] _slots = new Button[Army.SlotCount];
    private Army? _shown;
    private GameState? _state;

    /// <param name="group">The screen's bars, with what their moves do.</param>
    /// <param name="army">The army this bar shows, as the screen names it in an <see cref="ArmySlot"/>.</param>
    /// <param name="caption">The bar's name before it ("Garrison"), or null for none.</param>
    public ArmyBar(ArmyBarGroup group, object army, string? caption = null)
    {
        _group = group;
        _army = army;
        this.WithCellGap();
        if (caption is not null)
        {
            AddChild(new Label { Text = caption, CustomMinimumSize = new Vector2(UiTheme.ArmyNameWidth, 0), VerticalAlignment = VerticalAlignment.Center });
        }

        // The hero's frame: a portrait's frame, set apart from the slots by its bronze border and a lighter ground.
        foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed", "disabled" })
        {
            var lit = state is "hover" or "hover_pressed";
            _hero.AddThemeStyleboxOverride(state, UiTheme.Box(UiTheme.ButtonFace.Lightened(0.04f), lit ? UiTheme.BorderBright : UiTheme.Border, 2, 2));
        }

        AddChild(_hero);
        _hero.SetDragForwarding(Callable.From<Vector2, Variant>(_ => HeroDrag()), Callable.From<Vector2, Variant, bool>(CanDropHero),
            Callable.From<Vector2, Variant>(DropHero));
        AddChild(new VSeparator());
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var at = new ArmySlot(army, slot);
            var button = new Button { ThemeTypeVariation = UiTheme.SlotButton, CustomMinimumSize = SlotSize, ToggleMode = true, ClipText = true };
            button.SetDragForwarding(
                Callable.From<Vector2, Variant>(_ => StackDrag(at)),
                Callable.From<Vector2, Variant, bool>((_, data) => data.Obj is StackDragPayload held && held.Group == _group && held.From != at),
                Callable.From<Vector2, Variant>((_, data) => Drop(data, at)));
            button.Pressed += () => Click(at);
            button.GuiInput += e => SlotInput(e, at);
            _slots[slot] = button;
            AddChild(button);
        }

    }

    /// <summary>The bar's army key.</summary>
    public object ArmyKey => _army;

    /// <summary>The slot buttons, for a window's focus order.</summary>
    public IReadOnlyList<Button> Slots => _slots;

    /// <summary>The hero's frame, for a window's focus order.</summary>
    public Button HeroFrame => _hero;

    public override void _ExitTree() => _group.PickChanged -= ShowPicked;

    /// <summary>
    /// Shows <paramref name="army"/> (null: an empty, inactive row: no visiting hero) led by <paramref name="hero"/> (null: an empty frame).
    /// </summary>
    public void Show(GameState state, Army? army, string? hero)
    {
        _state = state;
        _shown = army;
        _heroName = hero;
        _hero.ClearChildren();
        if (hero is null)
        {
            _hero.AddChild(Overlay(new Label
            {
                Text = "no hero",
                ThemeTypeVariation = UiTheme.QuietLabel,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }, LayoutPreset.FullRect));
        }
        else
        {
            // The name until heroes have portraits: a label over the frame, so the scripts name the frame by its label ("Hero Orrin").
            _hero.AddChild(Overlay(new Label
            {
                Text = hero,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }, LayoutPreset.FullRect));
        }

        _hero.TooltipText = hero is null ? "No hero" : $"{hero}: drag onto the other row to swap the heroes";
        _hero.SetMeta(AutoInput.LabelMeta, hero is null ? "No hero" : $"Hero {hero}");
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            ShowSlot(_slots[slot], new ArmySlot(_army, slot), army?[slot]);
        }

        ShowPicked();
    }

    private void ShowSlot(Button button, ArmySlot at, CreatureStack? stack)
    {
        button.ClearChildren();
        button.Disabled = _shown is null;
        button.Text = "";
        button.Icon = null;
        button.TooltipText = "";
        if (stack is null || _state is null)
        {
            button.SetMeta(AutoInput.LabelMeta, "—");
            return;
        }

        var name = _state.Creatures.Get(stack.Creature).Name;
        button.SetMeta(AutoInput.LabelMeta, $"{stack.Count}\n{name}"); // the scripts' label, as the slots always had
        button.TooltipText = $"{stack.Count} {name}\nDrag: move · Shift+drag: split · Ctrl+drag: one · Alt+click: spread · double click: information";
        if (CreatureIcons.Get(stack.Creature.Value, PortraitSize) is { } portrait)
        {
            button.Icon = portrait;
            button.IconAlignment = HorizontalAlignment.Center;
            button.ExpandIcon = true;
        }
        else
        {
            button.AddChild(Overlay(new Label
            {
                Text = name,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            }, LayoutPreset.FullRect));
        }

        var count = new Label { Text = stack.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), HorizontalAlignment = HorizontalAlignment.Right };
        if (UiFonts.BodyBold is { } bold)
        {
            count.AddThemeFontOverride("font", bold);
        }

        count.AddThemeColorOverride("font_outline_color", Colors.Black);
        count.AddThemeConstantOverride("outline_size", 4);
        button.AddChild(Overlay(count, LayoutPreset.BottomRight));

        if (_group.Upgrade?.Invoke(at) is { } upgrade)
        {
            var badge = new Label { Text = "▲", TooltipText = upgrade.Tooltip, MouseFilter = MouseFilterEnum.Stop };
            badge.AddThemeColorOverride("font_color", UiTheme.TextComplete);
            badge.AddThemeColorOverride("font_outline_color", Colors.Black);
            badge.AddThemeConstantOverride("outline_size", 4);
            badge.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                {
                    badge.AcceptEvent();
                    _group.Picked = null;
                    _group.Run([upgrade.Command]);
                }
            };
            button.AddChild(Overlay(badge, LayoutPreset.TopRight));
        }
    }

    /// <summary>A control laid over the slot at <paramref name="corner"/>, a little inside its frame.</summary>
    private static Control Overlay(Control control, LayoutPreset corner)
    {
        if (control.MouseFilter != MouseFilterEnum.Stop)
        {
            control.MouseFilter = MouseFilterEnum.Ignore;
        }

        var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            margin.AddThemeConstantOverride($"margin_{side}", 4);
        }

        margin.AddChild(control);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        control.SizeFlagsHorizontal = corner is LayoutPreset.BottomRight or LayoutPreset.TopRight ? SizeFlags.ShrinkEnd : SizeFlags.Fill;
        control.SizeFlagsVertical = corner switch
        {
            LayoutPreset.BottomRight => SizeFlags.ShrinkEnd,
            LayoutPreset.TopRight => SizeFlags.ShrinkBegin,
            _ => SizeFlags.Fill,
        };
        return margin;
    }

    private void ShowPicked()
    {
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            _slots[slot].SetPressedNoSignal(_group.Picked == new ArmySlot(_army, slot));
        }
    }

    /// <summary>A click: picks the stack, or moves the picked one here (Shift: asks how many).</summary>
    private void Click(ArmySlot at)
    {
        if (Input.IsKeyPressed(Key.Alt))
        {
            _group.Picked = null;
            Spread(at);
            return;
        }

        if (_group.Picked is not { } from)
        {
            _group.Picked = _shown?[at.Slot] is not null ? at : null;
            return;
        }

        _group.Picked = null;
        if (from != at)
        {
            MoveFrom(from, at, Input.IsKeyPressed(Key.Shift), oneOnly: false);
        }
    }

    private void SlotInput(InputEvent @event, ArmySlot at)
    {
        if (@event is not InputEventMouseButton { Pressed: true } mouse || _shown?[at.Slot] is null)
        {
            return;
        }

        if ((mouse.ButtonIndex == MouseButton.Left && mouse.DoubleClick) || mouse.ButtonIndex == MouseButton.Right)
        {
            if (InfoWindow.Current is { } window && _group.Info(at, window.Detailed) is { } sheet)
            {
                _group.Picked = null;
                window.Open(sheet);
                _slots[at.Slot].AcceptEvent();
            }
        }
    }

    private void Spread(ArmySlot at)
    {
        if (_group.Spread?.Invoke(at) is { Count: > 0 } moves)
        {
            _group.Run(moves);
        }
    }

    private Variant StackDrag(ArmySlot at)
    {
        if (_shown?[at.Slot] is not { } stack || _state is null)
        {
            return default;
        }

        _group.Picked = null;
        var preview = new TextureRect
        {
            Texture = CreatureIcons.Get(stack.Creature.Value, PortraitSize),
            Modulate = new Color(1, 1, 1, 0.85f),
            Position = new Vector2(-PortraitSize / 2f, -PortraitSize / 2f),
            CustomMinimumSize = new Vector2(PortraitSize, PortraitSize),
        };
        if (preview.Texture is null)
        {
            var label = new PanelContainer { Position = new Vector2(12, 12) };
            label.AddChild(new Label { Text = $"{stack.Count} {_state.Creatures.Get(stack.Creature).Name}" });
            _slots[at.Slot].SetDragPreview(label);
        }
        else
        {
            _slots[at.Slot].SetDragPreview(preview);
        }

        return Variant.From(new StackDragPayload { Group = _group, From = at });
    }

    private void Drop(Variant data, ArmySlot at)
    {
        if (data.Obj is StackDragPayload held && held.Group == _group && held.From is { } from)
        {
            MoveFrom(from, at, Input.IsKeyPressed(Key.Shift), oneOnly: Input.IsKeyPressed(Key.Ctrl));
        }
    }

    /// <summary>Moves the stack at <paramref name="from"/> here: the whole of it, one creature, or as many as the split dialog says.</summary>
    private void MoveFrom(ArmySlot from, ArmySlot at, bool split, bool oneOnly)
    {
        if (StackAt(from) is not { } source)
        {
            return;
        }

        if (oneOnly)
        {
            Run(_group.Move(from, at, 1));
            return;
        }

        if (split && source.Count > 1)
        {
            SliderDialog.Open(this, new SliderRequest($"Split {source.Count}", 1, source.Count - 1, source.Count / 2, "Split",
                Describe: n => $"{source.Count - n} · {n}"), n => Run(_group.Move(from, at, n)));
            return;
        }

        Run(_group.Move(from, at, null));
    }

    /// <summary>The stack at a slot of this group: this bar's, or another bar's of the same group.</summary>
    private CreatureStack? StackAt(ArmySlot at) =>
        at.Army.Equals(_army) ? _shown?[at.Slot] : Bars().FirstOrDefault(b => b._army.Equals(at.Army))?._shown?[at.Slot];

    /// <summary>The bars of this group on the screen: its siblings in the window.</summary>
    private IEnumerable<ArmyBar> Bars() =>
        GetTree().GetNodesInGroup(GroupName).OfType<ArmyBar>().Where(b => b._group == _group);

    private const string GroupName = "army_bars";

    public override void _EnterTree()
    {
        AddToGroup(GroupName);
        _group.PickChanged += ShowPicked; // while in the tree only: the group outlives the bar
        ShowPicked();
    }

    private void Run(GameCommand? command)
    {
        if (command is not null)
        {
            _group.Run([command]);
        }
    }

    private Variant HeroDrag()
    {
        if (_heroName is null || _group.MoveHero is null)
        {
            return default;
        }

        var label = new PanelContainer { Position = new Vector2(12, 12) };
        label.AddChild(new Label { Text = _heroName });
        _hero.SetDragPreview(label);
        return Variant.From(new HeroDragPayload { Group = _group, From = _army });
    }

    private bool CanDropHero(Vector2 _, Variant data) =>
        data.Obj is HeroDragPayload held && held.Group == _group && !held.From!.Equals(_army) && _group.MoveHero?.Invoke(held.From, _army) is not null;

    private void DropHero(Vector2 _, Variant data)
    {
        if (data.Obj is HeroDragPayload held && held.Group == _group)
        {
            Run(_group.MoveHero?.Invoke(held.From!, _army));
        }
    }
}

/// <summary>What Godot carries while a stack is dragged.</summary>
internal sealed partial class StackDragPayload : RefCounted
{
    public ArmyBarGroup? Group { get; init; }

    public ArmySlot? From { get; init; }
}

/// <summary>What Godot carries while a hero's portrait is dragged.</summary>
internal sealed partial class HeroDragPayload : RefCounted
{
    public ArmyBarGroup? Group { get; init; }

    public object? From { get; init; }
}
