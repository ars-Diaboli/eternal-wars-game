using EW.CoreLib.Types.Events;

namespace EW.CoreLib.Messaging;

public class EventHub1
{
    // Maps an Event Type to a List of subscriber invocation actions
    private readonly Dictionary<Type, List<EventSubscription>> _subscribers = new();

    public void Publish<TEvent>(TEvent eventData) where TEvent : IGameEvent
    {
        var eventType = typeof(TEvent);
        if (!_subscribers.TryGetValue(eventType, out var subscriptions)) return;

        // Iterate backwards through invocation copy to allow safe unsubscription during loops
        var subscriptionsCopy = new List<EventSubscription>(subscriptions);
        foreach (var subscription in subscriptionsCopy)
        {
            subscription.Invocation(eventData);
        }
    }
    
    /// <summary>
    /// Automatically scans an object for all implemented IHandleEvent definitions
    /// and registers them to their matching message queues.
    /// </summary>
    public void Subscribe(object subscriber)
    {
        var interfaces = subscriber.GetType().GetInterfaces();

        foreach (var @interface in interfaces)
        {
            if (!@interface.IsGenericType || @interface.GetGenericTypeDefinition() != typeof(IHandleEvent<>)) 
                continue;

            var eventType = @interface.GetGenericArguments()[0];
            var methodInfo = @interface.GetMethod("Handle");

            // Create a fast, compiled delegate structure
            void ActionWrapper(object eventObj) => methodInfo.Invoke(subscriber, [eventObj]);

            if (!_subscribers.ContainsKey(eventType))
            {
                _subscribers[eventType] = new List<EventSubscription>();
            }
            
            _subscribers[eventType].Add(new EventSubscription(subscriber, ActionWrapper));
        }
    }
    
    public void Unsubscribe(object subscriber)
    {
        var interfaces = subscriber.GetType().GetInterfaces();

        foreach (var @interface in interfaces)
        {
            if (!@interface.IsGenericType || @interface.GetGenericTypeDefinition() != typeof(IHandleEvent<>))
                continue;

            var eventType = @interface.GetGenericArguments()[0];
            
            _subscribers[eventType].RemoveAll(s => s.Subscriber == subscriber);
        }
    }
}