using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Stop: the event it is in ends here.</summary>
internal sealed class StopHandler : MapEventActionHandler<StopAction>
{
    public override bool IsReward => false;

    protected override bool Apply(MapEventActionContext context, StopAction action) => false;

    protected override string Line(StopAction action, GameState? state) => "Stop this event";

    protected override EventWorth Worth(IMapEventValuer valuer, StopAction action) => EventWorth.Stop;
}
