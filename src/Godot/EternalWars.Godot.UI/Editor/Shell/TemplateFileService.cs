using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Editor.Shell;

/// <summary>
/// The template files as the <see cref="TemplateFileService"/> uses them; the app implements it with the template format (the UI library does
/// not read or write formats itself). Reading and writing throw <see cref="IOException"/>, <see cref="UnauthorizedAccessException"/>,
/// <see cref="FormatException"/> or <see cref="NotSupportedException"/> when they cannot be done.
/// </summary>
public interface ITemplateFiles
{
    /// <summary>A template file's extension, with its dot.</summary>
    string Extension { get; }

    /// <summary>The user's templates folder: where the file dialogs start and the Generate tab looks (ADR-070).</summary>
    string Folder { get; }

    /// <summary>The templates the game ships: opened read-only, saved as a copy.</summary>
    IReadOnlyList<MapTemplate> Shipped { get; }

    MapTemplate Load(string path);

    void Save(MapTemplate template, string path);

    /// <summary>Starts the editor's session; returns the autosave to offer when the last session did not close, else null.</summary>
    string? BeginSession();

    /// <summary>Nothing unsaved is left behind from before now: autosaves made until now are not offered any more.</summary>
    void MarkSafe();

    /// <summary>The editor closes properly.</summary>
    void EndSession();

    /// <summary>The work that writes <paramref name="template"/> (a value) as an autosave; it may run on a task.</summary>
    Action Autosave(MapTemplate template);

    /// <summary>When the autosave at <paramref name="path"/> was made, in local time.</summary>
    DateTime AutosaveTime(string path);
}

/// <summary>
/// The template editor's files (ADR-250), as the <see cref="FileService"/> does them for maps: which file the template is,
/// New, Open, Save and Save as with the file dialog, the recent files (kept in the <see cref="EditorSettingsFile"/>), the unsaved marker
/// (<see cref="Title"/>), the question before unsaved work is left (another template, closing the editor), the autosave every
/// <see cref="AutosaveMinutes"/> while there is unsaved work, and the offer to restore the newest autosave after a session that did not
/// close (<see cref="OfferRestore"/>: at start when the map asks nothing, else when the Template tab first shows; quitting first offers it too). A shipped template opens read-only: it has no file, and Save
/// asks where to save a copy. Every template the editor shows goes through here; the session (<see cref="TemplateEditorSession"/>) is one
/// for the editor's life.
/// </summary>
public partial class TemplateFileService : Node
{
    private const string Section = "template-files";
    private const string RecentKey = "recent";

    private readonly CanvasLayer _layer = new() { Name = "Questions", Layer = 50 };
    private readonly global::Godot.Timer _autosave = new() { Name = "Autosave", WaitTime = 120, Autostart = true };
    private TemplateEditorSession _session = null!;
    private EditorSettingsFile _settings = null!;
    private ITemplateFiles _files = null!;
    private Action<string> _say = null!;
    private bool _systemDialogs;
    private FileDialog? _dialog;

    /// <summary>The autosave the last session left, to offer once (<see cref="OfferRestore"/>).</summary>
    private string? _recoverable;

    /// <summary>The template changed since it was last autosaved.</summary>
    private bool _changed;

    /// <summary>The template's file; null for one that has none (new, shipped, restored).</summary>
    public string? Path { get; private set; }

    /// <summary>The template is a shipped one: Save makes a copy in the user's folder.</summary>
    public bool IsShipped { get; private set; }

    /// <summary>The recent files, the newest first.</summary>
    public IReadOnlyList<string> Recent { get; private set; } = [];

    /// <summary>The templates the game ships (<see cref="OpenShipped"/>).</summary>
    public IReadOnlyList<MapTemplate> Shipped => _files.Shipped;

    /// <summary>The template as the tab and the editor name it: its file without the extension, or its own name; an asterisk over unsaved work.</summary>
    public string Title => $"{(_session.IsDirty ? "*" : "")}{DisplayName}{(IsShipped ? " (shipped, read-only)" : "")}";

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

    /// <summary>The template's file, its save state or the recent files changed.</summary>
    public event Action? Changed;

    /// <summary>Another template was put in the editor (new, opened, restored): the views start on it afresh.</summary>
    public event Action? Opened;

