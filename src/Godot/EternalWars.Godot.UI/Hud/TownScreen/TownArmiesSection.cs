using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Info;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Controls;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud.TownScreen;

/// <summary>
/// The garrison's slots, the garrison hero's button (ADR-112: the visiting hero moves in, or the garrison hero leads the garrison out) and the
/// visiting hero's slots. Click a stack, then a slot to move it ("Split" moves half); the picked stack is the screen's
/// (<see cref="TownPanel.Picked"/>).
/// </summary>
internal sealed class TownArmiesSection(TownPanel panel) : TownSection(panel)
{
    public VBoxContainer Rows { get; } = new();

    protected override Node Anchor => Rows;

    public override void Show(Town town)
    {
        var state = State;
        Rows.ClearChildren();
        AddArmyRow(town, TownArmy.Garrison, town.GarrisonHero is { } inside ? $"Garrison ({inside.Name})" : "Garrison", town.Garrison);
        ShowGarrisonHero(state, town);
        if (TownRules.VisitingHero(state, town) is { } hero)
        {
            AddArmyRow(town, TownArmy.VisitingHero, hero.Name, hero.Army);
        }
    }

    /// <summary>The armies' stacks, and the heroes coming and going.</summary>
    protected override void Subscribe(RefreshScheduler refresh, IEventSubscriber events) =>
        OnHeroPresence(refresh, events)
            .On<GarrisonChanged>(events)
            .On<ArmyChanged>(events)
            .On<CreaturesRecruited>(events)
            .On<StackUpgraded>(events)
            .On<CreaturesTransformed>(events);

    private void ShowGarrisonHero(GameState state, Town town)
    {
        var player = state.CurrentPlayer.Id;
        var row = new HBoxContainer();
        if (town.GarrisonHero is { } inside)
        {
            var refusal = TownRules.CheckLeaveGarrison(state, town);
            var leave = new Button { Text = $"Lead {inside.Name} out", Disabled = refusal is not null, TooltipText = refusal ?? "The garrison becomes its army." };
            leave.Pressed += () => Run(new LeaveGarrisonCommand(player, town.Id));
            row.AddChild(leave);
        }
        else if (TownRules.VisitingHero(state, town) is { } visitor)
        {
            var refusal = TownRules.CheckEnterGarrison(state, town);
            var enter = new Button
            {
                Text = $"{visitor.Name} into the garrison",
                Disabled = refusal is not null,
                TooltipText = refusal ?? "Its army joins the garrison; it defends the town with its skills and war machines.",
            };
            enter.Pressed += () => Run(new EnterGarrisonCommand(player, town.Id));
            row.AddChild(enter);
        }

        Rows.AddChild(row);
    }

    private void AddArmyRow(Town town, TownArmy which, string name, Army army)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(UiTheme.ArmyNameWidth, 0) });
        for (var slot = 0; slot < Army.SlotCount; slot++)
        {
            var stack = army[slot];
            var target = (which, slot);
            var button = new Button
            {
                Text = stack is null ? "—" : $"{stack.Count}\n{stack.Creature.Split('.')[^1]}",
                CustomMinimumSize = UiTheme.TownSlotSize,
                ToggleMode = true,
                ButtonPressed = Panel.Picked == target,
            };
            button.Pressed += () => Panel.ClickSlot(town, target, stack is not null);
            if (stack is not null)
            {
                button.WithInfo(detailed => InfoSheets.Creature(Session.State, stack.Creature, detailed, InfoSheets.Known(stack)));
            }

            row.AddChild(button);
        }

        Rows.AddChild(row);
    }
}
