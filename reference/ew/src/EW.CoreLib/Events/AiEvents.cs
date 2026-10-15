using EW.CoreLib.Types.Events;

namespace EW.CoreLib.Events;

public class AiTurnStarted : IGameEvent
{
    public GamePlayer Player { get; init; }
}

public class PlayerEndedTurn : IGameEvent
{
    public GamePlayer Player { get; init; }
}
