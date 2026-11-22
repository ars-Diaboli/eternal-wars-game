using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using EternalWars.Godot.UI.Widgets;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// The garrison and the visiting hero as two <see cref="ArmyBar"/>s (ADR-294): drag to move, merge or swap stacks (Shift: split, Ctrl: one),
/// Alt + click to spread, the arrow badge to upgrade; the heroes' portraits dragged onto the other row swap them, move the visitor into the
/// garrison or lead the garrison hero out (ADR-112). The garrison hero's button below does the same in one click. The bars' group is the
/// screen's (<see cref="TownPanel.Armies"/>), so the caravan sends the stack picked there.
/// </summary>
internal sealed class TownArmiesSection : TownSection
{
    private readonly ArmyBar _garrison;
    private readonly ArmyBar _visiting;
    private readonly HBoxContainer _heroRow = new();

    public TownArmiesSection(ITownSectionHost panel)
        : base(panel)
    {
        _garrison = new ArmyBar(panel.Armies, TownArmy.Garrison, "Garrison");
        _visiting = new ArmyBar(panel.Armies, TownArmy.VisitingHero, "Visiting");
        Rows.AddChild(_garrison);
        Rows.AddChild(_heroRow);
        Rows.AddChild(_visiting);
    }

    public VBoxContainer Rows { get; } = new();

    protected override Node Anchor => Rows;

    /// <summary>The town's army bars' group: what a drag, a spread, an upgrade and a hero's drag do there.</summary>
    public static ArmyBarGroup Group(ITownSectionHost panel) => new()
    {
        Move = (from, to, count) => panel.Shown is { } town
            ? new MoveTownStackCommand(panel.Session.State.CurrentPlayer.Id, town.Id, (TownArmy)from.Army, from.Slot, (TownArmy)to.Army, to.Slot, count)
            : null,
        Run = panel.RunAll,
        Info = (at, detailed) => panel.Shown is { } town && Army(panel.Session.State, town, (TownArmy)at.Army)?[at.Slot] is { } stack
            ? InfoSheets.Creature(panel.Session.State, stack.Creature, detailed, InfoSheets.Known(stack))
            : null,
        Spread = at => panel.Shown is { } town && Army(panel.Session.State, town, (TownArmy)at.Army) is { } army
            ? StackSpread.ForTown(panel.Session.State.CurrentPlayer.Id, town.Id, (TownArmy)at.Army, army, at.Slot)
            : [],
        Upgrade = at => Upgrade(panel, at),
        MoveHero = (from, to) => panel.Shown is { } town ? HeroMove(panel.Session.State, town, (TownArmy)from, (TownArmy)to) : null,
    };

    public override void Show(Town town)
    {
        var state = State;
        var visitor = TownRules.VisitingHero(state, town);
        _garrison.Show(state, town.Garrison, town.GarrisonHero?.Name);
        _visiting.Show(state, visitor?.Army, visitor?.Name);
        ShowGarrisonHero(state, town);
    }

    /// <summary>The armies' stacks, and the heroes coming and going.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        OnHeroPresence(refresh, events)
            .On<GarrisonChanged>(events)
            .On<ArmyChanged>(events)
            .On<CreaturesRecruited>(events)
            .On<StackUpgraded>(events)
            .On<CreaturesTransformed>(events)
            .On<ResourcesChanged>(events); // the upgrade badges' prices

    /// <summary>The army of <paramref name="which"/> in the town, or null (no visiting hero).</summary>
    private static Army? Army(GameState state, Town town, TownArmy which) =>
        which == TownArmy.Garrison ? town.Garrison : TownRules.VisitingHero(state, town)?.Army;

    /// <summary>The badge of a stack the town can upgrade: <see cref="UpgradeStackCommand"/> with the price and any shortfall in the tooltip.</summary>
    private static (GameCommand Command, string Tooltip)? Upgrade(ITownSectionHost panel, ArmySlot at)
    {
        var state = panel.Session.State;
        if (panel.Shown is not { } town || Army(state, town, (TownArmy)at.Army)?[at.Slot] is not { } stack
            || TownRules.CheckUpgradeLine(state, town, stack.Creature, out var line) is not null)
        {
            return null;
        }

        var cost = TownRules.UpgradeCost(state, town, line!, stack.Count);
        var upgraded = state.Creatures.Get(line!.UpgradesTo!.Value).Name;
        var lacking = CostLabel.Shortfall(cost, state.CurrentPlayer.Resources);
        var tooltip = $"Upgrade to {upgraded}: {ResourceIcons.Describe(cost)}" + (lacking.IsEmpty ? "" : $"\nLacking: {ResourceIcons.Describe(lacking)}");
        return (new UpgradeStackCommand(state.CurrentPlayer.Id, town.Id, (TownArmy)at.Army, at.Slot), tooltip);
    }

    /// <summary>
    /// A hero's portrait dragged from one row to the other: both heroes there swap (ADR-294); the visitor alone goes into the garrison; the garrison
    /// hero alone comes out. Null when nobody is there to move.
    /// </summary>
    private static GameCommand? HeroMove(GameState state, Town town, TownArmy from, TownArmy to)
    {
        var player = state.CurrentPlayer.Id;
        var visitor = TownRules.VisitingHero(state, town);
        return (from, to, town.GarrisonHero, visitor) switch
        {
            (_, _, not null, not null) when from != to => new SwapTownHeroesCommand(player, town.Id),
            (TownArmy.VisitingHero, TownArmy.Garrison, null, not null) => new EnterGarrisonCommand(player, town.Id),
            (TownArmy.Garrison, TownArmy.VisitingHero, not null, null) => new LeaveGarrisonCommand(player, town.Id),
            _ => null,
        };
    }

    private void ShowGarrisonHero(GameState state, Town town)
    {
        _heroRow.ClearChildren();
        var player = state.CurrentPlayer.Id;
        var visitor = TownRules.VisitingHero(state, town);
        if (town.GarrisonHero is { } inside && visitor is not null)
        {
            var refusal = TownRules.CheckSwapHeroes(state, town);
            var swap = new Button
            {
                Text = $"Swap {inside.Name} and {visitor.Name}",
                Disabled = refusal is not null,
                TooltipText = refusal ?? "Each hero keeps its own troops: the garrison hero comes out with the garrison, the visitor's army stays as the garrison.",
            };
            swap.Pressed += () => Run(new SwapTownHeroesCommand(player, town.Id));
            _heroRow.AddChild(swap);
        }
        else if (town.GarrisonHero is { } alone)
        {
            var refusal = TownRules.CheckLeaveGarrison(state, town);
            var leave = new Button { Text = $"Lead {alone.Name} out", Disabled = refusal is not null, TooltipText = refusal ?? "The garrison becomes its army." };
            leave.Pressed += () => Run(new LeaveGarrisonCommand(player, town.Id));
            _heroRow.AddChild(leave);
        }
        else if (visitor is not null)
        {
            var refusal = TownRules.CheckEnterGarrison(state, town);
            var enter = new Button
            {
                Text = $"{visitor.Name} into the garrison",
                Disabled = refusal is not null,
                TooltipText = refusal ?? "Its army joins the garrison; it defends the town with its skills and war machines.",
            };
            enter.Pressed += () => Run(new EnterGarrisonCommand(player, town.Id));
            _heroRow.AddChild(enter);
        }
    }
}
