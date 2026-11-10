using EternalWars.Core.Events;
using Godot;

namespace EternalWars.Godot.Shared.Events;

/// <summary>
/// Refreshes a view once for a burst of events: <see cref="Request"/> marks it out of date, and the refresh runs once, deferred to the end of the
/// frame, however many events asked for it (a recruit or a hero's step publishes several). Explicit refreshes (opening, a button) stay direct.
/// </summary>
public sealed class RefreshScheduler(Node node, Action refresh)
{
    private bool _pending;

    /// <summary>Refreshes at the end of this frame (once), unless the node is gone by then.</summary>
    public void Request()
    {
        if (_pending)
        {
            return;
        }

        _pending = true;
        Callable.From(Run).CallDeferred();
    }

    /// <summary>Requests a refresh on each <typeparamref name="TEvent"/> while the node is in the tree.</summary>
    public RefreshScheduler On<TEvent>(IEventSubscriber events)
        where TEvent : IEvent
    {
        node.SubscribeWhileInTree<TEvent>(events, _ => Request());
        return this;
    }

    private void Run()
    {
        _pending = false;
        if (GodotObject.IsInstanceValid(node) && node.IsInsideTree())
        {
            refresh();
        }
    }
}
