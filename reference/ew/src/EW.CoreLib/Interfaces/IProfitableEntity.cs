using EW.CoreLib.Types;

namespace EW.CoreLib.Interfaces;

public interface IProfitableEntity
{
    IReadOnlyDictionary<ResourceType, int> GetTurnIncome();
    void OnOwnerTurnStarted(GamePlayer activePlayer);
}