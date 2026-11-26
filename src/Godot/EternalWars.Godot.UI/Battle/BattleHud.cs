using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Cursors;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Settings;
using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Hud;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// Screen overlay of the 3D battle scene, in the adventure HUD's bevelled frames (ADR-314), placed by <see cref="BattleHudLayout"/>: the
/// attacker's and defender's plates (<see cref="BattleSidePlate"/>) and the round plate along the top; a bottom row of the stack panel
/// (<see cref="BattleStackPanel"/>: the hovered stack, else the active one), the timeline (<see cref="BattleTimeline"/>; under the round plate
/// when the Layout tab says top) and the 4 × 2 action grid: Wait, Defend, Quick combat, Combat settings / Retreat, Surrender, Spell book,
/// Menu, a fifth column for the active creature caster's own spell. The buttons show icons; their names, keys and the reasons they are
/// greyed go to the status bar. The spell book (<see cref="BattleSpellBookWindow"/>) and the combat settings
/// (<see cref="CombatSettingsWindow"/>) are windows over the battle. The combat log stands above the action grid, "Start battle" in the
/// middle of the bottom row during the deployment step (ADR-072); the result window and, after it, the Necromancy window (ADR-072, MAG-7).
/// Refreshes when <see cref="BattleView3D"/> is idle, so it matches what is shown. Buttons are commands; the root lets mouse input through to
/// the battlefield.
/// </summary>
public partial class BattleHud : Control
{
    private const int LogLines = 8;

    /// <summary>An action button's icon side; the button is a square round it, as on the adventure map.</summary>
    private const int ActionIconSize = 36;

    private readonly BattleHudLayout _parts = new();
    private readonly BattleSidePlate _attackerPlate = new(BattleSide.Attacker);
    private readonly BattleSidePlate _defenderPlate = new(BattleSide.Defender);
    private readonly PanelContainer _roundPlate = new() { Name = "RoundPlate", MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _round = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly BattleStackPanel _stackPanel = new();
    private readonly BattleTimeline _timeline = new();
    private readonly GridContainer _actions = new() { Name = "Actions", Columns = 4 };
    private readonly PanelContainer _logPanel = new() { Name = "Log", MouseFilter = MouseFilterEnum.Ignore };
    private readonly Label _log = new() { VerticalAlignment = VerticalAlignment.Bottom, AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly Button _wait = ActionButton(UiIcon.Hourglass, Hint(BattleHotkeyAction.Wait));
    private readonly Button _defend = ActionButton(UiIcon.Defend, Hint(BattleHotkeyAction.Defend));
    private readonly Button _quick = ActionButton(UiIcon.QuickCombat, Hint(BattleHotkeyAction.QuickCombat));
    private readonly Button _settings = ActionButton(UiIcon.Settings, "Combat settings");
    private readonly Button _creature = ActionButton(UiIcon.Spells, "The stack's own spell");
    private readonly Control _creatureSpacer = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Button _retreat = ActionButton(UiIcon.Retreat, Hint(BattleHotkeyAction.Retreat));
    private readonly Button _surrender = ActionButton(UiIcon.Surrender, "Surrender");
    private readonly Button _book = ActionButton(UiIcon.SpellBook, Hint(BattleHotkeyAction.SpellBook));
    private readonly Button _menu = ActionButton(UiIcon.Menu, Hint(BattleHotkeyAction.Menu));
    private readonly Button _start = new() { Text = BattleHotkeys.Of(BattleHotkeyAction.Confirm).HintAs("Start battle"), Visible = false };
    private readonly BattleSpellBookWindow _spellBook = new();
    private readonly PanelContainer _result = new() { Visible = false };
    private readonly Label _resultTitle = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Label _resultText = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0) };
    private readonly Button _close = new() { Text = BattleHotkeys.Of(BattleHotkeyAction.Confirm).HintAs("Return to the map") };
    private readonly Queue<string> _lines = new();

    private LayoutSettings _layout = LayoutSettings.Default;
    private string? _raised;
    private int? _timelineHover;
    private KnownSpell? _creatureSpell;
    private GameSession _session = null!;
    private BattleView3D _view = null!;