    /// <summary>
    /// Starts the editor's template session on <paramref name="session"/>'s template (a new one). With <paramref name="systemDialogs"/> the
    /// system's file dialog is used where Godot offers one.
    /// </summary>
    public void Bind(TemplateEditorSession session, ITemplateFiles files, EditorSettingsFile settings, Action<string> say, bool systemDialogs)
    {
        _session = session;
        _files = files;
        _settings = settings;
        _say = say;
        _systemDialogs = systemDialogs;
        Recent = [.. settings.Get(Section, RecentKey, Array.Empty<string>()).AsStringArray()];
        AddChild(_layer);
        AddChild(_autosave);
        _autosave.Timeout += Autosave;
        this.SubscribeWhileInTree<UnsavedWorkChanged>(session.Events, _ => Changed?.Invoke());
        this.SubscribeWhileInTree<UndoHistoryChanged>(session.Events, _ =>
        {
            _changed = true;
            if (Path is null)
            {
                Changed?.Invoke(); // a template without a file is titled by its name, which a step may have changed
            }
        });
        try
        {
            _recoverable = files.BeginSession();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"No template autosave session: {e.Message}");
        }

        // Not marked safe while an autosave waits to be offered: a second crash before the offer still finds it.
        Show(session.Template, null, shipped: false, markSafe: _recoverable is null);
    }

    /// <summary>A template autosave from a session that did not close waits to be offered (<see cref="OfferRestore"/>).</summary>
    public bool HasRestoreOffer => _recoverable is not null;

    /// <summary>When the last session left an autosave: asks once whether to restore it (the Template tab shows it the first time).</summary>
    public void OfferRestore()
    {
        if (_recoverable is not { } autosave || IsBusy)
        {
            return;
        }

        _recoverable = null;
        WhenSafe(() => Ask("Restore the template?",
            $"The editor did not close properly last time. A template autosave from {_files.AutosaveTime(autosave):g} was found.",
            new("Restore", () => Restore(autosave)),
            new("Discard")));
    }

    /// <summary>A new template (<see cref="TemplateEditorSession.Blank"/>), after the question over unsaved work.</summary>
    public void New() => WhenSafe(() =>
    {
        Show(TemplateEditorSession.Blank(), null, shipped: false);
        _say("New template");
    });

    /// <summary>Open: the question over unsaved work, then the file dialog in the user's templates folder.</summary>
    public void Open() => WhenSafe(() => Pick(save: false, Load));

    /// <summary>Opens a file by its path (a recent one), after the question over unsaved work.</summary>
    public void Open(string path) => WhenSafe(() => Load(path));

    /// <summary>Opens a shipped template read-only, after the question over unsaved work.</summary>
    public void OpenShipped(MapTemplate template) => WhenSafe(() =>
    {
        Show(template, null, shipped: true);
        _say($"Opened the shipped template {template.Name}: Save keeps a copy in your templates folder");
    });

    /// <summary>Save: to the template's file; one without a file, or a shipped one, asks where (<see cref="SaveAs"/>). <paramref name="then"/> runs once it is saved.</summary>
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

    /// <summary>Save as: the file dialog, which asks before it overwrites a file; the template is that file from then on.</summary>
    public void SaveAs(Action? then = null) => Pick(save: true, path =>
    {
        if (Write(path))
        {
            then?.Invoke();
        }
    });

    /// <summary>Runs <paramref name="then"/> now when the template has no unsaved work; else asks: save first, leave the work, or stay.</summary>
    public void WhenSafe(Action then)
    {
        ArgumentNullException.ThrowIfNull(then);
        if (IsBusy)
        {
            return; // one question at a time
        }

        _session.EndStep(); // a drag still open is unsaved work too
        if (!_session.IsDirty)
        {
            then();
            return;
        }

        Ask("Unsaved template", $"The template {DisplayName} has changes that are not saved.",
            new("Save", () => Save(then)),
            new("Don't save", then),
            new("Cancel"));
    }

    /// <summary>The editor closes properly: the next start offers no template autosave.</summary>
    public void EndSession()
    {
        try
        {
            _files.EndSession();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Could not end the template autosave session: {e.Message}");
        }
    }

    private void Ask(string title, string text, params QuestionAnswer[] answers)
    {
        var box = new QuestionBox { Name = "Question" };
        _layer.AddChild(box);
        box.Bind(title, text, answers);
    }

    /// <summary>The file dialog; <paramref name="picked"/> gets the chosen path (a template file's, whatever was typed).</summary>
    private void Pick(bool save, Action<string> picked)
    {
        if (IsBusy)
        {
            return;
        }

        var folder = Path is null ? _files.Folder : System.IO.Path.GetDirectoryName(Path)!;
        var file = save ? (Path is null ? EditorFileDialog.SafeName(_session.Template.Name) : FileTitle(Path)) + _files.Extension : null;
        _dialog = EditorFileDialog.Open(
            this, save ? "Save the template as" : "Open a template", save, $"*{_files.Extension};Eternal Wars templates", file, folder, _files.Folder,
            _systemDialogs, path => picked(save ? WithExtension(path) : path), () => _dialog = null);
    }

    private void Load(string path)
    {
        MapTemplate template;
        try
        {
            template = _files.Load(path);
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            if (!File.Exists(path))
            {
                SetRecent(RecentFiles.Without(Recent, path));
                Changed?.Invoke();
            }

            _say($"Cannot open {path}: {e.Message}");
            return;
        }

        Show(template, path, shipped: false);
        Remember(path);
        _say($"Opened {path}");
    }

    /// <summary>Writes the template to <paramref name="path"/>, which becomes its file. False, with a message, when it could not be written.</summary>
    private bool Write(string path)
    {
        var template = _session.Template;
        try
        {
            _files.Save(template, path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or NotSupportedException)
        {
            _say($"Could not save {path}: {e.Message}");
            return false;
        }

        Path = path;
        IsShipped = false;
        _session.MarkSaved();
        MarkSafe();
        Remember(path);
        var errors = _session.Issues.Count(i => i.Severity == IssueSeverity.Error);
        _say(errors == 0 ? $"Saved {path}" : $"Saved {path} with {errors} error(s): it cannot generate until they are fixed");
        return true;
    }

    private void Restore(string autosave)
    {
        try
        {
            Show(_files.Load(autosave), null, shipped: false, unsaved: true);
            _say($"Restored the template autosave {autosave}: save it to keep it");
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            _say($"Cannot restore {autosave}: {e.Message}");
        }
    }

    /// <summary>
    /// Shows <paramref name="template"/> as the one being edited, its zones placed in the graph. <paramref name="unsaved"/>: it is unsaved work as
    /// it is (a restored autosave); otherwise nothing unsaved is left behind, so older autosaves are no longer offered.
    /// </summary>
    private void Show(MapTemplate template, string? path, bool shipped, bool unsaved = false, bool markSafe = true)
    {
        Path = path;
        IsShipped = shipped;
        _session.Load(template, arrange: true);
        if (unsaved)
        {
            _session.MarkUnsaved();
        }
        else if (markSafe)
        {
            MarkSafe();
        }

        _changed = unsaved;
        Opened?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>Every <see cref="AutosaveMinutes"/>: a copy of a template with unsaved work that changed since the last one, written on a task.</summary>
    private void Autosave()
    {
        if (!_session.IsDirty || !_changed)
        {
            return;
        }

        var write = _files.Autosave(_session.Template);
        _changed = false;
        var now = DateTime.Now;
        Task.Run(() =>
        {
            string? failure = null;
            try
            {
                write();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or FormatException or NotSupportedException)
            {
                failure = e.Message;
            }

            Callable.From(() =>
            {
                if (IsInstanceValid(this))
                {
                    _say(failure is null ? $"Autosaved the template {now:HH:mm}" : $"Template autosave failed: {failure}");
                }
            }).CallDeferred();
        });
    }

    private void MarkSafe()
    {
        try
        {
            _files.MarkSafe();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            GD.PushWarning($"Could not write the template autosave session marker: {e.Message}");
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

    /// <summary>The template as the questions name it: its file without the folder and the extension, or its own name.</summary>
    private string DisplayName =>
        Path is not null ? FileTitle(Path) : _session.Template.Name is { Length: > 0 } name ? name : "Untitled";

    private string FileTitle(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        return name.EndsWith(_files.Extension, StringComparison.OrdinalIgnoreCase) ? name[..^_files.Extension.Length] : name;
    }

    /// <summary>A template file's path with the template extension once (<see cref="RecentFiles.WithExtension"/>).</summary>
    private string WithExtension(string path) => RecentFiles.SavePath(path, _files.Extension, File.Exists);
}
