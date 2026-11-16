using EternalWars.Core.Domain.Settings;
using Godot;

namespace EternalWars.Godot.Shared.Controls;

/// <summary>
/// A touch screen as the mouse (ADR-190), in place of Godot's own mouse emulation, which it turns off while it is in the tree: each finger's
/// events go through <see cref="TouchGesture"/> (a tap clicks, a moved finger drags, a finger held still right-clicks, which opens the
/// information window) and come out as ordinary mouse events, so every screen handles touch as it handles the mouse.
/// </summary>
public partial class TouchInput : Node
{
    private readonly TouchGesture _gesture = new();
    private readonly List<PointerAction> _pending = [];
    private double _clock;
    private bool _emulated;
    private Vector2 _last;

    public override void _EnterTree()
    {
        _emulated = Input.EmulateMouseFromTouch;
        Input.EmulateMouseFromTouch = false;
    }

    public override void _ExitTree() => Input.EmulateMouseFromTouch = _emulated;

    public override void _Input(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventScreenTouch { Pressed: true } down:
                _pending.AddRange(_gesture.Down(down.Index, down.Position.X, down.Position.Y, _clock));
                break;
            case InputEventScreenTouch up:
                _pending.AddRange(_gesture.Up(up.Index, up.Position.X, up.Position.Y));
                break;
            case InputEventScreenDrag drag:
                _pending.AddRange(_gesture.Move(drag.Index, drag.Position.X, drag.Position.Y));
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        _clock += delta;
        _pending.AddRange(_gesture.Tick(_clock));
        if (_pending.Count == 0)
        {
            return;
        }

        // Sent from here, not from inside the touch event's handling.
        var actions = _pending.ToList();
        _pending.Clear();
        foreach (var action in actions)
        {
            Send(action);
        }
    }

    private void Send(PointerAction action)
    {
        var at = new Vector2(action.X, action.Y);
        var relative = at - _last;
        _last = at;
        switch (action.Kind)
        {
            case PointerActionKind.Hover:
                Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at, Relative = relative });
                break;
            case PointerActionKind.LeftDrag:
                Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at, Relative = relative, ButtonMask = MouseButtonMask.Left });
                break;
            case PointerActionKind.LeftPress or PointerActionKind.LeftRelease:
                Input.ParseInputEvent(new InputEventMouseButton
                {
                    ButtonIndex = MouseButton.Left, Pressed = action.Kind == PointerActionKind.LeftPress, Position = at, GlobalPosition = at,
                    ButtonMask = action.Kind == PointerActionKind.LeftPress ? MouseButtonMask.Left : 0,
                });
                break;
            case PointerActionKind.RightClick:
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true, Position = at, GlobalPosition = at, ButtonMask = MouseButtonMask.Right });
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = false, Position = at, GlobalPosition = at });
                break;
        }
    }
}
