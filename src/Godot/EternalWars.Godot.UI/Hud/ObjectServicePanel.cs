using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// What an own hero can buy at a map building it stands at (ADR-068): a Hill Fort's upgrades of tier 1–3 stacks for 150 % of the upgrade price,
/// or a War Machine Factory's machines at the blacksmith's prices. Closes with Escape or "Close", and when the hero leaves.
/// </summary>
public partial class ObjectServicePanel : PanelContainer
{
    private readonly Label _title = new();
    private readonly VBoxContainer _rows = new();
    private readonly Label _status = new() { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(380, 0) };
    private GameSession _session = null!;
    private MapObjectId? _object;

    public void Bind(GameSession session)
    {
        _session = session;
        SetAnchorsAndOffsetsPreset(LayoutPreset.Center);
        var box = new VBoxContainer();
        AddChild(box);
        var header = new HBoxContainer();
        box.AddChild(header);
        header.AddChild(_title);
        var close = new Button { Text = "Close" };
        close.Pressed += Close;
        header.AddChild(close);
        box.AddChild(_rows);
        box.AddChild(_status);
        Visible = false;

        this.SubscribeWhileInTree<ResourcesChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<ArmyChanged>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<WarMachineBought>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroMoved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh());
    }

    /// <summary>Whether the panel serves this kind of object.</summary>
    public static bool Serves(MapObject mapObject) => mapObject is HillFort or WarMachineFactory;

    public void Open(MapObjectId id)
    {
        _object = id;
        _status.Text = "";
        Refresh();
    }

    public void Close()
    {
        _object = null;
        Visible = false;
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Close();
            GetViewport().SetInputAsHandled();
        }
    }

    private void Refresh()
    {
        var state = _session.State;
        if (_object is not { } id || state.FindObject(id) is not { } mapObject || !Serves(mapObject)
            || state.HeroAt(mapObject.VisitPosition) is not { } hero || hero.Owner != state.CurrentPlayer.Id || state.PendingDecision is not null)
        {
            if (_object is not null)
            {
                Close();
            }

            return;
        }

        Visible = true;
        _title.Text = $"{mapObject.Definition.Name}: {hero.Name}";
        foreach (var child in _rows.GetChildren())
        {
            child.QueueFree();
        }

        var player = state.CurrentPlayer;
        if (mapObject is HillFort)
        {
            _rows.AddChild(new Label { Text = "Upgrades stacks of tiers 1–3 for 150 % of the upgrade price." });
            foreach (var (slot, stack) in hero.Army.Stacks)
            {
                var creature = state.Creatures.Get(stack.Creature);
                if (HillFortRules.UpgradeOf(state.Creatures, stack.Creature) is not { } upgraded)
                {
                    _rows.AddChild(new Label { Text = $"{stack.Count} × {creature.Name}: cannot be upgraded here" });
                    continue;
                }

                var cost = HillFortRules.Cost(state.Creatures, stack);
                var button = new Button
                {
                    Text = $"{stack.Count} × {creature.Name} → {upgraded.Name} for {ResourceProviderRules.Describe(cost)}",
                    Disabled = !player.Resources.CanAfford(cost),
                };
                var at = slot;
                button.Pressed += () => Run(new UpgradeAtHillFortCommand(player.Id, id, at));
                _rows.AddChild(button);
            }
        }
        else
        {
            _rows.AddChild(new Label { Text = "War machines at the blacksmith's prices." });
            foreach (var slot in Enum.GetValues<WarMachineSlot>())
            {
                var name = TownRules.WarMachineName(state, hero, slot);
                var cost = WarMachines.Cost(slot);
                var owned = hero.WarMachines.Contains(slot);
                var button = new Button
                {
                    Text = owned ? $"{name} (owned)" : $"Buy {name} for {ResourceProviderRules.Describe(cost)}",
                    Disabled = owned || !player.Resources.CanAfford(cost),
                };
                var which = slot;
                button.Pressed += () => Run(new BuyWarMachineAtFactoryCommand(player.Id, id, which));
                _rows.AddChild(button);
            }
        }
    }

    private void Run(GameCommand command)
    {
        var result = _session.Execute(command);
        _status.Text = result.IsAccepted ? "" : $"Refused: {result.Error}";
    }
}
