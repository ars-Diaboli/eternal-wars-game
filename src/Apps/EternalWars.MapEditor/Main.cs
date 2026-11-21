using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Settings;
using EternalWars.Core.Data.Stamps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Settings;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Editor;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Editor.Tools;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// Map editor entry point. Editing operations, undo/redo and validation live in core (<see cref="MapEditorSession"/>);
/// the views and panels live in the libraries (the window layout is <see cref="EditorWorkspace"/>); this class composes them, registers
/// the editor's commands (<see cref="CommandRegistry"/>: the menus, the toolbar and the shortcuts run them), routes the pointer to the tool
/// in use (<see cref="IEditorTool"/>); the map's files, the autosave and the questions over unsaved work are the <see cref="FileService"/>'s.
/// It applies the user's <see cref="EditorPreferences"/> (the UI's scale, the autosave interval, the camera's speeds, a new map's size).
/// Alt+Enter switches the window mode (ADR-186), kept in the editor's own settings file.
/// </summary>
public partial class Main : Node3D
{
    private const string MapsDirectory = "user://maps";

    /// <summary>Where the template editor saves (ADR-070: user templates live in the user data folder).</summary>
    private const string TemplatesDirectory = "user://templates";

    private MapEditorSession _editor = null!;
    private MapViewContext _context = null!;
    private bool _showGrid = true; // the editor's own toggle, kept from map to map
    private StrategyCamera _camera = null!;
    private Node3D? _world;
    private CanvasLayer? _ui;
    private ToolPalette _tools = null!;
    private InspectorPanel _inspector = null!;
    private EventsPanel _events = null!;
    private EventCellsView _eventCells = null!;
    private SelectionView _selectionView = null!;
    private FileService _files = null!;
    private EditorSettingsFile _settings = null!;
    private EditorPreferences _preferences = new();

    /// <summary>The command palette and the Preferences window while one is open: over the editor's UI, kept from map to map.</summary>
    private readonly CanvasLayer _overlays = new() { Name = "Overlays", Layer = 40 };
    private int _templatePage;
    private StatusBarView _statusBar = null!;

    /// <summary>The editor's messages: kept while maps are opened and closed, shown by the Output panel.</summary>
    private readonly EditorLog _log = new();

    private EditorToolContext _toolContext = null!;

    /// <summary>What was copied or cut last: kept from map to map, so a piece of one map can be pasted into another (EDIT-4).</summary>
    private readonly EditorClipboard _clipboard = new();

    /// <summary>What the object browser has chosen: kept from map to map.</summary>
    private readonly BrowserChoice _choice = new();
    private ObjectBrowser _browser = null!;
    private CommandRegistry _commands = null!;

    /// <summary>The left button is held since the tool in use got its press.</summary>
    private bool _pressed;
    private GenerateRequest? _lastGenerate;
    private EditorWorkspace _workspace = null!;
    private GeneratePanel _generate = null!;

    /// <summary>The template being edited: kept while maps are opened and generated (the editor itself is rebuilt with the UI).</summary>
    private readonly TemplateEditorSession _templates = new(content: GameContent.All);

    /// <summary>The Template tab's editor: made once and moved into each new workspace, so it stays as it was left when a map is opened.</summary>
    private TemplateEditorPanel? _templateEditor;
    private IReadOnlyList<MapTemplate> _generatorTemplates = [];

