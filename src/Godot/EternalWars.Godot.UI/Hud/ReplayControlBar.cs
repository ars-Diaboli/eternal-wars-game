using EternalWars.Core.Domain.Settings;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>What the replay viewer's bar shows (ADR-164).</summary>
/// <param name="Playing">Playing, not paused.</param>
/// <param name="Status">The line under the buttons: the date, whose turn, how far into the record.</param>
/// <param name="Day">The day being watched.</param>
/// <param name="LastDay">The last day the record reaches.</param>
/// <param name="InBattle">A battle is shown: "Skip to the result" is offered.</param>
/// <param name="CanTakeOver">
/// "Play from here" works now: in a battle, one a person commands or one only the computer fought; on the map, a game not over that a person still
/// plays.
/// </param>
/// <param name="AtEnd">The record has been played to its last command.</param>
/// <param name="FromTheStart">The battle shown is one only the computer fought: taking it over plays it again from its start (ADR-233).</param>
public sealed record ReplayBarState(bool Playing, string Status, int Day, int LastDay, bool InBattle, bool CanTakeOver, bool AtEnd, bool FromTheStart = false);

/// <summary>
/// The replay viewer's controls along the bottom of the screen (ADR-101, ADR-164, ADR-309): the one Play/Pause toggle (Space), the four speeds,
/// next turn, next day, a slider by day with a mark per week ("Day 5 / 34"), whose eyes the map is seen through (the whole map or a player's), "Skip to the result" in a battle, "Play from here" (a battle
/// on by hand, or the game on from the map), and leaving for the main menu. While a battle is played on from the replay (<see cref="TakingOver"/>) only "Back to the replay" is shown. The bar
/// only shows and asks: the app runs the replay.
/// </summary>
public partial class ReplayControlBar : PanelContainer
{
    /// <summary>The height the bar takes at the bottom of the screen, edge included; the HUDs keep it free.</summary>
    public const float ReservedHeight = 84;

