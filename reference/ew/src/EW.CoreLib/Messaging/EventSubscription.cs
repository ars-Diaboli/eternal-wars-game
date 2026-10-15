namespace EW.CoreLib.Messaging;

public record EventSubscription(object Subscriber, Action<object> Invocation);

