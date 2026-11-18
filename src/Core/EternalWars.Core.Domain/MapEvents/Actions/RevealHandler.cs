using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>Reveals a circle of the map to the player (EVT-4 c); the run's place moves there.</summary>
internal sealed class RevealHandler : MapEventActionHandler<RevealAction>
{
    protected override bool Apply(MapEventActionContext context, RevealAction action)
    {
        var run = context.Run;
        if (run.Player is { } viewer)
        {
            run.Place = new MapPosition(action.X, action.Y, action.Level);
            context.Service.Fog.RevealArea(viewer, run.Place.Value, action.Radius);
            context.Events.Publish(new AreaRevealed(viewer, run.Place.Value, action.Radius));
        }

        return true;
    }

    protected override MapPosition? Place(RevealAction action) => new MapPosition(action.X, action.Y, action.Level);
}
