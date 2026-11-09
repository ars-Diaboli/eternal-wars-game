using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// Exchange screen for a <see cref="HeroExchangeDecision"/> (ADR-153): the two heroes side by side, each with its seven army slots, war machines,
/// paper doll and backpack. Click a stack, then a slot (either hero) to move it there; with "Split" only half moves (<see cref="MoveStackCommand"/>).
/// Click an artifact, then where it goes: a slot of the other hero's paper doll puts it on, its backpack carries it (<see cref="GiveArtifactCommand"/>);
/// the own paper doll or backpack puts it on or takes it off. A war machine button gives it to the other hero (<see cref="GiveWarMachineCommand"/>).
/// Refusals show below; Close (or Escape) resolves the decision. It covers its parent.
/// </summary>
public partial class ExchangePanel : PanelContainer
{
    private readonly HBoxContainer _columns = new() { SizeFlagsHorizontal = SizeFlags.ExpandFill };
    private readonly Label _hint = new();
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
    private readonly CheckBox _split = new() { Text = "Split (move half)" };
    private GameSession _session = null!;
    private (HeroId Hero, int Slot)? _picked;
    private PickedArtifact? _artifact;

    /// <summary>The artifact picked to move: whose, which and whether it is the worn one.</summary>
    private sealed record PickedArtifact(HeroId Hero, string Id, bool Worn);

