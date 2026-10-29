using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The turn timer's clock (ADR-091), at the top of the screen over the map and the battle scene. The app runs the clock
/// (<see cref="Core.Domain.Turns.TurnTimer"/>) and shows it here every frame; the last minute is red, and a warning shows for a few seconds
/// at 1 minute, 30, 15 and 5 seconds left.
/// </summary>
public partial class TurnTimerPanel : PanelContainer
{
    private const double WarningShown = 3;

    private readonly Label _clock = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private string? _warning;
    private double _warningLeft;

    public void Bind()
    {
        Name = "TurnTimer";
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        OffsetLeft = -150;
        OffsetRight = 150;
        OffsetTop = 8;
        GrowHorizontal = GrowDirection.Both;
        MouseFilter = MouseFilterEnum.Ignore;
        Visible = false;
        AddChild(_clock);
    }

    /// <summary>A warning passed: <paramref name="secondsLeft"/> left.</summary>
    public void Warn(int secondsLeft)
    {
        _warning = secondsLeft >= 60 ? $"{secondsLeft / 60} minute left!" : $"{secondsLeft} seconds left!";
        _warningLeft = WarningShown;
    }

    /// <summary>The time left (null hides the clock), whether the clock stands still, and the frame's time for the warning's display.</summary>
    public void ShowTime(double? secondsLeft, bool stopped, bool up, double delta)
    {
        Visible = secondsLeft is not null;
        if (secondsLeft is not { } left)
        {
            _warning = null;
            return;
        }

        _warningLeft -= delta;
        var time = TimeSpan.FromSeconds(Math.Ceiling(left));
        _clock.Text = up ? "Time is up: the turn ends as soon as it can."
            : stopped ? $"Turn time {time:m\\:ss} (stopped during the battle)"
            : _warningLeft > 0 && _warning is not null ? $"Turn time {time:m\\:ss}  {_warning}"
            : $"Turn time {time:m\\:ss}";
        _clock.AddThemeColorOverride("font_color", left <= 60 && !stopped ? Colors.OrangeRed : Colors.White);
    }
}
