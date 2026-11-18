using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// What one <see cref="EventAction"/> type does when an event runs it (ADR-115, EVT-4), and how it shows as a message's reward. The running, the
/// frames and the answers are <see cref="MapEventService"/>'s; the handler for an action is <see cref="MapEventActionHandlers.For"/>.
/// </summary>
internal interface IMapEventActionHandler
{
    /// <summary>The action type this handler runs.</summary>
    Type Action { get; }

    /// <summary>Runs the action, publishing what happens, in order; false stops the event.</summary>
    bool Apply(MapEventActionContext context, EventAction action);

    /// <summary>The action as a message's reward, its amounts drawn now so the message tells what comes.</summary>
    EventAction Fix(MapEventActionContext context, EventAction action);

    /// <summary>The message's line for the reward (amounts already drawn), or null when it shows nothing.</summary>
    string? Describe(GameState state, EventAction action);

    /// <summary>The place a turn-start message's reward names, for the camera (EVT-2 f), or null.</summary>
    MapPosition? Place(EventAction action);
}
