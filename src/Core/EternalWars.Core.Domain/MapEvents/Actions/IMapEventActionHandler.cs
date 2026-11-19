using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// What one <see cref="EventAction"/> type does when an event runs it (ADR-115, EVT-4), how it shows as a message's reward, what it holds (its
/// action lists, conditions and expressions, which <see cref="MapEventTree"/> walks), its line in the editor and what the AI makes of it
/// (ADR-237). The running, the frames and the answers are <see cref="MapEventService"/>'s; the handler for an action is
/// <see cref="MapEventActionHandlers.For"/>.
/// </summary>
internal interface IMapEventActionHandler
{
    /// <summary>The action type this handler runs.</summary>
    Type Action { get; }

    /// <summary>
    /// Whether the action gives or takes something (else it steers the event: its flow, its variables, its messages). A reward with an amount
    /// draws it in <see cref="Fix"/> (a test checks it).
    /// </summary>
    bool IsReward { get; }

    /// <summary>Runs the action, publishing what happens, in order; false stops the event.</summary>
    bool Apply(MapEventActionContext context, EventAction action);

    /// <summary>The action as a message's reward, its amounts drawn now so the message tells what comes.</summary>
    EventAction Fix(MapEventActionContext context, EventAction action);

    /// <summary>The message's line for the reward (amounts already drawn), or null when it shows nothing.</summary>
    string? Describe(GameState state, EventAction action);

    /// <summary>The place a turn-start message's reward names, for the camera (EVT-2 f), or null.</summary>
    MapPosition? Place(EventAction action);

    /// <summary>The action lists the action holds (a branch's two, a loop's, each answer's, a message's rewards), in order; none for most.</summary>
    IReadOnlyList<IReadOnlyList<EventAction>> Lists(EventAction action);

    /// <summary>The names the editor gives <see cref="Lists"/>, one each.</summary>
    IReadOnlyList<string> ListNames(EventAction action);

    /// <summary>The action holding <paramref name="lists"/> in place of its own (as many as <see cref="Lists"/> gives).</summary>
    EventAction WithLists(EventAction action, IReadOnlyList<IReadOnlyList<EventAction>> lists);

    /// <summary>The conditions of the action itself (not of the actions it holds).</summary>
    IEnumerable<EventCondition> Conditions(EventAction action);

    /// <summary>The expressions of the action itself (not of its conditions or the actions it holds).</summary>
    IEnumerable<EventExpr> Expressions(EventAction action);

    /// <summary>The action's line in the editor's "Events" tab (EVT-6); with a state, ids become names.</summary>
    string Line(EventAction action, GameState? state);

    /// <summary>What the action is worth to the AI, and whether it ends the event (EVT-6).</summary>
    EventWorth Worth(IMapEventValuer valuer, EventAction action);
}
