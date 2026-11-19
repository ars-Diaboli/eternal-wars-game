using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>If: the branch the condition picks runs next.</summary>
internal sealed class IfHandler : MapEventActionHandler<IfAction>
{
    public override bool IsReward => false;

    protected override bool Apply(MapEventActionContext context, IfAction action)
    {
        context.Run.Frames.Push(new MapEventFrame(context.Test(action.Condition) ? action.Then : action.Else));
        return true;
    }

    protected override IReadOnlyList<IReadOnlyList<EventAction>> Lists(IfAction action) => [action.Then, action.Else];

    protected override IReadOnlyList<string> ListNames(IfAction action) => ["Then", "Else"];

    protected override EventAction WithLists(IfAction action, IReadOnlyList<IReadOnlyList<EventAction>> lists) =>
        action with { Then = lists[0], Else = lists[1] };

    protected override IEnumerable<EventCondition> Conditions(IfAction action) => [action.Condition];

    protected override string Line(IfAction action, GameState? state) => $"If {MapEventText.Describe(action.Condition, state)}";

    protected override EventWorth Worth(IMapEventValuer valuer, IfAction action) =>
        valuer.Worth(valuer.Holds(action.Condition) ? action.Then : action.Else);
}
