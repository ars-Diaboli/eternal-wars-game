namespace EternalWars.Core.Events;

/// <summary>
/// Marker for everything that can travel through the event bus.
/// Events describe facts that already happened, so they are named in the past tense
/// (<c>HeroMoved</c>, <c>DayStarted</c>) and should be immutable (prefer <c>sealed record</c>).
/// </summary>
public interface IEvent;
