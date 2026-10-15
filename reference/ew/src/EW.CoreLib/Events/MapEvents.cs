using EW.CoreLib.Interfaces;
using EW.CoreLib.Types;
using EW.CoreLib.Types.Events;

namespace EW.CoreLib.Events;

public sealed class MapObjectRemoved : IGameEvent
{
    public required IWorldMapObject MapObject { get; init; }
}

public sealed class MapActorPlaced : IGameEvent
{
    public required Hero Hero { get; init; }
    public required GamePlayer Player { get; init; }
}

public sealed class MapActorRemoved : IGameEvent
{
    public required Hero Hero { get; init; }
    public required GamePlayer Player { get; init; }
}

public sealed class MapActorPositionChanged : IGameEvent
{
    public required Hero Hero { get; init; }
    public required GamePlayer Player { get; init; }
    public required Position PreviousPosition { get; init; }
}
