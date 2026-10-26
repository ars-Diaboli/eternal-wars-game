using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>
/// The battle scene of the 3D game: ground textured with the terrain the battle is fought on, a cell grid, obstacles
/// and placeholder units (<see cref="BattleUnitView"/>), with its own camera. It opens on <see cref="BattleStarted"/>
/// and builds everything from that event; later battle events become steps of one animation queue (moves, lunges,
/// projectiles, hit flashes, deaths), so a whole AI turn that the core resolved at once plays in order. Quick combat
/// and battles without a human side skip the animation. While the queue plays the view is <see cref="IsBusy"/>;
/// once it catches up with the core state it shows where a human-commanded active unit can go and raises
/// <see cref="Idle"/>. After <see cref="BattleEnded"/> has played, <see cref="Finished"/> is raised and the view stays
/// open until <see cref="Close"/>. Input is mapped by the app: <see cref="PickPoint"/> and <see cref="ShowIntent"/>.
/// During the deployment step (ADR-072) the zone is shaded, <see cref="Pick"/> marks the stack being placed and
/// <see cref="ShowPlacement"/> previews where it would go.
/// </summary>
public partial class BattleView3D : Node3D
{
    private const float GroundMargin = 6f;
    private const float TiltDegrees = 38f;
    private const float MinZoom = 0.55f;
    private const float MaxZoom = 1.6f;

    private static readonly Color ReachColor = new(1, 1, 1, 0.14f);
    private static readonly Color MoveColor = new(0.30f, 0.90f, 0.30f, 0.45f);
    private static readonly Color AttackColor = new(0.95f, 0.25f, 0.20f, 0.50f);
    private static readonly Color ShootColor = new(0.95f, 0.60f, 0.15f, 0.50f);
    private static readonly Color HitColor = new(1.0f, 0.15f, 0.10f);
    private static readonly Color ZoneColor = new(0.35f, 0.65f, 1.0f, 0.18f);

    private readonly Dictionary<int, BattleUnitView> _units = [];

    /// <summary>Where each unit will stand once the queue has played: the geometry of steps queued after a move.</summary>
    private readonly Dictionary<int, CellRect> _planned = [];

    private readonly Dictionary<BattleSide, string> _sideNames = [];
    private readonly Dictionary<BattleSide, Color> _sideColors = [];
    private readonly Node3D _field = new() { Name = "Field" };
    private readonly Camera3D _camera = new() { Name = "BattleCamera", Fov = 45 };
    private readonly MeshInstance3D _ghost = Overlay("Ghost");
    private readonly MeshInstance3D _target = Overlay("Target");
    private MultiMesh _reach = null!;
    private GameSession _session = null!;
    private Tween? _queue;
    private int _pending;
    private bool _instant;
    private int _width;
    private float _zoom = 1f;

    /// <summary>The scene was built for a new battle (the app hides the adventure map and shows the battle HUD).</summary>
    public event Action? Opened;

    /// <summary>The animation caught up with the core state.</summary>
    public event Action? Idle;

    /// <summary>A combat log line, raised when its step plays.</summary>
    public event Action<string>? Narrated;

    /// <summary>The battle's last step played; the result is <see cref="Result"/>.</summary>
    public event Action<BattleEnded>? Finished;

    /// <summary>The player left the result screen (the app returns to the adventure map).</summary>
    public event Action? Closed;

    public bool IsOpen { get; private set; }

    /// <summary>Animations are still playing; the shown battlefield is behind the core state.</summary>
    public bool IsBusy => _pending > 0;

    public BattleEnded? Result { get; private set; }

    public Camera3D Camera => _camera;

    /// <summary>Who may act now: the commander of the active unit once the view has caught up, else null.</summary>
    public PlayerId? Commander =>
        IsOpen && !IsBusy && Result is null && _session.State.Battle is { Phase: BattlePhase.Fighting, ActiveUnit: { } active }
            ? _session.Battles.Commander(active.Side)
            : null;

    /// <summary>Who places stacks now: the deployer's commander during the deployment step once the view has caught up, else null.</summary>
    public PlayerId? Deployer =>
        IsOpen && !IsBusy && Result is null && _session.State.Battle is { Phase: BattlePhase.Deploying, Deployer: { } side }
            ? _session.Battles.Commander(side)
            : null;

