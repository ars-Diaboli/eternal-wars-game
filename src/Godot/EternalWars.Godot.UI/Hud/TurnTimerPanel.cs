using EternalWars.Core.Domain.Turns;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// The turn timer's clock (ADR-091, ADR-309), at the top centre over the map and the battle scene, just under the turn banner
/// (<see cref="TurnBannerPanel"/>): a pill with the time left, white above a minute and red from it down (<see cref="TurnClockText"/>), and
/// under it a red ribbon with the warning for a few seconds at 1 minute, 30, 15 and 5 seconds left. The app runs the clock
/// (<see cref="TurnTimer"/>) and shows it here every frame.
/// </summary>
public partial class TurnTimerPanel : VBoxContainer
{
    private const double WarningShown = 3;

    private static readonly StringName FontColor = "font_color";

    private static readonly Color Red = new(0.95f, 0.3f, 0.25f);

    private readonly PanelContainer _pill = new() { Name = "Pill", MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
    private readonly Label _clock = new() { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
    private readonly PanelContainer _ribbon = new() { Name = "Warning", MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
    private readonly Label _warningText = new() { HorizontalAlignment = HorizontalAlignment.Center, MouseFilter = MouseFilterEnum.Ignore };
    private string? _warning;
    private double _warningLeft;

    /// <summary>What the clock last showed: it is redrawn only when the second, the state or the warning changes, not every frame.</summary>
    private (double Seconds, bool Stopped, bool Up, string? Warning, bool Red)? _shown;

    /// <summary>What the pill says now; empty while the clock is hidden.</summary>
    public string Pill => Visible ? _clock.Text : "";

    /// <summary>The warning on the ribbon now; empty when there is none.</summary>
    public string Warning => Visible && _ribbon.Visible ? _warningText.Text : "";

    public void Bind()
    {
        Name = "TurnTimer";
        AnchorLeft = 0.5f;
        AnchorRight = 0.5f;
        OffsetLeft = -200;
        OffsetRight = 200;
        OffsetTop = TurnBannerPanel.Bottom + UiSpacing.Cell;
        GrowHorizontal = GrowDirection.Both;
        MouseFilter = MouseFilterEnum.Ignore;
        AddThemeConstantOverride("separation", UiSpacing.Cell);
        Visible = false;

        var pill = UiTheme.Box(UiTheme.Background, UiTheme.Border, UiSpacing.Cell, borderWidth: 2);
        pill.ContentMarginLeft = pill.ContentMarginRight = UiSpacing.Section;
        pill.SetCornerRadiusAll(14);
        _pill.AddThemeStyleboxOverride("panel", pill);
        _clock.AddThemeFontSizeOverride("font_size", 18);
        _pill.AddChild(_clock);
        AddChild(_pill);

        var ribbon = UiTheme.Box(new Color(0.55f, 0.1f, 0.08f, 0.95f), Red, UiSpacing.Cell, borderWidth: 1);
        ribbon.ContentMarginLeft = ribbon.ContentMarginRight = UiSpacing.Section;
        _ribbon.AddThemeStyleboxOverride("panel", ribbon);
        _ribbon.AddChild(_warningText);
        _ribbon.Visible = false;
        AddChild(_ribbon);
    }

    /// <summary>A warning passed: <paramref name="secondsLeft"/> left.</summary>
    public void Warn(int secondsLeft)
    {
        _warning = TurnClockText.Warning(secondsLeft);
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
        var seconds = Math.Ceiling(left);
        var warning = _warningLeft > 0 && !up ? _warning : null;
        var red = TurnClockText.IsRed(left, stopped, up);
        var shown = (seconds, stopped, up, warning, red);
        if (_shown == shown)
        {
            return;
        }

        _shown = shown;
        _clock.Text = TurnClockText.Pill(left, stopped, up);
        _clock.AddThemeColorOverride(FontColor, red ? Red : Colors.White);
        _ribbon.Visible = warning is not null;
        _warningText.Text = warning ?? "";
    }
}
