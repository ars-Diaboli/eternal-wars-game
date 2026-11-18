namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Stop: the event it is in ends here.</summary>
internal sealed class StopHandler : MapEventActionHandler<StopAction>
{
    protected override bool Apply(MapEventActionContext context, StopAction action) => false;
}
