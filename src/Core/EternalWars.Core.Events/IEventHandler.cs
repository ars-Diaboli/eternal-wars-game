namespace EternalWars.Core.Events;

/// <summary>
/// Reacts to a single kind of event. Contravariant, so a handler of a base event type
/// (or of <see cref="IEvent"/> itself) can be subscribed for more specific events.
/// </summary>
public interface IEventHandler<in TEvent>
    where TEvent : IEvent
{
    void Handle(TEvent @event);
}
