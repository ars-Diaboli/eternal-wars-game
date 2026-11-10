using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using Godot;

namespace EternalWars.Godot.Shared.Battle;

/// <summary>
/// The battlefield's live pieces under <paramref name="field"/> (the stacks' models, where each will stand once the queue has played, a
/// siege's structures, field zones) and the steps <see cref="BattleView3D"/> queues for battle events: each handler turns its event into an
/// animation and an end state passed to <paramref name="step"/>, whose end state raises the event's log line (<paramref name="narrator"/>)
/// through <paramref name="narrate"/>. It builds the field with <see cref="BattleFieldBuilder"/> and animates with <see cref="BattleAnimations"/>.
/// </summary>
public sealed class BattleStage(
    Node3D field,
    Func<GameState> state,
    BattleNarrator narrator,
    Func<BattleSide, Color> sideColor,
    Action<Action<Tween>?, Action> step,
    Action<string> narrate)
{
    private readonly Dictionary<int, BattleUnitView> _units = [];

    /// <summary>A siege's structures by id (ADR-106).</summary>
    private readonly Dictionary<int, (CellRect Rect, MeshInstance3D Node)> _structures = [];

    /// <summary>Where each unit will stand once the queue has played: the geometry of steps queued after a move.</summary>
    private readonly Dictionary<int, CellRect> _planned = [];

    /// <summary>The spells' field zones on the field (ADR-149), by zone id.</summary>
    private readonly Dictionary<int, MeshInstance3D> _zones = [];

    private readonly BattleFieldBuilder _builder = new(field);
    private BattleAnimations? _animations;

    /// <summary>The stacks' models by unit id; those destroyed stay, hidden.</summary>
    public IReadOnlyDictionary<int, BattleUnitView> Units => _units;

    /// <summary>Where each unit will stand once the queue has played.</summary>
    public IReadOnlyDictionary<int, CellRect> Planned => _planned;

    /// <summary>The cells of a siege's structures by id (ADR-106).</summary>
    public IEnumerable<CellRect> StructureRects => _structures.Values.Select(s => s.Rect);

    private BattleAnimations Animations => _animations ??= new BattleAnimations(_builder);

    /// <summary>The cells of the siege structure <paramref name="id"/>, if the field has it.</summary>
    public bool TryGetStructure(int id, out CellRect rect)
    {
        var found = _structures.TryGetValue(id, out var structure);
        rect = structure.Rect;
        return found;
    }

    /// <summary>Builds the field of <paramref name="e"/>: ground, grid, obstacles, a siege's fortifications and the stacks as they start.</summary>
    public void Build(BattleStarted e, BattleState battle)
    {
        _builder.Ground(battle.Terrain, e.Width, e.Height);
        _builder.Grid(e.Width, e.Height);
        foreach (var obstacle in e.Obstacles)
        {
            _builder.Obstacle(obstacle);
        }

        if (e.Siege is { } layout)
        {
            foreach (var (structure, node) in _builder.Siege(layout, e.Width))
            {
                _structures[structure.Structure] = (structure.Rect, node);
            }
        }

        foreach (var info in e.Units)
        {
            var view = new BattleUnitView();
            view.Bind(info.Unit, info.Side, battle.GetUnit(info.Unit).Creature, info.Count, info.Width, info.Height, SideColor(info.Side), info.Machine);
            var rect = info.Rect;
            view.Position = BattleSpace.Center(rect);
            field.AddChild(view);
            _units[info.Unit] = view;
            _planned[info.Unit] = rect;
        }
    }

    /// <summary>Frees every piece of the field.</summary>
    public void Clear()
    {
        field.ClearChildren();

        _units.Clear();
        _structures.Clear();
        _planned.Clear();
        _zones.Clear();
    }

    /// <summary>Rings the active stack (or none).</summary>
    public void SetActive(int? unit)
    {
        foreach (var view in _units.Values)
        {
            view.Active = view.Unit == unit;
        }
    }

    /// <summary>A field zone (ADR-149) on the field, kept by id until it expires.</summary>
    public void AddZone(int id, string spell, CellRect rect) => _zones[id] = _builder.Zone(id, spell, rect);

    /// <summary>A summon's creatures appear at their placement, or an own summoned stack grows (ADR-120).</summary>
    public void OnSummoned(BattleUnitSummoned e) => Step(null, Then(e, () => AddUnit(e.Unit)));

    /// <summary>Mirror Image's copy appears (ADR-152).</summary>
    public void OnCopied(BattleUnitCopied e) => Step(null, Then(e, () => AddUnit(e.Unit)));

    /// <summary>Raise Dead's skeletons appear (ADR-152).</summary>
    public void OnRaised(BattleUnitRaised e) => Step(null, Then(e, () => AddUnit(e.Unit)));

    /// <summary>Puppet Master III's part of a stack appears; the rest stays (ADR-152).</summary>
    public void OnSplit(BattleUnitSplit e) => Step(null, Then(e, () =>
    {
        if (_units.TryGetValue(e.From, out var from))
        {
            from.SetCount(e.FromCount);
        }

        AddUnit(e.Unit);
    }));

    public void OnZoneCreated(BattleZoneCreated e) => Step(null, Then(e, () => AddZone(e.Zone, e.Spell, e.Rect)));

    public void OnZoneExpired(BattleZoneExpired e) => Step(null, Then(e, () =>
    {
        if (_zones.Remove(e.Zone, out var node))
        {
            node.QueueFree();
        }
    }));

    /// <summary>A stack taken over or let go (ADR-150) takes its new side's colour.</summary>
    public void OnSideChanged(BattleUnitSideChanged e) => Step(null, Then(e, () =>
    {
        if (_units.TryGetValue(e.Unit, out var view))
        {
            view.SetSide(e.Side, SideColor(e.Side));
        }
    }));

    /// <summary>The end state <paramref name="apply"/> followed by <paramref name="e"/>'s log line (taken now, raised when the step plays).</summary>
    public Action Then(IEvent e, Action apply)
    {
        var line = Line(e);
        return () =>
        {
            apply();
            Narrate(line);
        };
    }

    /// <summary>The log line of <paramref name="e"/>; every event the view narrates has one.</summary>
    public string Line(IEvent e) => narrator.Line(e) ?? string.Empty;

    public void OnMoved(BattleUnitMoved e)
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

    /// <summary>A stack a move spell took (ADR-151) sinks out of sight and rises at its new place.</summary>
    public void OnTeleported(BattleUnitTeleported e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var rect = new CellRect(e.To.X, e.To.Y, view.Width, view.Height);
        _planned[e.Unit] = rect;
        var line = Line(e);
        Step(
            t =>
            {
                t.TweenProperty(view, "scale", new Vector3(1, 0.05f, 1), 0.15f);
                t.TweenCallback(Callable.From(() => view.Position = BattleSpace.Center(rect)));
                t.TweenProperty(view, "scale", Vector3.One, 0.15f);
            },
            () =>
            {
                view.Scale = Vector3.One;
                view.Position = BattleSpace.Center(rect);
                Narrate(line);
            });
    }

    /// <summary>A stack placed elsewhere during deployment jumps there.</summary>
    public void OnDeployed(BattleUnitDeployed e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var rect = new CellRect(e.To.X, e.To.Y, view.Width, view.Height);
        _planned[e.Unit] = rect;
        Step(null, () => view.Position = BattleSpace.Center(rect));
    }

    public void OnAttacked(BattleUnitAttacked e)
    {
        if (!_units.TryGetValue(e.Attacker, out var attacker) || !_units.TryGetValue(e.Target, out var target))
        {
            return;
        }

        var from = BattleSpace.Center(_planned[e.Attacker]);
        var to = BattleSpace.Center(_planned[e.Target]);
        var line = Line(e);
        Step(
            t =>
            {
                if (e.Kind is BattleAttackKind.Melee or BattleAttackKind.Retaliation)
                {
                    BattleAnimations.Lunge(t, attacker, from, to);
                }
                else if (e.Kind is BattleAttackKind.Ranged or BattleAttackKind.RangedRetaliation)
                {
                    Animations.Shot(t, from + (Vector3.Up * attacker.BodyHeight * 1.4f), to + (Vector3.Up * target.BodyHeight), Mathf.Clamp(from.DistanceTo(to) * 0.05f, 0.15f, 0.45f));
                }

                Animations.Hit(t, target, e.Luck switch { HitLuck.Lucky => $"-{e.Killed} lucky!", HitLuck.Unlucky => $"-{e.Killed} unlucky", _ => $"-{e.Killed}" }, to);
            },
            () =>
            {
                target.SetCount(e.RemainingCount);
                target.SetHealth(e.TopHealth);
                Narrate(line);
            });
    }

    /// <summary>The healing tent: a green number over the healed stack and a log line.</summary>
    public void OnHealed(BattleUnitHealed e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var at = BattleSpace.Center(_planned[e.Unit]) + (Vector3.Up * ((view.BodyHeight * 2) + 0.4f));
        var line = Line(e);
        Step(
            t => Animations.Caption(t, $"+{e.Healed}", at, 0.3f, 0.1f, new Color(0.45f, 1f, 0.45f)),
            () =>
            {
                view.SetHealth(e.TopHealth);
                Narrate(line);
            });
    }

    /// <summary>The siege engine's shot at a structure (ADR-106): a projectile and a shudder, or a miss; a destroyed structure falls to rubble.</summary>
    public void OnStructureShot(SiegeStructureShot e)
    {
        if (!_units.TryGetValue(e.Machine, out var machine) || !_structures.TryGetValue(e.Structure, out var structure))
        {
            return;
        }

        var from = BattleSpace.Center(_planned[e.Machine]) + (Vector3.Up * machine.BodyHeight * 1.4f);
        var to = BattleSpace.Center(structure.Rect) + (Vector3.Up * 0.5f);
        var line = Line(e);
        Step(
            t =>
            {
                Animations.Shot(t, from, to, Mathf.Clamp(from.DistanceTo(to) * 0.05f, 0.2f, 0.5f));
                if (e.Hit)
                {
                    BattleAnimations.Shudder(t, structure.Node, new Vector3(1.08f, 0.92f, 1.08f));
                }
            },
            () =>
            {
                if (e.Health == 0)
                {
                    BattleFieldBuilder.Ruin(structure.Node);
                }

                Narrate(line);
            });
    }

    /// <summary>A stack's blow at the gate or Earthquake's shake of a wall section (ADR-120): a shudder; a destroyed structure falls to rubble.</summary>
    public void OnStructureDamaged(SiegeStructureDamaged e)
    {
        if (!_structures.TryGetValue(e.Structure, out var structure))
        {
            return;
        }

        var line = Line(e);
        Step(
            t => BattleAnimations.Shudder(t, structure.Node, new Vector3(1.06f, 0.94f, 1.06f)),
            () =>
            {
                if (e.Health == 0)
                {
                    BattleFieldBuilder.Ruin(structure.Node);
                }

                Narrate(line);
            });
    }

    /// <summary>A tower's shot at a stack or the moat's damage to one that entered it (ADR-106).</summary>
    public void OnSiegeDamaged(BattleUnitSiegeDamaged e)
    {
        if (!_units.TryGetValue(e.Unit, out var target))
        {
            return;
        }

        var to = BattleSpace.Center(_planned[e.Unit]);
        var tower = e.Structure is { } id && _structures.TryGetValue(id, out var found) ? found : ((CellRect Rect, MeshInstance3D Node)?)null;
        var line = Line(e);
        Step(
            t =>
            {
                if (tower is { } shooter)
                {
                    Animations.Shot(t, BattleSpace.Center(shooter.Rect) + (Vector3.Up * 1.4f), to + (Vector3.Up * target.BodyHeight), 0.35f);
                }

                Animations.Hit(t, target, $"-{e.Killed}", to);
            },
            () =>
            {
                target.SetCount(e.RemainingCount);
                target.SetHealth(e.TopHealth);
                Narrate(line);
            });
    }

    /// <summary>A spell's damage to a stack (ADR-110): the stack flashes and shows its losses.</summary>
    public void OnSpellDamaged(BattleUnitSpellDamaged e)
    {
        if (!_units.TryGetValue(e.Unit, out var target))
        {
            return;
        }

        var to = BattleSpace.Center(_planned[e.Unit]);
        var line = Line(e);
        Step(
            t => Animations.Hit(t, target, $"-{e.Killed}", to),
            () =>
            {
                target.SetCount(e.RemainingCount);
                target.SetHealth(e.TopHealth);
                Narrate(line);
            });
    }

    /// <summary>Morale shows as a caption over the unit and a log line.</summary>
    public void OnMorale(BattleMoraleTriggered e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var caption = e.Effect switch
        {
            MoraleEffect.ExtraAction => "Morale!",
            MoraleEffect.Haste => "Eager",
            MoraleEffect.SkipTurn => "Freezes",
            MoraleEffect.Slow => "Hesitant",
            _ => string.Empty,
        };
        var line = Line(e);
        var at = BattleSpace.Center(_planned[e.Unit]) + (Vector3.Up * ((view.BodyHeight * 2) + 0.6f));
        Step(t => Animations.Caption(t, caption, at, 0.5f, 0.2f), () => Narrate(line));
    }

    /// <summary>A stack that came onto the field during the battle (a summon, a copy, raised skeletons, a split part; ADR-120, ADR-152), or one that grew.</summary>
    private void AddUnit(BattleUnitInfo info)
    {
        if (!_units.TryGetValue(info.Unit, out var view))
        {
            view = new BattleUnitView();
            view.Bind(info.Unit, info.Side, state().Creatures.Get(info.Creature), info.Count, info.Width, info.Height, SideColor(info.Side));
            view.Position = BattleSpace.Center(info.Rect);
            field.AddChild(view);
            _units[info.Unit] = view;
            _planned[info.Unit] = info.Rect;
        }

        view.SetCount(info.Count);
    }

    /// <summary>A martyr takes its share of a blow (Martyrdom, ADR-152): it flashes and shows its losses.</summary>
    public void OnShielded(BattleUnitShielded e)
    {
        if (!_units.TryGetValue(e.Martyr, out var martyr))
        {
            return;
        }

        var line = Line(e);
        Step(
            t => BattleAnimations.Flash(t, martyr),
            () =>
            {
                martyr.SetCount(e.RemainingCount);
                martyr.SetHealth(e.TopHealth);
                Narrate(line);
            });
    }

    /// <summary>A Phoenix rises from its ashes (ADR-120).</summary>
    public void OnReborn(BattleUnitReborn e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var line = Line(e);
        Step(
            t => t.TweenProperty(view, "scale", Vector3.One, 0.3f),
            () =>
            {
                view.Visible = true;
                view.Scale = Vector3.One;
                view.SetCount(e.Count);
                view.SetHealth(view.Creature.Health);
                Narrate(line);
            });
    }

    /// <summary>A revive (ADR-123): the stack grows back; one destroyed earlier stands up again.</summary>
    public void OnRevived(BattleUnitRevived e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var line = Line(e);
        Step(
            t => t.TweenProperty(view, "scale", Vector3.One, 0.3f),
            () =>
            {
                view.Visible = true;
                view.Scale = Vector3.One;
                view.SetCount(e.Count);
                view.SetHealth(e.TopHealth);
                Narrate(line);
            });
    }

    public void OnDied(BattleUnitDied e)
    {
        if (!_units.TryGetValue(e.Unit, out var view))
        {
            return;
        }

        var line = Line(e);
        Step(
            t => t.TweenProperty(view, "scale", new Vector3(1, 0.05f, 1), 0.3f),
            () =>
            {
                view.Visible = false;
                Narrate(line);
            });
    }

    private void Step(Action<Tween>? animate, Action apply) => step(animate, apply);

    private void Narrate(string line) => narrate(line);

    private Color SideColor(BattleSide side) => sideColor(side);
}
