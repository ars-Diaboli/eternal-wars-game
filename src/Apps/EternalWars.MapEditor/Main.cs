using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Settings;
using EternalWars.Core.Data.Stamps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Generation;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI;
using EternalWars.Godot.UI.Editor;
using EternalWars.Godot.UI.Editor.Help;
using EternalWars.Godot.UI.Editor.Shell;
using EternalWars.Godot.UI.Editor.Tools;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// Map editor entry point: composition and input mapping only. Editing operations, undo/redo and validation live in core
/// (<see cref="MapEditorSession"/>); the views and panels live in the libraries (the window layout is <see cref="EditorWorkspace"/>). This
/// class makes the editor's lasting parts, builds the views and the panels for each map, and routes the pointer to the tool in use
/// (<see cref="IEditorTool"/>) and the keys to the commands. The commands, the menus and the editor's windows are composed by one composer
/// per area (ADR-288), sharing the <see cref="EditorParts"/>; the map's files, the autosave and the questions over unsaved work are the
/// <see cref="FileService"/>'s, the template's the <see cref="TemplateFileService"/>'s.
/// Alt+Enter switches the window mode (ADR-186), kept in the editor's own settings file.
/// </summary>
public partial class Main : Node3D
{
    private readonly EditorParts _parts = new();
    private FileComposer _file = null!;
    private EditComposer _edit = null!;
    private MapComposer _map = null!;
    private TestComposer _test = null!;
    private ViewComposer _view = null!;
    private WindowComposer _windows = null!;
    private GenerateComposer _generator = null!;
    private MenuComposer _menus = null!;
    private ContextMenuComposer _contextMenus = null!;

    private CanvasLayer? _ui;
    private OverlayView _overlayView = null!;
    private EventCellsView _eventCells = null!;
    private ProblemsPanel _problems = null!;
    private MapFiles _mapFiles = null!;

    /// <summary>The empty state (ADR-259) is still to show: the editor started on a blank map and no other map was opened since.</summary>
    private bool _emptyState = LaunchArguments.MapPath is null;

