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
/// greyed go to the status bar, as do the prompt line, refusals, the other computer's moves and what a click on the field would do with its
/// estimate (<see cref="ShowHover"/>). The spell book (<see cref="BattleSpellBookWindow"/>) and the combat settings
/// (<see cref="CombatSettingsWindow"/>) are windows over the battle. The combat log stands above the action grid, its last 3 lines
/// see-through, the last 20 while the pointer is over it; the LAN chat's place is above the stack panel (<see cref="ChatRect"/>). During the
/// deployment step (ADR-072) the timeline gives way to "Start battle" and the grid keeps Quick combat, Combat settings and Menu. The result
/// window and, after it, the Necromancy window (ADR-072, MAG-7) stand framed over the field dimmed, the losses and the raised undead as
/// portraits. Below <see cref="NarrowWidth"/> of UI width the stack panel and the log take their short forms. Refreshes when
/// <see cref="BattleView3D"/> is idle, so it matches what is shown. Buttons are commands; the root lets mouse input through to the battlefield.
/// </summary>
public partial class BattleHud : Control
{
    /// <summary>The log's lines kept: the battle's last 20, all shown while the pointer is over it (ADR-314).</summary>
    private const int LogKept = 20;

    /// <summary>The log's lines at rest; one fewer in the narrow form.</summary>
    private const int LogLines = 3;

    /// <summary>Below this UI width the HUD takes its narrow form (ADR-314): the stack panel's short form, the log a line shorter.</summary>
    public const float NarrowWidth = 1400;

