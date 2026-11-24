using Godot;

namespace EternalWars.Godot.UI.Widgets;

/// <summary>The kinds of <see cref="BevelFrame"/>: a panel standing out, a sunken slot, a raised button.</summary>
public enum BevelKind
{
    Panel,
    Slot,
    Button,
}

/// <summary>
/// A frame drawn by a shader behind its parent control (<c>shaders/ui_bevel.gdshader</c>, ADR-310): a dark fill and a thin rim lit like a
/// metal bevel, brightest where it faces the light and near the lit corners, a little worn, with a drop shadow. A panel stands out, a slot
/// is sunk in, a button bulges slightly. It is a Node2D, so a container leaves it alone; it follows its parent's size and draws past it by
/// the room its shadow and glow need. Behind a button it follows the button's state at each redraw: brighter under the pointer, sunk while
/// held, dim when disabled, and a toggle button that is on takes the <see cref="SelectedTint"/> (rim and glow). Every frame's fill is as
/// opaque as <see cref="Opacity"/> (the interface setting), a button's never under <see cref="ButtonMinOpacity"/>.
/// </summary>
public partial class BevelFrame : Node2D
{
    private const string ShaderPath = "res://shared/shaders/ui_bevel.gdshader";

    /// <summary>A button's least opacity, so its icon keeps its ground over the map.</summary>
    public const float ButtonMinOpacity = 0.55f;

    private static Shader? _shader;
    private static float _opacity = 1f;

    private readonly ShaderMaterial _material;
    private Control? _host;
    private float _margin;

    // The parameters the room round the frame follows (a shader parameter left at its default reads back as null).
    private float _shadow = 14f;
    private Vector2 _shadowOffset = new(0, 4);
    private float _glow;

    // The fill at full opacity, top and bottom; the rim's light and width at rest; the rim's tint.
    private Color _fillTop = new(0.068f, 0.07f, 0.076f, 0.96f);
    private Color _fillBottom = new(0.052f, 0.054f, 0.06f, 0.96f);
    private float _highlight = 1f;
    private float _rimWidth = 1.5f;
    private Color? _tint;
    private float _tintAmount;

    public BevelFrame() : this(BevelKind.Panel)
    {
    }

    public BevelFrame(BevelKind kind)
    {
        _shader ??= GD.Load<Shader>(ShaderPath);
        _material = new ShaderMaterial { Shader = _shader };
        Material = _material;
        ShowBehindParent = true;
        Kind = kind;
        Apply(kind);
    }

    /// <summary>The frames' opacity changed (<see cref="Opacity"/>).</summary>
    private static event Action? OpacityChanged;

    /// <summary>How opaque every frame's fill is, 0 to 1 (the interface setting); the rim and the shadow stay.</summary>
    public static float Opacity
    {
        get => _opacity;
        set
        {
            value = Math.Clamp(value, 0f, 1f);
            if (Math.Abs(_opacity - value) < 0.001f)
            {
                return;
            }

            _opacity = value;
            OpacityChanged?.Invoke();
        }
    }

    public BevelKind Kind { get; }

    /// <summary>The rim's and glow's colour while the toggle button it lies behind is on (a selected hero: the player's colour).</summary>
    public Color? SelectedTint { get; set; }

    /// <summary>Shows the <see cref="SelectedTint"/> whatever the host's state (a picked row that is no toggle button: the style sample's).</summary>
    public bool Selected
    {
        get => _selected;
        set
        {
            _selected = value;
            Refresh();
        }
    }

    private bool _selected;

    /// <summary>A frame of <paramref name="kind"/> behind <paramref name="host"/>.</summary>
    public static BevelFrame Behind(Control host, BevelKind kind)
    {
        var frame = new BevelFrame(kind);
        host.AddChild(frame);
        return frame;
    }

    /// <summary>The rim in <paramref name="colour"/> (<paramref name="amount"/> of it), keeping its light and shade: a town's owner, the turn's player.</summary>
    public void Tint(Color? colour, float amount = 0.7f)
    {
        _tint = colour;
        _tintAmount = amount;
        Refresh();
    }

    /// <summary>Sets one of the shader's parameters (the style sample's sliders).</summary>
    public void SetParameter(string name, Variant value)
    {
        _material.SetShaderParameter(name, value);
        switch (name)
        {
            case "shadow_size":
                _shadow = value.AsSingle();
                break;
            case "shadow_offset":
                _shadowOffset = value.AsVector2();
                break;
            case "glow_size":
                _glow = value.AsSingle();
                break;
            case "highlight":
                _highlight = value.AsSingle();
                break;
            case "rim_width":
                _rimWidth = value.AsSingle();
                break;
        }

        UpdateMargin();
    }

    /// <summary>A uniform fill in place of the kind's gradient and grain (the style sample's variants).</summary>
    public void SetFill(Color fill)
    {
        _fillTop = _fillBottom = fill;
        _material.SetShaderParameter("grain", 0f);
        Refresh();
    }