    /// <summary>
    /// The layout settings (the "Layout" tab, ADR-156, ADR-314): the timeline in the bottom row, or under the round plate at the top.
    /// </summary>
    public LayoutSettings Layout
    {
        get => _layout;
        set
        {
            _layout = value;
            _parts.TimelineOnTop = value.InitiativeBar == ScreenEdge.Top;
            _parts.BottomReserve = _bottomReserve + StatusBar.Height; // the status bar along the bottom (ADR-204)
            Bar.BottomOffset = _bottomReserve;
        }
    }

    private float _bottomReserve;

    /// <summary>
    /// The battle's status bar (ADR-204): the buttons' names, keys and reasons, and the field's hints; the menu button sits on its right end.
    /// Without resources.
    /// </summary>
    public StatusBar Bar { get; } = new() { RightReserve = StatusBar.MenuButtonWidth };

    /// <summary>The combat settings window (ADR-314); the app shows the settings in it and keeps its changes.</summary>
    public CombatSettingsWindow CombatSettings { get; } = new();

    /// <summary>The action grid's Menu was pressed: the app's menu (F10).</summary>
    public event Action? MenuRequested;

    /// <summary>The timeline's entries, as shown (for the smoke checks).</summary>
    public IEnumerable<string> Timeline => _timeline.Shown;

    /// <summary>The stack panel's name line (for the smoke checks).</summary>
    public string StackTitle => _stackPanel.Title;

    /// <summary>Room kept free along the bottom edge (the replay viewer's bar, ADR-164): the bottom row stands above it.</summary>
    public float BottomReserve
    {
        get => _bottomReserve;
        set
        {
            _bottomReserve = value;
            Layout = _layout;
        }
    }

    /// <summary>A window of the HUD is open over the battle (the spell book, the combat settings).</summary>
    public bool WindowOpen => _spellBook.IsOpen || CombatSettings.IsOpen;

