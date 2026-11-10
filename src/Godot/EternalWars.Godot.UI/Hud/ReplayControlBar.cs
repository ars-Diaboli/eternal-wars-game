using EternalWars.Core.Domain.Settings;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>What the replay viewer's bar shows (ADR-164).</summary>
/// <param name="Playing">Playing, not paused.</param>
/// <param name="Status">The line under the buttons: the date, whose turn, how far into the record.</param>
/// <param name="Day">The day being watched.</param>
/// <param name="LastDay">The last day the record reaches.</param>
/// <param name="InBattle">A battle is shown: "Skip to the result" is offered.</param>
/// <param name="CanTakeOver">"Play from here" is offered (a battle a person commands).</param>
/// <param name="AtEnd">The record has been played to its last command.</param>
public sealed record ReplayBarState(bool Playing, string Status, int Day, int LastDay, bool InBattle, bool CanTakeOver, bool AtEnd);

/// <summary>
/// The replay viewer's controls along the bottom of the screen (ADR-101, ADR-164): play and pause (Space), the four speeds, next turn, next day,
/// a day to go to, whose eyes the map is seen through (the whole map or a player's), "Skip to the result" and "Play from here" in a battle, and
/// leaving for the main menu. While a battle is played on from the replay (<see cref="TakingOver"/>) only "Back to the replay" is shown. The bar
/// only shows and asks: the app runs the replay.
/// </summary>
public partial class ReplayControlBar : PanelContainer
{
    /// <summary>The height the bar takes at the bottom of the screen, edge included; the HUDs keep it free.</summary>
    public const float ReservedHeight = 84;

    private readonly Button _play = new() { Text = "Play (Space)", CustomMinimumSize = new Vector2(120, 0) };
    private readonly OptionButton _speed = new() { TooltipText = "How fast heroes walk and battles play" };
    private readonly Button _nextTurn = new() { Text = "Next turn", TooltipText = "Jump to the start of the next turn" };
    private readonly Button _nextDay = new() { Text = "Next day", TooltipText = "Jump to the start of the next day" };
    private readonly SpinBox _day = new() { MinValue = 1, Prefix = "Day", TooltipText = "The day to go to" };
    private readonly Button _goToDay = new() { Text = "Go", TooltipText = "Jump to the start of this day, back or forward" };
    private readonly OptionButton _viewer = new() { TooltipText = "Whose eyes the map is seen through" };
    private readonly Button _skip = new() { Text = "Skip to the result", Visible = false };
    private readonly Button _takeOver = new() { Text = "Play from here", Visible = false, TooltipText = "Play this battle on by hand, as a separate game that is not ranked; the replay stays as it is" };
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
        foreach (var control in new Control[] { _play, _speed, _nextTurn, _nextDay, _day, _goToDay, _viewer, _skip, _takeOver, _leave })
        {
            control.FocusMode = FocusModeEnum.None; // Space and Enter stay the viewer's keys
            _controls.AddChild(control);
        }

        _play.Pressed += () => PlayToggled?.Invoke();
        _speed.ItemSelected += index => SpeedChanged?.Invoke((AnimationSpeed)index);
        _nextTurn.Pressed += () => NextTurnRequested?.Invoke();
        _nextDay.Pressed += () => NextDayRequested?.Invoke();
        _goToDay.Pressed += () => DayRequested?.Invoke((int)_day.Value);
        _day.GetLineEdit().TextSubmitted += _ => DayRequested?.Invoke((int)_day.Value);
        _viewer.ItemSelected += index => ViewerChanged?.Invoke((int)index);
        _skip.Pressed += () => SkipRequested?.Invoke();
        _takeOver.Pressed += () => TakeOverRequested?.Invoke();
        _leave.Pressed += () => LeaveRequested?.Invoke();
        _back.Pressed += () => BackRequested?.Invoke();
    }

    /// <summary>Shows where the replay is and what may be done now.</summary>
    public void Display(ReplayBarState state)
    {
        _play.Text = state.AtEnd ? "Ended" : state.Playing ? "Pause (Space)" : "Play (Space)";
        _play.Disabled = state.AtEnd;
        _nextTurn.Disabled = _nextDay.Disabled = state.AtEnd;
        _day.MaxValue = state.LastDay;
        if (!_day.GetLineEdit().HasFocus())
        {
            _day.SetValueNoSignal(state.Day);
        }

        _skip.Visible = state.InBattle;
        _takeOver.Visible = state.InBattle;
        _takeOver.Disabled = !state.CanTakeOver;
        _takeOver.TooltipText = state.CanTakeOver
            ? "Play this battle on by hand, as a separate game that is not ranked; the replay stays as it is"
            : "Only a battle a person commands can be played on (the computer plays its own).";
        _status.Text = state.Status;
    }

    /// <summary>Shows a line in the status without changing the controls (the take-over's battle).</summary>
    public void SetStatus(string text) => _status.Text = text;
}
