using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Battle;
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
/// Battles open the 3D battle scene (<see cref="BattleView3D"/>): point at a cell to preview, click to move, attack (from
/// the side the pointer is on) or shoot; W wait, D defend, R retreat, Q quick combat, wheel zoom; Enter or Escape leave
/// the result screen.
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
    private CanvasLayer _hud = null!;
    private BattleView3D _battle = null!;
    private BattleHud _battleHud = null!;
    private GameResultPanel _result = null!;
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
        if (_context is null)
        {
            return;
        }

        if (_battle.IsOpen)
        {
            HandleBattleInput(@event);
            return;
        }

        if (_session.State.PendingDecision is not null)
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
        BuildBattle();
        BuildResult();

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

    private void HandleBattleInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseMotion motion:
                HoverBattle(motion.Position);
                return;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click:
                ClickBattle(click.Position);
                break;
            case InputEventKey { Pressed: true, Keycode: Key.Enter or Key.KpEnter or Key.Escape } when _battle.Result is not null:
                _battle.Close();
                break;
            case InputEventKey { Pressed: true } key when BattleKeyCommand(key.Keycode) is { } command:
                _battleHud.Report(_session.Execute(command));
                break;
            default:
                return;
        }

        GetViewport().SetInputAsHandled();
    }

    /// <summary>W wait, D defend, R retreat for the active unit's commander; Q quick combat for either human side.</summary>
    private GameCommand? BattleKeyCommand(Key key)
    {
        if (_battle.Result is not null)
        {
            return null;
        }

        if (key == Key.Q)
        {
            return (_session.Battles.Commander(BattleSide.Attacker) ?? _session.Battles.Commander(BattleSide.Defender)) is { } any
                ? new AutoResolveBattleCommand(any)
                : null;
        }

        return _battle.Commander is not { } player ? null : key switch
        {
            Key.W => new BattleWaitCommand(player),
            Key.D => new BattleDefendCommand(player),
            Key.R => new BattleRetreatCommand(player),
            _ => null,
        };
    }

    private void HoverBattle(Vector2 screen)
    {
        if (_session.State.Battle is not { } battle || _battle.PickPoint(screen) is not { } point)
        {
            _battle.ShowIntent(null);
            _battleHud.ShowUnit(null);
            return;
        }

        var cell = BattleSpace.ToCell(point);
        _battleHud.ShowUnit(battle.UnitAt(cell));
        _battle.ShowIntent(_battle.Commander is not null ? BattleTargeting.ActionAt(battle, cell, point.X, point.Y) : null);
    }

    private void ClickBattle(Vector2 screen)
    {
        if (_battle.Commander is not { } player || _session.State.Battle is not { } battle || _battle.PickPoint(screen) is not { } point)
        {
            return;
        }

        GameCommand? command = BattleTargeting.ActionAt(battle, BattleSpace.ToCell(point), point.X, point.Y) switch
        {
            MoveAction move => new BattleMoveCommand(player, move.To),
            AttackAction attack => new BattleAttackCommand(player, attack.Target, attack.From),
            ShootAction shoot => new BattleShootCommand(player, shoot.Target),
            _ => null,
        };
        if (command is not null)
        {
            _battle.ShowIntent(null);
            _battleHud.Report(_session.Execute(command));
        }
    }

    /// <summary>The battle scene replaces the adventure map and its HUD while it is open.</summary>
    private void BuildBattle()
    {
        _battle = new BattleView3D();
        AddChild(_battle);
        _battle.Bind(_session);

        var layer = new CanvasLayer { Name = "BattleHud", Layer = 2 };
        AddChild(layer);
        _battleHud = new BattleHud();
        layer.AddChild(_battleHud);
        _battleHud.Bind(_session, _battle);

        _battle.Opened += () =>
        {
            _world.Visible = false;
            _hud.Visible = false;
            _camera.ProcessMode = ProcessModeEnum.Disabled;
            _world.PathPreview.Clear();
        };
        _battle.Closed += () =>
        {
            _world.Visible = true;
            _hud.Visible = true;
            _camera.ProcessMode = ProcessModeEnum.Inherit;
            _camera.MakeCurrent();
        };
    }

    /// <summary>
    /// The result screen of a won or lost game (ADR-057). A battle that decided the game is shown to its end first.
    /// </summary>
    private void BuildResult()
    {
        var layer = new CanvasLayer { Name = "Result", Layer = 3 };
        AddChild(layer);
        _result = new GameResultPanel();
        layer.AddChild(_result);

        this.SubscribeWhileInTree<PlayerDefeated>(_session.Events, e =>
            _status.Text = $"{_session.State.GetPlayer(e.Player).Name} is defeated: {e.Reason}");
        this.SubscribeWhileInTree<GracePeriodStarted>(_session.Events, e =>
            _status.Text = $"{_session.State.GetPlayer(e.Player).Name} lost its last town and is defeated on {e.DefeatDay} unless it owns one again.");
        this.SubscribeWhileInTree<GracePeriodEnded>(_session.Events, e =>
            _status.Text = $"{_session.State.GetPlayer(e.Player).Name} owns a town again.");
        this.SubscribeWhileInTree<GameEnded>(_session.Events, _ =>
        {
            if (!_battle.IsOpen)
            {
                ShowResult();
            }
        });
        _battle.Closed += () =>
        {
            if (_session.State.Outcome is not null)
            {
                ShowResult();
            }
        };
    }

    private void ShowResult()
    {
        if (_session.State.Outcome is { } outcome)
        {
            _result.ShowOutcome(_session.State, outcome);
        }
    }

    private void BuildHud()
    {
        var hud = new CanvasLayer { Name = "Hud" };
        _hud = hud;
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

        choices.Add(new ScenarioChoice("Demo Valley", options => MapConverter.ToGameState(MapConverter.ToDocument(DemoScenario.Create()), options: options, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings)));
        choices.Add(new ScenarioChoice("Two Towns", options => MapConverter.ToGameState(MapConverter.ToDocument(TownsScenario.Create(GameContent.Creatures)), options: options, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings)));
        var editorMaps = Path.Combine(Path.GetDirectoryName(OS.GetUserDataDir())!, EditorMapsFolder);
        if (Directory.Exists(editorMaps))
        {
            choices.AddRange(Directory.GetFiles(editorMaps, "*" + MapSerializer.FileExtension).Order().Select(FromFile));
        }

        return choices;

        static ScenarioChoice FromFile(string file) =>
            new(Path.GetFileName(file), options => MapConverter.ToGameState(MapSerializer.Load(file), options: options, creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings));
    }
}
