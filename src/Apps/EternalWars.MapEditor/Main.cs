using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.MapEditor;

/// <summary>
/// Map editor entry point. Editing operations (<see cref="TerrainBrush"/>, later objects/undo) live in core;
/// this class owns the 3D view, the tool palette and file I/O.
/// </summary>
public partial class Main : Node3D
{
    private const string MapsDirectory = "user://maps";

    private readonly EventBus _events = new();
    private GameState _state = null!;
    private TerrainBrush _brush = null!;
    private StrategyCamera _camera = null!;
    private Node3D _world = null!;
    private LineEdit _fileName = null!;
    private Label _status = null!;
    private bool _painting;

    public override void _Ready()
    {
        _camera = new StrategyCamera { Name = "Camera" };
        AddChild(_camera);
        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0) });
        BuildToolbar();
        Open(BlankScenario.Create(36, 36));
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.Left } button:
                _painting = button.Pressed;
                if (_painting)
                {
                    PaintAt(button.Position);
                }

                break;
            case InputEventMouseMotion motion when _painting:
                PaintAt(motion.Position);
                break;
        }
    }

    private void PaintAt(Vector2 screenPoint)
    {
        if (MapSpace.PickTile(_camera, screenPoint) is { } tile && _state.Map.Contains(tile))
        {
            _brush.Paint(tile);
        }
    }

    private void Open(GameState state)
    {
        _world?.QueueFree();
        _world = new Node3D { Name = "World" };
        AddChild(_world);

        var previous = _brush;
        _state = state;
        _brush = new TerrainBrush(state.Map, _events)
        {
            Terrain = previous?.Terrain ?? TerrainType.Grass,
            Radius = previous?.Radius ?? 0,
        };

        var mapView = new AdventureMapView { Name = "Map" };
        _world.AddChild(mapView);
        mapView.Bind(state.Map, _events);

        foreach (var hero in state.Heroes)
        {
            var heroView = new HeroView();
            _world.AddChild(heroView);
            heroView.Bind(hero, _events);
        }

        _camera.FocusOn(MapSpace.ToWorld(new(state.Map.Width / 2, state.Map.Height / 2)));
        _fileName.Text = state.Map.Name;
    }

    private void BuildToolbar()
    {
        var hud = new CanvasLayer { Name = "Toolbar" };
        AddChild(hud);
        var rows = new VBoxContainer { Position = new Vector2(12, 8) };
        hud.AddChild(rows);

        var terrainRow = new HBoxContainer();
        rows.AddChild(terrainRow);
        var group = new ButtonGroup();
        foreach (var terrain in Enum.GetValues<TerrainType>())
        {
            var button = new Button { Text = terrain.ToString(), ToggleMode = true, ButtonGroup = group };
            button.AddThemeColorOverride("font_color", Palette.ForTerrain(terrain).Lightened(0.3f));
            button.Pressed += () => _brush.Terrain = terrain;
            button.ButtonPressed = terrain == TerrainType.Grass;
            terrainRow.AddChild(button);
        }

        var fileRow = new HBoxContainer();
        rows.AddChild(fileRow);

        fileRow.AddChild(new Label { Text = "Brush radius" });
        var radius = new SpinBox { MinValue = 0, MaxValue = 5 };
        radius.ValueChanged += value => _brush.Radius = (int)value;
        fileRow.AddChild(radius);

        _fileName = new LineEdit { CustomMinimumSize = new Vector2(240, 0) };
        fileRow.AddChild(_fileName);
        AddButton(fileRow, "New", () => Open(BlankScenario.Create(36, 36)));
        AddButton(fileRow, "Save", Save);
        AddButton(fileRow, "Load", Load);

        _status = new Label();
        rows.AddChild(_status);
        rows.AddChild(new Label { Text = "LMB: paint   WASD: pan   Wheel: zoom" });
    }

    private void Save()
    {
        _state.Map.Name = _fileName.Text.Trim();
        var path = MapPath(_state.Map.Name);
        MapSerializer.Save(MapConverter.ToDocument(_state), path);
        _status.Text = $"Saved {path}";
    }

    private void Load()
    {
        var path = MapPath(_fileName.Text.Trim());
        if (!File.Exists(path))
        {
            _status.Text = $"Not found: {path}";
            return;
        }

        Open(MapConverter.ToGameState(MapSerializer.Load(path)));
        _status.Text = $"Loaded {path}";
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
