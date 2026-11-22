using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Economy;

public sealed class EconomyService(GameState state, IEventPublisher events, IReadOnlyList<IIncomeSource>? incomeSources = null)
    : Turns.ITurnPhaseParticipant
{
    private readonly IReadOnlyList<IIncomeSource> _incomeSources = incomeSources ?? DefaultIncomeSources;

    /// <summary>Income sources that exist today; artifacts add theirs later.</summary>
    public static IReadOnlyList<IIncomeSource> DefaultIncomeSources { get; } = [new MineIncome(), new TownIncome(), new HeroIncome()];

    /// <summary>What <paramref name="playerId"/> receives at the start of each of their turns.</summary>
    public ResourceSet DailyIncome(PlayerId playerId) =>
        _incomeSources.Aggregate(ResourceSet.Empty, (total, source) => total + source.DailyIncome(state, playerId));

    /// <summary>The game's difficulty sets every player's starting resources (when the game has one).</summary>
    void Turns.ITurnPhaseParticipant.SetUpGame()
    {
        if (state.Options.Difficulty is { } difficulty)
        {
            foreach (var player in state.Players)
            {
                Set(player.Id, StartingResources.For(difficulty));
            }
        }
    }

    void Turns.ITurnPhaseParticipant.StockFirstWeek() => StockProviders();

    void Turns.ITurnPhaseParticipant.StockWeek() => StockProviders();

    void Turns.ITurnPhaseParticipant.PrepareTurn(PlayerId player) => CollectDailyIncome(player);

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

    internal void Grant(PlayerId playerId, ResourceSet amount)
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

    internal bool TrySpend(PlayerId playerId, ResourceSet cost)
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
            throw new CommandRefusedException("Gold cannot be traded.");
        }

        if (amount <= 0)
        {
            throw new CommandRefusedException("Trade a positive amount.");
        }

        MarketRate rate;
        if (mapMarketplace is { } id)
        {
            var marketplace = state.FindObject(id) as Marketplace ?? throw new CommandRefusedException($"{id} is not a marketplace.");
            if (state.HeroAt(marketplace.VisitPosition) is not { } hero || hero.Owner != playerId)
            {
                throw new CommandRefusedException("Trading at a map marketplace needs one of your heroes on its entrance.");
            }

            rate = MarketplaceRules.MapRate(resource);
        }
        else
        {
            rate = MarketplaceRules.TownRate(state, playerId, resource)
                ?? throw new CommandRefusedException("Trading needs a town with a marketplace.");
        }

        var player = state.GetPlayer(playerId);
        var gold = (long)amount * (direction == TradeDirection.Buy ? rate.BuyPrice : rate.SellPrice);
        if (gold > int.MaxValue)
        {
            throw new CommandRefusedException("That trade is too large.");
        }

        if (direction == TradeDirection.Buy ? player.Resources.Gold < gold : player.Resources[resource] < amount)
        {
            throw new CommandRefusedException(direction == TradeDirection.Buy ? "Not enough gold." : $"Not enough {resource}.");
        }

        var sign = direction == TradeDirection.Buy ? 1 : -1;
        var previous = player.Resources;
        player.Resources = previous + ResourceSet.Of(resource, sign * amount) - new ResourceSet(Gold: sign * (int)gold);
        events.Publish(new ResourcesChanged(playerId, previous, player.Resources));
        events.Publish(new ResourcesTraded(playerId, resource, direction, amount, (int)gold, mapMarketplace));
    }

    /// <summary>
    /// One resource for another (ADR-296): <paramref name="amount"/> units of <paramref name="get"/> for <see cref="MarketplaceRules.ExchangeCost"/>
    /// units of <paramref name="give"/>, at the rate of the player's marketplace towns or of the map marketplace one of its heroes stands on.
    /// </summary>
    internal void Exchange(PlayerId playerId, ResourceType give, ResourceType get, int amount, MapObjectId? mapMarketplace)
    {
        if (!MarketplaceRules.IsTradable(give) || !MarketplaceRules.IsTradable(get))
        {
            throw new CommandRefusedException("Trade gold with the buy and sell trades.");
        }

        if (give == get)
        {
            throw new CommandRefusedException("Pick two different resources.");
        }

        if (amount <= 0)
        {
            throw new CommandRefusedException("Trade a positive amount.");
        }

        var (giveRate, getRate) = mapMarketplace is { } id ? (MapRateFor(playerId, id, give), MarketplaceRules.MapRate(get)) : (TownRateFor(playerId, give), TownRateFor(playerId, get));
        var cost = MarketplaceRules.ExchangeCost(giveRate, getRate, amount);
        var player = state.GetPlayer(playerId);
        if (cost > player.Resources[give])
        {
            throw new CommandRefusedException($"Not enough {give}.");
        }

        var previous = player.Resources;
        player.Resources = previous - ResourceSet.Of(give, (int)cost) + ResourceSet.Of(get, amount);
        events.Publish(new ResourcesChanged(playerId, previous, player.Resources));
        events.Publish(new ResourcesExchanged(playerId, give, (int)cost, get, amount, mapMarketplace));
    }

    /// <summary>Sends resources to a teammate (ADR-296): the sender needs a town with a marketplace and the resources; enemies and itself are refused.</summary>
    internal void Send(PlayerId from, PlayerId to, ResourceSet amount)
    {
        if (!state.Players.Any(p => p.Id == to) || !state.AreAllies(from, to))
        {
            throw new CommandRefusedException("Resources go only to a teammate.");
        }

        if (state.GetPlayer(to).IsDefeated)
        {
            throw new CommandRefusedException($"{state.GetPlayer(to).Name} is out of the game.");
        }

        if (Enum.GetValues<ResourceType>().Any(t => amount[t] < 0) || amount == ResourceSet.Empty)
        {
            throw new CommandRefusedException("Send a positive amount.");
        }

        if (!MarketplaceRules.CanTrade(state, from))
        {
            throw new CommandRefusedException("Sending resources needs a town with a marketplace.");
        }

        if (!state.GetPlayer(from).Resources.CanAfford(amount))
        {
            throw new CommandRefusedException("Not enough resources.");
        }

        var sender = state.GetPlayer(from);
        var before = sender.Resources;
        sender.Resources = before - amount;
        events.Publish(new ResourcesChanged(from, before, sender.Resources));
        Grant(to, amount);
        events.Publish(new ResourcesSent(from, to, amount));
    }

    private MarketRate TownRateFor(PlayerId playerId, ResourceType resource) =>
        MarketplaceRules.TownRate(state, playerId, resource) ?? throw new CommandRefusedException("Trading needs a town with a marketplace.");

    private MarketRate MapRateFor(PlayerId playerId, MapObjectId id, ResourceType resource)
    {
        var marketplace = state.FindObject(id) as Marketplace ?? throw new CommandRefusedException($"{id} is not a marketplace.");
        if (state.HeroAt(marketplace.VisitPosition) is not { } hero || hero.Owner != playerId)
        {
            throw new CommandRefusedException("Trading at a map marketplace needs one of your heroes on its entrance.");
        }

        return MarketplaceRules.MapRate(resource);
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
