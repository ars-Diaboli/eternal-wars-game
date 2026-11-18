using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Gives or takes one of the hero's war machines (ADR-056).</summary>
internal sealed class WarMachineHandler : HeroActionHandler<WarMachineAction>
{
    protected override void ApplyTo(MapEventActionContext context, Hero hero, WarMachineAction action)
    {
        if (!action.Take && hero.AddWarMachine(action.Slot))
        {
            context.Events.Publish(new MapEventWarMachineGained(hero.Id, action.Slot));
        }
        else if (action.Take && hero.RemoveWarMachine(action.Slot))
        {
            context.Events.Publish(new WarMachineLost(hero.Id, action.Slot));
        }
    }

    protected override string Describe(GameState state, WarMachineAction action) => $"{(action.Take ? "−" : "+")} {action.Slot}";
}
