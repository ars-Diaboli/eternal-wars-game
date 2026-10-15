using System.Globalization;
using Godot;

namespace EternalWars.Godot.Shared.Controls;

/// <summary>
/// Replays scripted input for smoke tests and screenshots (together with Godot's <c>--write-movie</c>):
/// <c>-- --autoinput=30:click:800,450;45:key:Enter;60:rclick:400,300</c>. Each entry is
/// <c>frame:kind:argument</c>; kinds are <c>click</c>/<c>rclick</c> (screen x,y) and <c>key</c> (a Godot
/// <see cref="Key"/> name). Events go through Godot's normal input pipeline, so they hit the same handlers as a
/// real mouse and keyboard. Does nothing without the argument.
/// </summary>
public partial class AutoInput : Node
{
    private readonly List<(int Frame, InputEvent Event)> _script = [];
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
                case "key":
                    var key = Enum.Parse<Key>(parts[2], ignoreCase: true);
                    _script.Add((frame, new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true }));
                    _script.Add((frame + 1, new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false }));
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
        foreach (var (_, @event) in _script.Where(s => s.Frame == _frame))
        {
            if (@event is InputEventMouseButton mouse)
            {
                GetViewport().WarpMouse(mouse.Position);
            }

            Input.ParseInputEvent(@event);
        }
    }
}
