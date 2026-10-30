using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Pathfinding;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;
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
    private static readonly Color Unexplored = new(0.05f, 0.05f, 0.06f);

    private HeroId? _selectedHero;
    private HeroPath? _preview;

    /// <summary>The stack picked for placing during a battle's deployment step.</summary>
    private int? _deployPick;

    public override void _Ready()
    {
        if (AutoInput.FromLaunchArguments() is { } autoInput)
        {
            AddChild(autoInput);
        }

        _session = new GameSession(LoadState());
        BuildUi();

        this.SubscribeWhileInTree<HeroMoved>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<HeroTeleported>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<KnownObjectsChanged>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<HeroDefeated>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<DecisionResolved>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<TownOwnerChanged>(_session.Events, _ => QueueRedraw());
        this.SubscribeWhileInTree<MapObjectVisited>(_session.Events, e =>
        {
            // An own hero entering an own town opens the town screen.
            if (_session.State.FindObject(e.Object) is Town town && town.Owner == _session.State.GetHero(e.Hero).Owner)
            {
                GetNode<TownPanel>("Ui/Top/Town").Open(town.Id);
            }
        });
        this.SubscribeWhileInTree<TurnStarted>(_session.Events, e =>
        {
            _preview = null;
            _selectedHero = _session.State.HeroesOf(e.Player).FirstOrDefault()?.Id;
            QueueRedraw();
        });

        _session.Start();
    }

    public override void _Draw()
    {
        var state = _session.State;
        var map = state.Map;
        var viewer = state.CurrentPlayer.Id;
        foreach (var position in map.Positions(level: 0))
        {
            var color = !state.Fog.IsExplored(viewer, position) ? Unexplored
                : state.Fog.IsInSight(viewer, position) ? Palette.ForTerrain(map.GetTerrain(position))
                : Palette.ForTerrain(map.GetTerrain(position)).Darkened(0.45f);
            DrawRect(TileRect(position), color);
            if (map.GetRiver(position) != RiverType.None && state.Fog.IsExplored(viewer, position))
            {
                DrawRect(TileRect(position).Grow(-5), Palette.ForRiver(map.GetRiver(position)));
            }

            if (map.GetRoad(position) != RoadType.None && state.Fog.IsExplored(viewer, position))
            {
                DrawRect(TileRect(position).Grow(-9), Palette.ForRoad(map.GetRoad(position)));
            }
        }

        foreach (var step in _preview?.Steps.Where(s => s.Position.Level == 0) ?? [])
        {
            DrawCircle(TileRect(step.Position).GetCenter(), step == _preview!.Steps[^1] ? 7 : 4, Palette.ForPathDay(step.Day));
        }

        foreach (var known in state.Memory.Of(state.CurrentPlayer.Id))
        {
            foreach (var cell in known.Cells().Where(c => c.Level == 0))
            {
                var color = known.Definition.Kind == MapObjectKind.Monster ? new Color(0.85f, 0.1f, 0.1f)
                    : known.Owner is { } owner ? Palette.ForPlayer(owner)
                    : new Color(0.9f, 0.85f, 0.6f);
                DrawRect(TileRect(cell).Grow(known.Definition.Kind == MapObjectKind.Town ? -2 : -6), color);
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
        if (_session.State.ObjectCellAt(target) is { Object: Town town, Cell: FootprintCell.Blocked } && town.Owner == player)
        {
            GetNode<TownPanel>("Ui/Top/Town").Open(town.Id);
            return;
        }

        if (_session.State.HeroAt(target) is { } clicked && clicked.Owner == player)
        {
            _selectedHero = clicked.Id;
            _preview = null;
            QueueRedraw();
            return;
        }

        if (_selectedHero is not { } hero)
        {
            return;
        }

        // First click previews the route (day-coloured dots), a second click on the same tile goes.
        if (_preview is { } planned && planned.Hero == hero && planned.Destination == target)
        {
            _preview = null;
            Report(_session.Execute(new MoveHeroCommand(player, hero, target)));
        }
        else
        {
            _preview = _session.Movement.FindPath(hero, target);
            GetNode<Label>("Ui/Top/Status").Text = _preview is { } path ? $"{path.Steps.Count} steps, {path.Turns} day(s): click again to go" : "No route";
        }

        QueueRedraw();
    }

    /// <summary>Battle keys: W wait, D defend, R retreat, S surrender, Q quick combat; Enter starts the battle after deployment.</summary>
    private void HandleBattleKey(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true } deploying && _session.State.Battle is { Phase: BattlePhase.Deploying, Deployer: { } side }
            && _session.Battles.Commander(side) is { } deployer)
        {
            GameCommand? step = deploying.Keycode switch
            {
                Key.Enter or Key.KpEnter => new BattleStartCommand(deployer),
                Key.Q => new AutoResolveBattleCommand(deployer),
                _ => null,
            };
            if (step is not null)
            {
                _deployPick = null;
                Report(_session.Execute(step));
            }

            return;
        }

        if (@event is not InputEventKey { Pressed: true } key || ActiveCommander() is not { } player)
        {
            return;
        }

        GameCommand? command = key.Keycode switch
        {
            Key.W => new BattleWaitCommand(player),
            Key.D => new BattleDefendCommand(player),
            Key.R => new BattleRetreatCommand(player),
            Key.S => new BattleSurrenderCommand(player),
            Key.Q => new AutoResolveBattleCommand(player),
            _ => null,
        };
        if (command is not null)
        {
            Report(_session.Execute(command));
        }
    }

    /// <summary>
    /// Click on an enemy: shoot it if possible, else attack it; click elsewhere: move there (<see cref="BattleTargeting"/>). During the
    /// deployment step (ADR-072) a click on an own stack picks it and the next click puts it there; Enter starts the battle.
    /// </summary>
    private void HandleBattleClick(CellPosition cell)
    {
        if (_session.State.Battle is { Phase: BattlePhase.Deploying, Deployer: { } side } deploying && _session.Battles.Commander(side) is { } deployer)
        {
            if (deploying.UnitAt(cell) is { IsMachine: false } own && own.Side == side && own.Id != _deployPick)
            {
                _deployPick = own.Id;
            }
            else if (_deployPick is { } id && deploying.FindUnit(id) is { } picked)
            {
                var result = _session.Execute(new BattleDeployCommand(deployer, id, BattleTargeting.DeploymentPlacement(deploying, picked, cell.X + 0.5f, cell.Y + 0.5f)));
                Report(result);
                _deployPick = result.IsAccepted ? null : _deployPick;
            }

            return;
        }

        if (_session.State.Battle is not { } battle || ActiveCommander() is not { } player)
        {
            return;
        }

        GameCommand? command = BattleTargeting.ActionAt(battle, cell) switch
        {
            MoveAction move => new BattleMoveCommand(player, move.To),
            AttackAction attack => new BattleAttackCommand(player, attack.Target, attack.From),
            ShootAction shoot => new BattleShootCommand(player, shoot.Target),
            ShootStructureAction structure => new BattleShootStructureCommand(player, structure.Structure),
            _ => null,
        };
        GetNode<Label>("Ui/Top/Status").Text = command is null ? "Nothing to do there" : string.Empty;
        if (command is not null)
        {
            Report(_session.Execute(command));
        }
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
        top.AddChild(new Label { Name = "Status", Text = "Click hero, click tile twice to move, arrows & numpad; click an own town to open it. Battle: click, W wait, D defend, R retreat, S surrender, Q quick" });
        var decisions = new DecisionPanel();
        top.AddChild(decisions);
        decisions.Bind(_session);
        var town = new TownPanel { Name = "Town" };
        top.AddChild(town);
        town.Bind(_session);

        var board = new BattleBoard2D { Position = MapOrigin + new Vector2(0, 50) };
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
            ? MapConverter.ToGameState(MapSerializer.Load(path), options: new GameOptions(Seed: (ulong)Random.Shared.NextInt64()), creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings, artifacts: GameContent.Artifacts)
            : LaunchArguments.Scenario == "towns"
                ? TownsScenario.Create(GameContent.Creatures, GameContent.Heroes, GameContent.Buildings)
                : DemoScenario.Create(creatures: GameContent.Creatures, heroRoster: GameContent.Heroes, buildings: GameContent.Buildings);
}
