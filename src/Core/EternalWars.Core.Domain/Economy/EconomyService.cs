using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Economy;

public sealed class EconomyService(GameState state, IEventPublisher events, IReadOnlyList<IIncomeSource>? incomeSources = null)
{
    private readonly IReadOnlyList<IIncomeSource> _incomeSources = incomeSources ?? DefaultIncomeSources;

    /// <summary>Income sources that exist today; artifacts add theirs later.</summary>
    public static IReadOnlyList<IIncomeSource> DefaultIncomeSources { get; } = [new MineIncome(), new TownIncome(), new HeroIncome()];

    /// <summary>What <paramref name="playerId"/> receives at the start of each of their turns.</summary>
    public ResourceSet DailyIncome(PlayerId playerId) =>
        _incomeSources.Aggregate(ResourceSet.Empty, (total, source) => total + source.DailyIncome(state, playerId));

    /// <summary>
    /// The start of a week, and of the game: every flagged resource provider adds a week's output to what waits there (ADR-079, TOWN-9).
    /// </summary>
    internal void StockProviders()
    {
        foreach (var provider in state.ObjectsOf<ResourceProvider>().Where(p => p.Owner is not null).OrderBy(p => p.Id.Value))
        {
            provider.Stored += ResourceProviderRules.Roll(provider.Definition, state.Date, state.Random);
            events.Publish(new ProviderStockChanged(provider.Id, provider.Stored));
        }
    }

    public void Grant(PlayerId playerId, ResourceSet amount)
    {
        var player = state.GetPlayer(playerId);
        var previous = player.Resources;
        player.Resources = previous + amount;
        events.Publish(new ResourcesChanged(playerId, previous, player.Resources));
    }

    /// <summary>Sets the player's resources outright (the scenario's complexity at the start of a game).</summary>
    internal void Set(PlayerId playerId, ResourceSet amount)
    {
        var player = state.GetPlayer(playerId);
        var previous = player.Resources;
        player.Resources = amount;
        events.Publish(new ResourcesChanged(playerId, previous, amount));
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

    /// <summary>
    /// A marketplace trade (ADR-067): validates, then moves the gold and the resource. A map marketplace needs one of the player's
    /// heroes on its entrance and trades at the fixed rate; otherwise the player needs a town with a marketplace.
    /// </summary>
    internal void Trade(PlayerId playerId, ResourceType resource, TradeDirection direction, int amount, MapObjectId? mapMarketplace)
    {
        if (!MarketplaceRules.IsTradable(resource))
        {
            throw new InvalidOperationException("Gold cannot be traded.");
        }

        if (amount <= 0)
        {
            throw new InvalidOperationException("Trade a positive amount.");
        }

        MarketRate rate;
        if (mapMarketplace is { } id)
        {
            var marketplace = state.FindObject(id) as Marketplace ?? throw new InvalidOperationException($"{id} is not a marketplace.");
            if (state.HeroAt(marketplace.VisitPosition) is not { } hero || hero.Owner != playerId)
            {
                throw new InvalidOperationException("Trading at a map marketplace needs one of your heroes on its entrance.");
            }

            rate = MarketplaceRules.MapRate(resource);
        }
        else
        {
            rate = MarketplaceRules.TownRate(state, playerId, resource)
                ?? throw new InvalidOperationException("Trading needs a town with a marketplace.");
        }

        var player = state.GetPlayer(playerId);
        var gold = (long)amount * (direction == TradeDirection.Buy ? rate.BuyPrice : rate.SellPrice);
        if (gold > int.MaxValue)
        {
            throw new InvalidOperationException("That trade is too large.");
        }

        if (direction == TradeDirection.Buy ? player.Resources.Gold < gold : player.Resources[resource] < amount)
        {
            throw new InvalidOperationException(direction == TradeDirection.Buy ? "Not enough gold." : $"Not enough {resource}.");
        }

        var sign = direction == TradeDirection.Buy ? 1 : -1;
        var previous = player.Resources;
        player.Resources = previous + ResourceSet.Of(resource, sign * amount) - new ResourceSet(Gold: sign * (int)gold);
        events.Publish(new ResourcesChanged(playerId, previous, player.Resources));
        events.Publish(new ResourcesTraded(playerId, resource, direction, amount, (int)gold, mapMarketplace));
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
