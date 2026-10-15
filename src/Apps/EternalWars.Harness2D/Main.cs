using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Debugging;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Harness2D;

/// <summary>
/// Top-down 2D front-end over the exact same <see cref="GameSession"/> the 3D game uses.
/// Purpose: exercise and debug core rules quickly, without 3D assets, cameras or picking getting in the way.
/// Every event on the bus is listed in the log panel on the right.
/// </summary>
public partial class Main : Node2D
{
    private const int TileSize = 26;
    private static readonly Vector2 MapOrigin = new(16, 96);

    private GameSession _session = null!;
    private HeroId? _selectedHero;

    public override void _Ready()
    {
        _session = new GameSession(LoadState());
        BuildUi();

        this.SubscribeWhileInTree<HeroMoved>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<TerrainPainted>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<TurnStarted>(_session.Events, e =>
        {
            _selectedHero = _session.State.HeroesOf(e.Player).FirstOrDefault()?.Id;
            QueueRedraw();
        });

        _session.Turns.StartGame();
    }

    public override void _Draw()
    {
        var map = _session.State.Map;
        foreach (var position in map.Positions(level: 0))
        {
            DrawRect(TileRect(position), Palette.ForTerrain(map.GetTerrain(position)));
        }

        foreach (var hero in _session.State.Heroes.Where(h => h.Position.Level == 0))
        {
            var rect = TileRect(hero.Position);
            DrawCircle(rect.GetCenter(), TileSize * 0.35f, Palette.ForPlayer(hero.Owner));
            if (hero.Id == _selectedHero)
            {
                DrawRect(rect, Colors.White, filled: false, width: 2);
            }
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            HandleClick(ToMapPosition(click.Position));
        }
        else if (@event is InputEventKey { Pressed: true } key && _selectedHero is { } hero &&
                 KeyboardDirections.ToDirection(key.Keycode) is { } direction)
        {
            Report(_session.Movement.TryStep(hero, direction).ToString());
        }
    }

    private void HandleClick(MapPosition target)
    {
        if (_session.State.HeroAt(target) is { } clicked && clicked.Owner == _session.State.CurrentPlayer.Id)
        {
            _selectedHero = clicked.Id;
            QueueRedraw();
            return;
        }

        if (_selectedHero is not { } heroId)
        {
            return;
        }

        var from = _session.State.GetHero(heroId).Position;
        Report(DirectionExtensions.FromOffset(target.X - from.X, target.Y - from.Y) is { } direction
            ? _session.Movement.TryStep(heroId, direction).ToString()
            : "Click an adjacent tile to step");
    }

    private void Report(string message) => GetNode<Label>("Ui/Top/Status").Text = message;

    private void BuildUi()
    {
        var ui = new CanvasLayer { Name = "Ui" };
        AddChild(ui);

        var top = new VBoxContainer { Name = "Top", Position = new Vector2(16, 8) };
        ui.AddChild(top);
        var turnPanel = new TurnPanel();
        top.AddChild(turnPanel);
        turnPanel.Bind(_session);
        var resources = new ResourceBar();
        top.AddChild(resources);
        resources.Bind(_session);
        top.AddChild(new Label { Name = "Status", Text = "Click hero / adjacent tile, or use arrows & numpad" });

        var log = new EventLogPanel
        {
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorBottom = 1,
            OffsetLeft = -560,
            OffsetTop = 8,
            OffsetRight = -8,
            OffsetBottom = -8,
        };
        ui.AddChild(log);
        log.Bind(_session.Events);
    }

    private static Rect2 TileRect(MapPosition position) =>
        new(MapOrigin + new Vector2(position.X * TileSize, position.Y * TileSize), new Vector2(TileSize - 1, TileSize - 1));

    private static MapPosition ToMapPosition(Vector2 screen)
    {
        var local = (screen - MapOrigin) / TileSize;
        return new MapPosition(Mathf.FloorToInt(local.X), Mathf.FloorToInt(local.Y));
    }

    private static GameState LoadState() =>
        LaunchArguments.MapPath is { } path
            ? MapConverter.ToGameState(MapSerializer.Load(path))
            : DemoScenario.Create();
}
