namespace EternalWars.Core.Events;

/// <summary>
/// Publishes events to every subscribed handler. Game services depend on this interface only,
/// never on the concrete bus, so they stay trivially testable.
/// </summary>
public interface IEventPublisher
{
    void Publish<TEvent>(TEvent @event)
        where TEvent : IEvent;
}
