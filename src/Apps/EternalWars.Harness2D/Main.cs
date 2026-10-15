using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Godot.Shared;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using EternalWars.Godot.UI.Battle;
using EternalWars.Godot.UI.Debugging;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Harness2D;

/// <summary>
/// Top-down 2D front-end over the exact same <see cref="GameSession"/> the 3D game uses.
/// Purpose: exercise and debug core rules quickly, without 3D assets, cameras or picking getting in the way.
/// Every event on the bus is listed in the log panel on the right. Input is mapped to commands only.
/// </summary>
public partial class Main : Node2D
{
    private const int TileSize = 26;
    private static readonly Vector2 MapOrigin = new(16, 120);

    private GameSession _session = null!;
    private HeroId? _selectedHero;

    public override void _Ready()
    {
        _session = new GameSession(LoadState());
        BuildUi();

        this.SubscribeWhileInTree<HeroMoved>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<HeroTeleported>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<KnownObjectsChanged>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<HeroDefeated>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<DecisionResolved>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<TurnStarted>(_session.Events, e =>
        {
            _selectedHero = _session.State.HeroesOf(e.Player).FirstOrDefault()?.Id;
            QueueRedraw();
        });

        _session.Start();
    }

    public override void _Draw()
    {
        var state = _session.State;
        var map = state.Map;
        foreach (var position in map.Positions(level: 0))
        {
            DrawRect(TileRect(position), Palette.ForTerrain(map.GetTerrain(position)));
        }

        foreach (var known in state.Memory.Of(state.CurrentPlayer.Id))
        {
            foreach (var cell in known.Cells().Where(c => c.Level == 0))
            {
                var color = known.Definition.Kind == MapObjectKind.Monster ? new Color(0.85f, 0.1f, 0.1f)
                    : known.Owner is { } owner ? Palette.ForPlayer(owner)
                    : new Color(0.9f, 0.85f, 0.6f);
                DrawRect(TileRect(cell).Grow(-6), color);
            }
        }

        foreach (var hero in state.Heroes.Where(h => h.Position.Level == 0 && state.CanSee(state.CurrentPlayer.Id, h)))
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
        if (_session.State.Battle is not null)
        {
            HandleBattleKey(@event);
            return;
        }

        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            HandleClick(ToMapPosition(click.Position));
        }
        else if (@event is InputEventKey { Pressed: true } key && _selectedHero is { } hero &&
                 KeyboardDirections.ToDirection(key.Keycode) is { } direction)
        {
            Report(_session.Execute(new StepHeroCommand(_session.State.CurrentPlayer.Id, hero, direction)));
        }
    }

    private void HandleClick(MapPosition target)
    {
        var player = _session.State.CurrentPlayer.Id;
        if (_session.State.HeroAt(target) is { } clicked && clicked.Owner == player)
        {
            _selectedHero = clicked.Id;
            QueueRedraw();
            return;
        }

        if (_selectedHero is { } hero)
        {
            Report(_session.Execute(new MoveHeroCommand(player, hero, target)));
        }
    }

    /// <summary>Battle keys: W wait, D defend, R retreat, Q quick combat.</summary>
    private void HandleBattleKey(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } key || ActiveCommander() is not { } player)
        {
            return;
        }

        GameCommand? command = key.Keycode switch
        {
            Key.W => new BattleWaitCommand(player),
            Key.D => new BattleDefendCommand(player),
            Key.R => new BattleRetreatCommand(player),
            Key.Q => new AutoResolveBattleCommand(player),
            _ => null,
        };
        if (command is not null)
        {
            Report(_session.Execute(command));
        }
    }

    /// <summary>Click on an enemy: shoot it if possible, else attack it; click elsewhere: move there.</summary>
    private void HandleBattleClick(CellPosition cell)
    {
        if (_session.State.Battle is not { ActiveUnit: { } active } battle || ActiveCommander() is not { } player)
        {
            return;
        }

        GameCommand command = battle.UnitAt(cell) is { } target && target.Side != active.Side
            ? BattleGrid.CanShoot(battle, active, target, out _)
                ? new BattleShootCommand(player, target.Id)
                : new BattleAttackCommand(player, target.Id)
            : new BattleMoveCommand(player, cell);
        Report(_session.Execute(command));
    }

    private PlayerId? ActiveCommander() =>
        _session.State.Battle is { ActiveUnit: { } active } ? _session.Battles.Commander(active.Side) : null;

    private void Report(CommandResult result) =>
        GetNode<Label>("Ui/Top/Status").Text = result.IsAccepted ? $"{result.Outcome}" : $"Refused: {result.Error}";

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
        top.AddChild(new Label { Name = "Status", Text = "Click hero / tile, arrows & numpad. Battle: click, W wait, D defend, R retreat, Q quick" });
        var decisions = new DecisionPanel();
        top.AddChild(decisions);
        decisions.Bind(_session);

        var board = new BattleBoard2D { Position = MapOrigin };
        ui.AddChild(board);
        board.Bind(_session);
        board.CellClicked += HandleBattleClick;

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
            ? MapConverter.ToGameState(MapSerializer.Load(path), options: new GameOptions(Seed: (ulong)Random.Shared.NextInt64()))
            : DemoScenario.Create();
}
