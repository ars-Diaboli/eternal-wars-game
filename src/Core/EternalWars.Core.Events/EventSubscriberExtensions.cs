namespace EternalWars.Core.Events;

public static class EventSubscriberExtensions
{
    /// <summary>Subscribes a delegate instead of a dedicated handler class.</summary>
    public static IDisposable Subscribe<TEvent>(this IEventSubscriber subscriber, Action<TEvent> handler)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(subscriber);
        ArgumentNullException.ThrowIfNull(handler);
        return subscriber.Subscribe(new DelegateEventHandler<TEvent>(handler));
    }

    private sealed class DelegateEventHandler<TEvent>(Action<TEvent> handler) : IEventHandler<TEvent>
        where TEvent : IEvent
    {
        public void Handle(TEvent @event) => handler(@event);
    }
}
