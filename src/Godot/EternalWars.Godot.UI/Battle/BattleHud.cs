using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Battle;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// Screen overlay of the 3D battle scene: the sides and round, an initiative timeline (active unit, then the queue,
/// then waiting units), the combat log as the animation plays it, the hovered or active unit's stats, action buttons
/// (wait, defend, retreat, quick combat) and the result screen. Refreshes when <see cref="BattleView3D"/> is idle, so it
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
    private readonly Button _quick = new() { Text = "Quick combat (Q)" };
    private readonly PanelContainer _result = new() { Visible = false };
    private readonly Label _resultText = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0) };
    private readonly Queue<string> _lines = new();
    private GameSession _session = null!;
    private BattleView3D _view = null!;

    public void Bind(GameSession session, BattleView3D view)
    {
        _session = session;
        _view = view;
        Name = "BattleHud";
        Visible = false;
        SetAnchorsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;

        var top = new VBoxContainer { AnchorLeft = 0, AnchorRight = 1, OffsetTop = 8, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(top);
        top.AddChild(_title);
        top.AddChild(_timeline);

        var log = new PanelContainer { AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 8, OffsetTop = -190, OffsetRight = 560, OffsetBottom = -8 };
        AddChild(log);
        log.AddChild(_log);

        var actions = new VBoxContainer { AnchorLeft = 1, AnchorRight = 1, AnchorTop = 1, AnchorBottom = 1, OffsetLeft = -440, OffsetTop = -190, OffsetRight = -8, OffsetBottom = -8 };
        AddChild(actions);
        actions.AddChild(_status);
        actions.AddChild(_info);
        var buttons = new HBoxContainer();
        actions.AddChild(buttons);
        foreach (var button in new[] { _wait, _defend, _retreat, _quick })
        {
            buttons.AddChild(button);
        }

        _wait.Pressed += () => Act(player => new BattleWaitCommand(player));
        _defend.Pressed += () => Act(player => new BattleDefendCommand(player));
        _retreat.Pressed += () => Act(player => new BattleRetreatCommand(player));
        _quick.Pressed += QuickCombat;

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);
        center.AddChild(_result);
        var resultBox = new VBoxContainer();
        _result.AddChild(resultBox);
        resultBox.AddChild(_resultText);
        var close = new Button { Text = "Return to the map (Enter)" };
        close.Pressed += () => _view.Close();
        resultBox.AddChild(close);

        view.Opened += () =>
        {
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
                     $"{(unit.IsDefending ? "  defending" : string.Empty)}{capabilities}";

        static string Signed(int value) => value > 0 ? $"+{value}" : value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Report(CommandResult result) => _status.Text = result.IsAccepted ? string.Empty : $"Refused: {result.Error}";

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
        _retreat.Disabled = commander is null || battle?.ActiveUnit is not { } active || battle.Participant(active.Side).Hero is null;
        _quick.Disabled = _view.Result is not null || battle is null ||
                          (_session.Battles.Commander(BattleSide.Attacker) ?? _session.Battles.Commander(BattleSide.Defender)) is null;

        if (battle is null || _view.IsBusy)
        {
            return;
        }

        _title.Text = $"{_view.SideName(BattleSide.Attacker)}  vs  {_view.SideName(BattleSide.Defender)}   ·   round {battle.Round}";
        _status.Text = commander is not null && battle.ActiveUnit is { } acting
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
            ended.Reason == BattleEndReason.Retreated
                ? $"{_view.SideName(ended.Winner.Opponent())} retreated: {_view.SideName(ended.Winner)} wins."
                : $"{_view.SideName(ended.Winner)} wins the battle.",
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
