using EternalWars.Core.Domain.Editing;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>The user's shortcuts (ADR-275) in the <see cref="EditorSettingsFile"/>: only the commands whose key is not their default.</summary>
public static class ShortcutStore
{
    private const string Section = "shortcuts";

    /// <summary>The kept changes by command id; an empty or unreadable value is no shortcut.</summary>
    public static IReadOnlyDictionary<string, KeyChord?> Read(EditorSettingsFile settings) =>
        settings.Keys(Section).ToDictionary(id => id, id => KeyChord.Parse(settings.Get(Section, id, "").AsString()));

    /// <summary>Keeps <paramref name="current"/>'s changes from <paramref name="defaults"/>, and nothing else.</summary>
    public static void Write(EditorSettingsFile settings, IReadOnlyDictionary<string, KeyChord?> defaults, IReadOnlyDictionary<string, KeyChord?> current) =>
        settings.Replace(Section, ShortcutBindings.Changes(defaults, current).ToDictionary(c => c.Key, c => (Variant)(c.Value?.Text ?? "")));
}

/// <summary>
/// The Preferences' Shortcuts tab (ADR-275): every command with its key. A click on the key waits for the next key press, with its
/// modifiers, and makes it the command's (Escape gives up waiting); "None" takes the key away, "Default" gives back the registered one.
/// Two commands with the same key are shown in red, each naming the other, and a line over the list says which; a filter finds a
/// command by its name or its key.
/// </summary>
public partial class ShortcutEditor : VBoxContainer
{
    private const string Waiting = "Press a key…";

    private readonly Dictionary<string, KeyChord?> _bindings = [];
    private readonly Dictionary<string, Row> _rows = [];
    private readonly Label _conflicts = new() { Name = "Conflicts", AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };
    private readonly LineEdit _filter = new() { Name = "Filter", PlaceholderText = "Find a command or a key", ClearButtonEnabled = true };
    private IReadOnlyDictionary<string, KeyChord?> _defaults = new Dictionary<string, KeyChord?>();
    private string? _waitingFor;

    private sealed record Row(Label Name, Button Key, Button None, Button Default);

    /// <summary>A key changed, or the conflicts did.</summary>
    public event Action? Changed;

    /// <summary>Each command's key as the tab shows it now.</summary>
    public IReadOnlyDictionary<string, KeyChord?> Bindings => _bindings;

    /// <summary>The chords more than one command has now.</summary>
    public IReadOnlyList<(KeyChord Chord, IReadOnlyList<string> Commands)> Conflicts => ShortcutBindings.Conflicts(_bindings);

    /// <summary>Whether a click on a key waits for the next key press (the window then leaves Escape to the tab).</summary>
    public bool IsWaiting => _waitingFor is not null;

