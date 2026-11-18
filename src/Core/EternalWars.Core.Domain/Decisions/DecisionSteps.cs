using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Decisions;

/// <summary>
/// The steps every rule service takes around a decision, and when an object leaves the map, written once and taken in one order (ADR-215):
/// <list type="number">
/// <item><see cref="Ask"/>: the decision becomes <see cref="GameState.PendingDecision"/>, then <see cref="DecisionRequested"/>.</item>
/// <item><see cref="TakeDecision{T}"/>: the answer finds its decision pending, or is refused; nothing changes yet.</item>
/// <item>The answer's checks, a price that can be refused among them: a refusal leaves the decision pending and the state as it was.</item>
/// <item><see cref="Resolve"/>: the decision is cleared, then <see cref="DecisionResolved"/>.</item>
/// <item>The answer's effects. A decision they open (a level-up, a battle) is asked after this one's <see cref="DecisionResolved"/>.</item>
/// </list>
/// A battle's decision lasts the battle: it is resolved once the battle's consequences are applied.
/// </summary>
internal sealed class DecisionSteps(GameState state, IEventPublisher events)
{
    /// <summary>Opens a decision: it becomes pending, then <see cref="DecisionRequested"/>.</summary>
    internal void Ask(PendingDecision decision)
    {
        state.PendingDecision = decision;
        events.Publish(new DecisionRequested(decision));
    }

    /// <summary>The pending decision when it is a <typeparamref name="T"/>; otherwise the command is refused with <paramref name="refusal"/>.</summary>
    internal T TakeDecision<T>(string refusal)
        where T : PendingDecision =>
        state.PendingDecision as T ?? throw new CommandRefusedException(refusal);

    /// <summary>Closes the decision: no longer pending, then <see cref="DecisionResolved"/>.</summary>
    internal void Resolve(PendingDecision decision)
    {
        state.PendingDecision = null;
        events.Publish(new DecisionResolved(decision));
    }

    /// <summary>The object leaves the map: removed, then <see cref="MapObjectRemoved"/>.</summary>
    internal void RemoveObject(MapObject mapObject)
    {
        state.RemoveObject(mapObject);
        events.Publish(new MapObjectRemoved(mapObject.Id));
    }
}
