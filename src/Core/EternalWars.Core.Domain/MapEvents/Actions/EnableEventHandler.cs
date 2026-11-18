using EternalWars.Core.Domain.Events;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Enable or disable an event; no event named is the one the run is in.</summary>
internal sealed class EnableEventHandler : MapEventActionHandler<EnableEventAction>
{
    protected override bool Apply(MapEventActionContext context, EnableEventAction action)
    {
        var name = action.Event ?? MapEventService.EventOf(context.Run).Name;
        if (context.Data.IsEnabled(name) != action.Enabled)
        {
            context.Data.SetEnabled(name, action.Enabled);
            context.Events.Publish(new MapEventEnabledChanged(name, action.Enabled));
        }

        return true;
    }
}
