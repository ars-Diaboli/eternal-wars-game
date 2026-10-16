using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Exchange screen for a <see cref="HeroExchangeDecision"/>: both heroes' seven slots. Click a stack, then a
/// slot (either hero) to move it there; with "Split" only half moves. Every move is a <see cref="MoveStackCommand"/>;
/// refusals (e.g. the last stack) are shown. Close resolves the decision.
/// </summary>
public partial class ExchangePanel : PanelContainer
{
    private readonly VBoxContainer _rows = new();
    private readonly Label _status = new();
    private readonly CheckBox _split = new() { Text = "Split (move half)" };
    private GameSession _session = null!;
    private (HeroId Hero, int Slot)? _picked;

    public void Bind(GameSession session)
    {
        _session = session;
        var box = new VBoxContainer();
        AddChild(box);
        box.AddChild(new Label { Text = "Exchange" });
        box.AddChild(_rows);
        var bottom = new HBoxContainer();
        box.AddChild(bottom);
        bottom.AddChild(_split);
        var close = new Button { Text = "Close" };
        close.Pressed += () => _session.Execute(new CompleteExchangeCommand(_session.State.CurrentPlayer.Id));
        bottom.AddChild(close);
        box.AddChild(_status);
        Visible = false;

        this.SubscribeWhileInTree<DecisionRequested>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DecisionResolved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArmyChanged>(session.Events, _ => Refresh());
        Refresh();
    }

    private void Refresh()
    {
        foreach (var child in _rows.GetChildren())
        {
            child.QueueFree();
        }

        if (_session.State.PendingDecision is not HeroExchangeDecision exchange)
        {
            Visible = false;
            _picked = null;
            return;
        }

        Visible = true;
        foreach (var heroId in new[] { exchange.Hero, exchange.Other })
        {
            var hero = _session.State.GetHero(heroId);
            var row = new HBoxContainer();
            row.AddChild(new Label { Text = hero.Name, CustomMinimumSize = new Vector2(110, 0) });
            for (var slot = 0; slot < Army.SlotCount; slot++)
            {
                var stack = hero.Army[slot];
                var button = new Button
                {
                    Text = stack is null ? "—" : $"{stack.Count}\n{stack.Creature.Split('.')[^1]}",
                    CustomMinimumSize = new Vector2(86, 48),
                    ToggleMode = true,
                    ButtonPressed = _picked == (heroId, slot),
                };
                var target = (heroId, slot);
                button.Pressed += () => Click(target);
                row.AddChild(button);
            }

            _rows.AddChild(row);
        }
    }

    private void Click((HeroId Hero, int Slot) target)
    {
        if (_picked is not { } from)
        {
            if (_session.State.GetHero(target.Hero).Army[target.Slot] is not null)
            {
                _picked = target;
            }

            Refresh();
            return;
        }

        _picked = null;
        if (from != target)
        {
            var stack = _session.State.GetHero(from.Hero).Army[from.Slot]!;
            int? count = _split.ButtonPressed && stack.Count > 1 ? stack.Count / 2 : null;
            var result = _session.Execute(new MoveStackCommand(_session.State.CurrentPlayer.Id, from.Hero, from.Slot, target.Hero, target.Slot, count));
            _status.Text = result.IsAccepted ? "" : result.Error ?? "";
        }

        Refresh();
    }
}
