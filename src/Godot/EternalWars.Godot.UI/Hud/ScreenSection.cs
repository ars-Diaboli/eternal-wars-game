using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Hud;

/// <summary>
/// A part of a screen (the town or hero screen, ADR-223) over one subject (the town, the hero) that rebuilds alone: <see cref="Watch"/> subscribes
/// it to the events that change what it shows, through its own <see cref="RefreshScheduler"/> on its <see cref="Anchor"/>, so a burst of them
/// rebuilds it once at the end of the frame. The screen decides what it shows (<see cref="Shown"/>: null while closed or hidden) and rebuilds every
/// section itself when that changes.
/// </summary>
public abstract class ScreenSection<TSubject>(Func<TSubject?> shown)
    where TSubject : class
{
    /// <summary>A control of the section, in the tree while the screen is: its subscriptions live as long as it does.</summary>
    protected abstract Node Anchor { get; }

    /// <summary>The subject the screen shows now, or null.</summary>
    protected TSubject? Shown => shown();

    /// <summary>Subscribes the section to its events (<see cref="Subscribe"/>). Call it once, when the screen is built.</summary>
    public void Watch(IEventSubscriber events) => Subscribe(new RefreshScheduler(Anchor, Refresh), events);

    /// <summary>Rebuilds the section now, if the screen shows a subject.</summary>
    public void Refresh()
    {
        if (Shown is { } subject)
        {
            Show(subject);
        }
    }

    /// <summary>Fills the section for <paramref name="subject"/>.</summary>
    public abstract void Show(TSubject subject);

    /// <summary>The events that change what the section shows: <c>refresh.On&lt;T&gt;(events)</c> for each.</summary>
    protected abstract void Subscribe(RefreshScheduler refresh, IEventSubscriber events);
}
