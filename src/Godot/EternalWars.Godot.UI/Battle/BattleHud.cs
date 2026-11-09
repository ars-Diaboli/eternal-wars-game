using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Hud;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// Screen overlay of the 3D battle scene: the sides and round, an initiative timeline (active unit, then the queue,
/// then waiting units), the combat log as the animation plays it, the hovered or active unit's stats, action buttons
/// (wait, defend, retreat, surrender, quick combat), "Start battle" during the deployment step (ADR-072), the result screen and, after it, the
/// Necromancy screen (ADR-072, MAG-7). Refreshes when <see cref="BattleView3D"/> is idle, so it
/// matches what is shown. Buttons are commands; the root lets mouse input through to the battlefield.
/// </summary>
public partial class BattleHud : Control
{
    private const int LogLines = 8;
    private const int TimelineEntries = 14;

    private readonly Label _title = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly HBoxContainer _timeline = new() { Alignment = BoxContainer.AlignmentMode.Center };
    private readonly Label _log = new() { VerticalAlignment = VerticalAlignment.Bottom };
    private readonly Label _info = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };
    private readonly Label _status = new();
    private readonly Button _wait = new() { Text = "Wait (W)" };
    private readonly Button _defend = new() { Text = "Defend (D)" };
    private readonly Button _retreat = new() { Text = "Retreat (R)" };
    private readonly Button _surrender = new() { Text = "Surrender" };
    private readonly Button _quick = new() { Text = "Quick combat (Q)" };
    private readonly Button _start = new() { Text = "Start battle (Enter)", Visible = false };
    private readonly HFlowContainer _warcries = new();
    private readonly Dictionary<Warcry, Button> _warcryButtons = [];
    private readonly HFlowContainer _spells = new(); // wraps: a mage's spells do not fit in one row
    private readonly PanelContainer _result = new() { Visible = false };
    private readonly Label _resultText = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0) };
    private readonly Button _close = new() { Text = "Return to the map (Enter)" };
    private readonly Queue<string> _lines = new();
    private string? _raised;
    private GameSession _session = null!;
    private BattleView3D _view = null!;

    public void Bind(GameSession session, BattleView3D view)
    {
        _session = session;
        _view = view;
        Name = "BattleHud";
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var top = new VBoxContainer { AnchorLeft = 0, AnchorRight = 1, OffsetTop = 8, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(top);
        top.AddChild(_title);
        top.AddChild(_timeline);

        var log = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 8, OffsetTop = -190, OffsetRight = 560, OffsetBottom = -8 };
        AddChild(log);
        log.AddChild(_log);

        // Bottom right; taller content (many spells) grows upwards, never off the screen.
        var actions = new VBoxContainer
        {
            AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = -680, OffsetTop = -230, OffsetRight = -8, OffsetBottom = -48, // above the menu button
            GrowHorizontal = GrowDirection.Begin, GrowVertical = GrowDirection.Begin, Alignment = BoxContainer.AlignmentMode.End,
        };
        AddChild(actions);
        actions.AddChild(_status);
        actions.AddChild(_info);
        var buttons = new HFlowContainer();
        actions.AddChild(buttons);
        foreach (var button in new[] { _start, _wait, _defend, _retreat, _surrender, _quick })
        {
            buttons.AddChild(button);
        }

        // Stronghold heroes' warcries (ADR-067): one button each, shown while the commanding hero knows it.
        actions.AddChild(_warcries);
        foreach (var warcry in Enum.GetValues<Warcry>())
        {
            var button = new Button { Text = $"{warcry} (warcry)", Visible = false };
            button.Pressed += () => Act(player => new BattleWarcryCommand(player, warcry));
            _warcryButtons[warcry] = button;
            _warcries.AddChild(button);
        }

        // The acting side's hero's spells (ADR-110): a spell for a side or the field is cast at once, the others wait for a click on the field.
        actions.AddChild(_spells);

        _wait.Pressed += () => Act(player => new BattleWaitCommand(player));
        _defend.Pressed += () => Act(player => new BattleDefendCommand(player));
        _retreat.Pressed += () => Act(player => new BattleRetreatCommand(player));
        _surrender.Pressed += () => Act(player => new BattleSurrenderCommand(player));
        _quick.Pressed += QuickCombat;
        _start.Pressed += () =>
        {
            if (_view.Deployer is { } player)
            {
                Report(_session.Execute(new BattleStartCommand(player)));
            }
        };

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        center.AddChild(_result);
        var resultBox = new VBoxContainer();
        _result.AddChild(resultBox);
        resultBox.AddChild(_resultText);
        _close.Pressed += Continue;
        resultBox.AddChild(_close);
        this.SubscribeWhileInTree<UndeadRaised>(session.Events, e => _raised =
            $"Necromancy\n\n{_session.State.GetHero(e.Hero).Name} raises {string.Join(", ", e.Raised.Select(r => $"{r.Count} {CreatureName(r.Creature)}"))}.");

        view.Opened += () =>
        {
            _raised = null;
            _lines.Clear();
            _log.Text = string.Empty;
            _result.Visible = false;
            Visible = true;
            Refresh();
        };
        view.Idle += Refresh;
        view.Narrated += line =>
        {
            _lines.Enqueue(line);
            while (_lines.Count > LogLines)
            {
                _lines.Dequeue();
            }

            _log.Text = string.Join('\n', _lines);
        };
        view.Finished += ShowResult;
        view.Closed += () => Visible = false;
    }

    /// <summary>Shows a unit's stats (the hovered one); null falls back to the active unit.</summary>
    public void ShowUnit(BattleUnit? unit)
    {
        var battle = _session.State.Battle;
        unit ??= battle?.ActiveUnit;
        if (battle is null || unit is null || !_view.IsOpen || _view.IsBusy)
        {
            _info.Text = string.Empty;
            return;
        }

        var c = unit.Creature;
        var shots = !c.IsRanged ? string.Empty : battle.HasUnlimitedShots(unit) ? "  shots unlimited" : $"  shots {unit.ShotsLeft}";
        if (unit.IsMachine)
        {
            _info.Text = $"{c.Name} ({_view.SideName(unit.Side)}): war machine, health {unit.TopHealth}/{c.Health}" +
                         (c.IsRanged ? $"\nAttack {c.Attack}  Damage {c.MinDamage}–{c.MaxDamage}  Initiative {battle.InitiativeOf(unit)}{shots}" : string.Empty);
            return;
        }

        var capabilities = c.Capabilities.Count > 0 ? $"\n{string.Join(", ", c.Capabilities.Select(k => k.Kind))}" : string.Empty;
        _info.Text = $"{c.Name} ×{unit.Count} ({_view.SideName(unit.Side)})\n" +
                     $"Attack {c.Attack}  Defense {c.Defense}  Damage {c.MinDamage}–{c.MaxDamage}  Health {unit.TopHealth}/{c.Health}\n" +
                     $"Speed {battle.SpeedOf(unit)}  Initiative {battle.InitiativeOf(unit)}{shots}  Morale {Signed(battle.MoraleOf(unit))}  Luck {Signed(battle.LuckOf(unit))}" +
                     $"{(unit.IsDefending ? "  defending" : string.Empty)}{(battle.IsNative(unit) ? "  native terrain (+1 Attack, Defense, Speed)" : string.Empty)}{capabilities}";

        static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>"Return to the map" on the result screen: first the Necromancy screen if undead were raised, then the map.</summary>
    public void Continue()
    {
        if (_raised is { } text)
        {
            _raised = null;
            _title.Text = "Necromancy";
            _resultText.Text = text;
            return;
        }

        _view.Close();
    }

    public void Report(CommandResult result) => _status.Text = result.IsAccepted ? string.Empty : $"Refused: {result.Error}";

    /// <summary>The spell waiting for its target, if a spell button was pressed (ADR-110).</summary>
    public string? ArmedSpell { get; private set; }

    /// <summary>A two-click spell's first aim (ADR-151): the stack a move takes or a drain hits, once clicked; null before.</summary>
    private int? _firstAim;

    /// <summary>
    /// A click on the battlefield while a spell is armed: casts it at the stack on the cell (or at the cell, for an area) and returns true; false
    /// when no spell is armed, so the click is an ordinary action. A two-click spell (ADR-151) takes its stack first, then the cell a move goes
    /// to, the own stack a drain heals or the martyr that takes the blows (ADR-152).
    /// </summary>
    public bool TryCastAt(CellPosition cell)
    {
        if (ArmedSpell is not { } spell || _session.State.Battle is not { } battle)
        {
            return false;
        }

        if (!_creatureArmed && battle.ActiveUnit is { } active && battle.Participant(active.Side).Caster?.Find(spell) is { Effect.TwoClicks: true } known)
        {
            return AimTwice(battle, known, cell);
        }

        ArmedSpell = null;
        if (_creatureArmed)
        {
            _creatureArmed = false;
            Act(player => new BattleCreatureCastCommand(player, TargetAt(battle, cell), cell));
            return true;
        }

        Act(player => new BattleCastCommand(player, spell, TargetAt(battle, cell), cell));
        return true;
    }

    private bool AimTwice(BattleState battle, KnownSpell known, CellPosition cell)
    {
        var move = known.Effect!.Kind == Core.Domain.Magic.SpellEffectKind.Move;
        if (_firstAim is not { } first || battle.FindUnit(first) is not { IsAlive: true } aimed)
        {
            if (battle.UnitAt(cell) is not { } unit)
            {
                _status.Text = $"{known.Spell.Name}: click a stack first (Esc cancels).";
                return true;
            }

            _firstAim = unit.Id;
            _status.Text = move
                ? $"{known.Spell.Name}: click where {unit.Creature.Name} goes (Esc cancels)."
                : known.Effect.DrainPercent > 0
                    ? $"{known.Spell.Name}: click the own stack that drinks the life (Esc cancels)."
                    : $"{known.Spell.Name}: click the own stack that takes the blows meant for {unit.Creature.Name} (Esc cancels).";
            return true;
        }

        (ArmedSpell, _firstAim) = (null, null);
        Act(player => move
            ? new BattleCastCommand(player, known.Spell.Id, first, BattleTargeting.SpellPlacement(battle, aimed, cell))
            : new BattleCastCommand(player, known.Spell.Id, first, Second: battle.UnitAt(cell)?.Id));
        return true;
    }

    /// <summary>The stack at the cell, else a destroyed one lying there (a revive may bring it back, ADR-123).</summary>
    private static int? TargetAt(BattleState battle, CellPosition cell) =>
        battle.UnitAt(cell)?.Id ?? battle.Units.FirstOrDefault(u => !u.IsAlive && !u.IsMachine && u.Rect.Contains(cell))?.Id;

    /// <summary>Whether the armed spell is the active creature caster's own (ADR-120), not the hero's.</summary>
    private bool _creatureArmed;

    /// <summary>The active stack casts its own spell (ADR-120): at once for a summon or a spell for a side or the field, else after a target click.</summary>
    private void ArmCreature(KnownSpell known)
    {
        if (known.Effect!.Reach is Core.Domain.Magic.SpellReach.Side or Core.Domain.Magic.SpellReach.All)
        {
            Act(player => new BattleCreatureCastCommand(player));
            return;
        }

        (ArmedSpell, _creatureArmed) = (known.Spell.Id, true);
        _status.Text = $"{known.Spell.Name} (the stack's own spell): click a target (Esc cancels).";
    }

    /// <summary>Drops the armed spell (Escape).</summary>
    public void Disarm()
    {
        ArmedSpell = null;
        _firstAim = null;
        _creatureArmed = false;
        Refresh();
    }

    private void Arm(KnownSpell known)
    {
        if (known.Effect!.Reach is Core.Domain.Magic.SpellReach.Side or Core.Domain.Magic.SpellReach.All)
        {
            Act(player => new BattleCastCommand(player, known.Spell.Id));
            return;
        }

        (ArmedSpell, _creatureArmed, _firstAim) = (known.Spell.Id, false, null);
        _status.Text = known.Effect.TwoClicks
            ? $"{known.Spell.Name}: click the stack, then {(known.Effect.Kind == Core.Domain.Magic.SpellEffectKind.Move ? "where it goes" : known.Effect.DrainPercent > 0 ? "the own stack it heals" : "the own stack that takes its blows")} (Esc cancels)."
            : $"{known.Spell.Name}: click a target (Esc cancels).";
    }

    /// <summary>One button per battle spell of the acting side's hero, with its cost; disabled while it cannot be cast at all.</summary>
    private void RefreshSpells(BattleState? battle, Core.Domain.Common.PlayerId? commander)
    {
        foreach (var child in _spells.GetChildren())
        {
            child.QueueFree();
        }

        if (battle?.ActiveUnit is not { } active || commander is null)
        {
            return;
        }

        // A creature caster's own spell (ADR-120): its action, no mana.
        if (BattleMagic.CreatureSpell(active) is { Effect: not null } own)
        {
            var casts = active.CastsLeft.Where(c => BattleMagic.CasterSpells.GetValueOrDefault(c.Key) == own.Spell.Id).Sum(c => c.Value);
            var cast = new Button { Text = $"{active.Creature.Name}: {own.Spell.Name} ({casts} left)", TooltipText = $"The stack casts it as its action. {own.Spell.Notes}" };
            cast.Pressed += () => ArmCreature(own);
            var ownId = own.Spell.Id;
            cast.WithInfo(detailed => Core.Domain.Info.InfoSheets.Spell(_session.State, ownId, 1, detailed));
            _spells.AddChild(cast);
        }

        if (battle.Participant(active.Side).Caster is not { } caster)
        {
            return;
        }

        _spells.AddChild(new Label { Text = $"Mana {battle.ManaOf(active.Side)}" });
        foreach (var known in caster.Spells.Where(s => s.Effect is not null))
        {
            var cost = BattleMagic.Cost(battle, active.Side, known);
            var button = new Button { Text = $"{Core.Domain.Magic.MagicRules.NameAtRank(known.Spell, known.Rank)} ({cost})" };
            var refusal = battle.HasCastThisRound(active.Side) ? "The hero has already cast a spell this round."
                : battle.ManaOf(active.Side) < cost ? "Not enough mana." : null;
            button.Disabled = refusal is not null;
            button.TooltipText = refusal ?? known.Spell.Notes;
            button.Pressed += () => Arm(known);
            var spellId = known.Spell.Id;
            var rank = known.Rank;
            button.WithInfo(detailed => Core.Domain.Info.InfoSheets.Spell(_session.State, spellId, rank, detailed));
            _spells.AddChild(button);
        }
    }

    private void Act(Func<Core.Domain.Common.PlayerId, GameCommand> command)
    {
        if (_view.Commander is { } player)
        {
            Report(_session.Execute(command(player)));
        }
    }

    private void QuickCombat()
    {
        if (_view.Result is null && (_session.Battles.Commander(BattleSide.Attacker) ?? _session.Battles.Commander(BattleSide.Defender)) is { } player)
        {
            Report(_session.Execute(new AutoResolveBattleCommand(player)));
        }
    }

    private void Refresh()
    {
        foreach (var child in _timeline.GetChildren())
        {
            child.QueueFree();
        }

        var battle = _session.State.Battle;
        var commander = _view.Commander;
        _wait.Disabled = commander is null;
        _defend.Disabled = commander is null;
        var retreatRefusal = commander is null || battle?.ActiveUnit is not { } active ? "" : SurrenderRules.CheckRetreat(battle, active.Side);
        _retreat.Disabled = retreatRefusal is not null;
        _retreat.TooltipText = retreatRefusal is { Length: > 0 } why ? why : "";
        RefreshSurrender(battle, commander);
        _quick.Disabled = _view.Result is not null || battle is null ||
                          (_session.Battles.Commander(BattleSide.Attacker) ?? _session.Battles.Commander(BattleSide.Defender)) is null;
        RefreshWarcries(battle, commander);
        RefreshSpells(battle, commander);
        var deployer = _view.Deployer;
        _start.Visible = deployer is not null;
        _wait.Visible = _defend.Visible = _retreat.Visible = _surrender.Visible = deployer is null;

        if (battle is null || _view.IsBusy)
        {
            return;
        }

        _title.Text = $"{_view.SideName(BattleSide.Attacker)}  vs  {_view.SideName(BattleSide.Defender)}   ·   round {battle.Round}";
        _status.Text = deployer is { } placing
            ? $"{_session.State.GetPlayer(placing).Name}: place your stacks within the first {battle.DeploymentColumns} columns (click a stack, then a cell), then start the battle."
            : ArmedSpell is { } armed && _session.State.Spells.TryGet(armed, out var spell)
                ? $"{spell.Name}: click {(_firstAim is null ? "a target" : "its second aim")} (Esc cancels)."
            : commander is not null && battle.ActiveUnit is { } acting
                ? $"{_session.State.GetPlayer(commander.Value).Name}: {acting.Creature.Name} to act. Click to move, attack or shoot."
                : string.Empty;

        var order = new List<(BattleUnit Unit, bool Waiting)>();
        if (battle.ActiveUnit is { } current)
        {
            order.Add((current, false));
        }

        order.AddRange(battle.Queue.Select(id => (battle.GetUnit(id), false)));
        order.AddRange(battle.Waiting.Select(id => (battle.GetUnit(id), true)));
        foreach (var (unit, waiting) in order.Where(o => o.Unit.IsAlive).Take(TimelineEntries))
        {
            _timeline.AddChild(Entry(unit, waiting, unit == battle.ActiveUnit));
        }

        ShowUnit(null);
    }

    /// <summary>Surrender (ADR-070, ADR-081): its price, or why the acting side's hero cannot surrender.</summary>
    private void RefreshSurrender(BattleState? battle, Core.Domain.Common.PlayerId? commander)
    {
        if (battle?.ActiveUnit is not { } active || commander is null)
        {
            _surrender.Disabled = true;
            return;
        }

        var refusal = SurrenderRules.Check(_session.State, battle, active.Side);
        _surrender.Text = $"Surrender ({SurrenderRules.Price(battle, active.Side)} gold)";
        _surrender.Disabled = refusal is not null;
        _surrender.TooltipText = refusal ?? "Pay the enemy and leave with your army; the hero waits in your taverns.";
    }

    /// <summary>Shows the warcries the acting side's hero knows; those already used, or a second one in a round, are disabled.</summary>
    private void RefreshWarcries(BattleState? battle, Core.Domain.Common.PlayerId? commander)
    {
        var side = battle?.ActiveUnit?.Side;
        var level = battle is not null && side is { } s ? battle.Participant(s).Bonus.Warcry : Core.Domain.Heroes.SkillLevel.None;
        foreach (var (warcry, button) in _warcryButtons)
        {
            button.Visible = battle is not null && PrimalFury.Knows(level, warcry);
            var refusal = battle is not null && side is { } own ? battle.WarcryRefusal(own, warcry) : "No battle.";
            button.Disabled = commander is null || refusal is not null;
            button.TooltipText = refusal ?? string.Empty;
        }
    }

    private string CreatureName(string id) =>
        _session.State.Creatures.TryGet(id, out var creature) || WarMachines.TryGet(id, out creature) ? creature.Name : id;

    private PanelContainer Entry(BattleUnit unit, bool waiting, bool active)
    {
        var style = new StyleBoxFlat
        {
            BgColor = _view.SideColor(unit.Side).Darkened(active ? 0.1f : 0.5f),
            BorderColor = Colors.White,
            ContentMarginLeft = 6,
            ContentMarginRight = 6,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
        };
        style.SetBorderWidthAll(active ? 2 : 0);
        var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        panel.AddThemeStyleboxOverride("panel", style);
        panel.AddChild(new Label { Text = $"{unit.Creature.Name}\n×{unit.Count}{(waiting ? "  (w)" : string.Empty)}", HorizontalAlignment = HorizontalAlignment.Center });
        return panel;
    }

    private void ShowResult(BattleEnded ended)
    {
        Refresh();
        var lines = new List<string>
        {
            ended.Reason switch
            {
                BattleEndReason.Retreated => $"{_view.SideName(ended.Winner.Opponent())} retreated: {_view.SideName(ended.Winner)} wins.",
                BattleEndReason.Surrendered => $"{_view.SideName(ended.Winner.Opponent())} surrendered: {_view.SideName(ended.Winner)} wins.",
                _ => $"{_view.SideName(ended.Winner)} wins the battle.",
            },
            string.Empty,
        };
        foreach (var side in new[] { BattleSide.Attacker, BattleSide.Defender })
        {
            var lost = ended.Casualties.Where(c => c.Side == side)
                .Select(c => $"{c.Lost} {CreatureName(c.Creature)}")
                .ToList();
            lines.Add($"{_view.SideName(side)} lost: {(lost.Count > 0 ? string.Join(", ", lost) : "nothing")}");
        }

        if (ended.Experience > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"{_view.SideName(ended.Winner)} gains {ended.Experience} experience.");
        }

        _resultText.Text = string.Join('\n', lines);
        _result.Visible = true;
        _title.Text = "Battle over";
        _status.Text = string.Empty;
        _info.Text = string.Empty;
    }
}
