using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor;

/// <summary>
/// The map's problems (EDIT-1, ADR-250): <see cref="MapValidator"/>'s issues, checked a moment after every change, on a snapshot of the map
/// and off the main thread; the result comes back on the main thread. The two count buttons show and hide the errors and the warnings, the
/// list beside them picks one kind. Selecting an issue reports its tile; activating (double click) an event's issue opens the event. An
/// issue with a clear fix (ADR-265) says so in its row, and the Fix button runs the selected one's as one undo step.
/// </summary>
public partial class ProblemsPanel : VBoxContainer
{
    /// <summary>How long after the last change the map is checked, in seconds: a burst of edits is checked once.</summary>
    private const double Delay = 0.2;

    private readonly Button _errors = new() { ToggleMode = true, ButtonPressed = true, TooltipText = "Show the errors" };
    private readonly Button _warnings = new() { ToggleMode = true, ButtonPressed = true, TooltipText = "Show the warnings" };
    private readonly OptionButton _kinds = new() { TooltipText = "Show one kind of problem" };
    private readonly Button _fix = new() { Name = "Fix", Text = "Fix", Disabled = true, TooltipText = "Select a problem that has a fix" };
    private readonly ItemList _list = new() { SizeFlagsVertical = SizeFlags.ExpandFill, AllowSearch = false }; // a typed key is the app's, not a search
    private readonly global::Godot.Timer _timer = new() { OneShot = true, WaitTime = Delay };
    private readonly List<IssueKind?> _listedKinds = [];
    private List<MapIssue> _shown = [];
    private Func<Func<IReadOnlyList<MapIssue>>> _snapshot = null!;
    private MapEditorSession _editor = null!;
    private IssueKind? _kind;
    private int _check;

    /// <summary>The issues of the last finished check, in the validator's order.</summary>
    public IReadOnlyList<MapIssue> Issues { get; private set; } = [];

    public int Errors => Issues.Count(i => i.Severity == IssueSeverity.Error);

    public int Warnings => Issues.Count(i => i.Severity == IssueSeverity.Warning);

    /// <summary>A check is running or about to: <see cref="Issues"/> is older than the map.</summary>
    public bool IsChecking { get; private set; }

    /// <summary><see cref="Issues"/> or <see cref="IsChecking"/> changed.</summary>
    public event Action? Changed;

    public event Action<MapPosition>? IssueSelected;

    /// <summary>An issue of a map event was activated: the editor opens the event in the "Events" tab.</summary>
    public event Action<string>? EventSelected;

    /// <summary>A fix ran or was refused: what to tell the user.</summary>
    public event Action<string>? FixReported;

