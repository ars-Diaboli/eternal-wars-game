using System.Globalization;
using Godot;

namespace EternalWars.Godot.Shared.Controls;

/// <summary>
/// Replays scripted input for smoke tests and screenshots (together with Godot's <c>--write-movie</c>):
/// <c>-- --autoinput=30:click:800,450;45:key:Enter;60:rclick:400,300</c>. Each entry is
/// <c>frame:kind:argument</c>; kinds are <c>click</c>/<c>rclick</c> (screen x,y), <c>key</c> (a Godot
/// <see cref="Key"/> name), <c>hold</c> (<c>Key,frames</c>: held down that long), <c>wheel</c> (<c>up|down,count</c>) and
/// <c>drag</c>/<c>rdrag</c> (<c>x1,y1,x2,y2</c>: left or right button pressed, moved, released over three frames), <c>press</c> (a visible,
/// enabled button by its text or, without one, its tooltip; the topmost: exact, else the first that contains it, ignoring case; scrolled into view, then clicked at its centre the next
/// frame; an error when there is none) and <c>dump</c> (prints the visible buttons' texts (a text-less button by its tooltip) and centres, for writing scripts). Events go through
/// Godot's normal input pipeline, so they hit the same handlers as a real mouse and keyboard. Does nothing without the argument.
/// </summary>
public partial class AutoInput : Node
{
    private readonly List<(int Frame, InputEvent Event)> _script = [];
    private readonly List<(int Frame, string Kind, string Argument)> _lookups = [];
    private int _frame;

    public static AutoInput? FromLaunchArguments() =>
        LaunchArguments.AutoInput is { Length: > 0 } script ? new AutoInput(script) : null;

    public AutoInput()
    {
    }

