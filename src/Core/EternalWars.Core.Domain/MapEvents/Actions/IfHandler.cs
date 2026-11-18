namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>If: the branch the condition picks runs next.</summary>
internal sealed class IfHandler : MapEventActionHandler<IfAction>
{
    protected override bool Apply(MapEventActionContext context, IfAction action)
    {
        context.Run.Frames.Push(new MapEventFrame(context.Test(action.Condition) ? action.Then : action.Else));
        return true;
    }
}
