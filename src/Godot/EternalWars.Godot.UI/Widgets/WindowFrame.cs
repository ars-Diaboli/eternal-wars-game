using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Widgets;

/// <summary>
/// A window's frame (ADR-294): the nine-slice bronze frame of <see cref="UiFrames.Window"/> with the faction's accent on its inner line, a title
/// in the heading face on the painted title plate (<see cref="UiFrames.Header"/>; over a divider without it), an optional "×" that raises
/// <see cref="CloseRequested"/>, and the <see cref="Content"/> box below. The town screen's windows and the dialogs are built in it; the frame
/// does not place itself.
/// </summary>
public partial class WindowFrame : PanelContainer
{
    private readonly Label _title = new() { ThemeTypeVariation = UiTheme.TitleLabel, VerticalAlignment = VerticalAlignment.Center };
    private readonly HBoxContainer _header = new();
    private Button? _close;

    public WindowFrame(string title, Color? accent = null, bool closable = true)
    {
        Accent = accent ?? UiTheme.BorderBright;
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", UiSpacing.Row);
        AddChild(box);
        _title.Text = title;
        _header.AddChild(_title);
        if (UiFrames.Header() is { } plate)
        {
            // The title on the painted title plate, which takes the divider's place.
            var plated = new PanelContainer { Name = "TitlePlate" };
            plated.AddThemeStyleboxOverride("panel", plate);
            plated.AddChild(_header);
            box.AddChild(plated);
        }
        else
        {
            box.AddChild(_header);
        }

        if (closable)
        {
            _close = _header.AddCloseButton(() => CloseRequested?.Invoke());
        }

        if (UiFrames.Header() is null)
        {
            box.AddChild(new HSeparator());
        }

        Content.SizeFlagsVertical = SizeFlags.ExpandFill;
        box.AddChild(Content);
    }

    /// <summary>The "×" was pressed.</summary>
    public event Action? CloseRequested;

    /// <summary>The window's body.</summary>
    public VBoxContainer Content { get; } = new();

    /// <summary>The title row: the title, then controls added after it (before the "×").</summary>
    public HBoxContainer Header => _header;

    public string Title
    {
        get => _title.Text;
        set => _title.Text = value;
    }

    /// <summary>The colour of the frame's inner line: the faction's accent (<see cref="UiFrames.Accent"/>).</summary>
    public Color Accent
    {
        get;
        set
        {
            field = value;
            AddThemeStyleboxOverride("panel", UiFrames.Window(value));
        }
    }

    /// <summary>Adds <paramref name="control"/> to the title row, before the "×".</summary>
    public void AddToHeader(Control control)
    {
        _header.AddChild(control);
        if (_close is not null)
        {
            _header.MoveChild(control, _close.GetIndex() - 1); // before the spacer and the ×
        }
    }
}