    /// <summary>The stack picked for placing during the deployment step.</summary>
    public int? Picked { get; private set; }

    /// <summary>Marks the stack being placed (or none).</summary>
    public void Pick(int? unit)
    {
        Picked = unit;
        SetActive(unit);
    }

    /// <summary>Previews a placement during deployment: green when allowed, red when refused; null hides it.</summary>
    public void ShowPlacement(CellRect? rect, bool allowed)
    {
        _target.Visible = false;
        _ghost.Visible = false;
        if (rect is { } r)
        {
            ShowOverlay(_ghost, r, allowed ? MoveColor : AttackColor);
        }
    }

    public string SideName(BattleSide side) => _sideNames.GetValueOrDefault(side, side.ToString());

    public Color SideColor(BattleSide side) => _sideColors.GetValueOrDefault(side, Palette.Neutral);

    public void Bind(GameSession session)
    {
        _session = session;
        Name = "Battle";
        Visible = false;
        AddChild(_field);
        AddChild(_camera);
        _reach = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = new BoxMesh { Size = new Vector3(BattleSpace.CellSize * 0.9f, 0.01f, BattleSpace.CellSize * 0.9f), Material = OverlayMaterial() },
        };
        AddChild(new MultiMeshInstance3D { Multimesh = _reach, Name = "Reach" });
        AddChild(_ghost);
        AddChild(_target);