    public void Bind(GameSession session, BattleView3D view)
    {
        _session = session;
        _view = view;
        Name = "BattleHud";
        Visible = false;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        AddChild(Bar);
        Bar.Bind(null);
        AddChild(_parts);
        Layout = _layout;

        // Along the top: the plates and the round.
        _parts.Set(BattleHudPart.AttackerPlate, _attackerPlate);
        _parts.Set(BattleHudPart.DefenderPlate, _defenderPlate);
        foreach (var plate in new[] { _attackerPlate, _defenderPlate })
        {
            plate.PortraitPressed += () => ShowHero(plate.Side);
            plate.BookPressed += ToggleSpellBook;
        }

        UiFrames.Bevel(_roundPlate, BevelKind.Panel, padding: UiSpacing.Gap);
        _roundPlate.CustomMinimumSize = new Vector2(180, BattleHudLayout.RoundPlateHeight);
        _round.AddThemeFontSizeOverride("font_size", 18);
        _roundPlate.AddChild(_round);
        _parts.Set(BattleHudPart.RoundPlate, _roundPlate);

        // The bottom row: the stack panel, the timeline, the action grid; Start battle in the middle while deploying.
        _parts.Set(BattleHudPart.StackPanel, _stackPanel);
        _parts.Set(BattleHudPart.Timeline, _timeline);
        _timeline.Hovered += id =>
        {
            _timelineHover = id;
            _view.Light(id);
            ShowUnit(null);
        };

        var actionsFrame = new PanelContainer { Name = "ActionsFrame", MouseFilter = MouseFilterEnum.Ignore };
        UiFrames.Bevel(actionsFrame, BevelKind.Panel, padding: UiSpacing.Padding);
        _actions.AddThemeConstantOverride("h_separation", UiSpacing.Cell);
        _actions.AddThemeConstantOverride("v_separation", UiSpacing.Cell);
        _actions.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        actionsFrame.AddChild(_actions);
        foreach (var control in new Control[] { _wait, _defend, _quick, _settings, _creature, _retreat, _surrender, _book, _menu, _creatureSpacer })
        {
            _actions.AddChild(control);
        }

        _parts.Set(BattleHudPart.Actions, actionsFrame);
        UiFrames.Bevel(_start, BevelKind.Button);
        _parts.Set(BattleHudPart.Start, _start);

        // The log above the action grid, the prompt line first.
        UiFrames.Bevel(_logPanel, BevelKind.Panel, padding: UiSpacing.Padding);
        var logBox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        logBox.AddChild(_status);
        logBox.AddChild(_log);
        _logPanel.AddChild(logBox);
        _parts.Set(BattleHudPart.Log, _logPanel);

        _wait.Pressed += () => Act(player => new BattleWaitCommand(player));
        _defend.Pressed += () => Act(player => new BattleDefendCommand(player));
        _retreat.Pressed += () => Act(player => new BattleRetreatCommand(player));
        _surrender.Pressed += () => Act(player => new BattleSurrenderCommand(player));
        _quick.Pressed += QuickCombat;
        _settings.Pressed += ToggleCombatSettings;
        _book.Pressed += ToggleSpellBook;
        _menu.Pressed += () => MenuRequested?.Invoke();
        _creature.Pressed += () =>
        {
            if (_creatureSpell is { } own)
            {
                ArmCreature(own);
            }
        };
        _start.Pressed += () =>
        {
            if (_view.Deployer is { } player)
            {
                Report(_session.Execute(new BattleStartCommand(player)));
            }
        };

        // The windows over the battle.
        AddChild(_spellBook);
        _spellBook.Bind(session);
        _spellBook.SpellPicked += Arm;
        _spellBook.WarcryPicked += warcry => Act(player => new BattleWarcryCommand(player, warcry));
        AddChild(CombatSettings);
        CombatSettings.Bind();

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        center.AddChild(_result);
        _result.UseOpaqueBackground();
        var resultBox = new VBoxContainer();
        resultBox.AddThemeConstantOverride("separation", UiSpacing.Gap);
        _result.AddChild(resultBox);
        _resultTitle.AddThemeFontSizeOverride("font_size", 24);
        resultBox.AddChild(_resultTitle);
        resultBox.AddChild(_resultText);
        _close.Pressed += Continue;
        resultBox.AddChild(_close);
        this.SubscribeWhileInTree<UndeadRaised>(session.Events, e => _raised =
            $"{_session.State.GetHero(e.Hero).Name} raises {string.Join(", ", e.Raised.Select(r => $"{r.Count} {CreatureName(r.Creature.Value)}"))}.");

        view.Opened += () =>
        {
            _raised = null;
            _lines.Clear();
            _log.Text = string.Empty;
            _result.Visible = false;
            _spellBook.Close();
            CombatSettings.Close();
            _timelineHover = null;
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
            if (Shared.LaunchArguments.AutoInput is not null)
            {
                GD.Print($"battle log: {line}"); // a scripted run's log can be searched for what the battle did (ADR-235)
            }
        };
        view.Finished += ShowResult;
        view.Closed += () =>
        {
            _view.Light(null);
            Visible = false;
        };
    }

    /// <summary>
    /// Shows a stack in the stack panel: <paramref name="unit"/> (the stack under the pointer on the field), else the timeline's entry under the
    /// pointer, else the active stack.
    /// </summary>
    public void ShowUnit(BattleUnit? unit)
    {
        var battle = _session.State.Battle;
        unit ??= _timelineHover is { } id ? battle?.FindUnit(id) : null;
        unit ??= battle?.ActiveUnit;
        if (battle is null || unit is not { IsAlive: true } || !_view.IsOpen)
        {
            _stackPanel.Show(null, null, Colors.White);
            return;
        }

        if (_view.IsBusy)
        {
            return; // the field is still playing what happened: the panel matches it once idle
        }

        _stackPanel.Show(StackPanel.Of(battle, unit, _session.State.Spells), CreatureIcons.Of(unit, BattleStackPanel.PortraitSize), _view.SideColor(unit.Side));
    }

    /// <summary>"Return to the map" on the result window: first the Necromancy window if undead were raised, then the map.</summary>
    public void Continue()
    {
        if (_raised is { } text)
        {
            _raised = null;
            _resultTitle.Text = "Necromancy";
            _resultText.Text = text;
            return;
        }

        _view.Close();
    }

