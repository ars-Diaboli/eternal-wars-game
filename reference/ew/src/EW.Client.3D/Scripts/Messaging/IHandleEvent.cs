namespace EW.Scripts.Messaging;

public interface IHandleEvent<in TEvent> where TEvent : IGameEvent
{
    void Handle(TEvent eventData);
}