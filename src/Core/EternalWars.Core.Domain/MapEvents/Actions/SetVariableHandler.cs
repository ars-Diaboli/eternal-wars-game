using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Set variable: the map variable combined with the value.</summary>
internal sealed class SetVariableHandler : MapEventActionHandler<SetVariableAction>
{
    public override bool IsReward => false;

    protected override bool Apply(MapEventActionContext context, SetVariableAction action)
    {
        var value = MapEventEvaluator.Combine(action.Op, context.Data.Variable(action.Variable), context.Evaluate(action.Value));
        context.Data.SetVariable(action.Variable, value);
        context.Events.Publish(new MapVariableChanged(action.Variable, value));
        return true;
    }

    protected override IEnumerable<EventExpr> Expressions(SetVariableAction action) => [action.Value];

    protected override string Line(SetVariableAction action, GameState? state) => action.Op switch
    {
        VariableOp.Set => $"{action.Variable} = {Expr(action.Value)}",
        VariableOp.Add => $"{action.Variable} += {Expr(action.Value)}",
        VariableOp.Subtract => $"{action.Variable} −= {Expr(action.Value)}",
        VariableOp.Multiply => $"{action.Variable} ×= {Expr(action.Value)}",
        _ => $"{action.Variable} ÷= {Expr(action.Value)}",
    };
}
