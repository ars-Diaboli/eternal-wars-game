namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>While: the first round runs when the condition holds; the frame tests it again before each further round.</summary>
internal sealed class WhileHandler : MapEventActionHandler<WhileAction>
{
    protected override bool Apply(MapEventActionContext context, WhileAction action)
    {
        if (action.Actions.Count > 0 && context.Test(action.Condition))
        {
            context.Run.Frames.Push(new MapEventFrame(action.Actions) { While = action.Condition, Rounds = 1 });
        }

        return true;
    }
}
