using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Setup;
using Godot;

namespace EternalWars.Game;

/// <summary>
/// Game entry point: the new-game screen, then a <see cref="GameSession"/> wired to the 3D world view and the
/// HUD. This class only composes and maps input to commands; all rules live in core.
/// </summary>
/// <remarks>
/// Mouse: click an own hero to select it, click a tile to preview the route (markers coloured by day), click the
/// same tile again to go; right-click clears the preview. Keys: arrows/numpad step, Tab next hero, L switches
/// level, WASD/QE/wheel camera. Run with <c>-- --map=&lt;path&gt;</c> to offer an editor map first.
/// </remarks>
public partial class Main : Node3D
{
    private const string EditorMapsFolder = "Eternal Wars Map Editor/maps";

    private GameSession _session = null!;
    private MapViewContext _context = null!;
    private AdventureWorldView _world = null!;
    private StrategyCamera _camera = null!;
    private HeroListPanel _heroList = null!;
    private Label _status = null!;
    private HeroId? _selectedHero;

    public override void _Ready()
    {
        if (AutoInput.FromLaunchArguments() is { } autoInput)
        {
            AddChild(autoInput);
        }

        var layer = new CanvasLayer { Name = "Setup" };
        AddChild(layer);
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        layer.AddChild(center);
        var setup = new NewGameScreen();
        center.AddChild(setup);
        setup.Bind(Scenarios());
        setup.Started += state =>
        {
            layer.QueueFree();
            StartGame(state);
        };
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_context is null || _session.State.PendingDecision is not null)
        {
            return;
        }

