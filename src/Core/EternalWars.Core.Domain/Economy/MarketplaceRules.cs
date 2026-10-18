using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Economy;

public enum TradeDirection
{
    /// <summary>The player pays gold for resource units.</summary>
    Buy,

    /// <summary>The player gives resource units for gold.</summary>
    Sell,
}

/// <summary>Gold for one unit of a resource when buying it or selling it.</summary>
public readonly record struct MarketRate(int BuyPrice, int SellPrice);

/// <summary>
/// Marketplace trading (ADR-067, draft numbers). Gold is traded against one resource at a time. X is the number of the
/// player's towns that have a marketplace, a town with a governor counting 1.5; the factor is <c>min(X + 1, 10)</c>.
/// Buying one unit costs <c>base / factor</c> (basic 5000, magic 10000, rounded up), selling one gives
/// <c>base * factor</c> (basic 12.5, magic 25, rounded down). A map marketplace trades as if X were 4.
/// Everything is computed in half towns, so a governed town (3 halves) needs no fractions.
/// </summary>
public static class MarketplaceRules
{
    /// <summary>X the map marketplace trades at.</summary>
    public const int MapMarketplaceTowns = 4;

    private const int BasicBuyBase = 5000;
    private const int MagicBuyBase = 10000;

    /// <summary>12.5 gold, in quarters.</summary>
    private const int BasicSellQuarters = 50;

    /// <summary>25 gold, in quarters.</summary>
    private const int MagicSellQuarters = 100;

    private const int MaxFactor = 10;

    /// <summary>Lumber and ore; gold is not traded.</summary>
    public static bool IsBasic(ResourceType resource) => resource is ResourceType.Lumber or ResourceType.Ore;

    public static bool IsTradable(ResourceType resource) => resource != ResourceType.Gold;

    /// <summary>Marketplace towns in halves: 2 for a town, 3 for a governed town.</summary>
    public static int MarketplaceHalves(GameState state, PlayerId player)
    {
        var halves = 0;
        foreach (var town in state.ObjectsOf<Town>())
        {
            if (town.Owner == player && town.Buildings.GetValueOrDefault(BuiltInBuildings.Marketplace) > 0)
            {
                halves += StewardshipRules.GovernorOf(state, town) is null ? 2 : 3;
            }
        }

        return halves;
    }

    /// <summary>The player has a town with a marketplace, so it may trade.</summary>
    public static bool CanTrade(GameState state, PlayerId player) => MarketplaceHalves(state, player) > 0;

    /// <summary>The player's rate for a resource from its marketplace towns; null when it has none.</summary>
    public static MarketRate? TownRate(GameState state, PlayerId player, ResourceType resource) =>
        MarketplaceHalves(state, player) is > 0 and var halves ? RateAtHalves(resource, halves) : null;

    /// <summary>The fixed rate of a map marketplace.</summary>
    public static MarketRate MapRate(ResourceType resource) => RateAtHalves(resource, MapMarketplaceTowns * 2);

    /// <summary>The rate with X given in half towns.</summary>
    public static MarketRate RateAtHalves(ResourceType resource, int halves)
    {
        if (!IsTradable(resource))
        {
            throw new ArgumentOutOfRangeException(nameof(resource), resource, "Gold is not traded.");
        }

        // factor = min(X + 1, 10) = min(halves + 2, 20) / 2
        var factorHalves = Math.Min(halves + 2, MaxFactor * 2);
        var basic = IsBasic(resource);
        var buy = ((basic ? BasicBuyBase : MagicBuyBase) * 2 + factorHalves - 1) / factorHalves;
        var sell = (basic ? BasicSellQuarters : MagicSellQuarters) * factorHalves / 8;
        return new MarketRate(buy, sell);
    }
}
