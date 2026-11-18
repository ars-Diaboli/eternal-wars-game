using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Creatures to hire in the town event's town, at a line's dwelling; negative takes them, down to 0.</summary>
internal sealed class AvailableCreaturesHandler : MapEventActionHandler<AvailableCreaturesAction>
{
    protected override bool Apply(MapEventActionContext context, AvailableCreaturesAction action)
    {
        if (context.Town is { } town && TownRules.LineOf(context.State, town, action.Line) is { } line)
        {
            town.SetAvailable(line.Id, Math.Max(0, town.AvailableOf(line.Id) + context.Evaluate(action.Count)));
            context.Events.Publish(new TownCreaturesGrew(town.Id, town.Available.ToDictionary()));
        }

        return true;
    }

    protected override EventAction Fix(MapEventActionContext context, AvailableCreaturesAction action) =>
        action with { Count = new NumberExpr(context.Evaluate(action.Count)) };
}
