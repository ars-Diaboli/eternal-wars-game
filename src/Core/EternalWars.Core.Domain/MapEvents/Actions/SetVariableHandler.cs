using EternalWars.Core.Domain.Events;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Set variable: the map variable combined with the value.</summary>
internal sealed class SetVariableHandler : MapEventActionHandler<SetVariableAction>
{
    protected override bool Apply(MapEventActionContext context, SetVariableAction action)
    {
        var value = MapEventEvaluator.Combine(action.Op, context.Data.Variable(action.Variable), context.Evaluate(action.Value));
        context.Data.SetVariable(action.Variable, value);
        context.Events.Publish(new MapVariableChanged(action.Variable, value));
        return true;
    }
}
