using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Editor;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// Map editor entry point. Editing operations, undo/redo and validation live in core (<see cref="MapEditorSession"/>);
/// the views and panels live in the libraries; this class composes them, maps mouse and keys to the active
/// tool, and does file I/O.
/// </summary>
public partial class Main : Node3D
{
    private const string MapsDirectory = "user://maps";

    private MapEditorSession _editor = null!;
    private MapViewContext _context = null!;
    private StrategyCamera _camera = null!;
    private Node3D? _world;
    private CanvasLayer? _ui;
    private ToolPalette _tools = null!;
    private InspectorPanel _inspector = null!;
    private LineEdit _fileName = null!;
    private Label _status = null!;
    private Label _history = null!;
    private bool _painting;
    private MapObjectId? _linkFrom;

    public override void _Ready()
    {
        if (AutoInput.FromLaunchArguments() is { } autoInput)
        {
            AddChild(autoInput);
        }

        _camera = new StrategyCamera { Name = "Camera" };
        AddChild(_camera);
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0) });
        Open(BlankScenario.Create(36, 36, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } press when !_painting:
                if (Pick(press.Position) is not { } tile)
                {
                    return;
                }

                if (_tools.Tool.IsBrush)
                {
                    _painting = true;
                    _editor.BeginStroke(_tools.Tool.Kind == ToolKind.Road ? "Paint road" : $"Paint {_tools.Tool.Terrain}");
                    Paint(tile);
                }
                else
                {
                    Click(tile);
                }

                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } when _painting:
                _painting = false;
                _editor.EndStroke();
                break;
            case InputEventMouseMotion motion when _painting:
                if (Pick(motion.Position) is { } dragged)
                {
                    Paint(dragged);
                }

                break;
            case InputEventKey { Pressed: true, Keycode: Key.Z, CtrlPressed: true, ShiftPressed: false }:
                _editor.Undo();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Y, CtrlPressed: true }:
                _editor.Redo();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.L }:
                _context.SetLevel((_context.Level + 1) % _editor.Scenario.Map.Levels);
                _camera.FocusOn(MapSpace.ToWorld(new MapPosition(_editor.Scenario.Map.Width / 2, _editor.Scenario.Map.Height / 2, _context.Level)));
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private MapPosition? Pick(Vector2 screen) =>
        MapSpace.PickTile(_camera, screen, _context.Level) is { } tile && _editor.Scenario.Map.Contains(tile) ? tile : null;

    private void Paint(MapPosition tile)
    {
        var tool = _tools.Tool;
        if (tool.Kind == ToolKind.Road)
        {
            _editor.PaintRoad(tile, tool.Road, tool.Radius);
        }
        else
        {
            _editor.PaintTerrain(tile, tool.Terrain, tool.Radius);
        }
    }

    /// <summary>One click with a placement, erase, link or select tool.</summary>
    private void Click(MapPosition tile)
    {
        var tool = _tools.Tool;
        var scenario = _editor.Scenario;
        var hero = scenario.HeroAt(tile);
        var mapObject = scenario.ObjectCellAt(tile)?.Object;
        switch (tool.Kind)
        {
            case ToolKind.Object:
                Report(_editor.PlaceObject(tool.ObjectDefinition, tile, out _));
                break;
            case ToolKind.Monster:
                Report(_editor.PlaceMonster(tile, tool.Creature, tool.Count, tool.Disposition, neverFlees: false, out _));
                break;
            case ToolKind.Hero:
                Report(_editor.PlaceHero(tool.Player, tile, $"Hero {scenario.NextHeroId().Value}", out var placed), () => Inspect(placed));
                break;
            case ToolKind.Erase:
                Report(hero is not null ? _editor.RemoveHero(hero.Id) : mapObject is not null ? _editor.RemoveObject(mapObject.Id) : EditResult.InvalidTarget);
                break;
            case ToolKind.LinkGates:
                Link(mapObject);
                break;
            case ToolKind.Select when hero is not null:
                Inspect(hero.Id);
                break;
            case ToolKind.Select when mapObject is not null:
                _inspector.InspectObject(mapObject.Id);
                _status.Text = $"Inspecting {mapObject}";
                break;
        }
    }

    /// <summary>First click picks a two-way gate or one-way entrance, the second its partner or exit.</summary>
    private void Link(MapObject? clicked)
    {
        if (_linkFrom is not { } from)
        {
            if (clicked is TwoWayGate or OneWayGateEntrance)
            {
                _linkFrom = clicked.Id;
                _status.Text = $"Linking {clicked}: now click its partner (or exit).";
            }
            else
            {
                _status.Text = "Click a two-way gate or a one-way entrance first.";
            }

            return;
        }

        _linkFrom = null;
        var source = _editor.Scenario.GetObject(from);
        Report(clicked is null ? EditResult.InvalidTarget
            : source is OneWayGateEntrance ? _editor.SetGateExit(from, clicked.Id)
            : _editor.LinkGates(from, clicked.Id));
    }

    private void Inspect(HeroId hero)
    {
        _inspector.InspectHero(hero);
        _status.Text = $"Inspecting {_editor.Scenario.GetHero(hero).Name}";
    }

    private void Report(EditResult result, Action? onSuccess = null)
    {
        _status.Text = result == EditResult.Ok ? "" : $"Refused: {result}";
        if (result == EditResult.Ok)
        {
            onSuccess?.Invoke();
        }
    }

    private void Open(GameState state)
    {
        _world?.QueueFree();
        _ui?.QueueFree();
        _painting = false;
        _linkFrom = null;

        _editor = new MapEditorSession(state);
        _context = new MapViewContext(state, _editor.Events);
        var world = new AdventureWorldView { Name = "World" };
        _world = world;
        AddChild(world);
        world.Bind(_context);

        BuildUi();
        this.SubscribeWhileInTree<UndoHistoryChanged>(_editor.Events, e =>
            _history.Text = $"Undo: {e.UndoDescription ?? "—"}   Redo: {e.RedoDescription ?? "—"}");

        _camera.FocusOn(MapSpace.ToWorld(new(state.Map.Width / 2, state.Map.Height / 2)));
        _fileName.Text = state.Map.Name;
    }

    private void BuildUi()
    {
        _ui = new CanvasLayer { Name = "Ui" };
        AddChild(_ui);
        var rows = new VBoxContainer { Position = new Vector2(12, 8) };
        _ui.AddChild(rows);

        _tools = new ToolPalette();
        rows.AddChild(_tools);
        _tools.Bind(_editor);
        _tools.ToolChanged += _ => _linkFrom = null;

        var fileRow = new HBoxContainer();
        rows.AddChild(fileRow);
        _fileName = new LineEdit { CustomMinimumSize = new Vector2(240, 0) };
        fileRow.AddChild(_fileName);
        AddButton(fileRow, "New", () => Open(BlankScenario.Create(36, 36, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes)));
        AddButton(fileRow, "New (2 levels)", () => Open(BlankScenario.Create(36, 36, levels: 2, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes)));
        AddButton(fileRow, "Save", Save);
        AddButton(fileRow, "Load", Load);
        AddButton(fileRow, "Undo", () => _editor.Undo());
        AddButton(fileRow, "Redo", () => _editor.Redo());
        AddButton(fileRow, "Level (L)", () => _context.SetLevel((_context.Level + 1) % _editor.Scenario.Map.Levels));

        _status = new Label();
        rows.AddChild(_status);
        _history = new Label();
        rows.AddChild(_history);
        rows.AddChild(new Label { Text = "LMB: use tool   Ctrl+Z / Ctrl+Y: undo / redo   L: level   WASD/QE/wheel: camera" });

        var tabs = new TabContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -480, OffsetTop = 8, OffsetRight = -8, OffsetBottom = 560 };
        _ui.AddChild(tabs);
        var properties = new MapPropertiesPanel { Name = "Map" };
        tabs.AddChild(properties);
        properties.Bind(_editor);
        var players = new PlayersPanel { Name = "Players" };
        tabs.AddChild(players);
        players.Bind(_editor);
        _inspector = new InspectorPanel { Name = "Inspector" };
        tabs.AddChild(_inspector);
        _inspector.Bind(_editor);
        var validation = new ValidationPanel { Name = "Validation" };
        tabs.AddChild(validation);
        validation.Bind(_editor);
        validation.IssueSelected += position =>
        {
            _context.SetLevel(position.Level);
            _camera.FocusOn(MapSpace.ToWorld(position));
        };
    }

    private void Save()
    {
        var map = _editor.Scenario.Map;
        var name = _fileName.Text.Trim();
        if (name != map.Name)
        {
            _editor.SetMapProperties(name, map.Description);
        }

        var errors = MapValidator.Validate(_editor.Scenario).Count(i => i.Severity == IssueSeverity.Error);
        var path = MapPath(name);
        MapSerializer.Save(MapConverter.ToDocument(_editor.Scenario), path);
        _status.Text = errors == 0 ? $"Saved {path}" : $"Saved {path} with {errors} error(s), see Validation";
    }

    private void Load()
    {
        var path = MapPath(_fileName.Text.Trim());
        if (!File.Exists(path))
        {
            _status.Text = $"Not found: {path}";
            return;
        }

        try
        {
            Open(MapConverter.ToGameState(MapSerializer.Load(path), creatures: GameContent.Creatures, heroRoster: GameContent.Heroes));
            _status.Text = $"Loaded {path}";
        }
        catch (FormatException e)
        {
            _status.Text = $"Cannot load {path}: {e.Message}";
        }
    }

    private static string MapPath(string name)
    {
        var directory = ProjectSettings.GlobalizePath(MapsDirectory);
        Directory.CreateDirectory(directory);
        var safeName = string.Concat(name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(directory, (safeName.Length == 0 ? "untitled" : safeName) + MapSerializer.FileExtension);
    }

    private static void AddButton(Container parent, string text, Action onPressed)
    {
        var button = new Button { Text = text };
        button.Pressed += onPressed;
        parent.AddChild(button);
    }
}
