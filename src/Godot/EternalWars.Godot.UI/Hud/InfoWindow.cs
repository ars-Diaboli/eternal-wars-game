using EternalWars.Core.Domain.Info;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The right-click information window (ADR-118, UI-3; ADR-154): one <see cref="InfoSheet"/> in a panel beside the pointer, above every screen.
/// A right click opens it; holding the button and letting go closes it again (read while held), a quick click leaves it open until the next
/// click or key. A left click that closes it does nothing else; a right click goes on to open the next window. <see cref="Detailed"/> is the "Detailed tooltips" setting the sheets are built
/// with. The app adds one; controls reach it through <see cref="InfoWindowExtensions.WithInfo{T}"/>.
/// </summary>
public partial class InfoWindow : CanvasLayer
{
    /// <summary>A right button held at least this long closes the window when it is let go.</summary>
    private const ulong HoldMilliseconds = 300;

    private const float Width = 440;

    private readonly PanelContainer _panel = new();
    private readonly Label _title = new();
    private readonly Label _kind = new() { Modulate = new Color(0.75f, 0.75f, 0.8f) };
    private readonly VBoxContainer _lines = new();
    private ulong _openedAt;

    /// <summary>The window in the tree, if any.</summary>
    public static InfoWindow? Current { get; private set; }

    /// <summary>The "Detailed tooltips" setting: the sheets add their formulas and numbers.</summary>
    public bool Detailed { get; set; }

    /// <summary>The sheet shown, if the window is open.</summary>
    public InfoSheet? Shown { get; private set; }

    public InfoWindow()
    {
        Layer = 50;
        _panel.UseOpaqueBackground();
        _panel.MouseFilter = Control.MouseFilterEnum.Ignore;
        var box = new VBoxContainer();
        _panel.AddChild(box);
        _title.AddThemeFontSizeOverride("font_size", 20);
        box.AddChild(_title);
        box.AddChild(_kind);
        box.AddChild(new HSeparator());
        box.AddChild(_lines);
        _panel.Visible = false;
        AddChild(_panel);
    }

    public override void _EnterTree() => Current = this;

    public override void _ExitTree()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>Shows <paramref name="sheet"/> beside <paramref name="at"/> (the pointer when null), kept on the screen.</summary>
    public void Open(InfoSheet sheet, Vector2? at = null)
    {
        Shown = sheet;
        _title.Text = sheet.Title;
        _kind.Text = sheet.Kind;
        foreach (var child in _lines.GetChildren())
        {
            _lines.RemoveChild(child); // out now, so the panel's size is the new sheet's
            child.QueueFree();
        }

        foreach (var line in sheet.Lines)
        {
            _lines.AddChild(new Label { Text = line, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(Width, 0) });
        }

        _panel.Visible = true;
        _openedAt = Time.GetTicksMsec();
        var pointer = at ?? _panel.GetViewport().GetMousePosition();
        Place(pointer);
        Callable.From(() => Place(pointer)).CallDeferred(); // the wrapped labels know their height a frame later
    }

    public void Close()
    {
        Shown = null;
        _panel.Visible = false;
    }

    private void Place(Vector2 pointer)
    {
        _panel.Size = Vector2.Zero; // shrink to the content
        var screen = _panel.GetViewportRect().Size;
        var size = _panel.GetCombinedMinimumSize();
        var x = pointer.X + 16 + size.X > screen.X ? pointer.X - 16 - size.X : pointer.X + 16;
        var y = Math.Clamp(pointer.Y - (size.Y / 2), 8, Math.Max(8, screen.Y - size.Y - 8));
        _panel.Position = new Vector2(Math.Clamp(x, 8, Math.Max(8, screen.X - size.X - 8)), y);
    }

    public override void _Input(InputEvent @event)
    {
        if (Shown is null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { Pressed: false, ButtonIndex: MouseButton.Right } when Time.GetTicksMsec() - _openedAt >= HoldMilliseconds:
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Middle }:
            case InputEventKey { Pressed: true, Keycode: Key.Escape }:
                Close();
                GetViewport().SetInputAsHandled();
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
            case InputEventKey { Pressed: true }:
                Close(); // another right click goes on to open the next window
                break;
        }
    }
}

/// <summary>Gives a control its information window (ADR-154).</summary>
public static class InfoWindowExtensions
{
    /// <summary>
    /// A right click on the control opens <paramref name="sheet"/>'s window (built with the "Detailed tooltips" setting; null shows nothing).
    /// A label is made to take the click.
    /// </summary>
    public static T WithInfo<T>(this T control, Func<bool, InfoSheet?> sheet) where T : Control
    {
        if (control.MouseFilter == Control.MouseFilterEnum.Ignore)
        {
            control.MouseFilter = Control.MouseFilterEnum.Stop;
        }

        control.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right } && InfoWindow.Current is { } window && sheet(window.Detailed) is { } shown)
            {
                window.Open(shown);
                control.AcceptEvent();
            }
        };
        return control;
    }
}
