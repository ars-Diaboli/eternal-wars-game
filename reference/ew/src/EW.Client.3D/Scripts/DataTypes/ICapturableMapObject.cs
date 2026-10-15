using System.Collections.Generic;
using EW.Scripts.DataTypes.Enums;
using EW.Scripts.Players;

namespace EW.Scripts.DataTypes;

public interface ICapturableMapObject : IInteractableMapObject
{
    GamePlayer OwnerPlayer { get; }
    IReadOnlyDictionary<ResourceType, int> GetTurnIncome();
    void OnOwnerTurnStarted(GamePlayer activePlayer);
}
