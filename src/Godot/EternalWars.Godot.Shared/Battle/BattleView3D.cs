using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Settings;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.Shared.Map;
using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>
/// The battle scene of the 3D game: ground textured with the terrain the battle is fought on, a cell grid, obstacles
/// and placeholder units (<see cref="BattleUnitView"/>), with its own camera. It opens on <see cref="BattleStarted"/>
/// and builds everything from that event (<see cref="BattleFieldBuilder"/>); later battle events become steps of one animation queue
/// (<see cref="BattleAnimationQueue"/>: moves, lunges, projectiles, hit flashes, deaths), so a whole AI turn that the core resolved at once
/// plays in order. Each step's combat log line (<see cref="BattleNarrator"/>) is raised as <see cref="Narrated"/> when it plays. Quick combat
/// and battles without a human side skip the animation; <see cref="Speed"/> plays it slower, faster or not at all (ADR-090). While the queue plays the view is <see cref="IsBusy"/>;
/// once it catches up with the core state it shows where a human-commanded active unit can go and raises
/// <see cref="Idle"/>. After <see cref="BattleEnded"/> has played, <see cref="Finished"/> is raised and the view stays
/// open until <see cref="Close"/>. Input is mapped by the app: <see cref="PickPoint"/> and <see cref="ShowIntent"/>.
/// During the deployment step (ADR-072) the zone is shaded, <see cref="Pick"/> marks the stack being placed, <see cref="Carry"/> holds it under the pointer while it is dragged and
/// <see cref="ShowPlacement"/> previews where it would go. A siege (ADR-106) adds the wall sections, the gate and the moat on the field and the
/// keep and towers beside it; destroyed structures fall to rubble.
/// </summary>
public partial class BattleView3D : Node3D
{
    private const float TiltDegrees = 38f;
    private const float MinZoom = 0.55f;
    private const float MaxZoom = 1.6f;

    private static readonly Color ReachColor = new(1, 1, 1, 0.14f);
    private static readonly Color MoveColor = new(0.30f, 0.90f, 0.30f, 0.45f);
    private static readonly Color AttackColor = new(0.95f, 0.25f, 0.20f, 0.50f);
    private static readonly Color ShootColor = new(0.95f, 0.60f, 0.15f, 0.50f);
    private static readonly Color ZoneColor = new(0.35f, 0.65f, 1.0f, 0.18f);

    private readonly Dictionary<BattleSide, Color> _sideColors = [];
    private readonly Node3D _field = new() { Name = "Field" };
    private readonly Camera3D _camera = new() { Name = "BattleCamera", Fov = 45 };
    private readonly MeshInstance3D _ghost = Overlay("Ghost");
    private readonly MeshInstance3D _target = Overlay("Target");
    private readonly BattleAnimationQueue _queue;
    private readonly BattleNarrator _narrator;
    private readonly BattleStage _stage;
    private MultiMesh _reach = null!;
    private GameSession _session = null!;
    private bool _instant;
    private int _width;
    private float _zoom = 1f;

    public BattleView3D()
    {
        _queue = new BattleAnimationQueue(this, RefreshReach, OnIdle); // busy: no reach overlay or preview while the queue plays
        _narrator = new BattleNarrator(() => _session.State);
        _stage = new BattleStage(_field, () => _session.State, _narrator, SideColor, Step, Narrate);
    }

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

    /// <summary>How fast the battle animations play (ADR-090, UI-1); instant shows each step's result at once. A change applies to the queue playing.</summary>
    public AnimationSpeed Speed
    {
        get => _queue.Speed;
        set => _queue.Speed = value;
    }

    /// <summary>Animations are still playing; the shown battlefield is behind the core state.</summary>
    public bool IsBusy => _queue.IsBusy;

    /// <summary>
    /// The replay viewer watches (ADR-164): nobody commands or deploys from this view, and every battle plays move by move at <see cref="Speed"/>,
    /// computer and quick ones too (<see cref="FinishAnimations"/> skips to the end).
    /// </summary>
    public bool Watching { get; set; }

    /// <summary>Stops the animations and shows the end of every queued step at once ("skip to the result" in the replay viewer).</summary>
    public void FinishAnimations()
    {
        _queue.Flush();
        OnIdle();
    }

    public BattleEnded? Result { get; private set; }

    public Camera3D Camera => _camera;

    /// <summary>Who may act now: the commander of the active unit once the view has caught up, else null.</summary>
    public PlayerId? Commander =>
        !Watching && IsOpen && !IsBusy && Result is null && _session.State.Battle is { Phase: BattlePhase.Fighting, ActiveUnit: { } active }
            ? _session.Battles.Commander(active.Side)
            : null;