    private AutoInput(string script)
    {
        foreach (var entry in script.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = entry.Split(':', 3);
            var frame = int.Parse(parts[0], CultureInfo.InvariantCulture);
            switch (parts[1])
            {
                case "click" or "rclick":
                    var xy = parts[2].Split(',');
                    var at = new Vector2(float.Parse(xy[0], CultureInfo.InvariantCulture), float.Parse(xy[1], CultureInfo.InvariantCulture));
                    var button = parts[1] == "click" ? MouseButton.Left : MouseButton.Right;
                    _script.Add((frame, new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = at, GlobalPosition = at }));
                    _script.Add((frame + 1, new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = at, GlobalPosition = at }));
                    break;
                case "drag" or "rdrag":
                    // drag:x1,y1,x2,y2 presses at the first point, moves to the second the next frame and releases there the frame after.
                    var points = parts[2].Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                    var (from, to) = (new Vector2(points[0], points[1]), new Vector2(points[2], points[3]));
                    var dragButton = parts[1] == "drag" ? MouseButton.Left : MouseButton.Right;
                    _script.Add((frame, new InputEventMouseButton { ButtonIndex = dragButton, Pressed = true, Position = from, GlobalPosition = from }));
                    _script.Add((frame + 1, new InputEventMouseMotion
                    {
                        Position = to, GlobalPosition = to, Relative = to - from,
                        ButtonMask = dragButton == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right, // held, as a real drag reports it
                    }));
                    _script.Add((frame + 2, new InputEventMouseButton { ButtonIndex = dragButton, Pressed = false, Position = to, GlobalPosition = to }));
                    break;
                case "key" or "hold":
                    // hold:Key,frames keeps the key down that many frames (camera panning reads the held keys).
                    var keyParts = parts[2].Split(',');
                    var key = Enum.Parse<Key>(keyParts[0], ignoreCase: true);
                    var held = parts[1] == "hold" && keyParts.Length > 1 ? int.Parse(keyParts[1], CultureInfo.InvariantCulture) : 1;
                    _script.Add((frame, new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }));
                    _script.Add((frame + held, new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }));
                    break;
                case "wheel":
                    // wheel:up|down,count at the screen centre (camera zoom).
                    var wheelParts = parts[2].Split(',');
                    var wheel = wheelParts[0].Equals("up", StringComparison.OrdinalIgnoreCase) ? MouseButton.WheelUp : MouseButton.WheelDown;
                    var turns = wheelParts.Length > 1 ? int.Parse(wheelParts[1], CultureInfo.InvariantCulture) : 1;
                    for (var i = 0; i < turns; i++)
                    {
                        _script.Add((frame, new InputEventMouseButton { ButtonIndex = wheel, Pressed = true }));
                        _script.Add((frame, new InputEventMouseButton { ButtonIndex = wheel, Pressed = false }));
                    }

                    break;
                case "press" or "dump":
                    _lookups.Add((frame, parts[1], parts.Length > 2 ? parts[2] : ""));
                    break;
                default:
                    GD.PushWarning($"autoinput: unknown kind '{parts[1]}' in '{entry}'");
                    break;
            }
        }
    }

    public override void _Process(double delta)
    {
        _frame++;
        foreach (var (_, kind, argument) in _lookups.Where(l => l.Frame == _frame && l.Kind is "press" or "dump").ToList())
        {
            if (kind == "dump")
            {
                foreach (var button in VisibleButtons())
                {
                    GD.Print($"autoinput: button '{Label(button).ReplaceLineEndings(" | ")}' at {button.GetGlobalRect().GetCenter().Round()}{(button.Disabled ? " (disabled)" : "")}");
                }
            }
            else if (Find(argument) is { } button)
            {
                // Scrolled into view now, clicked where it then is the next frame.
                for (var parent = button.GetParent(); parent is not null; parent = parent.GetParent())
                {
                    if (parent is ScrollContainer scroll)
                    {
                        scroll.EnsureControlVisible(button);
                        break;
                    }
                }

                _lookups.Add((_frame + 1, "click-button", button.GetInstanceId().ToString(CultureInfo.InvariantCulture)));
            }
            else
            {
                GD.PushError($"autoinput: no visible button '{argument}' at frame {_frame}");
            }
        }

        foreach (var (_, _, argument) in _lookups.Where(l => l.Frame == _frame && l.Kind == "click-button").ToList())
        {
            if (InstanceFromId(ulong.Parse(argument, CultureInfo.InvariantCulture)) is Control button)
            {
                var at = button.GetGlobalRect().GetCenter();
                _script.Add((_frame, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at }));
                _script.Add((_frame + 1, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at }));
            }
        }

        foreach (var (_, @event) in _script.Where(s => s.Frame == _frame))
        {
            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } turn)
            {
                turn.Position = turn.GlobalPosition = GetViewport().GetVisibleRect().Size / 2;
            }

            if (@event is InputEventMouseButton mouse)
            {
                GetViewport().WarpMouse(mouse.Position);
            }
            else if (@event is InputEventMouseMotion motion)
            {
                GetViewport().WarpMouse(motion.Position);
            }

            Input.ParseInputEvent(@event);
        }
    }

    private BaseButton? Find(string text)
    {
        // The last in tree order is drawn on top (a screen over the map's HUD), so it wins.
        var buttons = VisibleButtons().Where(b => !b.Disabled).Reverse().ToList();
        return buttons.FirstOrDefault(b => string.Equals(Label(b), text, StringComparison.OrdinalIgnoreCase))
            ?? buttons.FirstOrDefault(b => Label(b).Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    private static string Label(BaseButton button) => button switch
    {
        Button { Text: { Length: > 0 } text } => text,
        _ => button.TooltipText,
    };

    private List<BaseButton> VisibleButtons()
    {
        var found = new List<BaseButton>();
        Collect(GetTree().Root, found);
        return found;
    }

    private static void Collect(Node node, List<BaseButton> found)
    {
        if (node is CanvasItem { Visible: false } or CanvasLayer { Visible: false })
        {
            return;
        }

        if (node is BaseButton button && button.IsVisibleInTree())
        {
            found.Add(button);
        }

        foreach (var child in node.GetChildren())
        {
            Collect(child, found);
        }
    }
}