    public override void _EnterTree()
    {
        _host = GetParent() as Control;
        if (_host is not null)
        {
            _host.Resized += OnResized;
            _host.Draw += Refresh; // a button's state shows at its redraw (hover, press, toggle, disable)
        }

        OpacityChanged += Refresh;
        OnResized();
        Refresh();
    }

    public override void _ExitTree()
    {
        OpacityChanged -= Refresh;
        if (_host is not null)
        {
            _host.Resized -= OnResized;
            _host.Draw -= Refresh;
        }

        _host = null;
    }

    public override void _Draw()
    {
        if (_host is null)
        {
            return;
        }

        var size = _host.Size;
        DrawRect(new Rect2(-_margin, -_margin, size.X + (2 * _margin), size.Y + (2 * _margin)), Colors.White);
    }

    private void OnResized()
    {
        if (_host is not null)
        {
            _material.SetShaderParameter("size", _host.Size);
        }

        QueueRedraw();
    }

    /// <summary>Puts the fill's opacity, the tint and the host button's state into the shader.</summary>
    private void Refresh()
    {
        var button = _host as BaseButton;
        var mode = button?.GetDrawMode() ?? BaseButton.DrawMode.Normal;
        var toggle = button is { ToggleMode: true };
        var on = toggle && button!.ButtonPressed;
        var disabled = mode == BaseButton.DrawMode.Disabled;
        var hover = !disabled && mode is BaseButton.DrawMode.Hover or BaseButton.DrawMode.HoverPressed;
        var held = Kind == BevelKind.Button && !toggle && mode is BaseButton.DrawMode.Pressed or BaseButton.DrawMode.HoverPressed;

        var opacity = Kind == BevelKind.Button ? Math.Max(_opacity, ButtonMinOpacity) : _opacity;
        var lift = hover ? 0.035f : 0f;
        _material.SetShaderParameter("fill_top", Lighter(_fillTop, lift, opacity));
        _material.SetShaderParameter("fill_bottom", Lighter(_fillBottom, lift, opacity));
        _material.SetShaderParameter("highlight", _highlight * (disabled ? 0.55f : hover ? 1.4f : 1f));
        _material.SetShaderParameter("raised", Kind == BevelKind.Slot || held ? -1f : 1f);

        var selected = (on || _selected) && SelectedTint is not null;
        var tint = selected ? SelectedTint : _tint;
        _material.SetShaderParameter("rim_tint", tint is { } t ? t with { A = selected ? 1f : _tintAmount } : new Color(0, 0, 0, 0));
        _material.SetShaderParameter("rim_width", selected ? Math.Max(_rimWidth, 2.2f) : _rimWidth);
        _material.SetShaderParameter("glow_color", selected ? SelectedTint!.Value with { A = 0.75f } : new Color(0, 0, 0, 0));
        if (selected && _glow < 12f)
        {
            _glow = 12f;
            _material.SetShaderParameter("glow_size", _glow);
            UpdateMargin();
        }
    }

    private static Color Lighter(Color colour, float lift, float opacity) =>
        new(colour.R + lift, colour.G + lift, colour.B + lift, colour.A * opacity);

    private void Apply(BevelKind kind)
    {
        var m = _material;
        m.SetShaderParameter("seed", (float)GD.RandRange(0.0, 100.0));
        m.SetShaderParameter("radius", 6f);
        switch (kind)
        {
            case BevelKind.Panel:
                _rimWidth = 1.5f;
                break;
            case BevelKind.Button:
                _rimWidth = 1.3f;
                _fillTop = new Color(0.085f, 0.088f, 0.096f, 0.97f);
                _fillBottom = new Color(0.066f, 0.068f, 0.075f, 0.97f);
                SetParameter("shadow_size", 7f);
                SetParameter("shadow_offset", new Vector2(0, 2));
                m.SetShaderParameter("corner_falloff", 30f);
                m.SetShaderParameter("bevel_width", 4f);
                m.SetShaderParameter("bevel_strength", 0.04f);
                break;
            case BevelKind.Slot:
                _rimWidth = 1.2f;
                _fillTop = new Color(0.038f, 0.04f, 0.045f, 0.92f);
                _fillBottom = new Color(0.05f, 0.052f, 0.058f, 0.92f);
                m.SetShaderParameter("rim_lit", new Color(0.55f, 0.55f, 0.58f));
                m.SetShaderParameter("inner_shadow_strength", 0.6f);
                SetParameter("shadow_size", 0f);
                m.SetShaderParameter("shadow_color", new Color(0, 0, 0, 0));
                m.SetShaderParameter("corner_falloff", 40f);
                break;
        }

        UpdateMargin();
    }

    private void UpdateMargin()
    {
        _margin = Math.Max(_shadow + Math.Max(Math.Abs(_shadowOffset.X), Math.Abs(_shadowOffset.Y)), _glow) + 2;
        QueueRedraw();
    }
}
