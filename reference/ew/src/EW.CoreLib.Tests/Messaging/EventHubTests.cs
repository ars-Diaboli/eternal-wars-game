using EW.CoreLib.Events;
using EW.CoreLib.Interfaces;
using EW.CoreLib.Messaging;
using EW.CoreLib.Types;
using EW.CoreLib.Types.Events;
using NUnit.Framework;

namespace EW.CoreLib.Tests.Messaging;

public class EventHubTests
{
    [Test]
    public void Publish_GamePlayerEvent_OnlyNotifiesMatchingPlayerAndOwnedSubscribers()
    {
        var hub = new EventHub();
        var playerA = CreatePlayerSubscriber("Player A", PlayerColor.Red);
        var playerB = CreatePlayerSubscriber("Player B", PlayerColor.Blue);
        var ownedByA = new TestOwnedSubscriber { OwnerPlayer = playerA };
        var ownedByB = new TestOwnedSubscriber { OwnerPlayer = playerB };
        var globalSubscriber = new TestGlobalSubscriber();

        hub.Subscribe(playerA);
        hub.Subscribe(playerB);
        hub.Subscribe(ownedByA);
        hub.Subscribe(ownedByB);
        hub.Subscribe(globalSubscriber);

        hub.Publish(new TestPlayerEvent { Player = playerA });

        Assert.Multiple(() =>
        {
            Assert.That(playerA.PlayerEventCount, Is.EqualTo(1));
            Assert.That(ownedByA.PlayerEventCount, Is.EqualTo(1));
            Assert.That(playerB.PlayerEventCount, Is.Zero);
            Assert.That(ownedByB.PlayerEventCount, Is.Zero);
            Assert.That(globalSubscriber.PlayerEventCount, Is.Zero);
        });
    }

    [Test]
    public void Publish_WithOwnableRecipientSource_RoutesToOwner()
    {
        var hub = new EventHub();
        var playerA = CreatePlayerSubscriber("Player A", PlayerColor.Red);
        var playerB = CreatePlayerSubscriber("Player B", PlayerColor.Blue);
        var ownedByA = new TestOwnedSubscriber { OwnerPlayer = playerA };
        var ownedByB = new TestOwnedSubscriber { OwnerPlayer = playerB };
        var globalSubscriber = new TestGlobalSubscriber();

        hub.Subscribe(playerA);
        hub.Subscribe(playerB);
        hub.Subscribe(ownedByA);
        hub.Subscribe(ownedByB);
        hub.Subscribe(globalSubscriber);

        hub.Publish(new BroadcastEvent(), ownedByB);

        Assert.Multiple(() =>
        {
            Assert.That(playerA.BroadcastEventCount, Is.Zero);
            Assert.That(ownedByA.BroadcastEventCount, Is.Zero);
            Assert.That(playerB.BroadcastEventCount, Is.EqualTo(1));
            Assert.That(ownedByB.BroadcastEventCount, Is.EqualTo(1));
            Assert.That(globalSubscriber.BroadcastEventCount, Is.Zero);
        });
    }

    [Test]
    public void Publish_WithoutRecipient_StillBroadcastsToAllSubscribers()
    {
        var hub = new EventHub();
        var playerA = CreatePlayerSubscriber("Player A", PlayerColor.Red);
        var playerB = CreatePlayerSubscriber("Player B", PlayerColor.Blue);
        var ownedByA = new TestOwnedSubscriber { OwnerPlayer = playerA };
        var globalSubscriber = new TestGlobalSubscriber();

        hub.Subscribe(playerA);
        hub.Subscribe(playerB);
        hub.Subscribe(ownedByA);
        hub.Subscribe(globalSubscriber);

        hub.Publish(new BroadcastEvent());

        Assert.Multiple(() =>
        {
            Assert.That(playerA.BroadcastEventCount, Is.EqualTo(1));
            Assert.That(playerB.BroadcastEventCount, Is.EqualTo(1));
            Assert.That(ownedByA.BroadcastEventCount, Is.EqualTo(1));
            Assert.That(globalSubscriber.BroadcastEventCount, Is.EqualTo(1));
        });
    }

    private static TestPlayerSubscriber CreatePlayerSubscriber(string displayName, PlayerColor playerColor)
    {
        return new TestPlayerSubscriber
        {
            DisplayName = displayName,
            PlayerColor = playerColor,
            ControlType = PlayerControlType.Human
        };
    }

    private sealed class TestPlayerEvent : GamePlayerEvent;

    private sealed class BroadcastEvent : IGameEvent;

    private sealed class TestPlayerSubscriber : GamePlayer, IHandleEvent<TestPlayerEvent>, IHandleEvent<BroadcastEvent>
    {
        public int PlayerEventCount { get; private set; }
        public int BroadcastEventCount { get; private set; }

        public void Handle(TestPlayerEvent e)
        {
            PlayerEventCount++;
        }

        public void Handle(BroadcastEvent e)
        {
            BroadcastEventCount++;
        }
    }

    private sealed class TestOwnedSubscriber : IOwnableObject, IHandleEvent<TestPlayerEvent>, IHandleEvent<BroadcastEvent>
    {
        public required GamePlayer OwnerPlayer { get; init; }
        public int PlayerEventCount { get; private set; }
        public int BroadcastEventCount { get; private set; }

        public void Handle(TestPlayerEvent e)
        {
            PlayerEventCount++;
        }

        public void Handle(BroadcastEvent e)
        {
            BroadcastEventCount++;
        }
    }

    private sealed class TestGlobalSubscriber : IHandleEvent<TestPlayerEvent>, IHandleEvent<BroadcastEvent>
    {
        public int PlayerEventCount { get; private set; }
        public int BroadcastEventCount { get; private set; }

        public void Handle(TestPlayerEvent e)
        {
            PlayerEventCount++;
        }

        public void Handle(BroadcastEvent e)
        {
            BroadcastEventCount++;
        }
    }
}
