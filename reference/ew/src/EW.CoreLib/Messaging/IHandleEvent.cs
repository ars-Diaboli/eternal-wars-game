using EW.CoreLib.Types.Events;

namespace EW.CoreLib.Messaging;

public interface IHandleEvent<in TEvent> where TEvent : IGameEvent
{
    void Handle(TEvent e);
}