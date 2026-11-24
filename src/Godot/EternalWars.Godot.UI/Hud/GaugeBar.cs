using EternalWars.Core.Domain.Heroes;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// A thin bar for one of a hero's <see cref="Gauge"/>s (ADR-309): movement green, mana blue, experience gold; upright in the hero list, filled
/// from the bottom, or lying beside the selected hero's portrait, filled from the left. It lets the mouse through to the entry it is on.
/// </summary>
public partial class GaugeBar : Control
{
    /// <summary>Movement left.</summary>
    public static readonly Color Movement = new(0.35f, 0.8f, 0.35f);

    /// <summary>Mana.</summary>
    public static readonly Color Mana = new(0.35f, 0.55f, 0.95f);

    /// <summary>Experience towards the next level.</summary>
    public static readonly Color Experience = new(0.85f, 0.68f, 0.36f);

    private static readonly Color Ground = new(0.05f, 0.05f, 0.06f, 0.9f);

    private Gauge _gauge;

    public GaugeBar()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    /// <summary>The bar's colour.</summary>
    public Color Colour { get; init; } = Movement;

    /// <summary>Upright, filled from the bottom; else lying, filled from the left.</summary>
    public bool Upright { get; init; } = true;

    /// <summary>What the bar shows.</summary>
    public Gauge Gauge
    {
        get => _gauge;
        set
        {
            _gauge = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var size = Size;
        DrawRect(new Rect2(Vector2.Zero, size), Ground);
        var fraction = (float)_gauge.Fraction;
        var filled = Upright
            ? new Rect2(0, size.Y * (1 - fraction), size.X, size.Y * fraction)
            : new Rect2(0, 0, size.X * fraction, size.Y);
        DrawRect(filled, Colour);
        DrawRect(new Rect2(Vector2.Zero, size), UiTheme.BorderDim, filled: false, width: 1);
    }
}
