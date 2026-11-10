using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// What the map object services share: the state, the publisher, the economy and the small steps every object rule uses (asking, removing an
/// object, collecting a limited reward, finding the hero at an object's entrance).
/// </summary>
public sealed class ObjectInteractionContext
{
    internal ObjectInteractionContext(GameState state, IEventPublisher events, EconomyService economy)
    {
        State = state;
        Events = events;
        Economy = economy;
    }

    internal GameState State { get; }

    internal IEventPublisher Events { get; }

    internal EconomyService Economy { get; }

    /// <summary>
    /// Takes the reward of an object with a limited <see cref="VisitScope"/> for the hero (ADR-068): false when the hero may not collect now,
    /// else the visit is recorded and <see cref="MapObjectCollected"/> published, before the caller hands out the reward.
    /// </summary>
    internal bool TryCollect(Hero hero, MapObject mapObject)
    {
        if (!mapObject.CanCollect(hero.Id, State.Date))
        {
            return false;
        }

        mapObject.Visits.Record(hero.Id, State.Date);
        Events.Publish(new MapObjectCollected(hero.Id, mapObject.Id));
        return true;
    }

    /// <summary>Opens a decision: it becomes <see cref="GameState.PendingDecision"/>, then <see cref="DecisionRequested"/>.</summary>
    internal void Ask(PendingDecision decision)
    {
        State.PendingDecision = decision;
        Events.Publish(new DecisionRequested(decision));
    }

    /// <summary>The object leaves the map: <see cref="MapObjectRemoved"/>.</summary>
    internal void RemoveFromMap(MapObject mapObject)
    {
        State.RemoveObject(mapObject);
        Events.Publish(new MapObjectRemoved(mapObject.Id));
    }

    /// <summary>The hero collects the object and its player gets the resources: <see cref="ResourcesFound"/>.</summary>
    internal void GiveResources(Hero hero, MapObject provider, ResourceSet resources)
    {
        TryCollect(hero, provider);
        Economy.Grant(hero.Owner, resources);
        Events.Publish(new ResourcesFound(hero.Id, provider.Id, resources));
    }

    /// <summary>The player's hero standing on the entrance of an object of type <typeparamref name="T"/>, with nothing pending.</summary>
    internal Hero HeroAt<T>(PlayerId player, MapObjectId id, out T mapObject)
        where T : MapObject
    {
        mapObject = State.FindObject(id) as T ?? throw new CommandRefusedException($"{id} is not a {typeof(T).Name}.");
        if (State.PendingDecision is not null)
        {
            throw new CommandRefusedException("Resolve the pending decision first.");
        }

        var hero = State.HeroAt(mapObject.VisitPosition);
        return hero is not null && hero.Owner == player ? hero : throw new CommandRefusedException($"No own hero stands at the {mapObject.Definition.Name}.");
    }
}