    public override void _Ready()
    {
        UiTheme.Apply(compact: true); // the look (ADR-191) and the spacing (ADR-163) in the compact variant for a tool (ADR-250)
        UiIcons.LabelsOnStatusBar = false; // the editor's status bar shows no hints: icon buttons name themselves in their tooltips (ADR-204)
        WindowModes.Apply(SettingsFile.LoadInterface(SettingsPath).Window);
        if (AutoInput.FromLaunchArguments() is { } autoInput)
        {
            AddChild(autoInput);
        }

        _camera = new StrategyCamera { Name = "Camera" };
        AddChild(_camera);
        _camera.Zoomed += () => _statusBar.ShowZoom(_camera.ZoomPercent);
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0) });
        AddChild(_overlays);
        _settings = new EditorSettingsFile(Path.Combine(DataFolder, EditorSettingsFile.FileName));
        _preferences = PreferencesStore.Read(_settings);
        _files = new FileService { Name = "Files" };
        AddChild(_files);
        _files.Bind(
            new FileServiceHost(Open, Say), _settings, new MapFiles(MapsFolder, AutosaveFolder),
            systemDialogs: LaunchArguments.AutoInput is null); // a scripted run sees only Godot's own dialog
        _files.Changed += ShowFile;
        GetTree().AutoAcceptQuit = false; // the window's close request waits for the question over unsaved work
        ApplyPreferences();
        _camera.KeysBlocked = () => IsBusy || _workspace.Page != EditorWorkspace.MapPage;

        // --map=file opens that map (the smoke scenarios), else a blank one.
        _files.Start(LaunchArguments.MapPath is { } path ? MapFiles.Read(path) : Blank(), LaunchArguments.MapPath);
    }

    /// <summary>A new, empty map of the size the preferences say.</summary>
    private GameState Blank(int levels = 1) =>
        BlankScenario.Create(_preferences.DefaultMapSize, _preferences.DefaultMapSize, levels, content: GameContent.All);

    /// <summary>The preferences take effect: the UI's scale, the autosave interval and the camera's speeds (a new map reads its size when it is made).</summary>
    private void ApplyPreferences()
    {
        GetWindow().ContentScaleFactor = _preferences.UiScalePercent / 100f;
        _files.AutosaveMinutes = _preferences.AutosaveMinutes;
        _camera.PanSpeed = _preferences.CameraPanSpeed;
        _camera.RotateSpeedDegrees = _preferences.CameraRotateSpeed;
        _camera.ZoomStep = _preferences.CameraZoomSpeed * 0.5f; // 3, the default, is the camera's own step
    }

    /// <summary>A question, a file dialog, the palette or the Preferences window is open: the editor takes no other input.</summary>
    private bool IsBusy => _files.IsBusy || _overlays.GetChildCount() > 0;

    private void OpenPalette()
    {
        if (IsBusy)
        {
            return;
        }

        var palette = new CommandPalette { Name = "Palette" };
        _overlays.AddChild(palette);
        palette.Bind(_commands, except: "view.palette");
    }

    private void OpenPreferences()
    {
        if (IsBusy)
        {
            return;
        }

        var window = new PreferencesWindow { Name = "Preferences" };
        _overlays.AddChild(window);
        window.Bind(_preferences, chosen =>
        {
            _preferences = chosen;
            PreferencesStore.Write(_settings, chosen);
            ApplyPreferences();
            Say("Preferences kept");
        });
    }

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            _files.Quit();
        }
    }

    /// <summary>Alt+Enter switches the window mode wherever the focus is, a text field included.</summary>
    public override void _Input(InputEvent @event)
    {
        if (!WindowModes.IsToggleKey(@event))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        var path = SettingsPath;
        var settings = SettingsFile.LoadInterface(path).WithWindowToggled();
        WindowModes.Apply(settings.Window);
        try
        {
            SettingsFile.Save(path, SettingsFile.Load(path), settings, SettingsFile.LoadLayout(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Say($"Could not keep the window mode: {e.Message}");
        }
    }

    /// <summary>The editor's settings file (only the window mode is used here), or the one the launch arguments name.</summary>
    private static string SettingsPath =>
        LaunchArguments.SettingsPath ?? Path.Combine(ProjectSettings.GlobalizePath("user://"), SettingsFile.FileName);

    /// <summary>
    /// Where the editor keeps its own files: the user data folder, or the folder of the settings file the launch arguments name (the smoke
    /// scenarios run side by side, each with its own settings, maps folder and autosaves).
    /// </summary>
    private static string DataFolder =>
        LaunchArguments.SettingsPath is { } settings ? Path.GetDirectoryName(Path.GetFullPath(settings))! : ProjectSettings.GlobalizePath("user://");

    private static string MapsFolder =>
        LaunchArguments.SettingsPath is null ? ProjectSettings.GlobalizePath(MapsDirectory) : Path.Combine(DataFolder, "maps");

    /// <summary>Where the browser keeps the pictures it rendered (ADR-254); deleting the folder only costs rendering them again.</summary>
    private static string ThumbnailsFolder =>
        LaunchArguments.SettingsPath is null ? ProjectSettings.GlobalizePath("user://cache/thumbs") : Path.Combine(DataFolder, "cache", "thumbs");

    /// <summary>Where "Save selection as stamp" writes and the browser's stamp list reads (ADR-250).</summary>
    private static string StampsFolder =>
        LaunchArguments.SettingsPath is null ? ProjectSettings.GlobalizePath("user://stamps") : Path.Combine(DataFolder, "stamps");

    private static string AutosaveFolder =>
        LaunchArguments.SettingsPath is null ? Path.Combine(ProjectSettings.GlobalizePath(MapsDirectory), ".autosave") : Path.Combine(DataFolder, "autosave");

    public override void _UnhandledInput(InputEvent @event)
    {
        if (IsBusy)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when !_pressed:
                GetViewport().GuiReleaseFocus(); // a click on the map leaves the text field that was typed into: the keys are the camera's again
                if (Point(press.Position) is not { } tile)
                {
                    return;
                }

                if (_events.PlacingCells is not null)
                {
                    _toolContext.Report(_events.ClickCell(tile));
                }
                else
                {
                    _pressed = true;
                    _toolContext.Pointer = new PointerPress(press.ShiftPressed, press.AltPressed, press.DoubleClick);
                    _tools.Tool.Press(tile);
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when _pressed:
                _pressed = false;
                _tools.Tool.Release();
                break;
            case InputEventMouseMotion motion when _pressed:
                if (Point(motion.Position) is { } dragged)
                {
                    _tools.Tool.Drag(dragged);
                }

                break;
            case InputEventMouseMotion motion:
                _tools.Tool.Hover(Point(motion.Position)); // the caption of what is under the pointer shows from afar
                return;
            case InputEventKey key when !_pressed && !IsTextKey(key) && _commands.RunShortcut(key): // no command in the middle of a stroke
                _camera.IgnoreHeldKeys(); // a key that ran a command (E, the Erase tool) does not turn or move the camera as well
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>
    /// A plain key (a letter, Delete, Escape) while a text field has the focus belongs to the field, not to a command, also when the
    /// field let it through. Keys with Ctrl or Alt and the function keys stay commands.
    /// </summary>
    private bool IsTextKey(InputEventKey key) =>
        GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit
        && !key.CtrlPressed && !key.AltPressed && key.Keycode is < Key.F1 or > Key.F35;

    /// <summary>The tiles of the shown level under the window's corners: what is in view (the whole level when a corner looks past the map's plane).</summary>
    private TileArea VisibleTiles()
    {
        var map = _editor.Scenario.Map;
        var whole = new TileArea(0, 0, map.Width - 1, map.Height - 1, _context.Level);
        var size = GetViewport().GetVisibleRect().Size;
        var corners = new[] { Vector2.Zero, new Vector2(size.X, 0), new Vector2(0, size.Y), size }
            .Select(c => MapSpace.PickTile(_camera, c, map, _context.Level))
            .ToList();
        return corners.Any(c => c is null)
            ? whole
            : new TileArea(
                Math.Max(0, corners.Min(c => c!.Value.X)), Math.Max(0, corners.Min(c => c!.Value.Y)),
                Math.Min(map.Width - 1, corners.Max(c => c!.Value.X)), Math.Min(map.Height - 1, corners.Max(c => c!.Value.Y)), _context.Level);
    }

    /// <summary>The tile at <paramref name="screen"/> becomes the pointed one: the views and the status bar show it.</summary>
    private MapPosition? Point(Vector2 screen)
    {
        var tile = Pick(screen);
        _context.Point(tile);
        _statusBar.ShowTile(_editor.Scenario, tile);
        _browser.PointAt(tile is { } at ? _editor.Scenario.Map.GetTerrain(at) : null); // for its filter "terrain under the pointer"
        return tile;
    }

    /// <summary>A message to the user: the status bar shows the last one, the Output panel keeps them all. Empty clears the status bar.</summary>
    private void Say(string text)
    {
        _statusBar.ShowMessage(text);
        if (text.Length > 0)
        {
            _log.Write(text);
        }
    }

    /// <summary>The map's file or its save state changed: the status bar and the commands follow.</summary>
    private void ShowFile()
    {
        _statusBar.ShowSaveState(_files.SaveState);
        _commands.NotifyStateChanged();
    }

    private MapPosition? Pick(Vector2 screen) =>
        MapSpace.PickTile(_camera, screen, _editor.Scenario.Map, _context.Level) is { } tile && _editor.Scenario.Map.Contains(tile) ? tile : null;

    /// <summary>Shows <paramref name="state"/> as the map being edited, for the <see cref="FileService"/>: every map is opened through it.</summary>
    private MapEditorSession Open(GameState state)
    {
        _world?.QueueFree();
        _templateEditor?.GetParent()?.RemoveChild(_templateEditor); // kept: the new workspace takes it
        _ui?.QueueFree();
        _pressed = false;

        _editor = new MapEditorSession(state, copier: MapObjectCopier.Instance);
        _context = new MapViewContext(state, _editor.Events) { ShowGrid = _showGrid };
        BuildWorld();

        BuildUi();
        // On the session's own UI, freed with it on the next Open; the root stays in the tree and would keep every old session alive.
        _ui!.SubscribeWhileInTree<UndoHistoryChanged>(_editor.Events, _ =>
        {
            _commands.NotifyStateChanged();
            _tools.Tool.Refresh(); // a paste's ghost follows the map
        });
        _ui!.SubscribeWhileInTree<MapResized>(_editor.Events, _ => ShowResizedMap());
        _ui!.SubscribeWhileInTree<SelectionChanged>(_editor.Events, _ => _commands.NotifyStateChanged());

        _camera.FocusOn(MapSpace.ToWorld(new(state.Map.Width / 2, state.Map.Height / 2)));
        return _editor;
    }

    /// <summary>The views of the map in the 3D scene, made from the scenario as it is now.</summary>
    private void BuildWorld()
    {
        var world = new AdventureWorldView { Name = "World" };
        _world = world;
        AddChild(world);
        world.Bind(_context);
        _eventCells = new EventCellsView { Name = "EventCells" };
        world.AddChild(_eventCells);
        _eventCells.Bind(_context);
        _selectionView = new SelectionView { Name = "Selection" };
        world.AddChild(_selectionView);
        _selectionView.Bind(_context, _editor.Selection);
        var zones = new StartZonesView { Name = "StartZones" };
        world.AddChild(zones);
        zones.Bind(_context);
    }

    /// <summary>
    /// The map has another size or its content moved (a resize, a crop, a shift or the undo of one): the views of the map are made
    /// again from the scenario. The old ones leave the tree at once, so they hear no later event of a map they no longer fit.
    /// </summary>
    private void ShowResizedMap()
    {
        var highlighted = _eventCells.Highlighted;
        if (_world is { } old)
        {
            RemoveChild(old);
            old.QueueFree();
        }

        // A view context of their own for the new views: the old ones' handlers stay behind with the old context.
        _pressed = false;
        var level = _context.Level;
        _context = new MapViewContext(_editor.Scenario, _editor.Events) { ShowGrid = _showGrid };
        _context.SetLevel(level);
        BuildWorld();
        _eventCells.Highlighted = highlighted;
        _toolContext.Marks = _selectionView;
        _tools.Tool.Hover(null); // what the tool drew before a click was on the old views
        _tools.Tool.Refresh();
        var map = _editor.Scenario.Map;
        _camera.FocusOn(MapSpace.ToWorld(new MapPosition(map.Width / 2, map.Height / 2, _context.Level)));
        _commands.NotifyStateChanged();
    }

    private void OpenResize()
    {
        if (IsBusy)
        {
            return;
        }

        var window = new ResizeWindow { Name = "Resize" };
        _overlays.AddChild(window);
        window.Bind(_editor, Say);
    }

    /// <summary>Crop to the selection: at once when nothing would be lost, else after the window's question.</summary>
    private void Crop()
    {
        if (!IsBusy && ResizeWindow.Crop(_editor, Say) is { } question)
        {
            _overlays.AddChild(question);
        }
    }

    /// <summary>"Save selection as stamp": asks for a name, then writes the selection's copy to the stamps folder.</summary>
    private void SaveStamp()
    {
        if (IsBusy || _editor.Fragments.Copy() is not { } fragment)
        {
            return;
        }

        var box = new NameBox { Name = "StampName" };
        _overlays.AddChild(box);
        var taken = StampFolder.Names(StampsFolder).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var free = Enumerable.Range(1, taken.Count + 1).Select(n => $"Stamp {n}").First(n => !taken.Contains(n));
        box.Bind("Save selection as stamp", free, name =>
        {
            try
            {
                var replaced = File.Exists(StampFolder.PathOf(StampsFolder, name));
                var path = StampFolder.Save(StampsFolder, name, fragment);
                Say($"{(replaced ? "Replaced" : "Saved")} stamp {Path.GetFileName(path)}; the browser lists it under Stamps");
                _browser.RefreshStamps();
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Say($"Could not save the stamp: {e.Message}");
            }
        });
    }

    /// <summary>A stamp picked in the browser: the Paste tool takes it, and every click puts it on the map.</summary>
    private void PasteStamp(string name)
    {
        MapFragment fragment;
        try
        {
            fragment = StampFolder.Load(StampsFolder, name, _editor.Scenario);
        }
        catch (Exception e) when (e is FormatException or IOException or UnauthorizedAccessException)
        {
            Say($"Could not read stamp {name}: {e.Message}");
            return;
        }

        var tool = _tools.Tools.OfType<PasteTool>().First();
        if (_tools.Tool != tool)
        {
            _tools.Use(_tools.Tools.OfType<SelectTool>().First()); // the tool in use lets go; the Paste tool is told its fragment before it comes in
        }

        tool.UseFragment(fragment);
        _tools.Use(tool);
        tool.Hover(_context.Pointed);
        Say($"Stamp {name}: click where it goes; Escape ends");
    }

    /// <summary>Something was chosen in the browser: the Object tool comes in and shows it.</summary>
    private void UseChoice()
    {
        var tool = _tools.Tools.OfType<ObjectTool>().First();
        _tools.Use(tool);
        _tools.RefreshOptions();
        tool.Hover(_context.Pointed);
        tool.Refresh();
        if (_choice.Entry is { } entry)
        {
            Say($"{entry.Name}: click the map to place it");
        }
    }

    private void BuildUi()
    {
        _ui = new CanvasLayer { Name = "Ui" };
        AddChild(_ui);
        _workspace = new EditorWorkspace { Name = "Workspace" };
        _ui.AddChild(_workspace);
        _workspace.Bind(_settings);

        RegisterCommands();
        var menuBar = new MenuBarView { Name = "Menus" };
        _workspace.SetMenus(menuBar);
        var toolbar = new ToolbarView { Name = "Toolbar" };
        _workspace.TopBar.AddChild(toolbar);
        _statusBar = new StatusBarView { Name = "Status" };
        _workspace.StatusBar.AddChild(_statusBar);
        _statusBar.ShowZoom(_camera.ZoomPercent);

        // Left: the tools and the options of the one in use, which wrap to the region's width; the object browser takes the room under
        // them and scrolls its own list.
        _tools = new ToolPalette();
        _browser = new ObjectBrowser { Name = "Browser", SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 320) };
        var left = new VBoxContainer { Name = "Tools" };
        left.AddChild(_tools);
        left.AddChild(new HSeparator());
        left.AddChild(_browser);
        _workspace.AddTab(EditorRegion.Left, left);

        // Right: the map's settings and the inspector.
        var properties = new MapPropertiesPanel { Name = "Map" };
        _workspace.AddTab(EditorRegion.Right, properties);
        properties.Bind(_editor);
        var players = new PlayersPanel { Name = "Players" };
        _workspace.AddTab(EditorRegion.Right, players);
        players.Bind(_editor);
        var conditions = new ConditionsPanel { Name = "Conditions" };
        _workspace.AddTab(EditorRegion.Right, conditions);
        conditions.Bind(_editor);
        _inspector = new InspectorPanel { Name = "Inspector" };
        _workspace.AddTab(EditorRegion.Right, _inspector);
        _inspector.Bind(_editor, _settings);
        _toolContext = new EditorToolContext(_editor, _inspector, Say) {
            Marks = _selectionView, VisibleArea = VisibleTiles, Clipboard = _clipboard, Choice = _choice,
        };
        _tools.Bind(EditorTools.All(_toolContext));
        _tools.ToolChanged += _commands.NotifyStateChanged;

        // The browser's choice is the Object tool's: the entry of the same name in this map's catalog, the treasure chest at first.
        var catalog = new BrowserCatalog(_editor.Catalog, _editor.Scenario.Creatures, _editor.Scenario.Artifacts);
        _choice.Rebase(catalog.Find(_choice.Entry?.Id ?? BuiltInMapObjects.TreasureChest)); // no one is told: the last map's UI still listens until it is freed
        _browser.Bind(catalog, _choice, _settings, () => StampFolder.Names(StampsFolder), PasteStamp, ThumbnailsFolder);
        _choice.Changed += UseChoice;
        _browser.TreeExiting += () => _choice.Changed -= UseChoice; // this UI goes when another map is opened; the choice stays
        _events = new EventsPanel { Name = "Events" };
        _workspace.AddTab(EditorRegion.Right, _events);
        _events.Bind(_editor, MapEventNodeJson.Instance);
        var bans = new BansPanel { Name = "Bans" }; // the map's bans (ADR-126)
        _workspace.AddTab(EditorRegion.Right, bans, UiIcon.Ban);
        bans.Bind(_editor);
        _generate = new GeneratePanel { Name = "Generate" }; // "From template" opens it
        _workspace.AddTab(EditorRegion.Right, _generate);
        _generatorTemplates = [.. BuiltInTemplates.All, .. UserTemplates().Select(u => u.Template).Where(t => TemplateValidator.IsValid(t))];
        _generate.Bind(
            _generatorTemplates.Select(t => new GeneratorTemplate(t.Name, t.Description, t.Sizes, t.MinPlayers, t.MaxHeroes, StartSlots.Of(t).Select(s => s.Name).ToList())).ToList(),
            Generate,
            _lastGenerate);

        // Bottom: the map's problems, the undo history and the editor's messages. Each scrolls by itself.
        var problems = new ProblemsPanel { Name = "Problems" };
        _workspace.AddTab(EditorRegion.Bottom, problems, scroll: false);
        problems.Changed += () =>
        {
            _statusBar.ShowProblems(problems.Errors, problems.Warnings);
            _statusBar.ShowWork(problems.IsChecking ? "Checking the map…" : null);
        };
        problems.Bind(_editor, ValidationOfSnapshot);
        problems.IssueSelected += ShowTile;
        problems.EventSelected += OpenEvent;
        _statusBar.ProblemsRequested += () => _workspace.ShowTab(problems);
        var history = new HistoryPanel { Name = "History" };
        _workspace.AddTab(EditorRegion.Bottom, history, scroll: false);
        history.Bind(_editor);
        var output = new OutputPanel { Name = "Output" };
        _workspace.AddTab(EditorRegion.Bottom, output, scroll: false);
        output.Bind(_log);
        _events.SelectionChanged += name => _eventCells.Highlighted = name;
        _events.ShowRequested += ShowTile;
        _inspector.EventRequested += OpenEvent;
        _inspector.LinkRequested += Jump;

        // The template editor (ADR-117) is the workspace's Template tab (EDIT-3, ADR-250), the same one from map to map.
        var templateEditor = _templateEditor;
        _templateEditor ??= new TemplateEditorPanel { Name = "TemplateEditor" };
        _templatePage = _workspace.AddPage("Template", _templateEditor);
        if (templateEditor is null)
        {
            _templateEditor.Bind(_templates, new TemplateEditorHost(TemplateSources, SaveTemplate, GenerateFromEditor));
        }

        _workspace.PageChanged += _commands.NotifyStateChanged;

        // The panels and the tools exist: the commands that open them, then the menus and the toolbar built from the registry.
        Command("map.properties", "Map properties", () => _workspace.ShowTab(properties));
        Command("map.players", "Players", () => _workspace.ShowTab(players));
        Command("map.conditions", "Victory and loss conditions", () => _workspace.ShowTab(conditions));
        Command("map.events", "Events", () => _workspace.ShowTab(_events));
        Command("map.bans", "Bans", () => _workspace.ShowTab(bans), UiIcon.Ban);
        Command("view.problems", "Problems", () => _workspace.ShowTab(problems));
        Command("view.history", "History", () => _workspace.ShowTab(history));
        Command("view.output", "Output", () => _workspace.ShowTab(output));
        Command("view.inspector", "Inspector", () => _workspace.ShowTab(_inspector));
        foreach (var tool in _tools.Tools)
        {
            _commands.Register(new EditorCommand(ToolCommand(tool), tool.Name, () => _tools.Use(tool))
            {
                Shortcut = tool.Shortcut is { } key ? new CommandShortcut(key) : null, WhyNot = OnMapTab, IsOn = () => _tools.Tool == tool, IsChoice = true,
            });
        }

        menuBar.Bind(_commands,
        [
            new("File", ["file.new", "file.new-two-levels", "file.generate", null, "file.open", ">Open recent", null, "file.save", "file.save-as", null, "file.quit"])
            {
                Lists =
                [
                    new("Open recent", () => [.. _files.Recent.Select(path => (path, (Action)(() => _files.Open(path))))], "No file was opened or saved yet"),
                ],
            },
            new("Edit", ["edit.undo", "edit.redo", null, "edit.cut", "edit.copy", "edit.paste", "edit.duplicate", "edit.delete", "edit.save-stamp", null, "edit.rotate", "edit.mirror", null, "edit.select-all", "edit.deselect", null, "edit.preferences"]),
            new("View", ["view.palette", null, "view.map-tab", "templates.editor", null, "view.level", "view.grid", null, "view.left", "view.bottom", "view.right", "view.panels", null, "view.inspector", "view.problems", "view.history", "view.output"]),
            new("Map", ["map.properties", "map.players", "map.conditions", "map.events", "map.bans", null, "map.resize", "map.crop", null, "view.problems"]),
            new("Tools", [.. _tools.Tools.Select(ToolCommand)]),
            new("Templates", ["templates.editor", "file.generate"]),
            new("Test", ["test.play"]),
            new("Help", ["help.help"]),
        ]);
        toolbar.Bind(_commands,
        [
            "file.new", "file.new-two-levels", "file.generate", "templates.editor", "file.open", "file.save", null,
            "edit.undo", "edit.redo", null, "view.level", "view.grid", null, "view.left", "view.bottom", "view.right",
        ]);
        _workspace.RegionsChanged += _commands.NotifyStateChanged;
    }

    private static string ToolCommand(IEditorTool tool) => $"tool.{tool.Name.ToLowerInvariant()}";

    /// <summary>Registers a command. <paramref name="onMap"/>: it works on the map's view, so it cannot run while the Template tab shows.</summary>
    private void Command(
        string id, string name, Action run, UiIcon? icon = null, CommandShortcut? shortcut = null, Func<string?>? whyNot = null, Func<bool>? isOn = null,
        bool onMap = false, bool isChoice = false) =>
        _commands.Register(new EditorCommand(id, name, run)
        {
            Icon = icon, Shortcut = shortcut, WhyNot = onMap ? () => OnMapTab() ?? whyNot?.Invoke() : whyNot, IsOn = isOn, IsChoice = isChoice,
        });

    /// <summary>Why a command that works on the map's view cannot run now; null on the Map tab.</summary>
    private string? OnMapTab() => _workspace.Page == EditorWorkspace.MapPage ? null : "Only on the Map tab";

    /// <summary>The commands that need no panel: the file, the history, the view. The rest are registered once the panels exist.</summary>
    private void RegisterCommands()
    {
        _commands = new CommandRegistry();
        Command("file.new", "New", () => _files.OpenNew(Blank()), shortcut: new(Key.N, Ctrl: true));
        Command("file.new-two-levels", "New (2 levels)", () => _files.OpenNew(Blank(levels: 2)));
        Command("file.generate", "From template", () => _workspace.ShowTab(_generate));
        Command("file.open", "Open", () => _files.Open(), UiIcon.Load, new(Key.O, Ctrl: true));
        Command("file.save", "Save", () => _files.Save(), UiIcon.Save, new(Key.S, Ctrl: true));
        Command("file.save-as", "Save as", () => _files.SaveAs(), shortcut: new(Key.S, Ctrl: true, Shift: true));
        Command("file.quit", "Quit", _files.Quit, shortcut: new(Key.Q, Ctrl: true));
        Command("edit.undo", "Undo", () => _editor.Undo(), UiIcon.Undo, new(Key.Z, Ctrl: true),
            () => _editor.History.CanUndo ? null : "Nothing to undo", onMap: true);
        Command("edit.redo", "Redo", () => _editor.Redo(), UiIcon.Redo, new(Key.Y, Ctrl: true),
            () => _editor.History.CanRedo ? null : "Nothing to redo", onMap: true);
        Command("edit.select-all", "Select all", () =>
            {
                _editor.Selecting.Set(_editor.Selecting.Everything(_context.Level, _toolContext.Filter));
                Say(EditorToolContext.Describe(_editor.Selection));
            }, shortcut: new(Key.A, Ctrl: true), onMap: true);
        Command("edit.deselect", "Deselect", Deselect, shortcut: new(Key.Escape),
            whyNot: () => _editor.Selection.IsEmpty && _tools.Tool is not PasteTool ? "Nothing is selected" : null, onMap: true);
        Command("edit.cut", "Cut", () => Copy(cut: true), shortcut: new(Key.X, Ctrl: true), whyNot: NothingToCopy, onMap: true);
        Command("edit.copy", "Copy", () => Copy(cut: false), shortcut: new(Key.C, Ctrl: true), whyNot: NothingToCopy, onMap: true);
        Command("edit.paste", "Paste", () => Paste(duplicate: false), shortcut: new(Key.V, Ctrl: true),
            whyNot: () => _clipboard.Fragment is null ? "Nothing was copied" : null, onMap: true);
        Command("edit.duplicate", "Duplicate", () => Paste(duplicate: true), shortcut: new(Key.D, Ctrl: true), whyNot: NothingToCopy, onMap: true);
        Command("edit.delete", "Delete", () => Say($"Deleted {_editor.Selecting.Delete()} thing(s)"), shortcut: new(Key.Delete),
            whyNot: () => _editor.Selection.HasThings ? null : "No object or hero is selected", onMap: true);
        Command("edit.save-stamp", "Save selection as stamp", SaveStamp, whyNot: NothingToCopy, onMap: true);
        Command("edit.rotate", "Rotate scenery", () => Turn(mirror: false), shortcut: new(Key.R, Ctrl: true), whyNot: NoScenery, onMap: true);
        Command("edit.mirror", "Mirror scenery", () => Turn(mirror: true), shortcut: new(Key.M, Ctrl: true), whyNot: NoScenery, onMap: true);
        Command("map.resize", "Resize map", OpenResize, onMap: true);
        Command("map.crop", "Crop to selection", Crop,
            whyNot: () => _editor.Selection.Tiles.Count > 0 ? null : "No tile is selected", onMap: true);
        Command("edit.preferences", "Preferences", OpenPreferences, UiIcon.Settings, new(Key.Comma, Ctrl: true));
        Command("view.palette", "Command palette", OpenPalette, shortcut: new(Key.P, Ctrl: true, Shift: true));
        Command("view.map-tab", "Map tab", () => _workspace.ShowPage(EditorWorkspace.MapPage),
            isOn: () => _workspace.Page == EditorWorkspace.MapPage, isChoice: true);
        Command("templates.editor", "Template tab", () => _workspace.ShowPage(_templatePage),
            isOn: () => _workspace.Page == _templatePage, isChoice: true);
        Command("view.level", "Switch level", SwitchLevel, UiIcon.SwitchLevel, new(Key.L),
            () => _editor.Scenario.Map.Levels > 1 ? null : "The map has one level", onMap: true);
        Command("view.grid", "Grid", () => _context.ShowGrid = _showGrid = !_showGrid, isOn: () => _showGrid, onMap: true);
        Command("view.left", "Left panel", () => _workspace.SetOpen(EditorRegion.Left, !_workspace.IsShown(EditorRegion.Left)),
            isOn: () => _workspace.IsShown(EditorRegion.Left), onMap: true);
        Command("view.bottom", "Bottom panel", () => _workspace.SetOpen(EditorRegion.Bottom, !_workspace.IsShown(EditorRegion.Bottom)),
            isOn: () => _workspace.IsShown(EditorRegion.Bottom), onMap: true);
        Command("view.right", "Right panel", () => _workspace.SetOpen(EditorRegion.Right, !_workspace.IsShown(EditorRegion.Right)),
            isOn: () => _workspace.IsShown(EditorRegion.Right), onMap: true);
        Command("view.panels", "Hide all panels", _workspace.ToggleAll, shortcut: new(Key.F11), isOn: () => _workspace.AllHidden, onMap: true);
        Command("test.play", "Test the map", () => { }, shortcut: new(Key.F5), whyNot: () => "Play-testing from the editor is not built yet");
        Command("help.help", "Help", () => { }, shortcut: new(Key.F1), whyNot: () => "The editor's help is not written yet");
    }

    /// <summary>Why Copy, Cut and Duplicate cannot run: a fragment holds tiles and objects, and none is selected.</summary>
    private string? NothingToCopy() =>
        _editor.Selection.Tiles.Count + _editor.Selection.Objects.Count > 0 ? null : "No tile and no object is selected";

    /// <summary>Why Rotate and Mirror cannot run: only scenery turns, and none is selected.</summary>
    private string? NoScenery() =>
        _editor.Selection.Objects.Any(id => _editor.Scenario.FindObject(id) is Scenery) ? null : "No scenery is selected";

    /// <summary>Rotate or Mirror: the selected scenery turns a quarter turn clockwise, or is mirrored left to right.</summary>
    private void Turn(bool mirror)
    {
        var pieces = _editor.Selection.Objects.Count(id => _editor.Scenario.FindObject(id) is Scenery);
        Say((mirror ? _editor.Selecting.Mirror() : _editor.Selecting.Rotate()) switch
        {
            EditResult.Ok => $"{(mirror ? "Mirrored" : "Rotated")} {pieces} piece(s) of scenery",
            EditResult.Overlaps => "No room: turned, a piece would lie on another object or a hero",
            EditResult.OutOfBounds => "No room: turned, a piece would reach off the map",
            var refused => $"Refused: {refused}",
        });
    }

    /// <summary>Copy or Cut: the selection goes to the clipboard as a fragment.</summary>
    private void Copy(bool cut)
    {
        if ((cut ? _editor.Fragments.Cut() : _editor.Fragments.Copy()) is not { } fragment)
        {
            return;
        }

        _clipboard.Fragment = fragment;
        _commands.NotifyStateChanged();
        _tools.Tool.Refresh();
        Say($"{(cut && fragment.Objects.Count > 0 ? "Cut" : "Copied")} {fragment.Tiles.Count} tile(s) and {fragment.Objects.Count} object(s); Ctrl+V pastes");
    }

    /// <summary>Paste or Duplicate: the Paste tool takes the clipboard or a copy of the selection, and every click puts it on the map.</summary>
    private void Paste(bool duplicate)
    {
        var tool = _tools.Tools.OfType<PasteTool>().First();
        _tools.Use(tool); // first: leaving the tool that was in use lets go of what it was doing
        if (duplicate)
        {
            tool.UseSelection();
        }
        else
        {
            tool.UseClipboard();
        }

        tool.Hover(_context.Pointed); // the ghost shows at once, where the pointer is
        Say("Click where it goes; Escape ends");
    }

    /// <summary>Escape: out of pasting back to the Select tool, else nothing is selected any more.</summary>
    private void Deselect()
    {
        if (_tools.Tool is PasteTool)
        {
            _tools.Use(_tools.Tools.OfType<SelectTool>().First());
        }
        else
        {
            _editor.Selecting.Clear();
        }
    }

    /// <summary>The next level of the map, with the camera on its middle.</summary>
    private void SwitchLevel()
    {
        _context.SetLevel((_context.Level + 1) % _editor.Scenario.Map.Levels);
        _tools.Tool.Refresh();
        _camera.FocusOn(MapSpace.ToWorld(new MapPosition(_editor.Scenario.Map.Width / 2, _editor.Scenario.Map.Height / 2, _context.Level)));
    }

    /// <summary>
    /// For the Problems panel: copies the map into a document now (on the main thread); the work returned builds a scenario of its own from
    /// the copy and validates that, so it can run on a task while the map is edited.
    /// </summary>
    private Func<IReadOnlyList<MapIssue>> ValidationOfSnapshot()
    {
        var document = MapConverter.ToDocument(_editor.Scenario);
        return () => MapValidator.Validate(MapConverter.ToGameState(document, GameContent.All, checkEvents: false));
    }

    private void ShowTile(MapPosition position)
    {
        _context.SetLevel(position.Level);
        _camera.FocusOn(MapSpace.ToWorld(position));
    }

    /// <summary>A jump link of the inspector (ADR-254): the view goes to the linked thing, which becomes the selection and the inspected one.</summary>
    private void Jump(MapLink link)
    {
        ShowTile(link.Position);
        var target = link.Object is not null || link.Hero is not null;
        if (target && _tools.Tool is not SelectTool)
        {
            _tools.Use(_tools.Tools.OfType<SelectTool>().First()); // the next click selects, not places or links
        }

        if (link.Object is { } id && _editor.Scenario.FindObject(id) is { } mapObject)
        {
            _editor.Selecting.Set(new SelectionItems([], [id], [], []));
            _toolContext.Inspect(mapObject);
        }
        else if (link.Hero is { } hero && _editor.Scenario.Heroes.Any(h => h.Id == hero))
        {
            _editor.Selecting.Set(new SelectionItems([], [], [hero], []));
            _toolContext.Inspect(hero);
        }
        else
        {
            Say(link.Text);
        }
    }

    /// <summary>An event link (inspector, validation): the "Events" tab with that event open.</summary>
    private void OpenEvent(string name)
    {
        _workspace.ShowTab(_events);
        _events.Select(name);
    }

    /// <summary>The templates the template editor can open: the shipped ones, then the user's files.</summary>
    private IReadOnlyList<TemplateSource> TemplateSources() =>
    [
        .. BuiltInTemplates.All.Select(t => new TemplateSource($"{t.Name} (shipped)", () => t)),
        .. UserTemplates().Select(u => new TemplateSource($"{u.Template.Name} ({Path.GetFileName(u.Path)})", () => TemplateSerializer.Load(u.Path))),
    ];

    /// <summary>The readable template files in the user's templates folder.</summary>
    private static List<(string Path, MapTemplate Template)> UserTemplates()
    {
        var directory = ProjectSettings.GlobalizePath(TemplatesDirectory);
        if (!Directory.Exists(directory))
        {
            return [];
        }

        var found = new List<(string, MapTemplate)>();
        foreach (var file in Directory.GetFiles(directory, "*" + TemplateSerializer.FileExtension).Order(StringComparer.Ordinal))
        {
            try
            {
                found.Add((file, TemplateSerializer.Load(file)));
            }
            catch (Exception e) when (e is FormatException or NotSupportedException or IOException)
            {
                GD.PushWarning($"Skipping template {file}: {e.Message}");
            }
        }

        return found;
    }

    private string SaveTemplate(MapTemplate template)
    {
        var directory = ProjectSettings.GlobalizePath(TemplatesDirectory);
        Directory.CreateDirectory(directory);
        var safeName = string.Concat(template.Name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim();
        var path = Path.Combine(directory, (safeName.Length == 0 ? "untitled" : safeName) + TemplateSerializer.FileExtension);
        TemplateSerializer.Save(template, path);
        var errors = TemplateValidator.Validate(template).Count(i => i.Severity == IssueSeverity.Error);
        return errors == 0 ? $"Saved {path}" : $"Saved {path} with {errors} error(s): it cannot generate until they are fixed.";
    }

    /// <summary>"Generate" in the template editor: the template as it is, every slot a player, towns drawn; opens the map like "From template".</summary>
    private string? GenerateFromEditor(MapTemplate template, int size, ulong seed)
    {
        EditorMap map;
        try
        {
            map = EditorMapGenerator.Generate(template, size, seed, template.MaxPlayers, new Dictionary<int, string>(), GameContent.All);
        }
        catch (ArgumentException e)
        {
            return e.Message;
        }

        _files.OpenNew(map.Scenario, () => Describe(template, size, seed, map));
        return null;
    }

    private void Describe(MapTemplate template, int size, ulong seed, EditorMap map)
    {
        var attempt = map.Generation.Attempt == 0 ? "" : $" (attempt {map.Generation.Attempt})";
        var problems = map.Problems.Count == 0 ? "" : $" Generator problems: {string.Join(" ", map.Problems)}";
        var issues = map.Issues.Count == 0 ? "" : $" {map.Issues.Count} validator issue(s), see Problems.";
        Say($"Generated {template.Name} {size}×{size}, seed {seed}{attempt}.{problems}{issues}");
    }

    /// <summary>
    /// "Generate from template" (ADR-087): a new map like "New", from <see cref="EditorMapGenerator"/>; a message tells how it was made, the
    /// generator's problems if every attempt had some, and how many validator issues there are. A refused request returns its reason to the panel.
    /// </summary>
    private string? Generate(GenerateRequest request)
    {
        var template = _generatorTemplates[request.Template];
        EditorMap map;
        try
        {
            map = EditorMapGenerator.Generate(
                template, request.Size, request.Seed, request.Players, request.Towns, GameContent.All, request.MaxHeroes);
        }
        catch (ArgumentException e)
        {
            return e.Message;
        }

        _lastGenerate = request;
        _files.OpenNew(map.Scenario, () => Describe(template, request.Size, request.Seed, map));
        return null;
    }
}