    private readonly Button _play = new() { Text = "Play (Space)" };
    private readonly OptionButton _speed = new() { TooltipText = "How fast heroes walk and battles play" };
    private readonly Button _nextTurn = new() { Text = "Next turn", TooltipText = "Jump to the start of the next turn" };
    private readonly Button _nextDay = new() { Text = "Next day", TooltipText = "Jump to the start of the next day" };
    private readonly HSlider _day = new()
    {
        Name = "DaySlider", MinValue = 1, MaxValue = 1, Step = 1, Scrollable = false, CustomMinimumSize = new Vector2(220, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter,
        TooltipText = "The day to go to: drag or click, back or forward; a mark per week",
    };
    private readonly Label _dayText = new() { Name = "DayText", VerticalAlignment = VerticalAlignment.Center };
    private bool _dragging;
    private readonly OptionButton _viewer = new() { TooltipText = "Whose eyes the map is seen through" };
    private readonly Button _skip = new() { Text = "Skip to the result", Visible = false };
    private readonly Button _takeOver = new() { Text = "Play from here" };
    private readonly Button _leave = new() { Text = "Main menu" };
    private readonly Button _back = new() { Text = "Back to the replay", Visible = false };
    private readonly Label _status = new() { ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly HBoxContainer _controls = new();
    private bool _takingOver;

    public event Action? PlayToggled;

    public event Action<AnimationSpeed>? SpeedChanged;

    public event Action? NextTurnRequested;

    public event Action? NextDayRequested;

    public event Action<int>? DayRequested;

    /// <summary>0 = the whole map, else the player at that index − 1 of the names given to <see cref="Bind"/>.</summary>
    public event Action<int>? ViewerChanged;

    public event Action? SkipRequested;

    public event Action? TakeOverRequested;

    public event Action? LeaveRequested;

    public event Action? BackRequested;

    /// <summary>A battle is being played on from the replay: only "Back to the replay" and the status are shown.</summary>
    public bool TakingOver
    {
        get => _takingOver;
        set
        {
            _takingOver = value;
            _controls.Visible = !value;
            _back.Visible = value;
        }
    }

    /// <summary>Builds the bar: the players' names for the view choice, the speed and the view (0 = the whole map) to start with.</summary>
    public void Bind(IReadOnlyList<string> players, AnimationSpeed speed, int viewer = 0)
    {
        Name = "ReplayBar";
        AnchorLeft = 0;
        AnchorRight = 1;
        AnchorTop = 1;
        AnchorBottom = 1;
        OffsetLeft = UiSpacing.ScreenEdge;
        OffsetRight = -148; // clear of the menu button
        OffsetBottom = -UiSpacing.ScreenEdge;
        OffsetTop = -ReservedHeight + UiSpacing.ScreenEdge;
        GrowVertical = GrowDirection.Begin;

        var rows = new VBoxContainer();
        AddChild(rows);
        var line = new HBoxContainer();
        rows.AddChild(line);
        line.AddChild(_controls);
        line.AddChild(_back);
        rows.AddChild(_status);

        foreach (var label in new[] { "Slow", "Normal", "Fast", "Instant" })
        {
            _speed.AddItem(label);
        }

        _speed.Selected = (int)speed;
        _viewer.AddItem("Whole map");
        foreach (var player in players)
        {
            _viewer.AddItem($"{player}'s eyes");
        }

        _viewer.Selected = Math.Clamp(viewer, 0, players.Count);
        _play.WithIcon(UiIcon.Play, UiIcons.WindowSize);
        _nextTurn.WithIcon(UiIcon.NextTurn, UiIcons.WindowSize);
        _nextDay.WithIcon(UiIcon.NextDay, UiIcons.WindowSize);
        _skip.WithIcon(UiIcon.SkipToResult, UiIcons.WindowSize);
        _takeOver.WithIcon(UiIcon.TakeOver, UiIcons.WindowSize);
        foreach (var control in new Control[] { _play, _speed, _nextTurn, _nextDay, _day, _dayText, _viewer, _skip, _takeOver, _leave })
        {
            control.FocusMode = FocusModeEnum.None; // Space and Enter stay the viewer's keys
            _controls.AddChild(control);
        }

        _play.Pressed += () => PlayToggled?.Invoke();
        _speed.ItemSelected += index => SpeedChanged?.Invoke((AnimationSpeed)index);
        _nextTurn.Pressed += () => NextTurnRequested?.Invoke();
        _nextDay.Pressed += () => NextDayRequested?.Invoke();
        // A click or a drag goes to its day when the button is let go (a seek builds the views, this bar too, again); the day shown follows
        // the knob meanwhile, and the replay's own day does not move it.
        _day.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left } button)
            {
                return;
            }

            if (button.Pressed)
            {
                _dragging = true;
            }
            else if (_dragging)
            {
                _dragging = false;
                DayRequested?.Invoke((int)_day.Value);
            }
        };
        _day.ValueChanged += value => ShowDay((int)value);
        _day.Draw += DrawWeekMarks;
        _viewer.ItemSelected += index => ViewerChanged?.Invoke((int)index);
        _skip.Pressed += () => SkipRequested?.Invoke();
        _takeOver.Pressed += () => TakeOverRequested?.Invoke();
        _leave.Pressed += () => LeaveRequested?.Invoke();
        _back.Pressed += () => BackRequested?.Invoke();
    }

    /// <summary>Shows where the replay is and what may be done now.</summary>
    public void Display(ReplayBarState state)
    {
        _play.SetLabel(state.AtEnd ? "Ended" : state.Playing ? "Pause (Space)" : "Play (Space)");
        if (_play.Icon is not null && UiIcons.Get(state.Playing && !state.AtEnd ? UiIcon.Pause : UiIcon.Play, UiIcons.WindowSize) is { } icon)
        {
            _play.Icon = icon; // the one toggle shows what pressing it does
        }

        _play.Disabled = state.AtEnd;
        _nextTurn.Disabled = _nextDay.Disabled = state.AtEnd;
        _day.MaxValue = Math.Max(1, state.LastDay);
        if (!_dragging)
        {
            _day.SetValueNoSignal(state.Day);
            ShowDay(state.Day);
        }

        if (_lastDay != state.LastDay)
        {
            _lastDay = state.LastDay;
            _day.QueueRedraw(); // the week marks
        }

        _skip.Visible = state.InBattle;
        _takeOver.Disabled = !state.CanTakeOver;
        _takeOver.TooltipText = (state.InBattle, state.CanTakeOver) switch
        {
            (true, true) when state.FromTheStart =>
                "Play this battle again from its start, commanding the attacker, as a separate game that is not ranked; the replay stays as it is",
            (true, true) => "Play this battle on by hand, as a separate game that is not ranked; the replay stays as it is",
            (true, false) => "This battle cannot be played from this point (a battlefield width still to choose).",
            (false, true) => "Play the game on from here as your own game, like a loaded save; the replay stays as it is",
            _ => "Only a game that is not over, with a person still in it, can be played on.",
        };
        _status.Text = state.Status;
    }

    private int _lastDay;

    private void ShowDay(int day) => _dayText.Text = $"Day {day} / {Math.Max(1, (int)_day.MaxValue)}";

    /// <summary>A mark under the slider at each week's first day (days 1, 8, 15, ...).</summary>
    private void DrawWeekMarks()
    {
        var last = (int)_day.MaxValue;
        if (last <= 1)
        {
            return;
        }

        var size = _day.Size;
        var grabber = _day.GetThemeIcon("grabber").GetWidth();
        var colour = new Color(1, 1, 1, 0.6f);
        for (var day = 1; day <= last; day += 7)
        {
            var x = (grabber / 2) + ((size.X - grabber) * (day - 1) / (last - 1));
            _day.DrawLine(new Vector2(x, size.Y - 5), new Vector2(x, size.Y), colour, 1);
        }
    }

    /// <summary>Shows a line in the status without changing the controls (the take-over's battle).</summary>
    public void SetStatus(string text) => _status.Text = text;
}
