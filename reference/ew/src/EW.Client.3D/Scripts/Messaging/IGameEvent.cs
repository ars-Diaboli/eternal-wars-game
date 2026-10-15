using EW.Scripts.Heroes;
using EW.Scripts.Players;

namespace EW.Scripts.Messaging;

public interface IGameEvent { }

public class GamePlayerEvent : IGameEvent
{
    public GamePlayer Player { get; init; }
}

public class PlayerHeroEvent : GamePlayerEvent
{
    public Hero Hero { get; init; }
}