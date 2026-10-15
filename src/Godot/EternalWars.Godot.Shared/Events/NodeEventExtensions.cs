using EternalWars.Core.Events;
using Godot;

namespace EternalWars.Godot.Shared.Events;

/// <summary>
/// Ties core event subscriptions to a node's lifetime so views never outlive their handlers
/// (and handlers never touch freed nodes).
/// </summary>
public static class NodeEventExtensions
{
    public static void SubscribeWhileInTree<TEvent>(this Node node, IEventSubscriber events, Action<TEvent> handler)
        where TEvent : IEvent
    {
        var subscription = events.Subscribe(handler);
        node.TreeExiting += subscription.Dispose;
    }
}
