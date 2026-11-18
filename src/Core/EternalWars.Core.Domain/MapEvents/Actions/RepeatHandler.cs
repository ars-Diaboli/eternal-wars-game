namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Repeat: the count is drawn once, at most <see cref="MapEventService.MaxLoopRounds"/>; 0 or less, or no actions, runs nothing.</summary>
internal sealed class RepeatHandler : MapEventActionHandler<RepeatAction>
{
    protected override bool Apply(MapEventActionContext context, RepeatAction action)
    {
        if (Math.Min(context.Evaluate(action.Times), MapEventService.MaxLoopRounds) is > 0 and var times && action.Actions.Count > 0)
        {
            context.Run.Frames.Push(new MapEventFrame(action.Actions) { RoundsLeft = times - 1 });
        }

        return true;
    }
}
