namespace EternalWars.Core.Events;

/// <summary>
/// Collects subscription tokens so an owner (a view, a service, a Godot node) can drop
/// all of them at once when it is torn down.
/// </summary>
public sealed class SubscriptionGroup : IDisposable
{
    private readonly List<IDisposable> _subscriptions = [];

    public void Add(IDisposable subscription)
    {
        ArgumentNullException.ThrowIfNull(subscription);
        _subscriptions.Add(subscription);
    }

    public void Dispose()
    {
        for (var i = _subscriptions.Count - 1; i >= 0; i--)
        {
            _subscriptions[i].Dispose();
        }

        _subscriptions.Clear();
    }
}