    public void Report(CommandResult result)
    {
        _status.Text = result.IsAccepted ? string.Empty : $"Refused: {result.Error}";
        if (!result.IsAccepted)
        {
            Bar.Message.Text = _status.Text;
        }
    }

    /// <summary>Opens the spell book of the side it is for (C, the action grid, the plate's button), or closes it.</summary>
    public void ToggleSpellBook()
    {
        if (_spellBook.IsOpen)
        {
            _spellBook.Close();
            return;
        }

        CombatSettings.Close();
        if (_session.State.Battle is { } battle && BookSide(battle) is { } side && BattleSpellBook.Of(battle, side) is { } book)
        {
            _spellBook.Open(book, _view.SideName(side), BookNotNow(battle, side));
        }
    }

    /// <summary>Opens the combat settings window, or closes it.</summary>
    public void ToggleCombatSettings()
    {
        _spellBook.Close();
        if (CombatSettings.IsOpen)
        {
            CombatSettings.Close();
        }
        else
        {
            CombatSettings.Open();
        }
    }

    /// <summary>Closes the window open over the battle (Escape); false when none was.</summary>
    public bool CloseWindow()
    {
        if (!WindowOpen)
        {
            return false;
        }

        _spellBook.Close();
        CombatSettings.Close();
        return true;
    }

    /// <summary>The spell waiting for its target, if a spell button was pressed (ADR-110).</summary>
    public string? ArmedSpell { get; private set; }

    /// <summary>Whether the armed spell hits an area (an area or a line, for the cursor, ADR-204); null when none is armed.</summary>
    public bool? ArmedSpellIsArea => ArmedSpell is null ? null : _armedArea;

    private bool _armedArea;

    /// <summary>A button's text with its key from the battle's hotkey table (ADR-314): "Wait [W]".</summary>
    private static string Hint(BattleHotkeyAction action) => BattleHotkeys.Of(action).Hint;

    private static bool HitsArea(KnownSpell known) => known.Effect is { } effect && (effect.Reach == Core.Domain.Magic.SpellReach.Area || effect.Zone is not null);

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
        var move = !known.Effect!.SecondIsStack; // a move's second aim is the cell it goes to
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

        (ArmedSpell, _creatureArmed, _armedArea) = (known.Spell.Id.Value, true, HitsArea(known));
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

