using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// The caravan (ADR-054): hire creatures from owned map dwellings within its reach (ADR-073), fetch what waits at own resource providers
/// (ADR-079), send the picked garrison stack to another own town, the queue of caravans coming here and those sent from here.
/// </summary>
internal sealed class CaravanSection(TownPanel panel) : TownSection(panel)
{
    public VBoxContainer Rows { get; } = new();

    protected override Node Anchor => Rows;

    public override void Show(Town town)
    {
        var state = State;
        Rows.ClearChildren();
        if (town.Level(BuiltInBuildings.Caravan) < 1)
        {
            return;
        }

        var player = state.CurrentPlayer.Id;
        Rows.AddChild(new Label
        {
            Text = $"Caravan: reaches {CaravanRules.MaxReach} movement points, {CaravanRules.DailyMovement} a day; "
                   + $"{CaravanRules.Queue(state, town).Count} of {CaravanRules.Limit(state, town)} caravans coming",
        });
        foreach (var dwelling in CaravanRules.Dwellings(state, town).Where(d => d.Available > 0))
        {
            var creature = state.Creatures.Get(dwelling.Creature);
            var available = dwelling.Available!.Value;
            var count = TownRules.MaxAffordable(state.CurrentPlayer.Resources, TownRules.CreatureCost(state, town, creature), available);
            var refusal = CaravanRules.CheckOrder(state, town, dwelling, Math.Max(1, count));
            var route = CaravanRules.RouteBetween(state, dwelling, town, out _);
            var row = new HBoxContainer();
            row.AddChild(new Label
            {
                Text = $"{creature.Name} dwelling at {dwelling.Position.X},{dwelling.Position.Y}: {available} waiting, {(route is null ? "no route" : $"{route.Days} day(s) away")}",
                CustomMinimumSize = new Vector2(UiTheme.TownRowWidth, 0),
            });
            var hire = new Button
            {
                Text = $"Hire {count}",
                Disabled = count == 0 || refusal is not null,
                TooltipText = refusal ?? "",
            }.WithCost(TownRules.CreatureCost(state, town, creature), " each");
            var dwellingId = dwelling.Id;
            hire.Pressed += () => Run(new OrderCaravanCommand(player, town.Id, dwellingId, count));
            row.AddChild(hire);
            Rows.AddChild(row);
        }

        foreach (var provider in CaravanRules.Providers(state, town).Where(p => !p.Stored.IsEmpty))
        {
            var refusal = CaravanRules.CheckFetch(state, town, provider);
            var route = CaravanRules.RouteBetween(state, provider, town, out _);
            var row = new HBoxContainer();
            row.AddChild(new Label
            {
                Text = $"{provider.Definition.Name} at {provider.Position.X},{provider.Position.Y}: {ResourceProviderRules.Describe(provider.Stored)}, "
                       + (route is null ? "no route" : $"{route.Days} day(s) away"),
                CustomMinimumSize = new Vector2(UiTheme.TownRowWidth, 0),
            });
            var fetch = new Button
            {
                Text = "Fetch",
                Disabled = refusal is not null,
                TooltipText = refusal ?? "",
            }.WithCost(CaravanRules.ResourceCaravanCost);
            var providerId = provider.Id;
            fetch.Pressed += () => Run(new FetchResourcesCommand(player, town.Id, providerId));
            row.AddChild(fetch);
            Rows.AddChild(row);
        }

        if (Panel.Picked is { Army: TownArmy.Garrison } picked && town.Garrison[picked.Slot] is { } stack)
        {
            int? count = Panel.Split && stack.Count > 1 ? stack.Count / 2 : null;
            foreach (var other in state.ObjectsOf<Town>().Where(t => t.Owner == player && t != town).OrderBy(t => t.Id.Value))
            {
                var refusal = CaravanRules.CheckSend(state, town, picked.Slot, other, count);
                var send = new Button { Text = $"Send {count ?? stack.Count} {state.Creatures.Get(stack.Creature).Name} to {other.Name}", Disabled = refusal is not null, TooltipText = refusal ?? "" };
                var target = other.Id;
                send.Pressed += () =>
                {
                    Panel.Picked = null;
                    Run(new SendTroopsCommand(player, town.Id, picked.Slot, target, count));
                };
                Rows.AddChild(send);
            }
        }

        var queue = CaravanRules.Queue(state, town);
        if (queue.Count > 0)
        {
            Rows.AddChild(new Label { Text = "Queue:" });
        }

        foreach (var caravan in queue)
        {
            Rows.AddChild(new Label
            {
                Text = $"  {Goods(state, caravan)} from {PlaceName(state, state.GetObject(caravan.Source))}: "
                       + (caravan.DaysLeft > 0 ? $"{caravan.DaysLeft} day(s) left" : "waiting for room in the garrison"),
            });
        }

        foreach (var caravan in state.Caravans.Where(c => c.Owner == player && c.Source == town.Id))
        {
            Rows.AddChild(new Label
            {
                Text = $"Sent: {caravan.Count} {state.Creatures.Get(caravan.Creature).Name} to {PlaceName(state, state.GetObject(caravan.Destination))}, "
                       + (caravan.DaysLeft > 0 ? $"{caravan.DaysLeft} day(s) left" : "waiting for room there"),
            });
        }
    }

    /// <summary>The caravans, the map dwellings and providers it reaches, what the player can pay, and the garrison stack it could send.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        refresh.On<CaravanDispatched>(events)
            .On<CaravanArrived>(events)
            .On<CaravanReturned>(events)
            .On<CaravanLost>(events)
            .On<DwellingCreaturesGrew>(events)
            .On<DwellingOwnerChanged>(events)
            .On<ProviderOwnerChanged>(events)
            .On<ProviderStockChanged>(events)
            .On<ResourcesChanged>(events)
            .On<GarrisonChanged>(events);

    private static string Goods(GameState state, Caravan caravan) =>
        caravan.Cargo is { } cargo ? ResourceProviderRules.Describe(cargo) : $"{caravan.Count} {state.Creatures.Get(caravan.Creature).Name}";

    /// <summary>A town by its name, a dwelling by its creature ("Footman dwelling at 12,1", as in the hire rows), anything else by its kind.</summary>
    private static string PlaceName(GameState state, MapObject place) => place switch
    {
        Town town => town.Name,
        CreatureDwelling dwelling => $"{(state.Creatures.TryGet(dwelling.Creature, out var creature) ? creature.Name : dwelling.Creature)} dwelling at {dwelling.Position.X},{dwelling.Position.Y}",
        _ => place.Definition.Name,
    };
}