    public override void _Ready()
    {
        UiTheme.Apply(compact: true); // the look (ADR-191) and the spacing (ADR-163) in the compact variant for a tool (ADR-250)
        UiIcons.LabelsOnStatusBar = false; // the editor's status bar shows no hints: icon buttons name themselves in their tooltips (ADR-204)
        WindowModes.Apply(SettingsFile.LoadInterface(EditorFolders.SettingsPath).Window);
        if (AutoInput.FromLaunchArguments() is { } autoInput)
        {
            AddChild(autoInput);
        }

        _parts.Root = this;
        _generator = new GenerateComposer(_parts);
        _file = new FileComposer(_parts, _generator);
        _edit = new EditComposer(_parts);
        _map = new MapComposer(_parts);
        _test = new TestComposer(_parts);
        _view = new ViewComposer(_parts);
        _windows = new WindowComposer(_parts);
        _menus = new MenuComposer(_parts, _file);
        _contextMenus = new ContextMenuComposer(_parts, _edit, _view);

        var camera = _parts.Camera = new EditorCamera { Name = "Camera" };
        AddChild(camera);
        camera.Zoomed += () => _parts.StatusBar.ShowZoom(camera.ZoomPercent);
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0) });
        AddChild(_parts.Overlays);
        var settings = _parts.Settings = new EditorSettingsFile(Path.Combine(EditorFolders.Data, EditorSettingsFile.FileName));
        _parts.Preferences = PreferencesStore.Read(settings);
        var files = _parts.Files = new FileService { Name = "Files" };
        AddChild(files);
        files.Bind(
            new FileServiceHost(Open, _parts.Say), settings, _mapFiles = new MapFiles(EditorFolders.Maps, EditorFolders.Autosave),
            systemDialogs: LaunchArguments.AutoInput is null); // a scripted run sees only Godot's own dialog
        files.Changed += ShowFile;
        var templateFiles = _parts.TemplateFiles = new TemplateFileService { Name = "TemplateFiles" };
        AddChild(templateFiles);
        templateFiles.Bind(
            _parts.Templates, new TemplateFiles(EditorFolders.Templates, EditorFolders.TemplateAutosave), settings, _parts.Say,
            systemDialogs: LaunchArguments.AutoInput is null);
        templateFiles.Changed += ShowTemplateFile;
        GetTree().AutoAcceptQuit = false; // the window's close request waits for the questions over unsaved work
        _windows.ApplyPreferences();
        camera.KeysBlocked = () => _parts.IsBusy || _parts.Workspace.Page != EditorWorkspace.MapPage;

        // --perf=report times opening the --map= file, painting and validating (ADR-278): the editor starts blank and the probe opens it.
        if (LaunchArguments.PerfReport is { } report && LaunchArguments.MapPath is { } timed)
        {
            StartPerfProbe(timed, report);
            files.Start(Blank(), null);
            return;
        }

        // --map=file opens that map (the smoke scenarios), else a blank one.
        files.Start(LaunchArguments.MapPath is { } path ? MapFiles.Read(path) : Blank(), LaunchArguments.MapPath);
        if (!files.IsBusy)
        {
            templateFiles.OfferRestore(); // else when the Template tab first shows, not over the map's question
        }
    }

    /// <summary>The editor's timing (ADR-278): the probe opens the map like File → Open, paints with the Terrain tool and waits for the Problems panel.</summary>
    private void StartPerfProbe(string map, string report)
    {
        var probe = new EditorPerfProbe { Name = "PerfProbe" };
        AddChild(probe);
        probe.Bind(
            new EditorPerfHost(
                path => _parts.Files.Open(path), () => _mapFiles.LastLoadMs, () => _parts.Editor.Scenario.Map, () => _parts.Workspace.MapView.GetGlobalRect(),
                () => _parts.Commands.Run("tool.terrain"), () => _problems, ValidationOfSnapshot),
            Path.GetFullPath(map), Path.GetFullPath(report));
    }

    /// <summary>The map the editor starts with: blank, of the size the preferences say.</summary>
    private GameState Blank() =>
        BlankScenario.Create(_parts.Preferences.DefaultMapSize, _parts.Preferences.DefaultMapSize, content: GameContent.All);

    public override void _Notification(int what)
    {
        if (what == NotificationWMCloseRequest)
        {
            _file.Quit();
        }
    }

    /// <summary>
    /// Alt+Enter switches the window mode wherever the focus is, a text field included; the help's key (F1) opens the help at what is under
    /// the pointer, over a window too.
    /// </summary>
    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventMouse mouse)
        {
            _windows.Pointer = mouse.Position; // where the GUI sees the pointer, for the help's page under it
        }

        if (@event is InputEventKey key && _parts.Commands?["help.help"].Shortcut is { } help && help.Matches(key) && !IsTextKey(key)) // a plain key rebound to it stays a text field's
        {
            GetViewport().SetInputAsHandled();
            _windows.OpenHelp(GetViewport().GuiGetHoveredControl());
            return;
        }

        if (!WindowModes.IsToggleKey(@event))
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        var path = EditorFolders.SettingsPath;
        var settings = SettingsFile.LoadInterface(path).WithWindowToggled();
        WindowModes.Apply(settings.Window);
        try
        {
            SettingsFile.Save(path, SettingsFile.Load(path), settings, SettingsFile.LoadLayout(path));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _parts.Say($"Could not keep the window mode: {e.Message}");
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_parts.IsBusy)
        {
            return;
        }

        var tools = _parts.Tools;
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when !_parts.Pressed:
                GetViewport().GuiReleaseFocus(); // a click on the map leaves the text field that was typed into: the keys are the camera's again
                if (Point(press.Position) is not { } tile)
                {
                    return;
                }

                if (_parts.Events.PlacingCells is not null)
                {
                    _parts.ToolContext.Report(_parts.Events.ClickCell(tile));
                }
                else
                {
                    _parts.Pressed = true;
                    _parts.ToolContext.Pointer = new PointerPress(press.ShiftPressed, press.AltPressed, press.DoubleClick);
                    tools.Tool.Press(tile);
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Right, Pressed: true } menu when !_parts.Pressed && _parts.Events.PlacingCells is null:
                GetViewport().GuiReleaseFocus();
                if (Point(menu.Position) is not { } pointed)
                {
                    return;
                }

                _contextMenus.OpenMapMenu(pointed, menu.Position); // the context menu (ADR-286)
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when _parts.Pressed:
                _parts.Pressed = false;
                tools.Tool.Release();
                break;
            case InputEventMouseMotion motion when _parts.Pressed:
                if (Point(motion.Position) is { } dragged)
                {
                    tools.Tool.Drag(dragged);
                }

                break;
            case InputEventMouseMotion motion:
                tools.Tool.Hover(Point(motion.Position)); // the caption of what is under the pointer shows from afar
                return;
            case InputEventKey key when !_parts.Pressed && !IsTextKey(key) && _parts.Commands.RunShortcut(key): // no command in the middle of a stroke
                _parts.Camera.IgnoreHeldKeys(); // a key that ran a command (E, the Erase tool) does not turn or move the camera as well
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

    /// <summary>The tile at <paramref name="screen"/> becomes the pointed one: the views and the status bar show it.</summary>
    private MapPosition? Point(Vector2 screen)
    {
        var scenario = _parts.Editor.Scenario;
        var tile = MapSpace.PickTile(_parts.Camera, screen, scenario.Map, _parts.Context.Level) is { } picked && scenario.Map.Contains(picked) ? picked : (MapPosition?)null;
        _parts.Context.Point(tile);
        _parts.StatusBar.ShowTile(scenario, tile);
        _parts.Browser.PointAt(tile is { } at ? scenario.Map.GetTerrain(at) : null); // for its filter "terrain under the pointer"
        return tile;
    }

    /// <summary>The map's file or its save state changed: the status bar and the commands follow.</summary>
    private void ShowFile()
    {
        _parts.StatusBar.ShowSaveState(_parts.Files.SaveState);
        _parts.Commands.NotifyStateChanged();
    }

    /// <summary>The template's file or its save state changed: the Template tab's title (an asterisk over unsaved work) and the commands follow.</summary>
    private void ShowTemplateFile()
    {
        if (_ui is null)
        {
            return; // the first map's UI is not built yet; it reads the state when it is
        }

        _parts.Workspace.SetPageTitle(_parts.TemplatePage, _parts.Templates.IsDirty ? "Template*" : "Template");
        _parts.Commands.NotifyStateChanged();
    }

    /// <summary>The page shown changed: the commands follow, and the Template tab, shown the first time, offers a template autosave.</summary>
    private void ShowPage()
    {
        if (_parts.OnTemplateTab)
        {
            _parts.TemplateFiles.OfferRestore();
        }

        _parts.Commands.NotifyStateChanged();
    }

    /// <summary>Shows <paramref name="state"/> as the map being edited, for the <see cref="FileService"/>: every map is opened through it.</summary>
    private MapEditorSession Open(GameState state)
    {
        _parts.World?.QueueFree();
        _parts.TemplateEditor?.GetParent()?.RemoveChild(_parts.TemplateEditor); // kept: the new workspace takes it
        _ui?.QueueFree();
        _parts.Pressed = false;

        var editor = _parts.Editor = new MapEditorSession(state, copier: MapObjectCopier.Instance);
        NewViewContext();
        BuildWorld();

        BuildUi();
        _parts.OverlayPanel.Attach(_parts.Context, _overlayView);
        // On the session's own UI, freed with it on the next Open; the root stays in the tree and would keep every old session alive.
        _ui!.SubscribeWhileInTree<UndoHistoryChanged>(editor.Events, _ =>
        {
            _parts.Commands.NotifyStateChanged();
            _parts.Tools.Tool.Refresh(); // a paste's ghost follows the map
        });
        _ui!.SubscribeWhileInTree<MapResized>(editor.Events, _ => ShowResizedMap());
        _ui!.SubscribeWhileInTree<SelectionChanged>(editor.Events, _ => _parts.Commands.NotifyStateChanged());
        _ui!.SubscribeWhileInTree<UndoHistoryChanged>(_parts.Templates.Events, _ => _parts.Commands.NotifyStateChanged()); // the template's Undo and Redo
        _ui!.SubscribeWhileInTree<MapLayersChanged>(editor.Events, e => _parts.Context.Hidden = e.Hidden); // the views leave out the hidden kinds (ADR-262)

        _parts.Camera.FocusOn(MapSpace.ToWorld(new(state.Map.Width / 2, state.Map.Height / 2)));
        _parts.Bookmarks.Clear();
        return editor;
    }

    /// <summary>A view context for the map's views, with the editor's grid, ghosting and hidden layers.</summary>
    private void NewViewContext()
    {
        var editor = _parts.Editor;
        _parts.Context = new MapViewContext(editor.Scenario, editor.Events) { ShowGrid = _parts.ShowGrid, Hidden = editor.Layers.Hidden, GhostOtherLevel = _parts.GhostLevel };
        _parts.Context.GhostOtherLevelChanged += _view.GhostChanged;
    }

    /// <summary>The views of the map in the 3D scene, made from the scenario as it is now.</summary>
    private void BuildWorld()
    {
        var context = _parts.Context;
        var world = new AdventureWorldView { Name = "World" };
        _parts.World = world;
        AddChild(world);
        world.Bind(context);
        _eventCells = new EventCellsView { Name = "EventCells" };
        world.AddChild(_eventCells);
        _eventCells.Bind(context);
        _parts.SelectionView = new SelectionView { Name = "Selection" };
        world.AddChild(_parts.SelectionView);
        _parts.SelectionView.Bind(context, _parts.Editor.Selection);
        var zones = new StartZonesView { Name = "StartZones" };
        world.AddChild(zones);
        zones.Bind(context);
        _overlayView = new OverlayView { Name = "Overlay" }; // the overlays panel binds it (ADR-264)
        world.AddChild(_overlayView);
    }

    /// <summary>
    /// The map has another size or its content moved (a resize, a crop, a shift or the undo of one): the views of the map are made
    /// again from the scenario. The old ones leave the tree at once, so they hear no later event of a map they no longer fit.
    /// </summary>
    private void ShowResizedMap()
    {
        var highlighted = _eventCells.Highlighted;
        if (_parts.World is { } old)
        {
            RemoveChild(old);
            old.QueueFree();
        }

        // A view context of their own for the new views: the old ones' handlers stay behind with the old context.
        _parts.Pressed = false;
        var level = _parts.Context.Level;
        NewViewContext();
        _parts.Context.SetLevel(level);
        BuildWorld();
        _parts.OverlayPanel.Attach(_parts.Context, _overlayView); // the overlay shown is drawn on the new views
        BuildMinimap(); // of the new size, on the new view context
        _eventCells.Highlighted = highlighted;
        _parts.ToolContext.Marks = _parts.SelectionView;
        _parts.ToolContext.Route = _parts.World!.PathPreview;
        _parts.Tools.Tool.Hover(null); // what the tool drew before a click was on the old views
        _parts.Tools.Tool.Refresh();
        _view.CentreLevel();
        _parts.Commands.NotifyStateChanged();
    }

    /// <summary>The minimap (ADR-261) under the right region's tabs, for the map as it is now.</summary>
    private void BuildMinimap()
    {
        var minimap = new MinimapPanel { Name = "MinimapPanel" };
        _parts.Workspace.SetFooter(EditorRegion.Right, minimap);
        minimap.Bind(_parts.Editor, _parts.Context, _parts.Camera, _parts.Workspace.MapView, _view.ShowTile);
    }

    /// <summary>
    /// For the Problems panel: copies the map into a document now (on the main thread); the work returned builds a scenario of its own from
    /// the copy and validates that, so it can run on a task while the map is edited.
    /// </summary>
    private Func<IReadOnlyList<MapIssue>> ValidationOfSnapshot()
    {
        var document = MapConverter.ToDocument(_parts.Editor.Scenario);
        return () => MapValidator.Validate(MapConverter.ToGameState(document, GameContent.All, checkEvents: false));
    }

    /// <summary>The workspace and its panels for the map just opened, then the commands, the menus and the toolbar from the registry.</summary>
    private void BuildUi()
    {
        var editor = _parts.Editor;
        var settings = _parts.Settings;
        _ui = new CanvasLayer { Name = "Ui" };
        AddChild(_ui);
        var workspace = _parts.Workspace = new EditorWorkspace { Name = "Workspace" };
        _ui.AddChild(workspace);
        workspace.Bind(settings);

        // The commands that need no panel, in the order the palette and the preferences list them; the rest once the panels exist.
        _parts.Commands = new CommandRegistry();
        _file.Register();
        _edit.Register();
        _map.Register();
        _windows.Register();
        _view.Register();
        _test.Register();
        _windows.RegisterHelp();
        var menuBar = new MenuBarView { Name = "Menus" };
        workspace.SetMenus(menuBar);
        var toolbar = new ToolbarView { Name = "Toolbar" };
        workspace.TopBar.AddChild(toolbar);
        var statusBar = _parts.StatusBar = new StatusBarView { Name = "Status" };
        workspace.StatusBar.AddChild(statusBar);
        statusBar.ShowZoom(_parts.Camera.ZoomPercent);
        BuildMinimap();

        // Left: the tools and the options of the one in use, which wrap to the region's width; the object browser takes the room under
        // them and scrolls its own list.
        var tools = _parts.Tools = new ToolPalette();
        var browser = _parts.Browser = new ObjectBrowser { Name = "Browser", SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 320) };
        var left = new VBoxContainer { Name = "Tools" };
        left.AddChild(tools);
        left.AddChild(new HSeparator());
        left.AddChild(browser);
        workspace.AddTab(EditorRegion.Left, left, UiIcon.ToolPanel);

        // Right: the inspector, the events and Generate; the map's settings are a window of their own (F7).
        var inspector = new InspectorPanel { Name = "Inspector" };
        workspace.AddTab(EditorRegion.Right, inspector, UiIcon.Inspector);
        inspector.Bind(editor, settings);
        var toolContext = _parts.ToolContext = new EditorToolContext(editor, inspector, _parts.Say) {
            Marks = _parts.SelectionView, Route = _parts.World!.PathPreview, VisibleArea = _view.VisibleTiles, Clipboard = _parts.Clipboard, Choice = _parts.Choice,
        };
        tools.Bind(EditorTools.All(toolContext));
        tools.ToolChanged += _parts.Commands.NotifyStateChanged;
        tools.ToolPicked += tool => _parts.Say(tool.ClickHint); // what a click does now (ADR-280)

        // The browser's choice is the Object tool's: the entry of the same name in this map's catalog, the treasure chest at first.
        var choice = _parts.Choice;
        var catalog = new BrowserCatalog(editor.Catalog, editor.Scenario.Creatures, editor.Scenario.Artifacts);
        toolContext.Browser = catalog;
        choice.Rebase(catalog.Find(choice.Entry?.Id ?? BuiltInMapObjects.TreasureChest)); // no one is told: the last map's UI still listens until it is freed
        browser.Bind(catalog, choice, settings, () => StampFolder.Names(EditorFolders.Stamps), _edit.PasteStamp, EditorFolders.Thumbnails);
        choice.Changed += _edit.UseChoice;
        browser.TreeExiting += () => choice.Changed -= _edit.UseChoice; // this UI goes when another map is opened; the choice stays
        var layers = _parts.Layers = new LayersPanel { Name = "Layers" };
        workspace.AddTab(EditorRegion.Right, layers, UiIcon.Layers);
        layers.Bind(editor, _parts.ShowGrid, _view.ShowGrid);
        var overlays = _parts.OverlayPanel = new OverlaysPanel { Name = "Overlays" };
        workspace.AddTab(EditorRegion.Right, overlays, UiIcon.Overlays);
        overlays.Bind(editor, state => GeneratedZones.Read(state, _generator.Offered, GameContent.All), workspace.MapView);
        var fairness = new FairnessPanel { Name = "Fairness" };
        workspace.AddTab(EditorRegion.Right, fairness, UiIcon.Fairness);
        fairness.Bind(editor);
        fairness.StartSelected += _view.ShowTile;
        var events = _parts.Events = new EventsPanel { Name = "Events" }.Tag("Map events"); // F1 (ADR-276): not the inspector's Events section
        workspace.AddTab(EditorRegion.Right, events, UiIcon.Events);
        events.Bind(editor, MapEventNodeJson.Instance);
        var generate = _parts.Generate = new GeneratePanel { Name = "Generate" }; // the new-map wizard ends in it with a template
        workspace.AddTab(EditorRegion.Right, generate, UiIcon.Generate);
        _generator.Reload();
        generate.Bind(_generator.GeneratorTemplates(), _generator.Generate, _generator.LastRequest);

        // The empty state (ADR-259): over the blank map the editor starts with, until its first edit or another map.
        if (_emptyState)
        {
            _emptyState = false;
            var card = new EmptyStateCard { SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter };
            var top = new MarginContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            top.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
            top.GrowHorizontal = Control.GrowDirection.Both;
            top.AddThemeConstantOverride("margin_top", 16);
            workspace.MapView.AddChild(top);
            top.AddChild(card);
            card.Bind(editor, _file.OpenWizard, () => _parts.Files.Open(), SampleMaps.All, _file.OpenSample);
        }

        // Bottom: the map's problems, the undo history and the editor's messages. Each scrolls by itself.
        var problems = _problems = new ProblemsPanel { Name = "Problems" };
        workspace.AddTab(EditorRegion.Bottom, problems, UiIcon.Error, scroll: false);
        problems.Changed += () =>
        {
            statusBar.ShowProblems(problems.Errors, problems.Warnings);
            statusBar.ShowWork(problems.IsChecking ? "Checking the map…" : null);
        };
        problems.Bind(editor, ValidationOfSnapshot);
        problems.IssueSelected += _view.ShowTile;
        problems.EventSelected += _view.OpenEvent;
        problems.FixReported += _parts.Say;
        statusBar.ProblemsRequested += () => workspace.ShowTab(problems);
        var history = new HistoryPanel { Name = "History" };
        workspace.AddTab(EditorRegion.Bottom, history, UiIcon.History, scroll: false);
        history.Bind(editor);
        var output = new OutputPanel { Name = "Output" };
        workspace.AddTab(EditorRegion.Bottom, output, UiIcon.Output, scroll: false);
        output.Bind(_parts.Log);
        events.SelectionChanged += name => _eventCells.Highlighted = name;
        events.ShowRequested += _view.ShowTile;
        inspector.EventRequested += _view.OpenEvent;
        inspector.LinkRequested += _view.Jump;

        // The template editor (ADR-117) is the workspace's Template tab (EDIT-3, ADR-250), the same one from map to map.
        var templateEditor = _parts.TemplateEditor;
        _parts.TemplateEditor ??= new TemplateEditorPanel { Name = "TemplateEditor" };
        _parts.TemplatePage = workspace.AddPage("Template", _parts.TemplateEditor);
        if (templateEditor is null)
        {
            _parts.TemplateEditor.Bind(
                _parts.Templates,
                new TemplateEditorHost(_parts.TemplateFiles, GameContent.All, _generator.OpenGenerated, new ZonePresetFiles(EditorFolders.TemplatePresets)));
        }

        workspace.SetPageTitle(_parts.TemplatePage, _parts.Templates.IsDirty ? "Template*" : "Template");
        workspace.PageChanged += ShowPage;

        // The panels and the tools exist: the commands that open them, then the menus and the toolbar built from the registry.
        var panelCommands = _view.RegisterPanels(
            ("map.events", "Events", UiIcon.Events, events),
            ("view.problems", "Problems", UiIcon.Error, problems),
            ("view.history", "History", UiIcon.History, history),
            ("view.output", "Output", UiIcon.Output, output),
            ("view.inspector", "Inspector", UiIcon.Inspector, inspector),
            ("view.layers", "Layers", UiIcon.Layers, layers),
            ("view.overlays", "Overlays", UiIcon.Overlays, overlays),
            ("view.fairness", "Fairness", UiIcon.Fairness, fairness));
        _menus.RegisterTools();
        _contextMenus.Bind(panelCommands);
        _windows.UseShortcuts();
        _menus.Bind(menuBar, toolbar);
        workspace.RegionsChanged += _parts.Commands.NotifyStateChanged;
    }
}
