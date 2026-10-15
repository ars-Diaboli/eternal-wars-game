using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Cameras;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Game;

/// <summary>
/// Game entry point. Wires a <see cref="GameSession"/> to 3D views and the HUD, then translates
/// player input into session calls. All rules live in core; this class only composes and forwards.
/// </summary>
/// <remarks>Run with <c>-- --map=&lt;path&gt;</c> to load a map saved by the editor.</remarks>
public partial class Main : Node3D
{
    private GameSession _session = null!;
    private StrategyCamera _camera = null!;
    private HeroId? _selectedHero;

    public override void _Ready()
    {
        _session = new GameSession(LoadState());

        var mapView = new AdventureMapView { Name = "Map" };
        AddChild(mapView);
        mapView.Bind(_session.State.Map, _session.Events);

        foreach (var hero in _session.State.Heroes)
        {
            var heroView = new HeroView();
            AddChild(heroView);
            heroView.Bind(hero, _session.Events);
        }

        AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-55, -35, 0), ShadowEnabled = true });
        _camera = new StrategyCamera { Name = "Camera" };
        AddChild(_camera);

        BuildHud();

        this.SubscribeWhileInTree<TurnStarted>(_session.Events, OnTurnStarted);

        // TODO(Phase 1 UI): replace with chest and exchange dialogs. Until then take the gold / close the exchange so the game never stalls.
        this.SubscribeWhileInTree<DecisionRequested>(_session.Events, e =>
        {
            switch (e.Decision)
            {
                case TreasureChestDecision:
                    _session.Interactions.ResolveTreasureChest(TreasureChestReward.Gold);
                    break;
                case HeroExchangeDecision:
                    _session.Interactions.CompleteExchange();
                    break;
            }
        });
        _session.Turns.StartGame();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } key && _selectedHero is { } hero &&
            KeyboardDirections.ToDirection(key.Keycode) is { } direction)
        {
            _session.Movement.TryStep(hero, direction);
            GetViewport().SetInputAsHandled();
        }
    }

    private void OnTurnStarted(TurnStarted e)
    {
        var hero = _session.State.HeroesOf(e.Player).FirstOrDefault();
        _selectedHero = hero?.Id;
        if (hero is not null)
        {
            _camera.FocusOn(MapSpace.ToWorld(hero.Position));
        }
    }

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

        top.AddChild(new Label { Text = "Arrows / numpad: move hero   WASD: pan   Q/E: rotate   Wheel: zoom" });
    }

    private static GameState LoadState() =>
        LaunchArguments.MapPath is { } path
            ? MapConverter.ToGameState(MapSerializer.Load(path))
            : DemoScenario.Create();
}
