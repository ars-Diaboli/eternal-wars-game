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
/// <item>Subscribing/unsubscribing during dispatch is safe; changes apply to the next event.</item>
/// <item>If a handler throws, dispatch stops, still-queued events are discarded and the
/// exception propagates to the outermost <see cref="Publish{TEvent}"/> caller.</item>
/// </list>
/// The bus is intentionally not thread-safe: the game simulation is turn-based and runs on one thread.
/// </remarks>
public sealed class EventBus : IEventBus
{
    private readonly List<Subscription> _subscriptions = [];
    private readonly Queue<IEvent> _pending = new();
    private Subscription[]? _snapshot;
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
        _snapshot = null;
        return subscription;
    }

    private void Dispatch(IEvent @event)
    {
        _snapshot ??= [.. _subscriptions];
        var eventType = @event.GetType();

        foreach (var subscription in _snapshot)
        {
            if (subscription.IsActive && subscription.EventType.IsAssignableFrom(eventType))
            {
                subscription.Invoke(@event);
            }
        }
    }

    private void Remove(Subscription subscription)
    {
        if (_subscriptions.Remove(subscription))
        {
            _snapshot = null;
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