    /// <summary>Lists <paramref name="commands"/> (id and name, in order) with their <paramref name="current"/> keys.</summary>
    public void Bind(IReadOnlyList<(string Id, string Name)> commands, IReadOnlyDictionary<string, KeyChord?> defaults, IReadOnlyDictionary<string, KeyChord?> current)
    {
        _defaults = defaults;
        AddChild(_filter);
        _filter.TextChanged += _ => Filter();
        AddChild(_conflicts);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(560, 300), SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        AddChild(scroll.KeepBarRoom());
        var grid = new GridContainer { Name = "Commands", Columns = 4, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", UiSpacing.Row);
        grid.AddThemeConstantOverride("v_separation", UiSpacing.Row / 2);
        scroll.AddChild(grid);
        foreach (var (id, name) in commands)
        {
            var row = new Row(
                new Label { Text = name, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true },
                new Button { Name = $"Key {id}", CustomMinimumSize = new Vector2(150, 0) },
                new Button { Text = "None", TooltipText = "No key runs the command" },
                new Button { Text = "Default", TooltipText = defaults.GetValueOrDefault(id) is { } chord ? $"Back to {chord.Text}" : "Back to no key" });
            row.Key.Pressed += () => Wait(id);
            row.None.Pressed += () => Set(id, null);
            row.Default.Pressed += () => Set(id, _defaults.GetValueOrDefault(id));
            grid.AddChild(row.Name);
            grid.AddChild(row.Key);
            grid.AddChild(row.None);
            grid.AddChild(row.Default);
            _rows[id] = row;
            _bindings[id] = current.GetValueOrDefault(id);
        }

        Refresh();
    }

    /// <summary>The filter takes the keyboard (the tab was opened).</summary>
    public void FocusFilter() => _filter.CallDeferred(Control.MethodName.GrabFocus);

    /// <summary>Every command back to its default key.</summary>
    public void ShowDefaults()
    {
        foreach (var id in _rows.Keys)
        {
            _bindings[id] = _defaults.GetValueOrDefault(id);
        }

        _waitingFor = null;
        Refresh();
    }

    /// <summary>Stops waiting for a key (the tab was left, or the filter or another place was clicked).</summary>
    public void StopWaiting()
    {
        if (_waitingFor is not null)
        {
            _waitingFor = null;
            Refresh();
        }
    }

    /// <summary>
    /// While waiting and shown, the next key press (Escape: none, giving up) is the command's; nothing else gets it. A click anywhere
    /// but on the waiting key stops waiting.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (_waitingFor is not { } id)
        {
            return;
        }

        if (!IsVisibleInTree())
        {
            StopWaiting();
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true } click && !_rows[id].Key.GetGlobalRect().HasPoint(click.Position))
        {
            StopWaiting();
            return;
        }

        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        if (key.Keycode == Key.Escape && !key.CtrlPressed && !key.ShiftPressed && !key.AltPressed)
        {
            GetViewport()?.SetInputAsHandled();
            _waitingFor = null;
            Refresh();
        }
        else if (CommandShortcut.Of(key) is { } shortcut)
        {
            GetViewport()?.SetInputAsHandled();
            Set(id, shortcut.Chord);
        }
    }

    private void Wait(string id)
    {
        _waitingFor = id;
        Refresh();
    }

    private void Set(string id, KeyChord? chord)
    {
        _waitingFor = null;
        _bindings[id] = chord;
        Refresh();
    }

    private void Refresh()
    {
        var conflicts = Conflicts;
        var names = _rows.ToDictionary(r => r.Key, r => r.Value.Name.Text);
        var sharing = conflicts.SelectMany(c => c.Commands.Select(id => (id, c.Commands))).ToDictionary(x => x.id, x => x.Commands);
        foreach (var (id, row) in _rows)
        {
            var chord = _bindings[id];
            row.Key.Text = id == _waitingFor ? Waiting : chord?.Text ?? "—";
            row.Key.TooltipText = id == _waitingFor ? "Press the key, with its modifiers; Escape gives up"
                : sharing.TryGetValue(id, out var others) ? $"Also the key of {string.Join(", ", others.Where(o => o != id).Select(o => names[o]))}"
                : "Click, then press the key";
            if (sharing.ContainsKey(id))
            {
                row.Key.AddThemeColorOverride("font_color", UiTheme.IssueError);
                row.Name.AddThemeColorOverride("font_color", UiTheme.IssueError);
            }
            else
            {
                row.Key.RemoveThemeColorOverride("font_color");
                row.Name.RemoveThemeColorOverride("font_color");
            }

            row.None.Disabled = chord is null;
            row.Default.Disabled = chord == _defaults.GetValueOrDefault(id);
        }

        _conflicts.Text = conflicts.Count == 0
            ? "No two commands share a key."
            : string.Join("\n", conflicts.Select(c => $"{c.Chord.Text} is the key of {string.Join(" and ", c.Commands.Select(id => names[id]))}: give all but one another key."));
        if (conflicts.Count == 0)
        {
            _conflicts.RemoveThemeColorOverride("font_color");
        }
        else
        {
            _conflicts.AddThemeColorOverride("font_color", UiTheme.IssueError);
        }

        Filter();
        Changed?.Invoke();
    }

    /// <summary>Shows the commands whose name or key holds the filter's text (the one waiting for a key always).</summary>
    private void Filter()
    {
        var text = _filter.Text.Trim();
        foreach (var (id, row) in _rows)
        {
            var shown = text.Length == 0 || id == _waitingFor
                || row.Name.Text.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (_bindings[id]?.Text.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false);
            row.Name.Visible = row.Key.Visible = row.None.Visible = row.Default.Visible = shown;
        }
    }
}