    /// <summary>The log's frame at rest, see-through; opaque while the pointer is over it.</summary>
    private const float LogOpacity = 0.6f;

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
    private readonly PanelContainer _logPanel = new() { Name = "Log", MouseFilter = MouseFilterEnum.Stop, TooltipText = "The battle's log" };
    private readonly Label _log = new() { VerticalAlignment = VerticalAlignment.Bottom, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
    private BevelFrame _logFrame = null!;
    private bool _logOpen;
    private readonly Button _wait = ActionButton(UiIcon.Hourglass, Hint(BattleHotkeyAction.Wait));
    private readonly Button _defend = ActionButton(UiIcon.Defend, Hint(BattleHotkeyAction.Defend));
    private readonly Button _quick = ActionButton(UiIcon.QuickCombat, Hint(BattleHotkeyAction.QuickCombat));
    private readonly Button _settings = ActionButton(UiIcon.Settings, "Combat settings");
    private readonly Button _creature = ActionButton(UiIcon.Cast, "The stack's own spell");
    private readonly Control _creatureSpacer = new() { MouseFilter = MouseFilterEnum.Ignore };
    private readonly Button _retreat = ActionButton(UiIcon.Retreat, Hint(BattleHotkeyAction.Retreat));
    private readonly Button _surrender = ActionButton(UiIcon.Surrender, "Surrender");
    private readonly Button _book = ActionButton(UiIcon.SpellBook, Hint(BattleHotkeyAction.SpellBook));
    private readonly Button _menu = ActionButton(UiIcon.Menu, Hint(BattleHotkeyAction.Menu));
    private readonly Button _start = new Button { Text = BattleHotkeys.Of(BattleHotkeyAction.Confirm).HintAs("Start battle"), Visible = false }.WithIcon(UiIcon.Confirm, keepText: true);
    private readonly BattleSpellBookWindow _spellBook = new();
    private readonly DimmedCenter _resultLayer = new() { Name = "Result", Visible = false };
    private readonly PanelContainer _result = new();
    private readonly Label _resultTitle = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Label _resultText = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0) };
    private readonly VBoxContainer _resultLosses = new();
    private readonly Button _close = new Button { Text = BattleHotkeys.Of(BattleHotkeyAction.Confirm).HintAs("Return to the map") }.WithIcon(UiIcon.Confirm, keepText: true);
    private readonly Queue<string> _lines = new();

    private LayoutSettings _layout = LayoutSettings.Default;
    private (string Hero, IReadOnlyList<RaisedUndead> Raised)? _raised;
    /// <summary>The prompt line as last written; null when the next refresh must write it whatever it says (a command was taken, a battle opened).</summary>
    private string? _prompt;
    private string? _bookRefusal;
    private bool _narrow;
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
            _resultLayer.OffsetBottom = -(_bottomReserve + StatusBar.Height); // the window centred above the status bar, its buttons clear of it
            _resultLayer.Overhang = _bottomReserve + StatusBar.Height;
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

    /// <summary>Where the LAN chat goes over the battle (ADR-314): at the bottom left, above the stack panel.</summary>
    public Rect2 ChatRect => _parts.ChatRect;

    /// <summary>The HUD's parts were placed again: the chat's place may have moved.</summary>
    public event Action? ChatPlaceChanged;

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
        _parts.Arranged += () => ChatPlaceChanged?.Invoke();
        Resized += FitWidth;

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

        // The log above the action grid: its last lines, see-through; the pointer over it grows it upwards to the last 20 (ADR-314).
        _logFrame = UiFrames.Bevel(_logPanel, BevelKind.Panel, padding: UiSpacing.Padding);
        _logFrame.OwnOpacity = LogOpacity;
        _logPanel.SetMeta(UiIcons.StatusLabelMeta, "Combat log");
        _logPanel.AddChild(_log);
        _logPanel.MouseEntered += () => OpenLog(true);
        _logPanel.MouseExited += () => OpenLog(false);
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
        _creature.WithInfo(detailed => _creatureSpell is { } shown ? InfoSheets.Spell(_session.State, shown.Spell.Id, shown.Rank, detailed) : null);
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

        // The result and, after it, the Necromancy window: framed, over the field dimmed (ADR-314).
        AddChild(_resultLayer);
        _resultLayer.AddChild(_result);
        UiFrames.Bevel(_result, BevelKind.Panel, padding: UiSpacing.Section).OwnOpacity = 1f;
        var resultBox = new VBoxContainer();
        resultBox.AddThemeConstantOverride("separation", UiSpacing.Gap);
        _result.AddChild(resultBox);
        _resultTitle.AddThemeFontSizeOverride("font_size", 26);
        resultBox.AddChild(_resultTitle);
        resultBox.AddChild(_resultText);
        _resultLosses.AddThemeConstantOverride("separation", UiSpacing.Gap);
        resultBox.AddChild(_resultLosses);
        _close.Pressed += Continue;
        _close.SizeFlagsHorizontal = SizeFlags.ShrinkEnd; // along the bottom, at the right (ADR-204)
        UiFrames.Bevel(_close, BevelKind.Button);
        resultBox.AddChild(_close);
        MoveChild(Bar, -1); // the status bar over the dimmed field: it names the portraits of the losses, and its menu button stays at hand
        this.SubscribeWhileInTree<UndeadRaised>(session.Events, e => _raised = (_session.State.GetHero(e.Hero).Name, e.Raised));

        view.Opened += () =>
        {
            _raised = null;
            _prompt = null;
            (ArmedSpell, _creatureArmed, _firstAim) = (null, false, null);
            _lines.Clear();
            _log.Text = string.Empty;
            _resultLayer.Visible = false;
            Bar.ShowSubject(null);
            _spellBook.Close();
            CombatSettings.Close();
            _timelineHover = null;
            Visible = true;
            Refresh();
        };
        view.Idle += Refresh;
        FitWidth(); // already as wide as the screen: Resized comes only with a change
        view.Narrated += line =>
        {
            _lines.Enqueue(line);
            while (_lines.Count > LogKept)
            {
                _lines.Dequeue();
            }

            ShowLog();
            if (_view.WaitingFor is not null)
            {
                Bar.Message.Text = line; // the other computer's move, on the status bar while this one waits (ADR-314)
            }
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
        if (_raised is { } raised)
        {
            _raised = null;
            _resultTitle.Text = "Necromancy";
            _resultText.Text = $"{raised.Hero} raises undead from the fallen.";
            _resultLosses.ClearChildren(detach: true);
            _resultLosses.AddChild(BattleLossRow.Of("They join the army:", raised.Raised.Select(r => (r.Creature.Value, CreatureName(r.Creature.Value), r.Count))));
            _close.Text = BattleHotkeys.Of(BattleHotkeyAction.Confirm).HintAs("Continue");
            return;
        }

        _view.Close();
    }

    /// <summary>A command's outcome: a refusal goes to the status bar.</summary>
    public void Report(CommandResult result)
    {
        if (!result.IsAccepted)
        {
            Bar.Message.Text = $"Refused: {result.Error}";
        }
        else
        {
            _prompt = null; // taken: the next prompt replaces an earlier refusal, even when it reads the same
        }
    }

    /// <summary>
    /// The pointer over the field (ADR-314): what a click on <paramref name="cell"/> would do on the status bar, the armed spell's aim or the
    /// active stack's <paramref name="action"/> with its estimate (<see cref="BattleHoverLine"/>); else the stack there; null: off the field.
    /// </summary>
    public void ShowHover(BattleState? battle, CellPosition? cell, BattleAction? action)
    {
        var line = battle is null || cell is not { } at ? null
            : ArmedSpell is { } spell ? BattleHoverLine.OfSpell(battle, at, spell, _creatureArmed, secondAim: SecondAim(battle))
            : BattleHoverLine.Of(battle, action);
        if (line is not null)
        {
            Bar.ShowSubject(line.Subject, line.Detail);
        }
        else if (cell is { } c && battle?.UnitAt(c) is { IsAlive: true } unit)
        {
            Bar.ShowSubject($"{unit.Creature.Name} ×{unit.Count}", null, CreatureIcons.Of(unit, 24));
        }
        else
        {
            Bar.ShowSubject(null);
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
        else if (_bookRefusal is { } why)
        {
            Bar.Message.Text = why;
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

    /// <summary>The stack whose turn the spell was armed in: the spell is dropped when another acts, a battle opens or the turn leaves this computer.</summary>
    private int? _armedFor;

    /// <summary>The next click is a two-click spell's second aim: its first-aimed stack still stands (else <see cref="AimTwice"/> aims afresh).</summary>
    private bool SecondAim(BattleState battle) => _firstAim is { } first && battle.FindUnit(first) is { IsAlive: true };

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
            var own = battle.ActiveUnit is { } caster ? BattleMagic.CreatureSpell(battle, caster) : null;
            Act(player => new BattleCreatureCastCommand(player, TargetAt(battle, own, cell), cell));
            return true;
        }

        var hero = battle.ActiveUnit is { } acting ? battle.Participant(acting.Side).Caster?.Find(spell) : null;
        Act(player => new BattleCastCommand(player, spell, TargetAt(battle, hero, cell), cell));
        return true;
    }

    private bool AimTwice(BattleState battle, KnownSpell known, CellPosition cell)
    {
        var move = !known.Effect!.SecondIsStack; // a move's second aim is the cell it goes to
        if (_firstAim is not { } first || battle.FindUnit(first) is not { IsAlive: true } aimed)
        {
            if (battle.UnitAt(cell) is not { } unit)
            {
                SetPrompt($"{known.Spell.Name}: click a stack first (Esc cancels).");
                return true;
            }

            _firstAim = unit.Id;
            SetPrompt(move
                ? $"{known.Spell.Name}: click where {unit.Creature.Name} goes (Esc cancels)."
                : known.Effect.DrainPercent > 0
                    ? $"{known.Spell.Name}: click the own stack that drinks the life (Esc cancels)."
                    : $"{known.Spell.Name}: click the own stack that takes the blows meant for {unit.Creature.Name} (Esc cancels).");
            return true;
        }

        (ArmedSpell, _firstAim) = (null, null);
        Act(player => move
            ? new BattleCastCommand(player, known.Spell.Id, first, BattleTargeting.SpellPlacement(battle, aimed, cell))
            : new BattleCastCommand(player, known.Spell.Id, first, Second: battle.UnitAt(cell)?.Id));
        return true;
    }

    /// <summary>The stack the spell aimed at the cell targets, a fallen one for a revive (<see cref="BattleState.SpellTargetAt"/>).</summary>
    private static int? TargetAt(BattleState battle, KnownSpell? spell, CellPosition cell) =>
        battle.ActiveUnit is { } active && spell?.Effect is { } effect ? battle.SpellTargetAt(cell, active.Side, effect)?.Id : battle.UnitAt(cell)?.Id;

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

        (ArmedSpell, _creatureArmed, _firstAim, _armedArea) = (known.Spell.Id.Value, true, null, HitsArea(known));
        _armedFor = _session.State.Battle?.ActiveUnit?.Id;
        SetPrompt($"{known.Spell.Name} (the stack's own spell): click a target (Esc cancels).");
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
        _armedFor = _session.State.Battle?.ActiveUnit?.Id;
        SetPrompt(known.Effect.TwoClicks
            ? $"{known.Spell.Name}: click the stack, then {(!known.Effect.SecondIsStack ? "where it goes" : known.Effect.DrainPercent > 0 ? "the own stack it heals" : "the own stack that takes its blows")} (Esc cancels)."
            : $"{known.Spell.Name}: click a target (Esc cancels).");
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
        if (ArmedSpell is not null && (commander is null || battle?.ActiveUnit?.Id != _armedFor))
        {
            (ArmedSpell, _creatureArmed, _firstAim) = (null, false, null); // the turn moved on: the spell was armed for another stack
        }

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
        var deploying = battle?.Phase == BattlePhase.Deploying; // Quick combat, Combat settings and Menu only (ADR-314)
        _start.Visible = deployer is not null;
        _wait.Visible = _defend.Visible = _retreat.Visible = _surrender.Visible = !deploying && !_view.Watching;
        _book.Visible = !deploying;
        _quick.Visible = !_view.Watching; // the replay viewer keeps Combat settings and Menu (ADR-164, ADR-314)

        if (battle is null || _view.IsBusy)
        {
            return;
        }

        _round.Text = battle.Phase == BattlePhase.Deploying ? "Deployment" : $"Round {battle.Round}";
        _attackerPlate.Show(_session.State, battle, _view.SideName(BattleSide.Attacker), _view.SideColor(BattleSide.Attacker));
        _defenderPlate.Show(_session.State, battle, _view.SideName(BattleSide.Defender), _view.SideColor(BattleSide.Defender));
        SetPrompt(deployer is { } placing
            ? $"{_session.State.GetPlayer(placing).Name}: place your stacks within the first {battle.DeploymentColumns} columns (drag a stack, or click it and then a cell), then start the battle."
            : ArmedSpell is { } armed && _session.State.Spells.TryGet(armed, out var spell)
                ? $"{spell.Name}: click {(SecondAim(battle) ? "its second aim" : "a target")} (Esc cancels)."
            : commander is not null && battle.ActiveUnit is { } acting
                ? $"{_session.State.GetPlayer(commander.Value).Name}: {acting.Creature.Name} to act. Click to move, attack or shoot.{LeavingNote(battle)}"
            : _view.WaitingFor is { } other
                ? $"{_session.State.GetPlayer(other).Name} plays on another computer: waiting for their move."
                : string.Empty);

        _timeline.Show(battle, _view.SideColor);
        _timeline.Visible &= !deploying; // no order before the battle starts; Start battle has the middle
        ShowUnit(null);
    }

    /// <summary>
    /// The prompt line on the status bar (ADR-314): who acts and how, the armed spell's aim, the deployment, the other computer's turn. Written
    /// only when it changes, so a refusal or the other computer's last move stays until the prompt has news.
    /// </summary>
    private void SetPrompt(string text)
    {
        if (text == _prompt)
        {
            return;
        }

        _prompt = text;
        Bar.Message.Text = text;
    }

    /// <summary>
    /// The pointer over the log (or off it): the battle's last 20 lines, grown upwards and opaque, as many as fit above it below the top quarter
    /// of the screen; else its last few, see-through.
    /// </summary>
    private void OpenLog(bool open)
    {
        _logOpen = open;
        _logFrame.OwnOpacity = open ? 1f : LogOpacity;
        ShowLog();
    }

    private void ShowLog()
    {
        var shown = _logOpen ? LogKept : _narrow ? LogLines - 1 : LogLines;
        _log.Text = string.Join('\n', _lines.Skip(Math.Max(0, _lines.Count - shown)));
        var room = _logOpen
            ? Math.Max(1, (int)((_logPanel.GetGlobalRect().End.Y - GetGlobalRect().Position.Y - (Size.Y / 4) - (2 * UiSpacing.Padding)) / (_log.GetLineHeight() + _log.GetThemeConstant("line_spacing"))))
            : -1;
        _log.MaxLinesVisible = room; // the wrapped lines that fit; the oldest skipped
        _log.LinesSkipped = room > 0 ? Math.Max(0, _log.GetLineCount() - room) : 0;
    }

    /// <summary>The narrow form below <see cref="NarrowWidth"/> of UI width (ADR-314): the stack panel's short form, the log a line shorter.</summary>
    private void FitWidth()
    {
        var narrow = Size.X > 0 && Size.X < NarrowWidth;
        if (narrow == _narrow)
        {
            return;
        }

        _narrow = narrow;
        _stackPanel.Compact = narrow;
        ShowLog();
        ShowUnit(null);
    }

    /// <summary>A tournament's battle (ADR-366) says in its first round why retreat and surrender are greyed out.</summary>
    private static string LeavingNote(BattleState battle) => battle.NoLeaving && battle.Round <= 1 ? $" {SurrenderRules.TournamentRefusal}" : string.Empty;

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

        _creature.Icon = SpellIcons.Get(spell.Spell.Id, ActionIconSize) ?? UiIcons.Get(UiIcon.Cast, ActionIconSize);
        _creature.SetLabel($"{caster.Creature.Name}: {spell.Spell.Name} ({caster.CastsLeft[spell.Spell.Id.Value]} left)");
        _creature.TooltipText = $"The stack casts it as its action. {spell.Spell.Notes}";
    }

    /// <summary>The spell book's buttons (the grid's, the plate's) and the open book, for the side it is for.</summary>
    private void RefreshBook(BattleState? battle)
    {
        var side = battle is null ? null : BookSide(battle);
        var refusal = _view.Watching ? "Not while watching a replay" : side is null ? "No hero here has a spell book or a warcry" : null;
        _bookRefusal = refusal;
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
        _resultTitle.Text = summary.Title;
        _resultText.Text = summary.Experience > 0
            ? $"{summary.Reason}\n{_view.SideName(ended.Winner)} gains {summary.Experience} experience."
            : summary.Reason;
        _resultLosses.ClearChildren(detach: true);
        foreach (var losses in summary.Losses)
        {
            _resultLosses.AddChild(BattleLossRow.Of($"{losses.Name} lost:", losses.Lost.Select(c => (c.Creature.Value, CreatureName(c.Creature.Value), c.Lost))));
        }

        _close.Text = BattleHotkeys.Of(BattleHotkeyAction.Confirm).HintAs("Return to the map");
        _resultLayer.Visible = true;
        SetPrompt(string.Empty);
        Bar.ShowSubject(null);
    }
}
