using EternalWars.Core.Domain.Info;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The small card beside the pointer over a map object or a hero (ADR-309): its name, its owner and what it gives
/// (<see cref="MapDescription.CardLines"/>), no picture. It shows once the pointer has rested on the thing for <see cref="Delay"/>, follows
/// the pointer, stays on the screen, lets the mouse through, and goes as the pointer leaves the thing or comes over a HUD control. The bare
/// ground has none. Right click still opens the full information window (ADR-118, ADR-154).
/// </summary>
public partial class MapHoverCard : PanelContainer
{
    /// <summary>How long the pointer rests on a thing before its card shows, in seconds.</summary>
    public const double Delay = 0.35;

    /// <summary>The card's distance from the pointer.</summary>
    private static readonly Vector2 Offset = new(18, 18);

    private readonly VBoxContainer _rows = new() { MouseFilter = MouseFilterEnum.Ignore };
    private MapDescription? _pointed;
    private MapDescription? _left; // put away while the pointer stayed on it: it comes back as the pointer moves there again
    private Vector2 _pointer;
    private double _rested;

    public MapHoverCard()
    {
        Name = "MapHoverCard";
        TopLevel = true;
        Visible = false;
        MouseFilter = MouseFilterEnum.Ignore;
        UiFrames.Bevel(this, BevelKind.Panel, UiFrames.Tooltip(UiSpacing.Gap), UiSpacing.Gap); // the HUD's panel, a tooltip's margins (ADR-310)
        _rows.AddThemeConstantOverride("separation", UiSpacing.Cell);
        AddChild(_rows);
    }

    /// <summary>The lines on the card now; empty while it is hidden.</summary>
    public IReadOnlyList<string> Lines => Visible ? _pointed?.CardLines ?? [] : [];

    /// <summary>The pointer came over <paramref name="pointed"/> (null, or the bare ground: nothing to show); its card shows after the delay.</summary>
    public void Point(MapDescription? pointed, Vector2 pointer)
    {
        _pointer = pointer;
        if (pointed is not null && pointed == _pointed)
        {
            Follow(pointer);
            return;
        }

        _left = null;
        _pointed = pointed is { HasCard: true } ? pointed : null;
        _rested = 0;
        Visible = false;
        _rows.ClearChildren(detach: true);
        if (_pointed is null)
        {
            return;
        }

        var lines = _pointed.CardLines;
        for (var i = 0; i < lines.Count; i++)
        {
            var label = new Label { Text = lines[i], MouseFilter = MouseFilterEnum.Ignore };
            if (i == 0)
            {
                label.AddThemeFontSizeOverride("font_size", 18); // the name
            }
            else
            {
                label.ThemeTypeVariation = UiTheme.QuietLabel;
            }

            _rows.AddChild(label);
        }
    }

    /// <summary>The pointer moved on the same thing: the card goes with it, or comes back after <see cref="Leave"/>.</summary>
    public void Follow(Vector2 pointer)
    {
        if (_pointed is null && _left is not null)
        {
            Point(_left, pointer);
            return;
        }

        _pointer = pointer;
        if (Visible)
        {
            Place();
        }
    }

    /// <summary>Hides the card until the pointer moves again (on the same thing) or comes over something else.</summary>
    public void Leave()
    {
        var left = _pointed ?? _left;
        Point(null, _pointer);
        _left = left;
    }

    public override void _Process(double delta)
    {
        if (_pointed is null)
        {
            return;
        }

        if (GetViewport().GuiGetHoveredControl() is not null)
        {
            Leave(); // over a HUD control: the map is not under the pointer
            return;
        }

        if (!Visible && (_rested += delta) >= Delay)
        {
            Visible = true;
            ResetSize();
            Place();
        }
    }

    /// <summary>Beside the pointer, below and to the right, or wherever it fits on the screen.</summary>
    private void Place()
    {
        var size = GetCombinedMinimumSize();
        var screen = GetViewportRect().Size;
        var x = _pointer.X + Offset.X + size.X <= screen.X ? _pointer.X + Offset.X : _pointer.X - Offset.X - size.X;
        var y = _pointer.Y + Offset.Y + size.Y <= screen.Y ? _pointer.Y + Offset.Y : _pointer.Y - Offset.Y - size.Y;
        GlobalPosition = new Vector2(Math.Max(0, x), Math.Max(0, y));
    }
}
