using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Settings;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Editor;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Editor.Tools;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// What the editor's composers share (ADR-288). The parts kept from map to map are made once by <see cref="Main"/>; the map's own (the
/// session, the view context, the workspace with its panels, the commands) are made again on every Open. A composer reads the map's parts
/// here when its command runs (or takes them when it registers into that map's registry), so it works on the map open.
/// </summary>
internal sealed class EditorParts
{
    /// <summary>The editor's node: the 3D scene the map's views go into.</summary>
    public Node3D Root { get; set; } = null!;

    public EditorCamera Camera { get; set; } = null!;

    public FileService Files { get; set; } = null!;

    public TemplateFileService TemplateFiles { get; set; } = null!;

    public EditorSettingsFile Settings { get; set; } = null!;

    public EditorPreferences Preferences { get; set; } = new();

    /// <summary>The command palette and the editor's windows while one is open: over the editor's UI, kept from map to map.</summary>
    public CanvasLayer Overlays { get; } = new() { Name = "Overlays", Layer = 40 };

    /// <summary>The editor's messages: kept while maps are opened and closed, shown by the Output panel.</summary>
    public EditorLog Log { get; } = new();

    /// <summary>What was copied or cut last: kept from map to map, so a piece of one map can be pasted into another (EDIT-4).</summary>
    public EditorClipboard Clipboard { get; } = new();

    /// <summary>What the object browser has chosen: kept from map to map.</summary>
    public BrowserChoice Choice { get; } = new();

    /// <summary>The camera's bookmarks (ADR-260): of the map open, cleared when another opens.</summary>
    public CameraBookmarks Bookmarks { get; } = new();

    /// <summary>The template being edited: kept while maps are opened and generated (the editor itself is rebuilt with the UI).</summary>
    public TemplateEditorSession Templates { get; } = new(content: GameContent.All);

    /// <summary>The tile grid shown: the editor's own toggle, kept from map to map.</summary>
    public bool ShowGrid { get; set; } = true;

    /// <summary>The other level faintly through the shown one (ADR-287), kept from map to map.</summary>
    public bool GhostLevel { get; set; }

    /// <summary>The left button is held since the tool in use got its press.</summary>
    public bool Pressed { get; set; }

    // The map's parts, made again on every Open.
    public MapEditorSession Editor { get; set; } = null!;

    public MapViewContext Context { get; set; } = null!;

    public AdventureWorldView? World { get; set; }

    public SelectionView SelectionView { get; set; } = null!;

    public CommandRegistry Commands { get; set; } = null!;

    public EditorWorkspace Workspace { get; set; } = null!;

    public StatusBarView StatusBar { get; set; } = null!;

    public ToolPalette Tools { get; set; } = null!;

    public EditorToolContext ToolContext { get; set; } = null!;

    public ObjectBrowser Browser { get; set; } = null!;

    public LayersPanel Layers { get; set; } = null!;

    public OverlaysPanel OverlayPanel { get; set; } = null!;

    public EventsPanel Events { get; set; } = null!;

    public GeneratePanel Generate { get; set; } = null!;

    /// <summary>The Template tab's editor: made once and moved into each new workspace, so it stays as it was left when a map is opened.</summary>
    public TemplateEditorPanel? TemplateEditor { get; set; }

    public int TemplatePage { get; set; }

    /// <summary>A question, a file dialog, the palette or a window is open: the editor takes no other input.</summary>
    public bool IsBusy => Files.IsBusy || TemplateFiles.IsBusy || Overlays.GetChildCount() > 0;

    /// <summary>The Template tab shows: Undo, Redo, Open and Save work on the template.</summary>
    public bool OnTemplateTab => Workspace.Page == TemplatePage;

    /// <summary>The template editor of the Template tab (built with the first map's UI, before any command can run).</summary>
    public TemplateEditorPanel TemplatePanel => TemplateEditor!;

    /// <summary>A message to the user: the status bar shows the last one, the Output panel keeps them all. Empty clears the status bar.</summary>
    public void Say(string text)
    {
        StatusBar.ShowMessage(text);
        if (text.Length > 0)
        {
            Log.Write(text);
        }
    }

    /// <summary>Why a command that works on the map's view cannot run now; null on the Map tab.</summary>
    public string? OnMapTab() => Workspace.Page == EditorWorkspace.MapPage ? null : "Only on the Map tab";

    /// <summary>Runs <paramref name="template"/> on the Template tab, else <paramref name="map"/>.</summary>
    public void OnTab(Action template, Action map)
    {
        if (OnTemplateTab)
        {
            template();
        }
        else
        {
            map();
        }
    }

    /// <summary>Registers a command. <paramref name="onMap"/>: it works on the map's view, so it cannot run while the Template tab shows.</summary>
    public void Command(
        string id, string name, Action run, UiIcon? icon = null, CommandShortcut? shortcut = null, Func<string?>? whyNot = null, Func<bool>? isOn = null,
        bool onMap = false, bool isChoice = false) =>
        Commands.Register(new EditorCommand(id, name, run)
        {
            Icon = icon, Shortcut = shortcut, WhyNot = onMap ? () => OnMapTab() ?? whyNot?.Invoke() : whyNot, IsOn = isOn, IsChoice = isChoice,
        });

    /// <summary>"<paramref name="text"/> (key)" with the key in effect of command <paramref name="id"/> (ADR-275); the text alone without one.</summary>
    public string KeyHint(string text, string id) => Commands[id].Shortcut is { } key ? $"{text} ({key.Text})" : text;
}
