using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Economy;

public sealed class EconomyService(GameState state, IEventPublisher events, IReadOnlyList<IIncomeSource>? incomeSources = null)
{
    private readonly IReadOnlyList<IIncomeSource> _incomeSources = incomeSources ?? DefaultIncomeSources;

    /// <summary>Income sources that exist today. Towns, skills, specialisations and artifacts add theirs later.</summary>
    public static IReadOnlyList<IIncomeSource> DefaultIncomeSources { get; } = [new MineIncome()];

    /// <summary>What <paramref name="playerId"/> receives at the start of each of their turns.</summary>
    public ResourceSet DailyIncome(PlayerId playerId) =>
        _incomeSources.Aggregate(ResourceSet.Empty, (total, source) => total + source.DailyIncome(state, playerId));

    public void Grant(PlayerId playerId, ResourceSet amount)
    {
        var player = state.GetPlayer(playerId);
        var previous = player.Resources;
        player.Resources = previous + amount;
        events.Publish(new ResourcesChanged(playerId, previous, player.Resources));
    }

    public bool TrySpend(PlayerId playerId, ResourceSet cost)
    {
        var player = state.GetPlayer(playerId);
        if (!player.Resources.CanAfford(cost))
        {
            return false;
        }

        var previous = player.Resources;
        player.Resources = previous - cost;
        events.Publish(new ResourcesChanged(playerId, previous, player.Resources));
        return true;
    }

    internal void CollectDailyIncome(PlayerId playerId)
    {
        var income = DailyIncome(playerId);
        if (income != ResourceSet.Empty)
        {
            Grant(playerId, income);
        }
    }
}