    /// <summary>
    /// <paramref name="snapshot"/> runs on the main thread and copies the map; the work it returns validates the copy and runs on a task, so it
    /// must not touch the live scenario.
    /// </summary>
    public void Bind(MapEditorSession editor, Func<Func<IReadOnlyList<MapIssue>>> snapshot)
    {
        _snapshot = snapshot;
        _editor = editor;
        var filters = new HBoxContainer();
        AddChild(filters);
        _errors.Icon = UiIcons.Get(UiIcon.Error);
        _warnings.Icon = UiIcons.Get(UiIcon.Warning);
        filters.AddChild(_errors);
        filters.AddChild(_warnings);
        filters.AddChild(_kinds);
        filters.AddChild(_fix);
        AddChild(_list);
        AddChild(_timer);

        _errors.Toggled += _ => ShowList();
        _warnings.Toggled += _ => ShowList();
        _kinds.ItemSelected += index =>
        {
            _kind = _listedKinds[(int)index];
            ShowList();
        };
        _list.ItemSelected += index =>
        {
            ShowFix();
            if ((int)index < _shown.Count && _shown[(int)index].Position is { } position)
            {
                IssueSelected?.Invoke(position);
            }
        };
        _fix.Pressed += RunFix;
        _list.ItemActivated += index =>
        {
            if ((int)index < _shown.Count && _shown[(int)index].Event is { } name)
            {
                EventSelected?.Invoke(name);
            }
        };
        _timer.Timeout += Check;
        this.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ =>
        {
            _timer.Start(); // again from the start: the check waits for the edits to stop
            SetChecking(true);
        });
        ShowIssues();
        Check();
    }

    /// <summary>
    /// Copies the map now and validates the copy on a task; a result that a newer check has overtaken is dropped. A map that cannot be
    /// copied (the copy is the map's file form, and an edit may leave a state that form refuses) is validated as it is, on the main thread
    /// (<see cref="CheckLive"/>): the problems shown are always the validator's.
    /// </summary>
    private void Check()
    {
        var check = ++_check;
        SetChecking(true);
        Func<IReadOnlyList<MapIssue>> work;
        try
        {
            work = _snapshot();
        }
        catch (Exception)
        {
            Checked(check, null);
            return;
        }

        Task.Run(() =>
        {
            // Nothing may escape the task: an exception lost here would leave the panel checking for ever.
            IReadOnlyList<MapIssue>? issues;
            try
            {
                issues = work();
            }
            catch (Exception)
            {
                issues = null;
            }

            Callable.From(() => Checked(check, issues)).CallDeferred();
        });
    }

    /// <summary>The live scenario's issues, on the main thread; a validator that throws is itself the one problem shown.</summary>
    private IReadOnlyList<MapIssue> CheckLive() => Validate(_editor.Scenario);

    /// <summary><paramref name="state"/>'s issues; a validator that throws is itself the one problem (the report lists it too).</summary>
    public static IReadOnlyList<MapIssue> Validate(Core.Domain.Game.GameState state)
    {
        try
        {
            return MapValidator.Validate(state);
        }
        catch (Exception e)
        {
            return [new MapIssue(IssueSeverity.Error, $"The map could not be checked: {e.Message}")];
        }
    }

    /// <summary>On the main thread: shows a check's result; <paramref name="issues"/> is null when the copy could not be made or checked.</summary>
    private void Checked(int check, IReadOnlyList<MapIssue>? issues)
    {
        if (!IsInstanceValid(this) || check != _check)
        {
            return; // the editor was rebuilt, or the map changed again since
        }

        Issues = issues ?? CheckLive();
        IsChecking = !_timer.IsStopped();
        ShowIssues();
        Changed?.Invoke();
    }

    private void SetChecking(bool checking)
    {
        if (IsChecking != checking)
        {
            IsChecking = checking;
            ShowFix();
            Changed?.Invoke();
        }
    }

    private void ShowIssues()
    {
        _errors.Text = $"Errors: {Errors}";
        _warnings.Text = $"Warnings: {Warnings}";

        // The kinds the map has problems of; the one picked stays listed while it has none, so the filter does not jump back by itself.
        _kinds.Clear();
        _listedKinds.Clear();
        _kinds.AddItem($"All kinds ({Issues.Count})");
        _listedKinds.Add(null);
        foreach (var kind in Enum.GetValues<IssueKind>())
        {
            var count = Issues.Count(i => i.Kind == kind);
            if (count == 0 && kind != _kind)
            {
                continue;
            }

            _kinds.AddItem($"{kind} ({count})");
            _listedKinds.Add(kind);
        }

        _kinds.Selected = _listedKinds.IndexOf(_kind);
        ShowList();
    }

    /// <summary>The issues the filters let through.</summary>
    private void ShowList()
    {
        _shown =
        [
            .. Issues.Where(i => (i.Severity == IssueSeverity.Error ? _errors.ButtonPressed : _warnings.ButtonPressed) && (_kind is null || i.Kind == _kind)),
        ];
        _list.Clear();
        ShowFix();
        if (_shown.Count == 0)
        {
            _list.AddItem(Issues.Count == 0 ? "No problems found." : "No problems of the kinds shown.");
            return;
        }

        foreach (var issue in _shown)
        {
            var index = _list.AddItem($"{issue.Severity}: {issue.Message}{(issue.Position is { } p ? $" {p}" : "")}"
                + $"{(issue.Event is not null ? " (double click: open the event)" : "")}{(issue.Fix is { } fix ? $" (Fix: {fix.Text})" : "")}",
                SeverityIcon(issue.Severity));
            _list.SetItemCustomFgColor(index, issue.Severity == IssueSeverity.Error ? UiTheme.IssueError : UiTheme.IssueWarning);
        }
    }

    /// <summary>The icon before an issue of <paramref name="severity"/> in a list: the error's or the warning's.</summary>
    public static Texture2D? SeverityIcon(IssueSeverity severity) => UiIcons.Get(severity == IssueSeverity.Error ? UiIcon.Error : UiIcon.Warning, 16);

    /// <summary>The issue selected in the list, if one is.</summary>
    private MapIssue? Selected => _list.GetSelectedItems() is [var index] && index < _shown.Count ? _shown[index] : null;

    /// <summary>The Fix button follows the selected issue: on with its fix named in the tooltip, else off; off while the map is being checked.</summary>
    private void ShowFix()
    {
        var fix = Selected?.Fix;
        _fix.Disabled = fix is null || IsChecking;
        _fix.TooltipText = fix is null ? "Select a problem that has a fix" : IsChecking ? "Wait for the map to be checked" : fix.Text;
    }

    /// <summary>Runs the selected issue's fix as one undo step; the map is checked again after it, as after any edit.</summary>
    private void RunFix()
    {
        if (Selected?.Fix is not { } fix)
        {
            return;
        }

        var result = _editor.Fix(fix);
        FixReported?.Invoke(result switch
        {
            EditResult.Ok => $"Fixed: {fix.Text}",
            EditResult.InvalidTarget => "Not fixed: the map changed since it was checked; the problems are checked again.",
            _ => $"Not fixed: {fix.Text} was refused ({result}).",
        });
        if (result != EditResult.Ok)
        {
            Check(); // a refusal changed nothing, so no edit starts the check
        }

        _fix.Disabled = true; // until the next check lists the issues again
    }
}
