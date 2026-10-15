namespace EternalWars.Core.Events;

/// <summary>
/// Registers handlers. Every subscription returns a token; disposing it unsubscribes.
/// </summary>
public interface IEventSubscriber
{
    /// <summary>
    /// Subscribes <paramref name="handler"/> to every published event assignable to <typeparamref name="TEvent"/>.
    /// Subscribing to <see cref="IEvent"/> receives everything (useful for logging and replays).
    /// </summary>
    IDisposable Subscribe<TEvent>(IEventHandler<TEvent> handler)
        where TEvent : IEvent;
}
