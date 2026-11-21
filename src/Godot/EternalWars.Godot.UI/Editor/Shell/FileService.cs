using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// What the <see cref="FileService"/> needs of the app: <paramref name="Open"/> shows a scenario as the map being edited and returns its
/// editing session; <paramref name="Say"/> tells the user something.
/// </summary>
public sealed record FileServiceHost(Func<GameState, MapEditorSession> Open, Action<string> Say);

/// <summary>
/// The map files as the <see cref="FileService"/> uses them; the app implements it with the map format (the UI library does not read
/// or write formats itself). Reading and writing throw <see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>,
/// <see cref="FormatException"/> or <see cref="NotSupportedException"/> when they cannot be done.
/// </summary>
public interface IMapFiles
{
    /// <summary>A map file's extension, with its dot.</summary>
    string Extension { get; }

    /// <summary>Where the file dialogs start.</summary>
    string Folder { get; }

    GameState Load(string path);

    void Save(GameState map, string path);

    /// <summary>Starts the editor's session; returns the autosave to offer when the last session did not close, else null.</summary>
    string? BeginSession();

    /// <summary>Nothing unsaved is left behind from before now: autosaves made until now are not offered any more.</summary>
    void MarkSafe();

    /// <summary>The editor closes properly.</summary>
    void EndSession();

    /// <summary>Copies <paramref name="map"/> now; the work returned writes the copy as an autosave and may run on a task.</summary>
    Action Autosave(GameState map);

    /// <summary>When the autosave at <paramref name="path"/> was made, in local time.</summary>
    DateTime AutosaveTime(string path);
}

/// <summary>
/// The map editor's files (EDIT-1, ADR-250): which file the map is, Open, Save and Save as with a file dialog (Godot's
/// <see cref="FileDialog"/>, the system's own where there is one; it asks itself before a file is overwritten), the recent files (kept in
/// the <see cref="EditorSettingsFile"/>), the window's title with an asterisk over unsaved work, the question before unsaved work is left
/// (another map, closing the window), the autosave every <see cref="AutosaveMinutes"/> while there is unsaved work, and the offer to
/// restore the newest autosave after a session that did not close. The files themselves are the app's <see cref="IMapFiles"/>. Every map the editor shows goes through
/// here, so the service always knows the map's session.
/// </summary>
public partial class FileService : Node
{
    private const string Section = "files";
    private const string RecentKey = "recent";
    private const string AppTitle = "Eternal Wars Map Editor";

    private readonly CanvasLayer _layer = new() { Name = "Questions", Layer = 50 };
    private readonly global::Godot.Timer _autosave = new() { Name = "Autosave", WaitTime = 120, Autostart = true };
    private FileServiceHost _host = null!;
    private EditorSettingsFile _settings = null!;
    private IMapFiles _maps = null!;
    private bool _systemDialogs;
    private MapEditorSession _editor = null!;
    private Node? _session;
    private FileDialog? _dialog;

    /// <summary>What <see cref="SaveState"/> says while the map has no unsaved work.</summary>
    private string _clean = "";

    /// <summary>The map changed since it was last autosaved.</summary>
    private bool _changed;

    /// <summary>The map's file; null for a map that has none yet (new, generated, restored).</summary>
    public string? Path { get; private set; }

    /// <summary>The recent files, the newest first.</summary>
    public IReadOnlyList<string> Recent { get; private set; } = [];

    /// <summary>For the status bar: "Not saved", "No changes", "Saved 12:05", or "Unsaved changes".</summary>
    public string SaveState => _editor.IsDirty ? "Unsaved changes" : _clean;

    /// <summary>The minutes between two autosaves (the user's preference). Setting it starts the wait anew.</summary>
    public int AutosaveMinutes
    {
        get => (int)Math.Round(_autosave.WaitTime / 60);
        set
        {
            if (value == AutosaveMinutes)
            {
                return;
            }

            _autosave.WaitTime = value * 60.0;
            if (_autosave.IsInsideTree())
            {
                _autosave.Start();
            }
        }
    }

    /// <summary>A question or a file dialog is open: the editor takes no other input.</summary>
    public bool IsBusy => _layer.GetChildCount() > 0 || _dialog is not null;

    /// <summary>The map's file, its save state or the recent files changed.</summary>
    public event Action? Changed;

    /// <summary>
    /// With <paramref name="systemDialogs"/> the system's file dialog is used where Godot offers one (scripted runs use Godot's own,
    /// which they can see).
    /// </summary>
    public void Bind(FileServiceHost host, EditorSettingsFile settings, IMapFiles maps, bool systemDialogs)
    {
        _host = host;
        _settings = settings;
        _maps = maps;
        _systemDialogs = systemDialogs;
        Recent = [.. settings.Get(Section, RecentKey, Array.Empty<string>()).AsStringArray()];
        AddChild(_layer);
        AddChild(_autosave);
        _autosave.Timeout += Autosave;
    }

