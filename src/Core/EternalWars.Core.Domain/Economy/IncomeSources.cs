using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Economy;

/// <summary>
/// One kind of daily income: mines now; towns, hero skills (Estates-like), hero specialisations and
/// artifacts later. There is no base income: a player earns only what their sources produce.
/// </summary>
public interface IIncomeSource
{
    ResourceSet DailyIncome(GameState state, PlayerId player);
}

/// <summary>Every mine pays its daily yield to its owner.</summary>
public sealed class MineIncome : IIncomeSource
{
    public ResourceSet DailyIncome(GameState state, PlayerId player) =>
        state.ObjectsOf<Mine>()
            .Where(m => m.Owner == player)
            .Aggregate(ResourceSet.Empty, (total, mine) => total + ResourceSet.Of(mine.Resource, mine.DailyYield));
}
