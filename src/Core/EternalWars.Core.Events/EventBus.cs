namespace EternalWars.Core.Events;

/// <summary>
/// Synchronous, single-threaded, deterministic event bus.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Handlers run in subscription order, on the publishing thread.</item>
/// <item>Events published from inside a handler are queued and dispatched after the current
/// event has reached every handler (breadth-first). This keeps ordering predictable: all
/// reactions to <c>A</c> are seen before anything <c>A</c>'s reactions caused.</item>
/// <item>Subscribing during dispatch is safe and applies from the next event; a handler unsubscribed during dispatch is not called again,
/// not even for the current event.</item>
/// <item>If a handler throws, dispatch stops, still-queued events are discarded and the
/// exception propagates to the outermost <see cref="Publish{TEvent}"/> caller.</item>
/// </list>
/// Each concrete event type's handlers are worked out once and kept until a subscription is added or removed.
/// The bus is intentionally not thread-safe: the game simulation is turn-based and runs on one thread.
/// </remarks>
public sealed class EventBus : IEventBus
{
    private readonly List<Subscription> _subscriptions = [];
    private readonly Queue<IEvent> _pending = new();
    private readonly Dictionary<Type, Subscription[]> _byEventType = []; // concrete event type -> its subscriptions, in subscription order
    private bool _dispatching;

    public void Publish<TEvent>(TEvent @event)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        _pending.Enqueue(@event);
        if (_dispatching)
        {
            return;
        }

        _dispatching = true;
        try
        {
            while (_pending.TryDequeue(out var next))
            {
                Dispatch(next);
            }
        }
        finally
        {
            _pending.Clear();
            _dispatching = false;
        }
    }

    public IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent
    {
        ArgumentNullException.ThrowIfNull(handler);

        var subscription = new Subscription(this, typeof(TEvent), e => handler.Handle((TEvent)e));
        _subscriptions.Add(subscription);
        _byEventType.Clear();
        return subscription;
    }

    private void Dispatch(IEvent @event)
    {
        var eventType = @event.GetType();
        if (!_byEventType.TryGetValue(eventType, out var subscriptions))
        {
            subscriptions = [.. _subscriptions.Where(s => s.EventType.IsAssignableFrom(eventType))];
            _byEventType[eventType] = subscriptions;
        }

        // The array is never changed (a new one replaces it), so handlers may subscribe and unsubscribe while it is walked.
        foreach (var subscription in subscriptions)
        {
            if (subscription.IsActive)
            {
                subscription.Invoke(@event);
            }
        }
    }

    private void Remove(Subscription subscription)
    {
        if (_subscriptions.Remove(subscription))
        {
            _byEventType.Clear();
        }
    }

    private sealed class Subscription(EventBus owner, Type eventType, Action<IEvent> invoke) : IDisposable
    {
        public Type EventType { get; } = eventType;

        public bool IsActive { get; private set; } = true;

        public void Invoke(IEvent @event) => invoke(@event);

        public void Dispose()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            owner.Remove(this);
        }
    }
}