    public void Bind(GameSession session)
    {
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        this.UseOpaqueBackground();
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        AddChild(scroll);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        scroll.AddChild(box);

        var header = new HBoxContainer();
        box.AddChild(header);
        header.AddChild(new Label { Text = "Exchange" });
        var close = new Button { Text = "Close" }.WithIcon(UiIcon.Close);
        close.Pressed += Close;
        header.AddChild(close);
        header.AddChild(_split);
        box.AddChild(_hint);
        box.AddChild(_columns);
        box.AddChild(_status);
        Visible = false;

        this.SubscribeWhileInTree<DecisionRequested>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DecisionResolved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArmyChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<WarMachineGiven>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactGiven>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactEquipped>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArtifactUnequipped>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<RelicAssembled>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<RelicTakenApart>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<RelicReady>(session.Events, e => _status.Text = $"All pieces of {_session.State.Artifacts.Get(e.Relic).Name} are worn: press Assemble to join them.");
        Refresh();
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            if (_picked is not null || _artifact is not null)
            {
                _picked = null;
                _artifact = null;
                Refresh();
            }
            else
            {
                Close();
            }

            GetViewport().SetInputAsHandled();
        }
    }

    private void Close() => Run(new CompleteExchangeCommand(_session.State.CurrentPlayer.Id));

    private void Refresh()
    {
        Clear(_columns);
        if (_session.State.PendingDecision is not HeroExchangeDecision exchange)
        {
            Visible = false;
            _picked = null;
            _artifact = null;
            _status.Text = "";
            return;
        }

        Visible = true;
        _hint.Text = _artifact is { } a
            ? $"{_session.State.Artifacts.Get(a.Id).Name}: click a paper-doll slot to put it on, or a backpack to carry it (Esc cancels)."
            : _picked is not null
                ? "Click the slot the stack goes to (Esc cancels)."
                : "Click a stack or an artifact, then where it goes.";
        foreach (var (heroId, otherId) in new[] { (exchange.Hero, exchange.Other), (exchange.Other, exchange.Hero) })
        {
            _columns.AddChild(Column(_session.State.GetHero(heroId), otherId));
        }
    }

    private VBoxContainer Column(Hero hero, HeroId other)
    {
        var state = _session.State;
        var column = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(560, 0) };
        column.AddChild(new Label { Text = $"{hero.Name}, level {hero.Level} {hero.Class}" });
        column.AddChild(new Label
        {
            Text = $"Attack {hero.TotalAttack}   Defense {hero.TotalDefense}   Spell Power {hero.TotalSpellPower}   Knowledge {hero.TotalKnowledge}",
        });

        column.AddChild(new Label { Text = "Army" });
        var army = new HBoxContainer();
        column.AddChild(army);
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var stack = hero.Army[slot];
            var button = new Button
            {
                Text = stack is null ? "—" : $"{stack.Count}\n{state.Creatures.Get(stack.Creature).Name}",
                CustomMinimumSize = new Vector2(78, 48),
                ClipText = true,
                ToggleMode = true,
                ButtonPressed = _picked == (hero.Id, slot),
            };
            var target = (hero.Id, slot);
            button.Pressed += () => ClickStack(target);
            army.AddChild(button);
        }

        var machines = new HFlowContainer();
        column.AddChild(machines);
        machines.AddChild(new Label { Text = hero.WarMachines.Count == 0 ? "War machines: none" : "War machines:" });
        foreach (var machine in hero.WarMachines.Order())
        {
            var give = new Button { Text = $"{TownRules.WarMachineName(state, hero, machine)} →", TooltipText = "Give to the other hero" };
            give.Pressed += () => Run(new GiveWarMachineCommand(state.CurrentPlayer.Id, hero.Id, machine, other));
            machines.AddChild(give);
        }

        column.AddChild(new Label { Text = "Worn artifacts" });
        var doll = new GridContainer { Columns = 3 };
        column.AddChild(doll);
        foreach (var slot in ArtifactSlots.All)
        {
            var worn = hero.Artifacts.Worn(slot);
            var blocked = hero.Artifacts.AllBlocked.Where(b => b.Slot == slot).Select(b => state.Artifacts.Get(b.Relic).Name).ToList();
            for (var i = 0; i < ArtifactSlots.Capacity(slot); i++)
            {
                var artifact = i < worn.Count ? state.Artifacts.Get(worn[i]) : null;
                var blocker = artifact is null && i - worn.Count < blocked.Count ? blocked[i - worn.Count] : null;
                var button = new Button
                {
                    Text = $"{slot}: {artifact?.Name ?? (blocker is null ? "—" : $"(held by {blocker})")}",
                    TooltipText = artifact is null ? "" : ArtifactText(artifact),
                    CustomMinimumSize = new Vector2(184, 0),
                    ClipText = true,
                    ToggleMode = true,
                    ButtonPressed = artifact is not null && _artifact == new PickedArtifact(hero.Id, artifact.Id, true),
                    Disabled = artifact is null && _artifact is null,
                };
                var id = artifact?.Id;
                button.Pressed += () => ClickDoll(hero.Id, id);
                doll.AddChild(button);
            }
        }

        var relics = new HFlowContainer();
        column.AddChild(relics);
        foreach (var relic in ArtifactService.Assemblable(state.Artifacts, hero))
        {
            var id = relic.Id;
            var join = new Button { Text = $"Assemble {relic.Name}", TooltipText = ArtifactText(relic) };
            join.Pressed += () => Run(new AssembleRelicCommand(state.CurrentPlayer.Id, hero.Id, id));
            relics.AddChild(join);
        }

        column.AddChild(new Label { Text = $"Backpack ({hero.Artifacts.Backpack.Count}/{ArtifactSlots.BackpackSize})" });
        var backpack = new HFlowContainer();
        column.AddChild(backpack);
        var into = new Button { Text = "Into the backpack", Disabled = _artifact is null, TooltipText = "Carry the picked artifact" };
        into.Pressed += () => ClickBackpack(hero.Id, null);
        backpack.AddChild(into);
        foreach (var id in hero.Artifacts.Backpack.Distinct())
        {
            var artifact = state.Artifacts.Get(id);
            var count = hero.Artifacts.Backpack.Count(b => b == id);
            var button = new Button
            {
                Text = $"{artifact.Name} ({artifact.Slot})" + (count > 1 ? $" ×{count}" : ""),
                TooltipText = ArtifactText(artifact),
                ToggleMode = true,
                ButtonPressed = _artifact == new PickedArtifact(hero.Id, id, false),
            };
            var artifactId = id;
            button.Pressed += () => ClickBackpack(hero.Id, artifactId);
            backpack.AddChild(button);
        }

        return column;
    }

    private void ClickStack((HeroId Hero, int Slot) target)
    {
        _artifact = null;
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
            Run(new MoveStackCommand(_session.State.CurrentPlayer.Id, from.Hero, from.Slot, target.Hero, target.Slot, count));
        }

        Refresh();
    }

    /// <summary>A paper-doll slot of <paramref name="hero"/>, wearing <paramref name="worn"/> or empty.</summary>
    private void ClickDoll(HeroId hero, string? worn)
    {
        _picked = null;
        var player = _session.State.CurrentPlayer.Id;
        switch (_artifact)
        {
            case null when worn is not null:
                _artifact = new PickedArtifact(hero, worn, true);
                break;
            case { } picked when picked.Hero != hero:
                _artifact = null;
                Run(new GiveArtifactCommand(player, picked.Hero, picked.Id, hero, picked.Worn, Wear: true));
                break;
            case { Worn: false } picked:
                _artifact = null;
                Run(new EquipArtifactCommand(player, hero, picked.Id));
                break;
            case { } picked when worn is not null && worn != picked.Id:
                _artifact = new PickedArtifact(hero, worn, true);
                break;
            default:
                _artifact = null;
                break;
        }

        Refresh();
    }

    /// <summary>The backpack of <paramref name="hero"/>: one of its artifacts (<paramref name="carried"/>) or the "Into the backpack" button.</summary>
    private void ClickBackpack(HeroId hero, string? carried)
    {
        _picked = null;
        var player = _session.State.CurrentPlayer.Id;
        switch (_artifact)
        {
            case null when carried is not null:
                _artifact = new PickedArtifact(hero, carried, false);
                break;
            case { } picked when picked.Hero != hero:
                _artifact = null;
                Run(new GiveArtifactCommand(player, picked.Hero, picked.Id, hero, picked.Worn));
                break;
            case { Worn: true } picked:
                _artifact = null;
                Run(new UnequipArtifactCommand(player, hero, picked.Id));
                break;
            case { } picked when carried is not null && carried != picked.Id:
                _artifact = new PickedArtifact(hero, carried, false);
                break;
            default:
                _artifact = null;
                break;
        }

        Refresh();
    }

    private string ArtifactText(ArtifactDefinition artifact) =>
        $"{artifact.Name} ({(artifact.IsAssembled ? "assembled relic" : artifact.Tier.ToString())}, {artifact.Slot})\n{ArtifactBonuses.Of(_session.State.Artifacts.EffectsOf(artifact)).Describe(_session.State.Spells)}"
        + (_session.State.Artifacts.RelicOf(artifact.Id) is { } relic ? $"\nA piece of {_session.State.Artifacts.Get(relic).Name}" : "")
        + (artifact.Notes.Length > 0 ? $"\n{artifact.Notes}" : "");

    private void Run(GameCommand command)
    {
        var result = _session.Execute(command);
        _status.Text = result.IsAccepted ? "" : $"Refused: {result.Error}";
    }

    private static void Clear(Node node)
    {
        // Freed at the end of the frame: the button whose signal is running stays valid.
        foreach (var child in node.GetChildren())
        {
            child.QueueFree();
        }
    }
}
