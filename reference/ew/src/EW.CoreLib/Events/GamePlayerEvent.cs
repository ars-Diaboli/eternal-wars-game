using EW.CoreLib.Types.Events;

namespace EW.CoreLib.Events;

public class GamePlayerEvent : IGameEvent
{
    public required GamePlayer Player { get; init; }
}