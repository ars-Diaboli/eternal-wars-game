using EternalWars.Core.Domain.Economy;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Widgets;

/// <summary>
/// What a <see cref="SliderDialog"/> asks: a count from <see cref="Min"/> to <see cref="Max"/> starting at <see cref="Value"/>, confirmed with
/// the <see cref="Confirm"/> button. With <see cref="CostOfOne"/> and <see cref="Treasury"/> it shows the cost of one, the total and the
/// treasury after paying (red where short), and Max stops at what the treasury pays for. <see cref="Describe"/> words the count ("12 of 30").
/// </summary>
public sealed record SliderRequest(
    string Title, int Min, int Max, int Value, string Confirm, ResourceSet? CostOfOne = null, ResourceSet? Treasury = null, Func<int, string>? Describe = null)
{
    /// <summary>The most the treasury pays for, within <see cref="Max"/>.</summary>
    public int Affordable => CostOfOne is { } one && Treasury is { } have ? Math.Clamp(EternalWars.Core.Domain.Towns.TownRules.MaxAffordable(have, one, Max), Min, Max) : Max;
}

/// <summary>
/// A count chosen with a slider (ADR-294): − / + by one, Max, the slider itself, and the cost lines when the count has a price; Enter or the
/// confirm button takes it, Escape or Cancel drops it. It opens in the middle of the screen over a dimmed backdrop that takes every click,
/// above the screens and below the information window. The town's split, recruit and trade dialogs are built on it.
/// </summary>
public partial class SliderDialog : CanvasLayer
{
    private readonly SliderRequest _request;
    private readonly Action<int> _confirmed;
    private readonly HSlider _slider = new() { CustomMinimumSize = new Vector2(320, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Step = 1 };
    private readonly Label _count = new() { CustomMinimumSize = new Vector2(120, 0), HorizontalAlignment = HorizontalAlignment.Center };
    private readonly CostLabel _total = new();
    private readonly HBoxContainer _after = new();
    private Button? _confirm;
    private bool _done;

    private SliderDialog(SliderRequest request, Action<int> confirmed)
    {
        Layer = 40;
        _request = request;
        _confirmed = confirmed;
    }

    /// <summary>The dialog in the tree, if one is open.</summary>
    public static SliderDialog? Current { get; private set; }

    /// <summary>The count the slider shows.</summary>
    public int Value => (int)_slider.Value;

    /// <summary>Opens a dialog for <paramref name="request"/> over <paramref name="anchor"/>'s screen; <paramref name="confirmed"/> gets the count.</summary>
    public static SliderDialog Open(Node anchor, SliderRequest request, Action<int> confirmed)
    {
        Current?.Cancel();
        var dialog = new SliderDialog(request, confirmed);
        anchor.GetTree().Root.AddChild(dialog);
        return dialog;
    }

    public override void _Ready()
    {
        Current = this;
        var backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.45f), MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(backdrop);
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var middle = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(middle);
        middle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var frame = new WindowFrame(_request.Title, closable: false);
        middle.AddChild(frame);

        var minus = new Button { Text = "−", TooltipText = "One fewer" }.WithIcon(UiIcon.Minus);
        var plus = new Button { Text = "+", TooltipText = "One more" }.WithIcon(UiIcon.Plus);
        var max = new Button { Text = "Max", TooltipText = _request.CostOfOne is null ? "All of them" : "As many as you can pay for" };
        _slider.MinValue = _request.Min;
        _slider.MaxValue = _request.Max;
        _slider.Value = Math.Clamp(_request.Value, _request.Min, _request.Max);
        minus.Pressed += () => _slider.Value -= 1;
        plus.Pressed += () => _slider.Value += 1;
        max.Pressed += () => _slider.Value = _request.Affordable;
        var row = new HBoxContainer();
        row.AddChild(minus);
        row.AddChild(_slider);
        row.AddChild(plus);
        row.AddChild(max);
        row.AddChild(_count);
        frame.Content.AddChild(row);

        if (_request.CostOfOne is { } one)
        {
            frame.Content.AddChild(Line("Cost of one", new CostLabel(one, null)));
            frame.Content.AddChild(Line("Total", _total));
            frame.Content.AddChild(Line("After paying", _after));
        }

        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += Cancel;
        var confirm = _confirm = new Button { Text = _request.Confirm };
        confirm.Pressed += Confirm;
        frame.Content.AddChild(UiSpacing.Space(UiSpacing.Row));
        frame.Content.AddChild(Screens.ButtonRow(cancel, confirm));
        _slider.ValueChanged += _ => ShowCount(confirm);
        ShowCount(confirm);
        FocusOrder.Chain(minus, _slider, plus, max, cancel, confirm);
        _slider.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _ExitTree()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventKey { Pressed: true, Keycode: Key.Escape }:
                Cancel();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter }:
                Confirm();
                break;
        }

        GetViewport().SetInputAsHandled(); // modal: no key reaches the screens behind it
    }

    /// <summary>Takes the count shown.</summary>
    public void Confirm()
    {
        if (_done || _confirm is { Disabled: true })
        {
            return; // the treasury is short, or the count was taken already
        }

        _done = true;
        var value = Value;
        QueueFree();
        Current = null;
        _confirmed(value);
    }

    /// <summary>Closes without a count.</summary>
    public void Cancel()
    {
        _done = true;
        QueueFree();
        Current = null;
    }

    private void ShowCount(Button confirm)
    {
        var value = Value;
        _count.Text = _request.Describe?.Invoke(value) ?? $"{value} of {_request.Max}";
        if (_request.CostOfOne is not { } one)
        {
            return;
        }

        var total = one * value;
        _total.Show(total, _request.Treasury);
        _after.ClearChildren();
        var have = _request.Treasury ?? ResourceSet.Empty;
        foreach (var resource in Enum.GetValues<ResourceType>().Where(r => total[r] > 0))
        {
            _after.AddChild(ResourceIcons.Badge(resource));
            var left = have[resource] - total[resource];
            var amount = new Label { Text = left.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            if (left < 0)
            {
                amount.AddThemeColorOverride("font_color", UiTheme.TextRefusal);
            }

            _after.AddChild(amount);
        }

        confirm.Disabled = _request.Treasury is not null && !_total.Lacking.IsEmpty;
    }

    private static HBoxContainer Line(string caption, Control value)
    {
        var line = new HBoxContainer();
        line.AddChild(new Label { Text = caption, ThemeTypeVariation = UiTheme.QuietLabel, CustomMinimumSize = new Vector2(130, 0) });
        line.AddChild(value);
        return line;
    }
}
