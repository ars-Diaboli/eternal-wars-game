using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Settings;
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
/// in use (<see cref="IEditorTool"/>), and does file I/O. Alt+Enter switches the window mode (ADR-186), kept in the editor's own settings file.
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
    private LineEdit _fileName = null!;
    private StatusBarView _statusBar = null!;

    /// <summary>The editor's messages: kept while maps are opened and closed, shown by the Output panel.</summary>
    private readonly EditorLog _log = new();

    /// <summary>What the status bar says about saving: set when a map is opened, changed and saved.</summary>
    private string _saveState = "";
    private EditorToolContext _toolContext = null!;
    private CommandRegistry _commands = null!;

    /// <summary>The left button is held since the tool in use got its press.</summary>
    private bool _pressed;
    private GenerateRequest? _lastGenerate;
    private EditorWorkspace _workspace = null!;
    private GeneratePanel _generate = null!;

    /// <summary>The template being edited: kept while maps are opened and generated (the editor itself is rebuilt with the UI).</summary>
    private readonly TemplateEditorSession _templates = new(content: GameContent.All);
    private TemplateEditorPanel _templateEditor = null!;
    private IReadOnlyList<MapTemplate> _generatorTemplates = [];

    public override void _Ready()
    {
        UiTheme.Apply(); // the shared margins and gaps (ADR-163) and the look (ADR-191)
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
        // --map=file opens that map (the smoke scenarios), else a blank one.
        Open(LaunchArguments.MapPath is { } path
            ? MapConverter.ToGameState(MapSerializer.Load(path), GameContent.All, checkEvents: false)
            : BlankScenario.Create(36, 36, content: GameContent.All));
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

    /// <summary>The editor's own settings file (the workspace layout), beside the settings file the launch arguments name when they name one.</summary>
    private static string LayoutPath => Path.Combine(
        LaunchArguments.SettingsPath is { } settings ? Path.GetDirectoryName(Path.GetFullPath(settings))! : ProjectSettings.GlobalizePath("user://"),
        EditorWorkspace.SettingsFileName);

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when !_pressed:
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
            case InputEventKey key when _commands.RunShortcut(key):
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>The tile at <paramref name="screen"/> becomes the pointed one: the views and the status bar show it.</summary>
    private MapPosition? Point(Vector2 screen)
    {
        var tile = Pick(screen);
        _context.Point(tile);
        _statusBar.ShowTile(_editor.Scenario, tile);
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

    /// <summary><paramref name="state"/> is what the status bar says while the map has no unsaved work.</summary>
    private void SetSaveState(string state)
    {
        _saveState = state;
        ShowSaveState();
    }

    private void ShowSaveState() => _statusBar.ShowSaveState(_editor.IsDirty ? "Unsaved changes" : _saveState);

    private MapPosition? Pick(Vector2 screen) =>
        MapSpace.PickTile(_camera, screen, _editor.Scenario.Map, _context.Level) is { } tile && _editor.Scenario.Map.Contains(tile) ? tile : null;

    /// <summary>Opens <paramref name="state"/> as the map being edited; <paramref name="saveState"/> is what the status bar says until the map is edited.</summary>
    private void Open(GameState state, string saveState = "Not saved")
    {
        _saveState = saveState;
        _world?.QueueFree();
        _ui?.QueueFree();
        _pressed = false;

        _editor = new MapEditorSession(state);
        _context = new MapViewContext(state, _editor.Events) { ShowGrid = _showGrid };
        var world = new AdventureWorldView { Name = "World" };
        _world = world;
        AddChild(world);
        world.Bind(_context);
        _eventCells = new EventCellsView { Name = "EventCells" };
        world.AddChild(_eventCells);
        _eventCells.Bind(_context);
        var zones = new StartZonesView { Name = "StartZones" };
        world.AddChild(zones);
        zones.Bind(_context);

        BuildUi();
        // On the session's own UI, freed with it on the next Open; the root stays in the tree and would keep every old session alive.
        _ui!.SubscribeWhileInTree<UndoHistoryChanged>(_editor.Events, _ => _commands.NotifyStateChanged());
        _ui!.SubscribeWhileInTree<UnsavedWorkChanged>(_editor.Events, _ => ShowSaveState());

        _camera.FocusOn(MapSpace.ToWorld(new(state.Map.Width / 2, state.Map.Height / 2)));
        _fileName.Text = state.Map.Name;
    }

    private void BuildUi()
    {
        _ui = new CanvasLayer { Name = "Ui" };
        AddChild(_ui);
        _workspace = new EditorWorkspace { Name = "Workspace" };
        _ui.AddChild(_workspace);
        _workspace.Bind(LayoutPath);

        RegisterCommands();
        var menuBar = new MenuBarView { Name = "Menus" };
        _workspace.TopBar.AddChild(menuBar);
        var toolRow = new HBoxContainer();
        _workspace.TopBar.AddChild(toolRow);
        var toolbar = new ToolbarView { Name = "Toolbar" };
        toolRow.AddChild(toolbar);
        _fileName = new LineEdit { CustomMinimumSize = new Vector2(240, 0), TooltipText = "The map's name: Save and Load use it as the file name" };
        toolRow.AddChild(_fileName);
        _statusBar = new StatusBarView { Name = "Status" };
        _workspace.StatusBar.AddChild(_statusBar);
        ShowSaveState();
        _statusBar.ShowZoom(_camera.ZoomPercent);

        // Left: the tools and the options of the one in use, which wrap to the region's width.
        _tools = new ToolPalette { Name = "Tools" };
        _workspace.AddTab(EditorRegion.Left, _tools);

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
        _inspector.Bind(_editor);
        _toolContext = new EditorToolContext(_editor, _inspector, Say);
        _tools.Bind(EditorTools.All(_toolContext));
        _tools.ToolChanged += _commands.NotifyStateChanged;
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

        // The template editor (ADR-117): over everything, hidden until "Templates".
        _templateEditor = new TemplateEditorPanel { Name = "TemplateEditor", Visible = false };
        _ui.AddChild(_templateEditor);
        _templateEditor.Bind(_templates, new TemplateEditorHost(TemplateSources, SaveTemplate, GenerateFromEditor));

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
            _commands.Register(new EditorCommand(ToolCommand(tool), tool.Name, () => _tools.Use(tool)) { IsOn = () => _tools.Tool == tool, IsChoice = true });
        }

        menuBar.Bind(_commands,
        [
            new("File", ["file.new", "file.new-two-levels", "file.generate", null, "file.save", "file.load"]),
            new("Edit", ["edit.undo", "edit.redo"]),
            new("View", ["view.level", "view.grid", null, "view.left", "view.bottom", "view.right", "view.panels", null, "view.inspector", "view.problems", "view.history", "view.output"]),
            new("Map", ["map.properties", "map.players", "map.conditions", "map.events", "map.bans", null, "view.problems"]),
            new("Tools", [.. _tools.Tools.Select(ToolCommand)]),
            new("Templates", ["templates.editor", "file.generate"]),
            new("Test", ["test.play"]),
            new("Help", ["help.help"]),
        ]);
        toolbar.Bind(_commands,
        [
            "file.new", "file.new-two-levels", "file.generate", "templates.editor", "file.save", "file.load", null,
            "edit.undo", "edit.redo", null, "view.level", "view.grid", null, "view.left", "view.bottom", "view.right",
        ]);
        _workspace.RegionsChanged += _commands.NotifyStateChanged;
    }

    private static string ToolCommand(IEditorTool tool) => $"tool.{tool.Name.ToLowerInvariant()}";

    private void Command(
        string id, string name, Action run, UiIcon? icon = null, CommandShortcut? shortcut = null, Func<string?>? whyNot = null, Func<bool>? isOn = null) =>
        _commands.Register(new EditorCommand(id, name, run) { Icon = icon, Shortcut = shortcut, WhyNot = whyNot, IsOn = isOn });

    /// <summary>The commands that need no panel: the file, the history, the view. The rest are registered once the panels exist.</summary>
    private void RegisterCommands()
    {
        _commands = new CommandRegistry();
        Command("file.new", "New", () => Open(BlankScenario.Create(36, 36, content: GameContent.All)));
        Command("file.new-two-levels", "New (2 levels)", () => Open(BlankScenario.Create(36, 36, levels: 2, content: GameContent.All)));
        Command("file.generate", "From template", () => _workspace.ShowTab(_generate));
        Command("file.save", "Save", Save, UiIcon.Save);
        Command("file.load", "Load", Load, UiIcon.Load);
        Command("edit.undo", "Undo", () => _editor.Undo(), UiIcon.Undo, new(Key.Z, Ctrl: true),
            () => _editor.History.CanUndo ? null : "Nothing to undo");
        Command("edit.redo", "Redo", () => _editor.Redo(), UiIcon.Redo, new(Key.Y, Ctrl: true),
            () => _editor.History.CanRedo ? null : "Nothing to redo");
        Command("view.level", "Switch level", SwitchLevel, UiIcon.SwitchLevel, new(Key.L),
            () => _editor.Scenario.Map.Levels > 1 ? null : "The map has one level");
        Command("view.grid", "Grid", () => _context.ShowGrid = _showGrid = !_showGrid, isOn: () => _showGrid);
        Command("view.left", "Left panel", () => _workspace.SetOpen(EditorRegion.Left, !_workspace.IsShown(EditorRegion.Left)),
            isOn: () => _workspace.IsShown(EditorRegion.Left));
        Command("view.bottom", "Bottom panel", () => _workspace.SetOpen(EditorRegion.Bottom, !_workspace.IsShown(EditorRegion.Bottom)),
            isOn: () => _workspace.IsShown(EditorRegion.Bottom));
        Command("view.right", "Right panel", () => _workspace.SetOpen(EditorRegion.Right, !_workspace.IsShown(EditorRegion.Right)),
            isOn: () => _workspace.IsShown(EditorRegion.Right));
        Command("view.panels", "Hide all panels", _workspace.ToggleAll, shortcut: new(Key.F11), isOn: () => _workspace.AllHidden);
        Command("templates.editor", "Templates", () => _templateEditor.Open());
        Command("test.play", "Test the map", () => { }, shortcut: new(Key.F5), whyNot: () => "Play-testing from the editor is not built yet");
        Command("help.help", "Help", () => { }, shortcut: new(Key.F1), whyNot: () => "The editor's help is not written yet");
    }

    /// <summary>The next level of the map, with the camera on its middle.</summary>
    private void SwitchLevel()
    {
        _context.SetLevel((_context.Level + 1) % _editor.Scenario.Map.Levels);
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

        Open(map.Scenario);
        Describe(template, size, seed, map);
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
    /// "Generate from template" (ADR-087): a new map like "New", from <see cref="EditorMapGenerator"/>; the status line tells how it was made, the
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
        Open(map.Scenario);
        Describe(template, request.Size, request.Seed, map);
        return null;
    }

    private void Save()
    {
        var map = _editor.Scenario.Map;
        var name = _fileName.Text.Trim();
        if (name != map.Name)
        {
            _editor.Map.SetProperties(name, map.Description);
        }

        var errors = MapValidator.Validate(_editor.Scenario).Count(i => i.Severity == IssueSeverity.Error);
        var path = MapPath(name);
        MapSerializer.Save(MapConverter.ToDocument(_editor.Scenario), path);
        _editor.MarkSaved();
        SetSaveState($"Saved {DateTime.Now:HH:mm}");
        Say(errors == 0 ? $"Saved {path}" : $"Saved {path} with {errors} error(s), see Problems");
    }

    private void Load()
    {
        var path = MapPath(_fileName.Text.Trim());
        if (!File.Exists(path))
        {
            Say($"Not found: {path}");
            return;
        }

        try
        {
            Open(MapConverter.ToGameState(MapSerializer.Load(path), GameContent.All, checkEvents: false), "No changes");
            Say($"Loaded {path}");
        }
        catch (FormatException e)
        {
            Say($"Cannot load {path}: {e.Message}");
        }
    }

    private static string MapPath(string name)
    {
        var directory = ProjectSettings.GlobalizePath(MapsDirectory);
        Directory.CreateDirectory(directory);
        var safeName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(directory, (safeName.Length == 0 ? "untitled" : safeName) + MapSerializer.FileExtension);
    }
}
