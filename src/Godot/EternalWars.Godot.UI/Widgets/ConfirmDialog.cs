using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Widgets;

/// <summary>
/// A question before something is paid or lost (ADR-296): a titled window over a dimmed backdrop with what will happen (Recruit all's and Upgrade
/// all's summaries, a stack to dismiss), Cancel and the confirm button. Enter or the button confirms, Escape or Cancel drops it; it takes every
/// key while open, like <see cref="SliderDialog"/>.
/// </summary>
public partial class ConfirmDialog : CanvasLayer
{
    private readonly string _title;
    private readonly Control _body;
    private readonly string _confirmText;
    private readonly Action _confirmed;
    private bool _done;

    private ConfirmDialog(string title, Control body, string confirm, Action confirmed)
    {
        Layer = 40;
        (_title, _body, _confirmText, _confirmed) = (title, body, confirm, confirmed);
    }

    /// <summary>The dialog in the tree, if one is open.</summary>
    public static ConfirmDialog? Current { get; private set; }

    /// <summary>Whether a modal dialog (this or a <see cref="SliderDialog"/>) is open: the screens behind keep their keys off.</summary>
    public static bool AnyOpen => Current is not null || SliderDialog.Current is not null;

    /// <summary>Opens the question over <paramref name="anchor"/>'s screen; <paramref name="confirmed"/> runs on the confirm button.</summary>
    public static ConfirmDialog Open(Node anchor, string title, Control body, string confirm, Action confirmed)
    {
        Current?.Cancel();
        var dialog = new ConfirmDialog(title, body, confirm, confirmed);
        anchor.GetTree().Root.AddChild(dialog);
        return dialog;
    }

    /// <summary>A body of plain lines.</summary>
    public static Control Lines(params string[] lines)
    {
        var box = new VBoxContainer();
        foreach (var line in lines)
        {
            box.AddChild(new Label { Text = line, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) });
        }

        return box;
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
        var frame = new WindowFrame(_title, closable: false);
        middle.AddChild(frame);
        frame.Content.AddChild(_body);
        var cancel = new Button { Text = "Cancel" };
        cancel.Pressed += Cancel;
        var confirm = new Button { Text = _confirmText };
        confirm.Pressed += Confirm;
        frame.Content.AddChild(UiSpacing.Space(UiSpacing.Row));
        frame.Content.AddChild(Screens.ButtonRow(cancel, confirm));
        FocusOrder.Chain(cancel, confirm);
        confirm.CallDeferred(Control.MethodName.GrabFocus);
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

        GetViewport().SetInputAsHandled();
    }

    public void Confirm()
    {
        if (_done)
        {
            return;
        }

        _done = true;
        QueueFree();
        Current = null;
        _confirmed();
    }

    public void Cancel()
    {
        _done = true;
        QueueFree();
        Current = null;
    }
}