        var events = session.Events;
        this.SubscribeWhileInTree<BattleStarted>(events, Open);
        this.SubscribeWhileInTree<BattleRoundStarted>(events, e => Step(null, () => Narrate($"Round {e.Round}.")));
        this.SubscribeWhileInTree<BattleTurnStarted>(events, e => Step(null, () => SetActive(e.Unit)));
        this.SubscribeWhileInTree<BattleUnitMoved>(events, OnMoved);
        this.SubscribeWhileInTree<BattleDeploymentStarted>(events, e => Step(null, () =>
            Narrate($"{SideName(e.Side)} deploys within {e.Columns} columns: click a stack, then where it should stand; then start the battle.")));
        this.SubscribeWhileInTree<BattleUnitDeployed>(events, OnDeployed);
        this.SubscribeWhileInTree<BattleDeploymentEnded>(events, _ => Step(null, () =>
        {
            Pick(null);
            ShowPlacement(null, false);
            Narrate("The battle begins.");
        }));
        this.SubscribeWhileInTree<BattleUnitAttacked>(events, OnAttacked);
        this.SubscribeWhileInTree<BattleUnitDied>(events, OnDied);
        this.SubscribeWhileInTree<BattleMoraleTriggered>(events, OnMorale);
        this.SubscribeWhileInTree<BattleUnitHealed>(events, OnHealed);
        this.SubscribeWhileInTree<BattleRageGained>(events, e => Step(null, () => Narrate($"Blood Rage: {NameOf(e.Unit)} has +{e.Attack} Attack.")));
        this.SubscribeWhileInTree<BattleWarcryUsed>(events, e => Step(null, () => Narrate(WarcryLine(e))));
        this.SubscribeWhileInTree<BattleUnitTerrified>(events, e => Step(null, () => Narrate($"Terror: {NameOf(e.Unit)} loses its action.")));
        this.SubscribeWhileInTree<BattleUnitWaited>(events, e => Step(null, () => Narrate($"{NameOf(e.Unit)} waits.")));
        this.SubscribeWhileInTree<BattleUnitDefended>(events, e => Step(null, () => Narrate($"{NameOf(e.Unit)} defends.")));
        this.SubscribeWhileInTree<BattleEnded>(events, e => Step(null, () =>
        {
            Result = e;
            SetActive(null);
            Narrate(e.Reason switch
            {
                BattleEndReason.Retreated => $"{SideName(e.Winner.Opponent())} retreats. {SideName(e.Winner)} wins.",
                BattleEndReason.Surrendered => $"{SideName(e.Winner.Opponent())} surrenders. {SideName(e.Winner)} wins.",
                _ => $"{SideName(e.Winner)} wins.",
            });
            Finished?.Invoke(e);
        }));
    }

    /// <summary>
    /// Opens the scene for a battle that is already under way (a loaded game, ADR-058): the field is built from the
    /// core state as it is now, nothing is animated. Does nothing before the field is set up; a battle still choosing
    /// its width opens on <see cref="BattleStarted"/> like any other.
    /// </summary>
    public void Restore()
    {
        if (_session.State.Battle is not { Phase: BattlePhase.Fighting or BattlePhase.Deploying } battle)
        {
            return;
        }

        var units = battle.LivingUnits
            .Select(u => new BattleUnitInfo(u.Id, u.Side, u.Creature.Id, u.Count, u.Position, u.Width, u.Height, u.Machine))
            .ToList();
        Open(new BattleStarted(
            battle.Attacker.Hero, battle.Defender.Hero, battle.Defender.Guard, battle.Width, BattleState.Height, battle.IsAmbush,
            units, battle.Obstacles.ToList()));
        SetActive(battle.ActiveUnit?.Id);
        RefreshReach();
    }

    /// <summary>Hides the scene after the result has been seen.</summary>
    public void Close()
    {
        _queue?.Kill();
        _queue = null;
        _pending = 0;
        IsOpen = false;
        Visible = false;
        Clear();
        Closed?.Invoke();
    }

    /// <summary>The ground point under the screen position, in cell units (fractions kept), or null off the field.</summary>
    public Vector2? PickPoint(Vector2 screenPoint) =>
        IsOpen && BattleSpace.PickPoint(_camera, screenPoint) is { } point &&
        point.X >= 0 && point.Y >= 0 && point.X < _width && point.Y < BattleState.Height
            ? point
            : null;

    /// <summary>Previews what a click would do: the placement a move or attack ends on, the unit attacked or shot.</summary>
    public void ShowIntent(BattleAction? action)
    {
        _ghost.Visible = false;
        _target.Visible = false;
        if (action is null || _session.State.Battle is not { ActiveUnit: { } active } battle)
        {
            return;
        }

        switch (action)
        {
            case MoveAction move:
                ShowOverlay(_ghost, active.RectAt(move.To), MoveColor);
                break;
            case AttackAction attack:
                ShowOverlay(_target, battle.GetUnit(attack.Target).Rect, AttackColor);
                if (attack.From is { } from && from != active.Position)
                {
                    ShowOverlay(_ghost, active.RectAt(from), MoveColor);
                }

                break;
            case ShootAction shoot:
                ShowOverlay(_target, battle.GetUnit(shoot.Target).Rect, ShootColor);
                break;
        }
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen || @event is not InputEventMouseButton { Pressed: true } wheel)
        {
            return;
        }

        var zoom = wheel.ButtonIndex switch
        {
            MouseButton.WheelUp => -0.1f,
            MouseButton.WheelDown => 0.1f,
            _ => 0f,
        };
        if (zoom != 0f)
        {
            _zoom = Mathf.Clamp(_zoom + zoom, MinZoom, MaxZoom);
            UpdateCamera();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Open(BattleStarted e)
    {
        _queue?.Kill();
        _queue = null;
        _pending = 0;
        Clear();

        var state = _session.State;
        var battle = state.Battle!;
        IsOpen = true;
        Result = null;
        _width = e.Width;
        _instant = battle.AutoResolve || (battle.IsComputerControlled(BattleSide.Attacker) && battle.IsComputerControlled(BattleSide.Defender));
        foreach (var side in new[] { BattleSide.Attacker, BattleSide.Defender })
        {
            var participant = battle.Participant(side);
            _sideColors[side] = participant.Owner is { } owner ? Palette.ForPlayer(owner) : Palette.Neutral;
            _sideNames[side] = NameOf(participant);
        }

        BuildGround(battle.Terrain, e.Width, e.Height);
        BuildGrid(e.Width, e.Height);
        foreach (var obstacle in e.Obstacles)
        {
            _field.AddChild(ObstacleNode(obstacle));
        }

        foreach (var info in e.Units)
        {
            var view = new BattleUnitView();
            view.Bind(info.Unit, info.Side, battle.GetUnit(info.Unit).Creature, info.Count, info.Width, info.Height, SideColor(info.Side), info.Machine);
            var rect = info.Rect;
            view.Position = BattleSpace.Center(rect);
            _field.AddChild(view);
            _units[info.Unit] = view;
            _planned[info.Unit] = rect;
        }

        _zoom = 1f;
        Visible = true;
        UpdateCamera();
        _camera.MakeCurrent();
        Narrate(e.IsAmbush ? $"Ambush! {SideName(BattleSide.Attacker)} against {SideName(BattleSide.Defender)}." : $"{SideName(BattleSide.Attacker)} against {SideName(BattleSide.Defender)}.");
        Opened?.Invoke();
        OnIdle();
    }

    private void OnMoved(BattleUnitMoved e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var rect = new CellRect(e.To.X, e.To.Y, view.Width, view.Height);
        _planned[e.Unit] = rect;
        var cells = Math.Max(Math.Abs(e.To.X - e.From.X), Math.Abs(e.To.Y - e.From.Y));
        Step(
            t => t.TweenProperty(view, "position", BattleSpace.Center(rect), Mathf.Clamp(cells * 0.05f, 0.15f, 0.6f)).SetTrans(Tween.TransitionType.Sine),
            () => view.Position = BattleSpace.Center(rect));
    }

    /// <summary>A stack placed elsewhere during deployment jumps there.</summary>
    private void OnDeployed(BattleUnitDeployed e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var rect = new CellRect(e.To.X, e.To.Y, view.Width, view.Height);
        _planned[e.Unit] = rect;
        Step(null, () => view.Position = BattleSpace.Center(rect));
    }

    private void OnAttacked(BattleUnitAttacked e)
    {
        if (!_units.TryGetValue(e.Attacker, out var attacker) || !_units.TryGetValue(e.Target, out var target))
        {
            return;
        }

        var from = BattleSpace.Center(_planned[e.Attacker]);
        var to = BattleSpace.Center(_planned[e.Target]);
        var verb = e.Kind switch
        {
            BattleAttackKind.Ranged => "shoots",
            BattleAttackKind.Retaliation => "strikes back at",
            BattleAttackKind.RangedRetaliation => "shoots back at",
            BattleAttackKind.Area => "also hits",
            _ => "attacks",
        };
        var luck = e.Luck switch
        {
            HitLuck.Lucky => " Lucky strike!",
            HitLuck.Unlucky => " Bad luck!",
            _ => string.Empty,
        };
        var line = $"{attacker.Creature.Name} {verb} {target.Creature.Name}: {e.Damage} damage, {e.Killed} killed.{luck}";
        Step(
            t =>
            {
                if (e.Kind is BattleAttackKind.Melee or BattleAttackKind.Retaliation)
                {
                    var lunge = (to - from) with { Y = 0 };
                    lunge = lunge.LengthSquared() > 0 ? lunge.Normalized() * 0.25f : Vector3.Zero;
                    t.TweenProperty(attacker.Body, "position", lunge, 0.12f);
                    t.TweenProperty(attacker.Body, "position", Vector3.Zero, 0.12f);
                }
                else if (e.Kind is BattleAttackKind.Ranged or BattleAttackKind.RangedRetaliation)
                {
                    var projectile = Projectile(from + (Vector3.Up * attacker.BodyHeight * 1.4f));
                    t.TweenCallback(Callable.From(() => projectile.Visible = true));
                    t.TweenProperty(projectile, "position", to + (Vector3.Up * target.BodyHeight), Mathf.Clamp(from.DistanceTo(to) * 0.05f, 0.15f, 0.45f));
                    t.TweenCallback(Callable.From(projectile.QueueFree));
                }

                var text = FloatingText(
                    e.Luck switch { HitLuck.Lucky => $"-{e.Killed} lucky!", HitLuck.Unlucky => $"-{e.Killed} unlucky", _ => $"-{e.Killed}" },
                    to + (Vector3.Up * ((target.BodyHeight * 2) + 0.4f)));
                t.TweenCallback(Callable.From(() => text.Visible = true));
                t.TweenProperty(target.BodyMaterial, "albedo_color", HitColor, 0.08f);
                t.Parallel().TweenProperty(text, "position", text.Position + (Vector3.Up * 0.6f), 0.45f);
                t.Parallel().TweenProperty(text, "modulate:a", 0f, 0.45f);
                t.Parallel().TweenProperty(target.BodyMaterial, "albedo_color", target.BaseColor, 0.15f).SetDelay(0.08f);
                t.TweenCallback(Callable.From(text.QueueFree));
            },
            () =>
            {
                target.SetCount(e.RemainingCount);
                Narrate(line);
            });
    }

    /// <summary>The healing tent: a green number over the healed stack and a log line.</summary>
    private void OnHealed(BattleUnitHealed e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var at = BattleSpace.Center(_planned[e.Unit]) + (Vector3.Up * ((view.BodyHeight * 2) + 0.4f));
        Step(
            t =>
            {
                var text = FloatingText($"+{e.Healed}", at);
                text.Modulate = new Color(0.45f, 1f, 0.45f);
                t.TweenCallback(Callable.From(() => text.Visible = true));
                t.TweenProperty(text, "position", at + (Vector3.Up * 0.4f), 0.3f);
                t.Parallel().TweenProperty(text, "modulate:a", 0f, 0.3f).SetDelay(0.1f);
                t.TweenCallback(Callable.From(text.QueueFree));
            },
            () => Narrate($"The healing tent restores {e.Healed} health to {view.Creature.Name}."));
    }

    /// <summary>The log line of a warcry (ADR-067).</summary>
    private string WarcryLine(BattleWarcryUsed e) => e.Warcry switch
    {
        Warcry.Rally => $"{SideName(e.Side)} rallies: +{PrimalFury.RallyMorale} morale for {PrimalFury.RallyRounds} rounds.",
        Warcry.Intimidate => $"{SideName(e.Side)} intimidates the enemy: −1 morale, −20 % initiative for {PrimalFury.IntimidateRounds} rounds.",
        _ => $"{SideName(e.Side)} roars a terror cry: {e.Affected.Count} enemy stack(s) will lose their next action.",
    };

    /// <summary>Morale shows as a caption over the unit and a log line.</summary>
    private void OnMorale(BattleMoraleTriggered e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var (caption, line) = e.Effect switch
        {
            MoraleEffect.ExtraAction => ("Morale!", $"High morale: {view.Creature.Name} acts again."),
            MoraleEffect.Haste => ("Eager", $"High morale: {view.Creature.Name} is eager this round (+50 % initiative)."),
            MoraleEffect.SkipTurn => ("Freezes", $"Low morale: {view.Creature.Name} freezes and loses its turn."),
            MoraleEffect.Slow => ("Hesitant", $"Low morale: {view.Creature.Name} hesitates this round (−50 % initiative)."),
            _ => (string.Empty, string.Empty),
        };
        var at = BattleSpace.Center(_planned[e.Unit]) + (Vector3.Up * ((view.BodyHeight * 2) + 0.6f));
        Step(
            t =>
            {
                var text = FloatingText(caption, at);
                t.TweenCallback(Callable.From(() => text.Visible = true));
                t.TweenProperty(text, "position", at + (Vector3.Up * 0.4f), 0.5f);
                t.Parallel().TweenProperty(text, "modulate:a", 0f, 0.5f).SetDelay(0.2f);
                t.TweenCallback(Callable.From(text.QueueFree));
            },
            () => Narrate(line));
    }

    private void OnDied(BattleUnitDied e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        Step(
            t => t.TweenProperty(view, "scale", new Vector3(1, 0.05f, 1), 0.3f),
            () =>
            {
                view.Visible = false;
                Narrate($"{view.Creature.Name} perish.");
            });
    }

    /// <summary>
    /// Queues one step: <paramref name="animate"/> adds its tweeners (skipped in quick combat), then
    /// <paramref name="apply"/> sets the end state. With nothing queued, a step without animation applies at once.
    /// </summary>
    private void Step(Action<Tween>? animate, Action apply)
    {
        if (!IsOpen)
        {
            return;
        }

        _instant |= _session.State.Battle?.AutoResolve == true;
        if ((_instant || animate is null) && _pending == 0)
        {
            apply();
            OnIdle();
            return;
        }

        if (++_pending == 1)
        {
            RefreshReach(); // busy now: no reach overlay or preview while the queue plays
        }

        if (_queue is null || !_queue.IsValid() || !_queue.IsRunning())
        {
            _queue = CreateTween();
        }

        if (!_instant)
        {
            animate?.Invoke(_queue);
        }

        _queue.TweenCallback(Callable.From(() =>
        {
            apply();
            _pending--;
            if (_pending == 0)
            {
                OnIdle();
            }
        }));
    }

    private void OnIdle()
    {
        RefreshReach();
        Idle?.Invoke();
    }

    private void Narrate(string line) => Narrated?.Invoke(line);

    private void SetActive(int? unit)
    {
        foreach (var view in _units.Values)
        {
            view.Active = view.Unit == unit;
        }
    }

    /// <summary>
    /// Every cell a human-commanded active unit can cover this turn, as a faint overlay; during the deployment step the
    /// deployer's zone instead.
    /// </summary>
    private void RefreshReach()
    {
        var cells = new HashSet<CellPosition>();
        var color = ReachColor;
        if (Deployer is not null && _session.State.Battle is { } deploying)
        {
            var from = deploying.Deployer == BattleSide.Attacker ? 0 : deploying.Width - deploying.DeploymentColumns;
            cells.UnionWith(new CellRect(from, 0, deploying.DeploymentColumns, BattleState.Height).Cells());
            color = ZoneColor;
        }
        else if (Commander is not null && _session.State.Battle is { ActiveUnit: { } active } battle)
        {
            foreach (var placement in BattleGrid.Reachable(battle, active).Keys)
            {
                cells.UnionWith(active.RectAt(placement).Cells());
            }
        }

        _reach.InstanceCount = cells.Count;
        var i = 0;
        foreach (var cell in cells)
        {
            _reach.SetInstanceTransform(i, new Transform3D(Basis.Identity, BattleSpace.ToWorld(cell) + (Vector3.Up * 0.015f)));
            _reach.SetInstanceColor(i, color);
            i++;
        }

        if (cells.Count == 0)
        {
            ShowIntent(null);
        }
    }

    private void Clear()
    {
        foreach (var child in _field.GetChildren())
        {
            child.QueueFree();
        }

        _units.Clear();
        _planned.Clear();
        _reach.InstanceCount = 0;
        _ghost.Visible = false;
        _target.Visible = false;
    }

    private string NameOf(int unit) => _units.TryGetValue(unit, out var view) ? view.Creature.Name : $"#{unit}";

    private string NameOf(BattleParticipant participant)
    {
        var state = _session.State;
        if (participant.Hero is { } hero && state.Heroes.FirstOrDefault(h => h.Id == hero) is { } found)
        {
            return found.Name;
        }

        return participant.Guard is { } guard
            ? state.FindObject(guard) switch
            {
                Town town => $"{town.Name} garrison",
                MonsterGuard monster => state.Creatures.TryGet(monster.Creature, out var creature) ? $"Neutral {creature.Name}" : "Neutral monsters",
                Mine mine => $"{mine.Definition.Name} guard",
                CreatureDwelling dwelling => $"{dwelling.Definition.Name} guard",
                CreatureBank bank => $"{bank.Definition.Name} guard",
                _ => "Neutral",
            }
            : participant.Side.ToString();
    }

    private void UpdateCamera()
    {
        var focus = BattleSpace.ToWorld(_width / 2f, BattleState.Height / 2f) + new Vector3(0, 0, 0.6f);
        var distance = _width * BattleSpace.CellSize * 1.15f * _zoom;
        var tilt = Mathf.DegToRad(TiltDegrees);
        _camera.LookAtFromPosition(focus + (new Vector3(0, Mathf.Cos(tilt), Mathf.Sin(tilt)) * distance), focus, Vector3.Up);
    }

    /// <summary>The field in the battle terrain's texture, inside a darker band of the same ground.</summary>
    private void BuildGround(TerrainType terrain, int width, int height)
    {
        var size = new Vector2(width, height) * BattleSpace.CellSize;
        var center = BattleSpace.ToWorld(width / 2f, height / 2f);
        var material = AdventureMapView.TerrainMaterial(terrain, 4f);
        _field.AddChild(new MeshInstance3D { Name = "Ground", Mesh = new PlaneMesh { Size = size, Material = material }, Position = center });

        var surround = (StandardMaterial3D)material.Duplicate();
        surround.AlbedoColor = material.AlbedoColor.Darkened(0.55f);
        _field.AddChild(new MeshInstance3D
        {
            Name = "Surround",
            Mesh = new PlaneMesh { Size = size + (Vector2.One * GroundMargin * 2), Material = surround },
            Position = center + (Vector3.Down * 0.02f),
        });
    }

    private void BuildGrid(int width, int height)
    {
        var mesh = new ImmediateMesh();
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1, 1, 1, 0.12f),
        };
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines, material);
        var lift = Vector3.Up * 0.01f;
        for (var x = 0; x <= width; x++)
        {
            mesh.SurfaceAddVertex(BattleSpace.ToWorld(x, 0) + lift);
            mesh.SurfaceAddVertex(BattleSpace.ToWorld(x, height) + lift);
        }

        for (var y = 0; y <= height; y++)
        {
            mesh.SurfaceAddVertex(BattleSpace.ToWorld(0, y) + lift);
            mesh.SurfaceAddVertex(BattleSpace.ToWorld(width, y) + lift);
        }

        mesh.SurfaceEnd();
        _field.AddChild(new MeshInstance3D { Name = "Grid", Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
    }

    /// <summary>Small obstacles are boulders, big ones crags, wall segments blocks.</summary>
    private static MeshInstance3D ObstacleNode(BattleObstacle obstacle)
    {
        var extent = obstacle.Rect.Width * BattleSpace.CellSize;
        var material = new StandardMaterial3D { AlbedoColor = new Color(0.40f, 0.36f, 0.31f), Roughness = 1f };
        var (mesh, height) = obstacle.Class switch
        {
            ObstacleClass.Small => ((PrimitiveMesh)new SphereMesh { Radius = extent * 0.42f, Height = extent * 0.55f }, extent * 0.2f),
            ObstacleClass.Big => (new CylinderMesh { TopRadius = extent * 0.15f, BottomRadius = extent * 0.45f, Height = extent * 0.8f }, extent * 0.4f),
            _ => (new BoxMesh { Size = new Vector3(extent, extent * 1.5f, extent) }, extent * 0.75f),
        };
        mesh.Material = material;
        return new MeshInstance3D
        {
            Name = $"Obstacle_{obstacle.Id}",
            Mesh = mesh,
            Position = BattleSpace.Center(obstacle.Rect) + (Vector3.Up * height),
        };
    }

    private MeshInstance3D Projectile(Vector3 at)
    {
        var projectile = new MeshInstance3D
        {
            Mesh = new SphereMesh
            {
                Radius = 0.06f,
                Height = 0.12f,
                Material = new StandardMaterial3D { AlbedoColor = new Color(1f, 0.9f, 0.6f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            },
            Position = at,
            Visible = false,
        };
        _field.AddChild(projectile);
        return projectile;
    }

    private Label3D FloatingText(string text, Vector3 at)
    {
        var label = new Label3D
        {
            Text = text,
            Position = at,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            PixelSize = 0.005f,
            FontSize = 44,
            OutlineSize = 10,
            Modulate = new Color(1f, 0.85f, 0.4f),
            NoDepthTest = true,
            Visible = false,
        };
        _field.AddChild(label);
        return label;
    }

    private static void ShowOverlay(MeshInstance3D overlay, CellRect rect, Color color)
    {
        var mesh = (BoxMesh)overlay.Mesh;
        mesh.Size = new Vector3(rect.Width * BattleSpace.CellSize * 0.96f, 0.02f, rect.Height * BattleSpace.CellSize * 0.96f);
        ((StandardMaterial3D)mesh.Material).AlbedoColor = color;
        overlay.Position = BattleSpace.Center(rect) + (Vector3.Up * 0.03f);
        overlay.Visible = true;
    }

    private static MeshInstance3D Overlay(string name) => new()
    {
        Name = name,
        Mesh = new BoxMesh { Material = OverlayMaterial() },
        Visible = false,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
    };

    private static StandardMaterial3D OverlayMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true,
        AlbedoColor = Colors.White,
    };
}
