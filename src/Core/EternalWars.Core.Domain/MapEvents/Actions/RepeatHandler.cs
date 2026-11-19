using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Repeat: the count is drawn once, at most <see cref="MapEventService.MaxLoopRounds"/>; 0 or less, or no actions, runs nothing.</summary>
internal sealed class RepeatHandler : MapEventActionHandler<RepeatAction>
{
    public override bool IsReward => false;

    protected override bool Apply(MapEventActionContext context, RepeatAction action)
    {
        if (Math.Min(context.Evaluate(action.Times), MapEventService.MaxLoopRounds) is > 0 and var times && action.Actions.Count > 0)
        {
            context.Run.Frames.Push(new MapEventFrame(action.Actions) { RoundsLeft = times - 1 });
        }

        return true;
    }

    protected override IReadOnlyList<IReadOnlyList<EventAction>> Lists(RepeatAction action) => [action.Actions];

    protected override IReadOnlyList<string> ListNames(RepeatAction action) => ["Do"];

    protected override EventAction WithLists(RepeatAction action, IReadOnlyList<IReadOnlyList<EventAction>> lists) => action with { Actions = lists[0] };

    protected override IEnumerable<EventExpr> Expressions(RepeatAction action) => [action.Times];

    protected override string Line(RepeatAction action, GameState? state) => $"Repeat {Expr(action.Times)} times";

    protected override EventWorth Worth(IMapEventValuer valuer, RepeatAction action)
    {
        var times = (int)Math.Clamp(Math.Round(valuer.Average(action.Times)), 0, MapEventService.MaxLoopRounds);
        if (times == 0)
        {
            return EventWorth.Nothing;
        }

        var round = valuer.Worth(action.Actions);
        return round.Stops ? round : EventWorth.Gain(times * round.Value);
    }
}
