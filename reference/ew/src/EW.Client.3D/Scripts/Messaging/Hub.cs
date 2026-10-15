using System;
using System.Collections.Generic;
using Godot;

namespace EW.Scripts.Messaging;

public record EventSubscription(object Subscriber, Action<object> Invocation);

public partial class Hub : Node
{
    public static Hub Instance { get; private set; }

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
    
    public override void _EnterTree()
    {
        Instance = this;
    }

    #region Registration
    
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

    /// <summary>
    /// Recursively scans a node and all of its nested children, 
    /// automatically registering any that handle events.
    /// </summary>
    public void RegisterHierarchy(Node root)
    {
        if (root == null) return;

        // 1. Try to register the current node
        Subscribe(root);

        // 2. Recursively crawl down into every child branch
        foreach (Node child in root.GetChildren())
        {
            RegisterHierarchy(child);
        }
    }
    /// <summary>
    /// Recursively unregisters a node and its children to prevent memory leaks
    /// when switching maps or ending a combat scene.
    /// </summary>
    public void UnregisterHierarchy(Node root)
    {
        if (root == null) return;

        Unsubscribe(root);

        foreach (Node child in root.GetChildren())
        {
            UnregisterHierarchy(child);
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

    #endregion Registration
}