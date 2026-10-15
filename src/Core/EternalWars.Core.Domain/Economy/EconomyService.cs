using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Economy;

public sealed class EconomyService(GameState state, IEventPublisher events)
{
    /// <summary>Placeholder until towns exist.</summary>
    public static ResourceSet BaseDailyIncome { get; } = new(Gold: 500);

    /// <summary>What <paramref name="playerId"/> receives at the start of each of their turns.</summary>
    public ResourceSet DailyIncome(PlayerId playerId) =>
        state.ObjectsOf<Mine>()
            .Where(m => m.Owner == playerId)
            .Aggregate(BaseDailyIncome, (total, mine) => total + ResourceSet.Of(mine.Resource, mine.DailyYield));

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

    internal void CollectDailyIncome(PlayerId playerId) => Grant(playerId, DailyIncome(playerId));
}
