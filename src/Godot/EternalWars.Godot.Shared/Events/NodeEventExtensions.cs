using EternalWars.Core.Events;
using Godot;

namespace EternalWars.Godot.Shared.Events;

/// <summary>
/// Ties core event subscriptions to a node's lifetime so views never outlive their handlers
/// (and handlers never touch freed nodes).
/// </summary>
public static class NodeEventExtensions
{
    /// <summary>
    /// Subscribes the handler while the node is in the scene tree: now if it is in it, else when it enters; it is let go when the node leaves,
    /// and subscribed again if the node comes back. A node never added to the tree never holds the subscription (nor what its handler keeps alive).
    /// </summary>
    public static void SubscribeWhileInTree<TEvent>(this Node node, IEventSubscriber events, Action<TEvent> handler)
        where TEvent : IEvent
    {
        var subscription = node.IsInsideTree() ? events.Subscribe(handler) : null;
        node.TreeEntered += () => subscription ??= events.Subscribe(handler);
        node.TreeExiting += () =>
        {
            subscription?.Dispose();
            subscription = null;
        };
    }
}
