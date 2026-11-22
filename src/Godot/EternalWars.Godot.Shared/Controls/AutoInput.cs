using System.Globalization;
using Godot;

namespace EternalWars.Godot.Shared.Controls;

/// <summary>
/// Replays scripted input for smoke tests and screenshots (together with Godot's <c>--write-movie</c>):
/// <c>-- --autoinput=30:click:800,450;45:key:Enter;60:rclick:400,300</c>. Each entry is
/// <c>frame:kind:argument</c>; kinds are <c>click</c>/<c>rclick</c> (screen x,y), <c>key</c> (a Godot
/// <see cref="Key"/> name, with its modifiers before it: <c>key:Ctrl+Shift+P</c>), <c>text</c> (letters typed into the focused text field, one a frame), <c>touch</c> (<c>x,y,frames</c>: one finger down there, lifted that many frames later; a long press
/// opens the information window through <see cref="TouchInput"/>), <c>hold</c> (<c>Key,frames</c>: held down that long), <c>wheel</c> (<c>up|down,count</c> at the screen's centre, or <c>up|down,count,x,y</c> at that point) and
/// <c>drag</c>/<c>rdrag</c>/<c>mdrag</c> (<c>x1,y1,x2,y2</c>: left, right or middle button pressed, moved, released over three frames), <c>press</c> (a visible,
/// enabled button by its text or, without one, its tooltip; the topmost: exact, else the first that contains it, ignoring case; scrolled into view, then clicked at its centre the next
/// frame; an error when there is none) and <c>dump</c> (prints the visible buttons' texts (a text-less button by its tooltip), centres and sizes, and the tabs, for writing scripts). A click,
/// right click or drag may name buttons instead of points, so a layout change does not move its target: <c>click:@Close</c>,
/// <c>rclick:@Head: —#2</c> (the second button labelled so, enabled or not, in tree order), <c>drag:@Rusted Blade&gt;@Head: —#2</c> or
/// <c>drag:@Axe&gt;1048,456</c> (a button onto a point); a label is the one <c>dump</c> prints, line breaks as " | ", or a tab's title
/// (<c>click:@Settings</c>). <c>choose:Slow</c> picks an item of the open drop-down list by its text. Points are the UI's, whatever its
/// scale. Events go through Godot's normal input pipeline, so they hit the same handlers as a real mouse and keyboard. Does nothing
/// without the argument.
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
                case "click" or "rclick" or "drag" or "rdrag" when parts[2].Contains('@', StringComparison.Ordinal):
                    _lookups.Add((frame, parts[1], parts[2])); // aimed at a button: found when the frame comes
                    break;
                case "click" or "rclick":
                    var xy = parts[2].Split(',');
                    var at = new Vector2(float.Parse(xy[0], CultureInfo.InvariantCulture), float.Parse(xy[1], CultureInfo.InvariantCulture));
                    var button = parts[1] == "click" ? MouseButton.Left : MouseButton.Right;
                    _script.Add((frame, new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = at, GlobalPosition = at }));
                    _script.Add((frame + 1, new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = at, GlobalPosition = at }));
                    break;
                case "drag" or "rdrag" or "mdrag":
                    // drag:x1,y1,x2,y2 presses at the first point, moves to the second the next frame and releases there the frame after.
                    var points = parts[2].Split(',').Select(v => float.Parse(v, CultureInfo.InvariantCulture)).ToArray();
                    var (from, to) = (new Vector2(points[0], points[1]), new Vector2(points[2], points[3]));
                    var dragButton = parts[1] switch { "drag" => MouseButton.Left, "rdrag" => MouseButton.Right, _ => MouseButton.Middle };
                    _script.Add((frame, new InputEventMouseButton { ButtonIndex = dragButton, Pressed = true, Position = from, GlobalPosition = from }));
                    _script.Add((frame + 1, new InputEventMouseMotion
                    {
                        Position = to, GlobalPosition = to, Relative = to - from,
                        ButtonMask = dragButton switch { MouseButton.Left => MouseButtonMask.Left, MouseButton.Right => MouseButtonMask.Right, _ => MouseButtonMask.Middle }, // held, as a real drag reports it
                    }));
                    _script.Add((frame + 2, new InputEventMouseButton { ButtonIndex = dragButton, Pressed = false, Position = to, GlobalPosition = to }));
                    break;
                case "touch":
                    // touch:x,y,frames puts a finger down there and lifts it that many frames later (default 1: a tap).
                    var touchParts = parts[2].Split(',');
                    var finger = new Vector2(float.Parse(touchParts[0], CultureInfo.InvariantCulture), float.Parse(touchParts[1], CultureInfo.InvariantCulture));
                    var touchFrames = touchParts.Length > 2 ? int.Parse(touchParts[2], CultureInfo.InvariantCulture) : 1;
                    _script.Add((frame, new InputEventScreenTouch { Index = 0, Pressed = true, Position = finger }));
                    _script.Add((frame + touchFrames, new InputEventScreenTouch { Index = 0, Pressed = false, Position = finger }));
                    break;
                case "key" or "hold":
                    // hold:Key,frames keeps the key down that many frames (camera panning reads the held keys).
                    var keyParts = parts[2].Split(',');
                    // key:Ctrl+Shift+P presses the key with those modifiers (Ctrl, Shift, Alt) held.
                    var chord = keyParts[0].Split('+');
                    var key = Enum.Parse<Key>(chord[^1], ignoreCase: true);
                    var modifiers = chord[..^1];
                    bool With(string modifier) => modifiers.Contains(modifier, StringComparer.OrdinalIgnoreCase);
                    var held = parts[1] == "hold" && keyParts.Length > 1 ? int.Parse(keyParts[1], CultureInfo.InvariantCulture) : 1;
                    foreach (var (when, pressed) in new[] { (frame, true), (frame + held, false) })
                    {
                        _script.Add((when, new InputEventKey
                        {
                            Keycode = key, PhysicalKeycode = key, Pressed = pressed,
                            CtrlPressed = With("Ctrl"), ShiftPressed = With("Shift"), AltPressed = With("Alt"),
                        }));
                    }

                    break;
                case "text":
                    // text:hello types the letters, one a frame, as the keyboard would (a focused text field takes them).
                    for (var i = 0; i < parts[2].Length; i++)
                    {
                        var letter = parts[2][i];
                        _script.Add((frame + i, new InputEventKey { Unicode = letter, Pressed = true }));
                        _script.Add((frame + i, new InputEventKey { Unicode = letter, Pressed = false }));
                    }

                    break;
                case "wheel":
                    // wheel:up|down,count (camera zoom); wheel:up|down,count,x,y turns it with the pointer at that screen point (over a control).
                    var wheelParts = parts[2].Split(',');
                    var wheel = wheelParts[0].Equals("up", StringComparison.OrdinalIgnoreCase) ? MouseButton.WheelUp : MouseButton.WheelDown;
                    var turns = wheelParts.Length > 1 ? int.Parse(wheelParts[1], CultureInfo.InvariantCulture) : 1;
                    var over = wheelParts.Length > 3
                        ? new Vector2(float.Parse(wheelParts[2], CultureInfo.InvariantCulture), float.Parse(wheelParts[3], CultureInfo.InvariantCulture))
                        : Vector2.Zero;
                    for (var i = 0; i < turns; i++)
                    {
                        _script.Add((frame, new InputEventMouseButton { ButtonIndex = wheel, Pressed = true, Position = over, GlobalPosition = over }));
                        _script.Add((frame, new InputEventMouseButton { ButtonIndex = wheel, Pressed = false, Position = over, GlobalPosition = over }));
                    }

                    break;
                case "press" or "dump" or "choose":
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
        foreach (var (_, kind, argument) in _lookups.Where(l => l.Frame == _frame && l.Kind is "click" or "rclick" or "drag" or "rdrag").ToList())
        {
            Aim(kind, argument);
        }

        foreach (var (_, _, argument) in _lookups.Where(l => l.Frame == _frame && l.Kind == "choose").ToList())
        {
            Choose(argument);
        }

        foreach (var (_, kind, argument) in _lookups.Where(l => l.Frame == _frame && l.Kind is "press" or "dump").ToList())
        {
            if (kind == "dump")
            {
                GD.Print($"autoinput: dump at frame {_frame}");
                foreach (var button in VisibleButtons())
                {
                    var rect = button.GetGlobalRect();
                    GD.Print($"autoinput: button '{Shown(button)}' at {rect.GetCenter().Round()} size {rect.Size.Round()}{(button.Disabled ? " (disabled)" : "")}");
                }

                foreach (var (bar, index) in VisibleTabs(GetTree().Root))
                {
                    GD.Print($"autoinput: tab '{bar.GetTabTitle(index)}' at {(bar.GetGlobalTransformWithCanvas() * bar.GetTabRect(index).GetCenter()).Round()}");
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
            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.WheelUp or MouseButton.WheelDown } turn && turn.Position == Vector2.Zero)
            {
                // no point given: the screen's centre
                turn.Position = turn.GlobalPosition = GetViewport().GetVisibleRect().Size / 2;
            }

            // The script's points are the UI's; the window takes its own pixels, which differ under a UI scale (the editor's preference).
            var window = GetViewport().GetFinalTransform();
            if (@event is InputEventMouse pointer)
            {
                GetViewport().WarpMouse(pointer.Position);
                pointer.Position = pointer.GlobalPosition = window * pointer.Position;
                if (pointer is InputEventMouseMotion motion)
                {
                    motion.Relative = window.BasisXform(motion.Relative);
                }
            }
            else if (@event is InputEventScreenTouch touch)
            {
                touch.Position = window * touch.Position;
            }

            Input.ParseInputEvent(@event);
        }
    }

    /// <summary>
    /// A click, right click or drag whose points name buttons (<c>@label</c>, <c>@label#n</c>) or stay screen points (<c>x,y</c>; a drag's two
    /// points are joined by <c>&gt;</c>): each button is looked up now and the events go at its centre, this frame and the next ones.
    /// </summary>
    private void Aim(string kind, string argument)
    {
        var points = (kind is "drag" or "rdrag" ? argument.Split('>', 2) : [argument]).Select(Point).ToList();
        if (points.Any(p => p is null))
        {
            return;
        }

        var button = kind is "click" or "drag" ? MouseButton.Left : MouseButton.Right;
        var from = points[0]!.Value;
        _script.Add((_frame, new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = from, GlobalPosition = from }));
        if (points.Count == 1)
        {
            // A drop-down opens its list on the press; a release over the list (a long one opens over the button) would pick an item, so it is
            // released at the screen's corner, off the list.
            var release = button == MouseButton.Left && VisibleButtons().OfType<OptionButton>().Any(b => b.GetGlobalRect().HasPoint(from))
                ? Vector2.Zero
                : from;
            _script.Add((_frame + 1, new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = release, GlobalPosition = release }));
            return;
        }

        var to = points[1]!.Value;
        _script.Add((_frame + 1, new InputEventMouseMotion
        {
            Position = to, GlobalPosition = to, Relative = to - from,
            ButtonMask = button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right,
        }));
        _script.Add((_frame + 2, new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = to, GlobalPosition = to }));
    }

    /// <summary>
    /// A point of an aimed event: <c>x,y</c>, or <c>@label</c> (the centre of the first visible button, enabled or not, whose label as
    /// <c>dump</c> shows it is that, ignoring case) or <c>@label#n</c> (the n-th of them in tree order). A button a scrolling box has
    /// scrolled out of sight does not count (<see cref="InView"/>). Null, with an error, when there is no such button.
    /// </summary>
    private Vector2? Point(string point)
    {
        point = point.Trim();
        if (!point.StartsWith('@'))
        {
            var xy = point.Split(',');
            return new Vector2(float.Parse(xy[0], CultureInfo.InvariantCulture), float.Parse(xy[1], CultureInfo.InvariantCulture));
        }

        var label = point[1..];
        var nth = 1;
        var hash = label.LastIndexOf('#');
        if (hash > 0 && int.TryParse(label[(hash + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0)
        {
            (label, nth) = (label[..hash], n);
        }

        var matches = VisibleButtons().Where(b => string.Equals(Shown(b), label, StringComparison.OrdinalIgnoreCase) && InView(b))
            .Select(b => b.GetGlobalRect().GetCenter())
            .Concat(VisibleTabs(GetTree().Root).Where(t => string.Equals(t.Bar.GetTabTitle(t.Index), label, StringComparison.OrdinalIgnoreCase))
                .Select(t => TabCentre(t.Bar, t.Index)))
            .ToList();
        if (matches.Count < nth)
        {
            GD.PushError($"autoinput: no visible button or tab '{point[1..]}' at frame {_frame} ({matches.Count} with that label)");
            return null;
        }

        return matches[nth - 1];
    }

    /// <summary>
    /// The centre of a tab on the screen. A tab the bar has scrolled out of sight (behind its arrows, on a narrow window or at a large UI scale)
    /// is scrolled into it first, as a person would with the arrows.
    /// </summary>
    private static Vector2 TabCentre(TabBar bar, int index)
    {
        bar.EnsureTabVisible(index); // nothing changes for a tab in sight
        return bar.GetGlobalTransformWithCanvas() * bar.GetTabRect(index).GetCenter();
    }

    /// <summary>The tabs of the visible tab bars (tabs are not buttons).</summary>
    private static IEnumerable<(TabBar Bar, int Index)> VisibleTabs(Node node)
    {
        if (node is CanvasItem { Visible: false } or CanvasLayer { Visible: false })
        {
            yield break;
        }

        if (node is TabBar bar && bar.IsVisibleInTree())
        {
            for (var i = 0; i < bar.TabCount; i++)
            {
                yield return (bar, i);
            }
        }

        foreach (var child in node.GetChildren(includeInternal: true))
        {
            foreach (var tab in VisibleTabs(child))
            {
                yield return tab;
            }
        }
    }

    /// <summary>
    /// <c>choose:item</c>: picks the item of that text (ignoring case) in the open drop-down list (the last visible popup menu) with the
    /// keyboard: the item gets the focus, then Enter, so it does not depend on where the list opened.
    /// </summary>
    private void Choose(string item)
    {
        var menu = VisiblePopups(GetTree().Root).LastOrDefault();
        var index = menu is null ? -1 : Enumerable.Range(0, menu.ItemCount).FirstOrDefault(i => string.Equals(menu.GetItemText(i), item, StringComparison.OrdinalIgnoreCase), -1);
        if (menu is null || index < 0)
        {
            GD.PushError($"autoinput: no open list with '{item}' at frame {_frame}");
            return;
        }

        menu.SetFocusedItem(index);
        _script.Add((_frame, new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = true }));
        _script.Add((_frame + 1, new InputEventKey { Keycode = Key.Enter, PhysicalKeycode = Key.Enter, Pressed = false }));
    }

    private static IEnumerable<PopupMenu> VisiblePopups(Node node)
    {
        if (node is PopupMenu { Visible: true } menu)
        {
            yield return menu;
        }

        foreach (var child in node.GetChildren(includeInternal: true))
        {
            foreach (var popup in VisiblePopups(child))
            {
                yield return popup;
            }
        }
    }

    /// <summary>
    /// Whether a click on <paramref name="button"/>'s centre reaches it: no scrolling box round it has scrolled the centre out of sight
    /// (the click would land on whatever is drawn there instead).
    /// </summary>
    private static bool InView(Control button)
    {
        var centre = button.GetGlobalRect().GetCenter();
        for (var parent = button.GetParent(); parent is not null and not Window; parent = parent.GetParent()) // a window has its own coordinates
        {
            if (parent is ScrollContainer scroll && !scroll.GetGlobalRect().HasPoint(centre))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>A button's label on one line, as <c>dump</c> prints it and aimed events name it.</summary>
    private static string Shown(BaseButton button) => Label(button).ReplaceLineEndings(" | ");

    private BaseButton? Find(string text)
    {
        // The last in tree order is drawn on top (a screen over the map's HUD), so it wins.
        var buttons = VisibleButtons().Where(b => !b.Disabled).Reverse().ToList();
        return buttons.FirstOrDefault(b => string.Equals(Label(b), text, StringComparison.OrdinalIgnoreCase))
            ?? buttons.FirstOrDefault(b => Label(b).Contains(text, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// The metadata naming a button whose label is drawn by child controls (a cost row of icons) rather than its text: what its text would
    /// have been, which <c>dump</c> prints and aimed events name.
    /// </summary>
    public const string LabelMeta = "autoinput_label";

    private static string Label(BaseButton button) => button switch
    {
        Button { Text: { Length: > 0 } text } => text,
        _ when button.HasMeta(LabelMeta) => button.GetMeta(LabelMeta).AsString(),
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
