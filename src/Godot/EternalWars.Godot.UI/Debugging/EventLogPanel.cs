using EternalWars.Core.Events;
using EternalWars.Godot.Shared.Events;
using Godot;

namespace EternalWars.Godot.UI.Debugging;

/// <summary>Scrolling list of every event on the bus. Invaluable in the harness and as a debug overlay.</summary>
public partial class EventLogPanel : ItemList
{
    public int MaxEntries { get; set; } = 200;

    public void Bind(IEventSubscriber events)
    {
        this.SubscribeWhileInTree<IEvent>(events, e =>
        {
            AddItem(e.ToString());
            if (ItemCount > MaxEntries)
            {
                RemoveItem(0);
            }

            Select(ItemCount - 1);
            EnsureCurrentIsVisible();
        });
    }
}
