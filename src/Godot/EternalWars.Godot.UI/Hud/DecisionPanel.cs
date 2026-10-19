using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Placeholder prompt for pending decisions: chest reward, guard offers, the battle
/// width and quick combat. Answers are commands; the panel hides itself when the decision is resolved.
/// </summary>
public partial class DecisionPanel : PanelContainer
{
    private readonly Label _prompt = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart };
    private readonly HBoxContainer _buttons = new();
    private GameSession _session = null!;

    public void Bind(GameSession session)
    {
        _session = session;
        var box = new VBoxContainer();
        AddChild(box);
        box.AddChild(_prompt);
        box.AddChild(_buttons);
        Visible = false;

        this.SubscribeWhileInTree<DecisionRequested>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<BattleStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<BattleRoundStarted>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DecisionResolved>(session.Events, _ => Refresh());
        Refresh(); // a loaded game may already be waiting for an answer
    }

    private void Refresh()
    {
        foreach (var child in _buttons.GetChildren())
        {
            child.QueueFree();
        }

        var state = _session.State;
        var player = state.CurrentPlayer.Id;
        Visible = state.PendingDecision is not null;
        switch (state.PendingDecision)
        {
            case TreasureChestDecision chest:
                _prompt.Text = $"Treasure chest: take {chest.Gold} gold or {chest.Experience} experience?";
                Button("Gold", new ResolveTreasureChestCommand(player, TreasureChestReward.Gold));
                Button("Experience", new ResolveTreasureChestCommand(player, TreasureChestReward.Experience));
                break;
            case ResourceChoiceDecision choice:
                _prompt.Text = $"{state.GetObject(choice.Provider).Definition.Name}: take one.";
                for (var i = 0; i < choice.Options.Count; i++)
                {
                    Button(Core.Domain.Objects.ResourceProviderRules.Describe(choice.Options[i]), new ChooseResourceRewardCommand(player, i));
                }

                break;
            case TrainingDecision training:
                var site = (Core.Domain.Objects.TrainingSite)state.GetObject(training.Site);
                _prompt.Text = $"{site.Definition.Name}: {state.GetHero(training.Hero).Name} may train once here.";
                foreach (var option in training.Options)
                {
                    var price = Core.Domain.Objects.TrainingRules.Price(site.Rule, option);
                    var label = Core.Domain.Objects.TrainingRules.Stat(option) is { } trained
                        ? $"+{Core.Domain.Objects.TrainingRules.StatBonus} {StatName(trained)}"
                        : $"Pay {Core.Domain.Objects.ResourceProviderRules.Describe(price)}";
                    Button(label, new ChooseTrainingCommand(player, option));
                }

                if (training.CanDecline)
                {
                    Button("Leave", new ChooseTrainingCommand(player, null));
                }

                break;
            case HeroExchangeDecision:
                Visible = false; // ExchangePanel handles it
                break;
            case GuardEncounterDecision guard:
                var creature = state.Creatures.TryGet(guard.Creature, out var definition) ? definition.Name : guard.Creature;
                _prompt.Text = guard.Offer == GuardOffer.Join
                    ? $"{guard.Count} × {creature} offer to join you."
                    : $"{guard.Count} × {creature} try to flee.";
                Button(guard.Offer == GuardOffer.Join ? "Accept" : "Let them go", new RespondToGuardCommand(player, true));
                Button("Fight", new RespondToGuardCommand(player, false));
                break;
            case BattleDecision when state.Battle is { } battle:
                ShowBattle(battle);
                break;
            case LevelUpDecision levelUp:
                var hero = state.GetHero(levelUp.Hero);
                _prompt.Text = $"{hero.Name} reached level {levelUp.Level}: +1 {StatName(levelUp.Stat)}. Choose a skill:";
                foreach (var skill in levelUp.Options)
                {
                    var skillDefinition = BuiltInSkills.Get(skill);
                    Button($"{skillDefinition.Name} → {hero.Skills.Level(skill) + 1}", new ChooseSkillCommand(player, skill));
                }

                break;
        }
    }

    private static string StatName(PrimaryStat stat) => stat switch
    {
        PrimaryStat.SpellPower => "Spell Power",
        _ => stat.ToString(),
    };

    private void ShowBattle(BattleState battle)
    {
        var commander = _session.Battles.Commander(BattleSide.Attacker) ?? _session.Battles.Commander(BattleSide.Defender);
        if (battle.Phase == BattlePhase.ChoosingWidth && battle.WidthChooser is { } side && _session.Battles.Commander(side) is { } chooser)
        {
            _prompt.Text = "Your Tactics lets you choose the battlefield width:";
            foreach (var width in battle.WidthOptions)
            {
                Button(width.ToString(), new ChooseBattleWidthCommand(chooser, width));
            }
        }
        else
        {
            _prompt.Text = $"Battle, round {battle.Round}.";
        }

        if (commander is { } player)
        {
            Button("Quick combat", new AutoResolveBattleCommand(player));
        }
    }

    private void Button(string text, GameCommand command)
    {
        var button = new Button { Text = text };
        button.Pressed += () => _session.Execute(command);
        _buttons.AddChild(button);
    }
}
