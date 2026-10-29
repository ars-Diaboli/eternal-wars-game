using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Battle;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Battle;

/// <summary>
/// Auto combat (ADR-090, CMB-10). When a battle against a computer player or neutrals begins and the human player's setting is "ask", the
/// panel asks whether to fight by hand or see auto combat's outcome; with "always" it shows the outcome at once. The outcome is a preview
/// (<see cref="BattleService.Preview"/>) that leaves the game untouched: Accept runs quick combat, which ends the battle exactly so, and
/// returns to the map without a second result screen; Fight fights the battle by hand from where it stands. A battle loaded from a save is
/// not asked about. The panel covers the screen while it is open, so nothing else takes the clicks; over the battle scene it waits until the
/// scene has played what happened so far (a command given while it animates would be lost).
/// </summary>
public partial class AutoCombatPanel : CenterContainer
{
    private readonly Label _title = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Label _text = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(460, 0) };
    private readonly HBoxContainer _buttons = new() { Alignment = BoxContainer.AlignmentMode.Center };
    private GameSession _session = null!;
    private BattleState? _seen;
    private PlayerId? _waiting;
    private bool _accepted;
    private BattleView3D _view = null!;

    public void Bind(GameSession session, BattleView3D view)
    {
        _session = session;
        _view = view;
        Name = "AutoCombat";
        Visible = false;
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Stop;

        var panel = new PanelContainer();
        AddChild(panel);
        var rows = new VBoxContainer();
        rows.AddThemeConstantOverride("separation", 10);
        panel.AddChild(rows);
        rows.AddChild(_title);
        rows.AddChild(_text);
        rows.AddChild(_buttons);

        // A battle under way when the game is loaded is not asked about.
        _seen = session.State.Battle;
        this.SubscribeWhileInTree<CommandExecuted>(session.Events, _ => Check());
        view.Idle += () =>
        {
            if (_waiting is { } player)
            {
                _waiting = null;
                Offer(player);
            }
        };
        view.Finished += _ =>
        {
            if (_accepted)
            {
                // The outcome was seen before it was accepted: straight back to the map.
                _accepted = false;
                Callable.From(view.Close).CallDeferred();
            }
        };
    }

    /// <summary>A battle that has just begun gets the player's auto combat setting.</summary>
    private void Check()
    {
        var battle = _session.State.Battle;
        if (battle is null || ReferenceEquals(battle, _seen))
        {
            return;
        }

        _seen = battle;
        if (_session.Battles.AutoCombatPlayer() is not { } player)
        {
            return;
        }

        if (_view.IsOpen && _view.IsBusy)
        {
            _waiting = player;
            return;
        }

        Offer(player);
    }

    private void Offer(PlayerId player)
    {
        if (_session.Battles.AutoCombatPlayer() != player)
        {
            return; // the battle ended while the scene played it
        }

        switch (_session.State.GetPlayer(player).AutoCombat)
        {
            case AutoCombatMode.Ask:
                Ask(player);
                break;
            case AutoCombatMode.Always:
                ShowPreview(player);
                break;
        }
    }

    private void Ask(PlayerId player)
    {
        _title.Text = "Auto combat";
        _text.Text = $"{Names()}.\n\nFight by hand, or see how auto combat would end it?";
        SetButtons(("Auto combat", () => ShowPreview(player)), ("Fight", Hide));
        Visible = true;
    }

    private void ShowPreview(PlayerId player)
    {
        var preview = _session.Battles.Preview();
        var battle = _session.State.Battle!;
        var lines = new List<string>
        {
            Names(),
            string.Empty,
            preview.Reason switch
            {
                BattleEndReason.Retreated => $"{SideName(battle, preview.Winner.Opponent())} would retreat: {SideName(battle, preview.Winner)} wins.",
                BattleEndReason.Surrendered => $"{SideName(battle, preview.Winner.Opponent())} would surrender: {SideName(battle, preview.Winner)} wins.",
                _ => $"{SideName(battle, preview.Winner)} would win the battle.",
            },
            string.Empty,
        };
        foreach (var side in new[] { BattleSide.Attacker, BattleSide.Defender })
        {
            var lost = preview.Casualties.Where(c => c.Side == side).Select(c => $"{c.Lost} {CreatureName(c.Creature)}").ToList();
            lines.Add($"{SideName(battle, side)} would lose: {(lost.Count > 0 ? string.Join(", ", lost) : "nothing")}");
        }

        if (preview.Experience > 0)
        {
            lines.Add($"{SideName(battle, preview.Winner)} would gain {preview.Experience} experience.");
        }

        if (preview.Raised.Count > 0)
        {
            lines.Add($"Necromancy would raise {string.Join(", ", preview.Raised.Select(r => $"{r.Count} {CreatureName(r.Creature)}"))}.");
        }

        if (preview.Artifacts.Count > 0)
        {
            var names = preview.Artifacts.Select(a => _session.State.Artifacts.TryGet(a, out var artifact) ? artifact.Name : a);
            lines.Add($"Artifacts taken from the defeated hero: {string.Join(", ", names)}.");
        }

        _title.Text = "Auto combat: the outcome";
        _text.Text = string.Join('\n', lines);
        SetButtons(("Accept", () => Accept(player)), ("Fight", Hide));
        Visible = true;
    }

    private void Accept(PlayerId player)
    {
        Hide();
        _accepted = true;
        if (!_session.Execute(new AutoResolveBattleCommand(player)).IsAccepted)
        {
            _accepted = false;
        }
    }

    private void SetButtons(params (string Text, Action Pressed)[] buttons)
    {
        foreach (var child in _buttons.GetChildren())
        {
            child.QueueFree();
        }

        foreach (var (text, pressed) in buttons)
        {
            var button = new Button { Text = text };
            button.Pressed += pressed;
            _buttons.AddChild(button);
        }
    }

    private string Names()
    {
        var battle = _session.State.Battle!;
        return $"{SideName(battle, BattleSide.Attacker)} against {SideName(battle, BattleSide.Defender)}";
    }

    private string SideName(BattleState battle, BattleSide side)
    {
        var participant = battle.Participant(side);
        var state = _session.State;
        return participant.Hero is { } hero ? state.GetHero(hero).Name
            : participant.Guard is { } guard && state.FindObject(guard) is Core.Domain.Objects.MonsterGuard monster ? $"Neutral {CreatureName(monster.Creature)}"
            : participant.Guard is { } other && state.FindObject(other) is { } held ? held.Definition.Name
            : participant.Owner is { } owner ? state.GetPlayer(owner).Name
            : "The neutrals";
    }

    private string CreatureName(string id) =>
        _session.State.Creatures.TryGet(id, out var creature) || WarMachines.TryGet(id, out creature) ? creature.Name : id;
}
