using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>While: the first round runs when the condition holds; the frame tests it again before each further round.</summary>
internal sealed class WhileHandler : MapEventActionHandler<WhileAction>
{
    public override bool IsReward => false;

    protected override bool Apply(MapEventActionContext context, WhileAction action)
    {
        if (action.Actions.Count > 0 && context.Test(action.Condition))
        {
            context.Run.Frames.Push(new MapEventFrame(action.Actions) { While = action.Condition, Rounds = 1 });
        }

        return true;
    }

    protected override IReadOnlyList<IReadOnlyList<EventAction>> Lists(WhileAction action) => [action.Actions];

    protected override IReadOnlyList<string> ListNames(WhileAction action) => ["Do"];

    protected override EventAction WithLists(WhileAction action, IReadOnlyList<IReadOnlyList<EventAction>> lists) => action with { Actions = lists[0] };

    protected override IEnumerable<EventCondition> Conditions(WhileAction action) => [action.Condition];

    protected override string Line(WhileAction action, GameState? state) => $"While {MapEventText.Describe(action.Condition, state)}";

    /// <summary>One round: what the round changes is not followed.</summary>
    protected override EventWorth Worth(IMapEventValuer valuer, WhileAction action) =>
        valuer.Holds(action.Condition) ? valuer.Worth(action.Actions) : EventWorth.Nothing;
}