    /// <summary>Who places stacks now: the deployer's commander during the deployment step once the view has caught up, else null.</summary>
    public PlayerId? Deployer =>
        !Watching && IsOpen && !IsBusy && Result is null && _session.State.Battle is { Phase: BattlePhase.Deploying, Deployer: { } side }
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

    /// <summary>
    /// Carries the picked stack's model under <paramref name="point"/> (a field point, see <see cref="PickPoint"/>), lifted a little, while
    /// it is dragged during deployment (ADR-155); null puts it back where it stands.
    /// </summary>
    public void Carry(Vector2? point)
    {
        if (Picked is not { } id || !_stage.Units.TryGetValue(id, out var view) || !_stage.Planned.TryGetValue(id, out var rect))
        {
            return;
        }

        view.Position = point is { } p
            ? BattleSpace.ToWorld(p.X, p.Y) + new Vector3(0, BattleSpace.CellSize * 0.35f, 0)
            : BattleSpace.Center(rect);
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

    public string SideName(BattleSide side) => _narrator.SideName(side);

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
        Tell<BattleRoundStarted>(events);
        this.SubscribeWhileInTree<BattleTurnStarted>(events, e => Step(null, () => SetActive(e.Unit)));
        this.SubscribeWhileInTree<BattleUnitMoved>(events, _stage.OnMoved);
        this.SubscribeWhileInTree<BattleUnitTeleported>(events, _stage.OnTeleported);
        Tell<BattleUnitTurnRegained>(events);
        Tell<BattleDeploymentStarted>(events);
        this.SubscribeWhileInTree<BattleUnitDeployed>(events, _stage.OnDeployed);
        this.SubscribeWhileInTree<BattleDeploymentEnded>(events, e => Step(null, _stage.Then(e, () =>
        {
            Pick(null);
            ShowPlacement(null, false);
        })));
        this.SubscribeWhileInTree<BattleUnitAttacked>(events, _stage.OnAttacked);
        this.SubscribeWhileInTree<BattleUnitDied>(events, _stage.OnDied);
        this.SubscribeWhileInTree<BattleMoraleTriggered>(events, _stage.OnMorale);
        this.SubscribeWhileInTree<BattleUnitHealed>(events, _stage.OnHealed);
        this.SubscribeWhileInTree<SiegeStructureShot>(events, _stage.OnStructureShot);
        this.SubscribeWhileInTree<BattleUnitSiegeDamaged>(events, _stage.OnSiegeDamaged);
        this.SubscribeWhileInTree<SiegeStructureDamaged>(events, _stage.OnStructureDamaged);
        this.SubscribeWhileInTree<BattleUnitSummoned>(events, _stage.OnSummoned);
        this.SubscribeWhileInTree<BattleUnitCopied>(events, _stage.OnCopied);
        Tell<BattleCopyVanished>(events);
        this.SubscribeWhileInTree<BattleUnitRaised>(events, _stage.OnRaised);
        this.SubscribeWhileInTree<BattleUnitSplit>(events, _stage.OnSplit);
        this.SubscribeWhileInTree<BattleUnitShielded>(events, _stage.OnShielded);
        Tell<BattleManaGained>(events);
        Tell<BattleUnitActsAgain>(events);
        this.SubscribeWhileInTree<BattleUnitReborn>(events, _stage.OnReborn);
        this.SubscribeWhileInTree<BattleUnitRevived>(events, _stage.OnRevived);
        Tell<BattleSpellDispelled>(events);
        Tell<BattleUnitSpellbound>(events);
        Tell<BattleUnitStunned>(events);
        Tell<BattleCreatureCast>(events);
        Tell<BattleUnitLeftKeep>(events);
        Tell<BattleSpellCast>(events);
        this.SubscribeWhileInTree<BattleUnitSpellDamaged>(events, _stage.OnSpellDamaged);
        this.SubscribeWhileInTree<BattleZoneCreated>(events, _stage.OnZoneCreated);
        this.SubscribeWhileInTree<BattleZoneExpired>(events, _stage.OnZoneExpired);
        this.SubscribeWhileInTree<BattleRubbleFell>(events, _stage.OnRubbleFell);
        Tell<BattleManaStolen>(events);
        Tell<BattleSpellPowerSapped>(events);
        Tell<BattleSpellApplied>(events);
        this.SubscribeWhileInTree<BattleUnitSideChanged>(events, _stage.OnSideChanged);
        Tell<BattleSpellResisted>(events);
        Tell<BattleSpellExpired>(events);
        Tell<BattleRageGained>(events);
        Tell<BattleWarcryUsed>(events);
        Tell<BattleUnitTerrified>(events);
        Tell<BattleUnitWaited>(events);
        Tell<BattleUnitDefended>(events);
        this.SubscribeWhileInTree<BattleEnded>(events, e =>
        {
            var line = _stage.Line(e);
            Step(null, () =>
            {
                Result = e;
                SetActive(null);
                Narrate(line);
                Finished?.Invoke(e);
            });
        });
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
            units, battle.Obstacles.ToList(), battle.Siege is { } siege ? SiegeLayout.Of(siege) : null));
        foreach (var zone in battle.Zones)
        {
            _stage.AddZone(zone.Id, zone.Spell, zone.Rect);
        }

        SetActive(battle.ActiveUnit?.Id);
        RefreshReach();
    }

    /// <summary>Hides the scene after the result has been seen.</summary>
    public void Close()
    {
        _queue.Reset();
        IsOpen = false;
        Visible = false;
        Clear();
        Closed?.Invoke();
    }

    /// <summary>The ground point under the screen position, in cell units (fractions kept), or null off the field and a siege's structures.</summary>
    public Vector2? PickPoint(Vector2 screenPoint) =>
        IsOpen && BattleSpace.PickPoint(_camera, screenPoint) is { } point &&
        ((point.X >= 0 && point.Y >= 0 && point.X < _width && point.Y < BattleState.Height)
         || _stage.StructureRects.Any(r => r.Contains(BattleSpace.ToCell(point))))
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
            case ShootStructureAction aim when _stage.TryGetStructure(aim.Structure, out var structure):
                ShowOverlay(_target, structure, ShootColor);
                break;
            case AttackStructureAction hit when _stage.TryGetStructure(hit.Structure, out var gate):
                ShowOverlay(_target, gate, AttackColor);
                if (hit.From is { } stand && stand != active.Position)
                {
                    ShowOverlay(_ghost, active.RectAt(stand), MoveColor);
                }

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
        _queue.Reset();
        Clear();

        var state = _session.State;
        var battle = state.Battle!;
        IsOpen = true;
        Result = null;
        _width = e.Width;
        _instant = !Watching && (battle.AutoResolve || (battle.IsComputerControlled(BattleSide.Attacker) && battle.IsComputerControlled(BattleSide.Defender)));
        var opening = _stage.Line(e);
        foreach (var side in new[] { BattleSide.Attacker, BattleSide.Defender })
        {
            _sideColors[side] = battle.Participant(side).Owner is { } owner ? Palette.ForPlayer(owner) : Palette.Neutral;
        }

        _stage.Build(e, battle);
        _zoom = 1f;
        Visible = true;
        UpdateCamera();
        _camera.MakeCurrent();
        Narrate(opening);
        Opened?.Invoke();
        OnIdle();
    }

    /// <summary>
    /// Queues one step on <see cref="BattleAnimationQueue"/>: <paramref name="animate"/> adds its tweeners (skipped in quick combat), then
    /// <paramref name="apply"/> sets the end state. Quick combat applies everything queued at once.
    /// </summary>
    private void Step(Action<Tween>? animate, Action apply)
    {
        if (!IsOpen)
        {
            return;
        }

        _instant |= !Watching && _session.State.Battle?.AutoResolve == true;
        _queue.Play(animate, apply, _instant);
    }

    /// <summary>An event that only adds its log line: a step without animation that narrates it.</summary>
    private void Tell<T>(IEventSubscriber events)
        where T : IEvent =>
        this.SubscribeWhileInTree<T>(events, e => Step(null, _stage.Then(e, () => { })));

    private void OnIdle()
    {
        RefreshReach();
        Idle?.Invoke();
    }

    private void Narrate(string line) => Narrated?.Invoke(line);

    private void SetActive(int? unit) => _stage.SetActive(unit);

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
        _stage.Clear();
        _reach.InstanceCount = 0;
        _ghost.Visible = false;
        _target.Visible = false;
    }

    private void UpdateCamera()
    {
        var focus = BattleSpace.ToWorld(_width / 2f, BattleState.Height / 2f) + new Vector3(0, 0, 0.6f);
        var distance = _width * BattleSpace.CellSize * 1.15f * _zoom;
        var tilt = Mathf.DegToRad(TiltDegrees);
        _camera.LookAtFromPosition(focus + (new Vector3(0, Mathf.Cos(tilt), Mathf.Sin(tilt)) * distance), focus, Vector3.Up);
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
