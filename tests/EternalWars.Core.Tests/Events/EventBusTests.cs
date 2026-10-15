using EternalWars.Core.Events;

namespace EternalWars.Core.Tests.Events;

public class EventBusTests
{
    private sealed record Ping(int Value) : IEvent;

    private sealed record Pong(int Value) : IEvent;

    [Fact]
    public void Publish_DeliversToMatchingSubscribersInSubscriptionOrder()
    {
        var bus = new EventBus();
        var log = new List<string>();
        bus.Subscribe<Ping>(e => log.Add($"a{e.Value}"));
        bus.Subscribe<Pong>(e => log.Add($"pong{e.Value}"));
        bus.Subscribe<Ping>(e => log.Add($"b{e.Value}"));

        bus.Publish(new Ping(1));

        Assert.Equal(["a1", "b1"], log);
    }

    [Fact]
    public void Subscribe_ToIEvent_ReceivesEverything()
    {
        var bus = new EventBus();
        var received = new List<IEvent>();
        bus.Subscribe<IEvent>(received.Add);

        bus.Publish(new Ping(1));
        bus.Publish(new Pong(2));

        Assert.Equal([new Ping(1), new Pong(2)], received);
    }

    [Fact]
    public void DisposingSubscription_StopsDelivery()
    {
        var bus = new EventBus();
        var count = 0;
        var subscription = bus.Subscribe<Ping>(_ => count++);

        bus.Publish(new Ping(1));
        subscription.Dispose();
        subscription.Dispose();
        bus.Publish(new Ping(2));

        Assert.Equal(1, count);
    }

    [Fact]
    public void EventsPublishedDuringDispatch_AreQueuedBreadthFirst()
    {
        var bus = new EventBus();
        var log = new List<string>();
        bus.Subscribe<Ping>(e =>
        {
            log.Add("first handler");
            bus.Publish(new Pong(e.Value));
        });
        bus.Subscribe<Ping>(_ => log.Add("second handler"));
        bus.Subscribe<Pong>(_ => log.Add("pong"));

        bus.Publish(new Ping(1));

        Assert.Equal(["first handler", "second handler", "pong"], log);
    }

    [Fact]
    public void UnsubscribingDuringDispatch_SkipsLaterHandlerForSameEvent()
    {
        var bus = new EventBus();
        var secondCalled = false;
        IDisposable? second = null;
        bus.Subscribe<Ping>(_ => second!.Dispose());
        second = bus.Subscribe<Ping>(_ => secondCalled = true);

        bus.Publish(new Ping(1));

        Assert.False(secondCalled);
    }

    [Fact]
    public void HandlerException_PropagatesAndDropsQueuedEvents()
    {
        var bus = new EventBus();
        var pongs = 0;
        bus.Subscribe<Ping>(_ =>
        {
            bus.Publish(new Pong(1));
            throw new InvalidOperationException("boom");
        });
        bus.Subscribe<Pong>(_ => pongs++);

        Assert.Throws<InvalidOperationException>(() => bus.Publish(new Ping(1)));
        bus.Publish(new Pong(2));

        Assert.Equal(1, pongs);
    }

    [Fact]
    public void SubscriptionGroup_DisposesAllSubscriptions()
    {
        var bus = new EventBus();
        var count = 0;
        var group = new SubscriptionGroup();
        group.Add(bus.Subscribe<Ping>(_ => count++));
        group.Add(bus.Subscribe<IEvent>(_ => count++));

        group.Dispose();
        bus.Publish(new Ping(1));

        Assert.Equal(0, count);
    }
}
