namespace EW.CoreLib.Events;

public class PlayerHeroEvent : GamePlayerEvent
{
    public required Hero Hero { get; init; }
}