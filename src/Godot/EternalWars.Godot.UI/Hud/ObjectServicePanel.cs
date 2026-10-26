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
/// a War Machine Factory's machines at the blacksmith's prices, an artifact merchant's stock, a refugee camp's creatures, or what it may
/// sacrifice at an altar (ADR-079). Closes with Escape or "Close", and when the hero leaves.
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
        this.SubscribeWhileInTree<ArtifactBought>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<SacrificeMade>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<DecisionResolved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<HeroMoved>(session.Events, _ => Refresh());
        this.SubscribeWhileInTree<TurnStarted>(session.Events, _ => Refresh());
    }

    /// <summary>Whether the panel serves this kind of object.</summary>
    public static bool Serves(MapObject mapObject) => mapObject is HillFort or WarMachineFactory or ArtifactMerchant or RefugeeCamp or SacrificialAltar;

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
        switch (mapObject)
        {
            case ArtifactMerchant merchant:
                ShowMerchant(merchant, hero, player);
                return;
            case RefugeeCamp camp:
                ShowCamp(camp, hero, player);
                return;
            case SacrificialAltar altar:
                ShowAltar(altar, hero);
                return;
        }

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

    private void ShowMerchant(ArtifactMerchant merchant, Core.Domain.Heroes.Hero hero, Core.Domain.Players.Player player)
    {
        var state = _session.State;
        _rows.AddChild(new Label { Text = "Artifacts for twice their worth; new stock on the first day of every month." });
        for (var slot = 0; slot < merchant.Stock.Count; slot++)
        {
            if (merchant.Stock[slot] is not { } id)
            {
                _rows.AddChild(new Label { Text = "(sold)" });
                continue;
            }

            var artifact = state.Artifacts.Get(id);
            var price = ArtifactMerchantRules.Price(artifact);
            var button = new Button
            {
                Text = $"{artifact.Name} ({artifact.Tier}) for {ResourceProviderRules.Describe(price)}",
                Disabled = !player.Resources.CanAfford(price) || hero.Artifacts.BackpackFull,
            };
            var at = slot;
            button.Pressed += () => Run(new BuyArtifactCommand(player.Id, merchant.Id, at));
            _rows.AddChild(button);
        }
    }

    private void ShowCamp(RefugeeCamp camp, Core.Domain.Heroes.Hero hero, Core.Domain.Players.Player player)
    {
        var state = _session.State;
        if (camp.Creature is not { } id || camp.Available == 0)
        {
            _rows.AddChild(new Label { Text = "No refugees this week." });
            return;
        }

        var creature = state.Creatures.Get(id);
        var count = TownRules.MaxAffordable(player.Resources, creature.Cost, camp.Available);
        _rows.AddChild(new Label { Text = $"{camp.Available} {creature.Name} wait here, {ResourceProviderRules.Describe(creature.Cost)} each." });
        var button = new Button { Text = $"Hire {count}", Disabled = count == 0 || !hero.Army.CanAccept(id) };
        button.Pressed += () => Run(new RecruitAtRefugeeCampCommand(player.Id, camp.Id, count));
        _rows.AddChild(button);
    }

    private void ShowAltar(SacrificialAltar altar, Core.Domain.Heroes.Hero hero)
    {
        var state = _session.State;
        var player = hero.Owner;
        var kind = SacrificeRules.For(Core.Domain.Heroes.ProgressionService.TownOf(state, hero));
        if (kind == SacrificeKind.None)
        {
            _rows.AddChild(new Label { Text = $"{hero.Name} has nothing the altar accepts." });
            return;
        }

        if (kind.HasFlag(SacrificeKind.Creatures))
        {
            foreach (var (slot, stack) in hero.Army.Stacks)
            {
                var creature = state.Creatures.Get(stack.Creature);
                var button = new Button { Text = $"Sacrifice {stack.Count} × {creature.Name}: {SacrificeRules.Experience(creature, stack.Count)} experience" };
                var at = slot;
                var count = stack.Count;
                button.Pressed += () => Run(new SacrificeCreaturesCommand(player, altar.Id, at, count));
                _rows.AddChild(button);
            }
        }

        if (kind.HasFlag(SacrificeKind.Artifacts))
        {
            foreach (var id in hero.Artifacts.Backpack.Concat(hero.Artifacts.AllWorn.Select(w => w.Id)).ToList())
            {
                var artifact = state.Artifacts.Get(id);
                var button = new Button { Text = $"Sacrifice {artifact.Name}: {SacrificeRules.Experience(artifact)} experience" };
                button.Pressed += () => Run(new SacrificeArtifactCommand(player, altar.Id, id));
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