    /// <summary>
    /// The editor starts: shows <paramref name="state"/> (the file at <paramref name="path"/>, or a new map), then offers the newest
    /// autosave when the last session did not close.
    /// </summary>
    public void Start(GameState state, string? path)
    {
        string? autosave = null;
        try
        {
            autosave = _maps.BeginSession();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"No autosave session: {e.Message}");
        }

        Show(state, path, path is null ? "Not saved" : "No changes");
        if (path is not null)
        {
            Remember(path);
        }

        if (autosave is null)
        {
            return;
        }

        var made = _maps.AutosaveTime(autosave);
        Ask("Restore the map?", $"The editor did not close properly last time. An autosave from {made:g} was found.",
            new("Restore", () => Restore(autosave)),
            new("Discard"));
    }

    /// <summary>A new map (blank or generated) replaces the one being edited, after the question over unsaved work; then <paramref name="opened"/> runs.</summary>
    public void OpenNew(GameState state, Action? opened = null) => WhenSafe(() =>
    {
        Show(state, null, "Not saved");
        opened?.Invoke();
    });

    /// <summary>Open: the question over unsaved work, then the file dialog.</summary>
    public void Open() => WhenSafe(() => Pick(save: false, Load));

    /// <summary>Opens a file by its path (a recent one), after the question over unsaved work.</summary>
    public void Open(string path) => WhenSafe(() => Load(path));

    /// <summary>Save: to the map's file; a map without one asks for it (<see cref="SaveAs"/>). <paramref name="then"/> runs once it is saved.</summary>
    public void Save(Action? then = null)
    {
        if (Path is null)
        {
            SaveAs(then);
        }
        else if (Write(Path))
        {
            then?.Invoke();
        }
    }

    /// <summary>Save as: the file dialog, which asks before it overwrites a file; the map is that file from then on.</summary>
    public void SaveAs(Action? then = null) => Pick(save: true, path =>
    {
        if (Write(path))
        {
            then?.Invoke();
        }
    });

    /// <summary>Closes the editor, after the question over unsaved work; <paramref name="ending"/> runs just before it closes.</summary>
    public void Quit(Action? ending = null) => WhenSafe(() =>
    {
        try
        {
            _maps.EndSession();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Could not end the autosave session: {e.Message}");
        }

        ending?.Invoke();
        GetTree().Quit();
    });

    /// <summary>Runs <paramref name="then"/> now when the map has no unsaved work; else asks: save first, leave the work, or stay.</summary>
    private void WhenSafe(Action then)
    {
        if (IsBusy)
        {
            return; // one question at a time
        }

        if (!_editor.IsDirty)
        {
            then();
            return;
        }

        Ask("Unsaved changes", $"{DisplayName} has changes that are not saved.",
            new("Save", () => Save(then)),
            new("Don't save", then),
            new("Cancel"));
    }

    private void Ask(string title, string text, params QuestionAnswer[] answers)
    {
        var box = new QuestionBox { Name = "Question" };
        _layer.AddChild(box);
        box.Bind(title, text, answers);
    }

    /// <summary>
    /// Writes the map as it is to <paramref name="path"/> (the folder made when missing) without making it the map's file: the map's file and
    /// its save state stay as they are (the Test's copy, ADR-267).
    /// </summary>
    public void SaveCopy(string path)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        _maps.Save(_editor.Scenario, path);
    }

    /// <summary>The file dialog; <paramref name="picked"/> gets the chosen path (a map file's, whatever was typed).</summary>
    private void Pick(bool save, Action<string> picked) => ShowDialog(
        save ? "Save the map as" : "Open a map", save, $"*{_maps.Extension};Eternal Wars maps", save ? MapTitle + _maps.Extension : null,
        path => picked(save ? WithExtension(path) : path));

    /// <summary>
    /// File → Export (ADR-266): a save dialog for a file of another kind, named after the map plus <paramref name="suffix"/> and
    /// <paramref name="extension"/> (".md", ".png"), beside the map's file (else in the maps folder); <paramref name="picked"/> gets the
    /// chosen path, with the extension added when it was typed without it. The dialog asks before it overwrites a file.
    /// </summary>
    public void PickExport(string title, string suffix, string extension, string kind, Action<string> picked)
    {
        ArgumentNullException.ThrowIfNull(picked);
        ShowDialog(title, save: true, $"*{extension};{kind}", MapTitle + suffix + extension,
            path => picked(path.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? path : path + extension));
    }

    /// <summary>The map's file's name without its extension, else a file name made from the map's name.</summary>
    private string MapTitle => Path is null ? EditorFileDialog.SafeName(_editor.Scenario.Map.Name) : FileTitle(Path);

    /// <summary>A file dialog in the map's folder (else the maps folder), one at a time; <paramref name="picked"/> gets the path chosen.</summary>
    private void ShowDialog(string title, bool save, string filter, string? file, Action<string> picked)
    {
        if (IsBusy)
        {
            return;
        }

        var folder = Path is null ? _maps.Folder : System.IO.Path.GetDirectoryName(Path)!;
        _dialog = EditorFileDialog.Open(this, title, save, filter, file, folder, _maps.Folder, _systemDialogs, picked, () => _dialog = null);
    }

    private void Load(string path)
    {
        GameState state;
        try
        {
            state = _maps.Load(path);
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (!File.Exists(path))
            {
                SetRecent(RecentFiles.Without(Recent, path));
                Changed?.Invoke();
            }

            _host.Say($"Cannot open {path}: {e.Message}");
            return;
        }

        Show(state, path, "No changes");
        Remember(path);
        _host.Say($"Opened {path}");
    }

    /// <summary>Writes the map to <paramref name="path"/>, which becomes its file. False, with a message, when it could not be written.</summary>
    private bool Write(string path)
    {
        try
        {
            _maps.Save(_editor.Scenario, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or NotSupportedException)
        {
            _host.Say($"Could not save {path}: {e.Message}");
            return false;
        }

        Path = path;
        _clean = $"Saved {DateTime.Now:HH:mm}";
        _editor.MarkSaved();
        MarkSafe();
        Remember(path);
        var errors = MapValidator.Validate(_editor.Scenario).Count(i => i.Severity == IssueSeverity.Error);
        _host.Say(errors == 0 ? $"Saved {path}" : $"Saved {path} with {errors} error(s), see Problems");
        Notify();
        return true;
    }

    private void Restore(string autosave)
    {
        try
        {
            var state = _maps.Load(autosave);
            Show(state, null, "Not saved", unsaved: true);
            _host.Say($"Restored the autosave {autosave}: save it to keep it");
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _host.Say($"Cannot restore {autosave}: {e.Message}");
        }
    }

    /// <summary>
    /// Shows <paramref name="state"/> as the map being edited. <paramref name="unsaved"/>: the map is unsaved work as it is (a restored
    /// autosave); otherwise nothing unsaved is left behind, so older autosaves are no longer offered.
    /// </summary>
    private void Show(GameState state, string? path, string clean, bool unsaved = false)
    {
        Path = path;
        _clean = clean;
        _session?.QueueFree();
        _editor = _host.Open(state);
        if (unsaved)
        {
            _editor.MarkUnsaved();
        }
        else
        {
            MarkSafe();
        }

        // On a node of the session's own: this service stays in the tree and would keep every old session alive.
        _session = new Node { Name = "Session" };
        AddChild(_session);
        _session.SubscribeWhileInTree<UnsavedWorkChanged>(_editor.Events, _ => Notify());
        _session.SubscribeWhileInTree<UndoHistoryChanged>(_editor.Events, _ =>
        {
            _changed = true;
            if (Path is null)
            {
                Notify(); // a map without a file is titled by its name, which an edit may have changed
            }
        });
        _changed = unsaved;
        Notify();
    }

    /// <summary>Every <see cref="AutosaveMinutes"/>: a copy of a map with unsaved work that changed since the last one, written on a task.</summary>
    private void Autosave()
    {
        if (!_editor.IsDirty || !_changed)
        {
            return;
        }

        Action write;
        try
        {
            write = _maps.Autosave(_editor.Scenario);
        }
        catch (Exception e) when (e is FormatException or NotSupportedException)
        {
            _host.Say($"No autosave: {e.Message}");
            return;
        }

        _changed = false;
        var now = DateTime.Now;
        Task.Run(() =>
        {
            string? failure = null;
            try
            {
                write();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                failure = e.Message;
            }

            Callable.From(() =>
            {
                if (IsInstanceValid(this))
                {
                    _host.Say(failure is null ? $"Autosaved {now:HH:mm}" : $"Autosave failed: {failure}");
                }
            }).CallDeferred();
        });
    }

    private void MarkSafe()
    {
        try
        {
            _maps.MarkSafe();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Could not write the autosave session marker: {e.Message}");
        }
    }

    private void Remember(string path)
    {
        SetRecent(RecentFiles.With(Recent, System.IO.Path.GetFullPath(path)));
        Changed?.Invoke();
    }

    private void SetRecent(IReadOnlyList<string> recent)
    {
        Recent = recent;
        _settings.Set(Section, RecentKey, recent.ToArray());
    }

    private void Notify()
    {
        GetWindow().Title = $"{(_editor.IsDirty ? "*" : "")}{DisplayName} - {AppTitle}";
        Changed?.Invoke();
    }

    /// <summary>The map as the title and the questions name it: its file without the folder and the extension, or its own name.</summary>
    private string DisplayName =>
        Path is not null ? FileTitle(Path) : _editor.Scenario.Map.Name is { Length: > 0 } name ? name : "Untitled";

    private string FileTitle(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        return name.EndsWith(_maps.Extension, StringComparison.OrdinalIgnoreCase) ? name[..^_maps.Extension.Length] : name;
    }

    /// <summary>A map file's path: the map extension is added to a name typed without it (".json" alone is completed, not doubled).</summary>
    private string WithExtension(string path)
    {
        if (path.EndsWith(_maps.Extension, StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }

        const string Json = ".json";
        return (path.EndsWith(Json, StringComparison.OrdinalIgnoreCase) ? path[..^Json.Length] : path) + _maps.Extension;
    }
}
