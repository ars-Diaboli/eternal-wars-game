namespace EW.CoreLib.Interfaces;

public interface IOwnableObject
{
    GamePlayer? OwnerPlayer { get; }
}