        switch (@event)
        {
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                if (MapSpace.PickTile(_camera, click.Position, _context.Level) is { } tile && _session.State.Map.Contains(tile))
                {
                    ClickTile(tile);
                }

                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
                _world.PathPreview.Clear();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Tab }:
                SelectNextHero();
                break;
            case InputEventKey { Pressed: true, Keycode: Key.L }:
                _context.SetLevel((_context.Level + 1) % _session.State.Map.Levels);
                _camera.FocusOn(MapSpace.ToWorld(new MapPosition(_session.State.Map.Width / 2, _session.State.Map.Height / 2, _context.Level)));
                break;
            case InputEventKey { Pressed: true } key when _selectedHero is { } hero && KeyboardDirections.ToDirection(key.Keycode) is { } direction:
                _world.PathPreview.Clear();
                Report(_session.Execute(new StepHeroCommand(_session.State.CurrentPlayer.Id, hero, direction)));
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    private void StartGame(GameState state)
    {
        _session = new GameSession(state);
        _context = new MapViewContext(state, _session.Events, state.Players[0].Id);

        _world = new AdventureWorldView { Name = "World" };
        AddChild(_world);
        _world.Bind(_context);

        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0), ShadowEnabled = true });
        _camera = new StrategyCamera { Name = "Camera" };
        AddChild(_camera);

        BuildHud();

        this.SubscribeWhileInTree<TurnStarted>(_session.Events, e =>
        {
            // Hot-seat: the map is drawn through the eyes of whoever plays now.
            _context.SetViewer(e.Player);
            Select(_session.State.HeroesOf(e.Player).FirstOrDefault()?.Id, focus: true);
        });
        this.SubscribeWhileInTree<HeroDefeated>(_session.Events, e =>
        {
            if (e.Hero == _selectedHero)
            {
                Select(_session.State.HeroesOf(_session.State.CurrentPlayer.Id).FirstOrDefault()?.Id, focus: false);
            }
        });
        this.SubscribeWhileInTree<HeroTeleported>(_session.Events, e =>
        {
            if (e.Hero == _selectedHero && e.To.Level != _context.Level)
            {
                _context.SetLevel(e.To.Level);
                _camera.FocusOn(MapSpace.ToWorld(e.To));
            }
        });

        _session.Start();
    }

    private void ClickTile(MapPosition tile)
    {
        var state = _session.State;
        var player = state.CurrentPlayer.Id;
        if (state.HeroAt(tile) is { } clicked && clicked.Owner == player)
        {
            Select(clicked.Id, focus: false);
            return;
        }

        if (_selectedHero is not { } hero)
        {
            return;
        }

        // Second click on the previewed destination: go.
        if (_world.PathPreview.Path is { } planned && planned.Hero == hero && planned.Destination == tile)
        {
            _world.PathPreview.Clear();
            Report(_session.Execute(new MoveHeroCommand(player, hero, tile)));
            return;
        }

        if (_session.Movement.FindPath(hero, tile) is { } path)
        {
            _world.PathPreview.Show(path);
            _status.Text = $"{path.Steps.Count} steps, {path.Turns} day(s). Click again to go.";
        }
        else
        {
            _world.PathPreview.Clear();
            _status.Text = "No route there.";
        }
    }

    private void Select(HeroId? hero, bool focus)
    {
        _selectedHero = hero;
        _world.Select(hero);
        _world.PathPreview.Clear();
        _heroList.ShowSelected(hero);
        if (hero is { } id && focus)
        {
            var position = _session.State.GetHero(id).Position;
            _context.SetLevel(position.Level);
            _camera.FocusOn(MapSpace.ToWorld(position));
        }
    }

    private void SelectNextHero()
    {
        var heroes = _session.State.HeroesOf(_session.State.CurrentPlayer.Id).Select(h => h.Id).ToList();
        if (heroes.Count > 0)
        {
            var next = _selectedHero is { } current ? heroes[(heroes.IndexOf(current) + 1) % heroes.Count] : heroes[0];
            Select(next, focus: true);
        }
    }

    private void Report(CommandResult result) =>
        _status.Text = result.IsAccepted ? $"{result.Outcome}" : $"Refused: {result.Error}";

    private void BuildHud()
    {
        var hud = new CanvasLayer { Name = "Hud" };
        AddChild(hud);

        var top = new VBoxContainer { Position = new Vector2(12, 8) };
        hud.AddChild(top);
        var turnPanel = new TurnPanel();
        top.AddChild(turnPanel);
        turnPanel.Bind(_session);
        var resources = new ResourceBar();
        top.AddChild(resources);
        resources.Bind(_session);
        _status = new Label { Text = "Click: route, click again: go. Tab: next hero  L: level  WASD/QE/wheel: camera" };
        top.AddChild(_status);
        var decisions = new DecisionPanel();
        top.AddChild(decisions);
        decisions.Bind(_session);
        var exchange = new ExchangePanel();
        top.AddChild(exchange);
        exchange.Bind(_session);

        // HoMM-style right-hand panel: minimap and hero list.
        var side = new VBoxContainer { AnchorLeft = 1, AnchorRight = 1, OffsetLeft = -340, OffsetTop = 8, OffsetRight = -8 };
        hud.AddChild(side);
        var minimap = new MinimapWidget { CellPixels = Math.Max(2, 320f / Math.Max(_session.State.Map.Width, _session.State.Map.Height)) };
        side.AddChild(minimap);
        minimap.Bind(_context);
        minimap.TileClicked += tile => _camera.FocusOn(MapSpace.ToWorld(tile));
        var level = new Button { Text = "Switch level (L)", Disabled = _session.State.Map.Levels < 2 };
        level.Pressed += () => _context.SetLevel((_context.Level + 1) % _session.State.Map.Levels);
        side.AddChild(level);
        _heroList = new HeroListPanel();
        side.AddChild(_heroList);
        _heroList.Bind(_session);
        _heroList.HeroSelected += hero => Select(hero, focus: true);
    }

    /// <summary>The demo, the map given with --map, and every map saved by the editor.</summary>
    private static List<ScenarioChoice> Scenarios()
    {
        var choices = new List<ScenarioChoice>();
        if (LaunchArguments.MapPath is { } path)
        {
            choices.Add(FromFile(path));
        }

        choices.Add(new ScenarioChoice("Demo Valley", options => MapConverter.ToGameState(MapConverter.ToDocument(DemoScenario.Create()), options: options)));
        var editorMaps = Path.Combine(Path.GetDirectoryName(OS.GetUserDataDir())!, EditorMapsFolder);
        if (Directory.Exists(editorMaps))
        {
            choices.AddRange(Directory.GetFiles(editorMaps, "*" + MapSerializer.FileExtension).Order().Select(FromFile));
        }

        return choices;

        static ScenarioChoice FromFile(string file) =>
            new(Path.GetFileName(file), options => MapConverter.ToGameState(MapSerializer.Load(file), options: options));
    }
}
