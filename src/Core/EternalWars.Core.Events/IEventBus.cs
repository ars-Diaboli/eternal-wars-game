namespace EternalWars.Core.Events;

/// <summary>Convenience union of <see cref="IEventPublisher"/> and <see cref="IEventSubscriber"/>.</summary>
public interface IEventBus : IEventPublisher, IEventSubscriber;