        (ArmedSpell, _creatureArmed, _firstAim, _armedArea) = (known.Spell.Id.Value, false, null, HitsArea(known));
        _status.Text = known.Effect.TwoClicks
            ? $"{known.Spell.Name}: click the stack, then {(!known.Effect.SecondIsStack ? "where it goes" : known.Effect.DrainPercent > 0 ? "the own stack it heals" : "the own stack that takes its blows")} (Esc cancels)."
            : $"{known.Spell.Name}: click a target (Esc cancels).";
    }

    /// <summary>A square icon button of the action grid (ADR-314): its <paramref name="label"/> ("Wait [W]") and reasons on the status bar.</summary>
    private static Button ActionButton(UiIcon icon, string label)
    {
        var button = new Button { Text = label, FocusMode = FocusModeEnum.None }.WithIcon(icon, ActionIconSize);
        var side = ActionIconSize + (2 * UiIcons.IconPadding);
        button.CustomMinimumSize = new Vector2(side, side);
        UiFrames.Bevel(button, BevelKind.Button, padding: UiIcons.IconPadding); // the theme's wider margins would stretch it
        return button;
    }

    /// <summary>
    /// Whose spell book the HUD opens: the active stack's side while its commander plays here, else the first side played here with a book; null
    /// in the replay viewer or when no side here has one.
    /// </summary>
    private BattleSide? BookSide(BattleState battle)
    {
        if (_view.Watching)
        {
            return null;
        }

        if (_view.Commander is not null && battle.ActiveUnit is { } active && BattleSpellBook.Of(battle, active.Side) is not null)
        {
            return active.Side;
        }

        foreach (var side in new[] { BattleSide.Attacker, BattleSide.Defender })
        {
            if (_view.PlayedHere.Contains(side) && BattleSpellBook.Of(battle, side) is not null)
            {
                return side;
            }
        }

        return null;
    }

    /// <summary>Why nothing in <paramref name="side"/>'s book can be used now (another stack's turn, deployment, the result), or null.</summary>
    private string? BookNotNow(BattleState battle, BattleSide side) =>
        _view.Result is not null ? "The battle is over"
        : battle.Phase == BattlePhase.Deploying ? "Not during the deployment"
        : _view.Commander is null || battle.ActiveUnit?.Side != side ? "Wait for one of your stacks to act"
        : null;

    /// <summary>The hero of <paramref name="side"/> in its information window: one's own in full, the opponent's by the viewer's Scouting (ADR-314).</summary>
    private void ShowHero(BattleSide side)
    {
        if (_session.State.Battle is not { } battle || InfoWindow.Current is not { } window)
        {
            return;
        }

        var other = side == BattleSide.Attacker ? BattleSide.Defender : BattleSide.Attacker;
        var viewer = _view.PlayedHere.Contains(side) ? battle.Participant(side).Owner
            : _view.PlayedHere.Contains(other) ? battle.Participant(other).Owner
            : battle.Participant(side).Owner; // nobody here: the replay viewer sees it in full
        if (viewer is { } player && InfoSheets.BattleHero(_session.State, battle, side, player, window.Detailed) is { } sheet)
        {
            window.Open(sheet);
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
        if (_view.QuickCombatPlayer is { } player)
        {
            Report(_session.Execute(new AutoResolveBattleCommand(player)));
        }
    }

    private void Refresh()
    {
        var battle = _session.State.Battle;
        var commander = _view.Commander;
        Enable(_wait, commander is null ? "Wait for one of your stacks to act" : null, "The stack acts later this round");
        Enable(_defend, commander is null ? "Wait for one of your stacks to act" : null, "The stack skips its turn and defends until its next");
        var retreatRefusal = commander is null || battle?.ActiveUnit is not { } active ? "Wait for one of your stacks to act" : SurrenderRules.CheckRetreat(battle, active.Side);
        Enable(_retreat, retreatRefusal, "Flee the battle");
        RefreshSurrender(battle, commander);
        Enable(_quick, battle is null || _view.QuickCombatPlayer is null
            ? _view.PlaysHere is not null && _view.Result is null ? "A battle between players on two computers is fought by hand." : "Not now"
            : null, "The computer fights the rest of the battle for both sides");
        RefreshCreature(battle, commander);
        RefreshBook(battle);
        var deployer = _view.Deployer;
        _start.Visible = deployer is not null;
        _wait.Visible = _defend.Visible = _retreat.Visible = _surrender.Visible = deployer is null && !_view.Watching;
        _book.Visible = deployer is null;
        _quick.Visible = !_view.Watching; // the replay viewer only watches (ADR-164)

        if (battle is null || _view.IsBusy)
        {
            return;
        }

        _round.Text = battle.Phase == BattlePhase.Deploying ? "Deployment" : $"Round {battle.Round}";
        _attackerPlate.Show(_session.State, battle, _view.SideName(BattleSide.Attacker), _view.SideColor(BattleSide.Attacker));
        _defenderPlate.Show(_session.State, battle, _view.SideName(BattleSide.Defender), _view.SideColor(BattleSide.Defender));
        _status.Text = deployer is { } placing
            ? $"{_session.State.GetPlayer(placing).Name}: place your stacks within the first {battle.DeploymentColumns} columns (drag a stack, or click it and then a cell), then start the battle."
            : ArmedSpell is { } armed && _session.State.Spells.TryGet(armed, out var spell)
                ? $"{spell.Name}: click {(_firstAim is null ? "a target" : "its second aim")} (Esc cancels)."
            : commander is not null && battle.ActiveUnit is { } acting
                ? $"{_session.State.GetPlayer(commander.Value).Name}: {acting.Creature.Name} to act. Click to move, attack or shoot."
            : _view.WaitingFor is { } other
                ? $"{_session.State.GetPlayer(other).Name} plays on another computer: waiting for their move."
                : string.Empty;

        _timeline.Show(battle, _view.SideColor);
        ShowUnit(null);
    }

    private static void Enable(Button button, string? refusal, string tooltip)
    {
        button.Disabled = refusal is not null;
        button.TooltipText = refusal ?? tooltip;
    }

    /// <summary>Surrender (ADR-070, ADR-081): its price, or why the acting side's hero cannot surrender.</summary>
    private void RefreshSurrender(BattleState? battle, Core.Domain.Common.PlayerId? commander)
    {
        if (battle?.ActiveUnit is not { } active || commander is null)
        {
            Enable(_surrender, "Wait for one of your stacks to act", "");
            return;
        }

        var refusal = SurrenderRules.Check(_session.State, battle, active.Side);
        _surrender.SetLabel($"Surrender ({SurrenderRules.Price(battle, active.Side)} gold)");
        Enable(_surrender, refusal, "Pay the enemy and leave with your army; the hero waits in your taverns.");
    }

    /// <summary>The active creature caster's own spell (ADR-120): a fifth column of the action grid while it acts.</summary>
    private void RefreshCreature(BattleState? battle, Core.Domain.Common.PlayerId? commander)
    {
        _creatureSpell = commander is not null && battle?.ActiveUnit is { } active && BattleMagic.CreatureSpell(battle, active) is { Effect: not null } own ? own : null;
        var shown = _creatureSpell is not null && _view.Deployer is null;
        _creature.Visible = _creatureSpacer.Visible = shown;
        _actions.Columns = shown ? 5 : 4;
        if (_creatureSpell is not { } spell || battle?.ActiveUnit is not { } caster)
        {
            return;
        }

        _creature.Icon = SpellIcons.Get(spell.Spell.Id, ActionIconSize) ?? UiIcons.Get(UiIcon.Spells, ActionIconSize);
        _creature.SetLabel($"{caster.Creature.Name}: {spell.Spell.Name} ({caster.CastsLeft[spell.Spell.Id.Value]} left)");
        _creature.TooltipText = $"The stack casts it as its action. {spell.Spell.Notes}";
    }

    /// <summary>The spell book's buttons (the grid's, the plate's) and the open book, for the side it is for.</summary>
    private void RefreshBook(BattleState? battle)
    {
        var side = battle is null ? null : BookSide(battle);
        var refusal = _view.Watching ? "Not while watching a replay" : side is null ? "No hero here has a spell book or a warcry" : null;
        Enable(_book, refusal, "The battle spells and warcries");
        _attackerPlate.ShowBook(side == BattleSide.Attacker, null);
        _defenderPlate.ShowBook(side == BattleSide.Defender, null);
        if (_spellBook.IsOpen)
        {
            if (battle is not null && side is { } own && BattleSpellBook.Of(battle, own) is { } book)
            {
                _spellBook.Open(book, _view.SideName(own), BookNotNow(battle, own));
            }
            else
            {
                _spellBook.Close();
            }
        }
    }

    private string CreatureName(string id) =>
        _session.State.Creatures.TryGet(id, out var creature) || WarMachines.TryGet(id, out creature) ? creature.Name : id;

    private void ShowResult(BattleEnded ended)
    {
        Refresh();
        _spellBook.Close();
        CombatSettings.Close();
        var summary = BattleSummary.Of(ended, _view.PlayedHere, _view.SideName); // the title for this screen, the reason, the losses (ADR-314)
        var lines = new List<string> { summary.Reason, string.Empty };
        foreach (var losses in summary.Losses)
        {
            var lost = losses.Lost.Select(c => $"{c.Lost} {CreatureName(c.Creature.Value)}").ToList();
            lines.Add($"{losses.Name} lost: {(lost.Count > 0 ? string.Join(", ", lost) : "nothing")}");
        }

        if (summary.Experience > 0)
        {
            lines.Add(string.Empty);
            lines.Add($"{_view.SideName(ended.Winner)} gains {summary.Experience} experience.");
        }

        _resultText.Text = string.Join('\n', lines);
        _resultTitle.Text = summary.Title;
        _result.Visible = true;
        _status.Text = string.Empty;
    }
}
