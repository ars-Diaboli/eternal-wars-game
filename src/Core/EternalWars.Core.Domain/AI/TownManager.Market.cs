using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The AI at the marketplace (ADR-189), through <see cref="TradeResourceCommand"/> like a player, at its towns' rate (ADR-067). Trading is
/// dear, so it is only the last step to the building a town is saving for: it buys the resources that building lacks when its income would
/// not bring them within <see cref="AiWeights.TradeWaitDays"/> days and the gold covers the building and the trade, the trade taking at most
/// <see cref="AiWeights.TradeTreasuryPercent"/> % of the gold; or, when gold alone is missing, it sells what it holds beyond the building's needs and a
/// stock it keeps (<see cref="AiWeights.KeepBasic"/>, <see cref="AiWeights.KeepMagic"/>). It never trades for troops, and it does not visit map marketplaces.
/// </summary>
internal sealed partial class TownManager
{
    /// <summary>
    /// The gold that buying what <paramref name="have"/> lacks of <paramref name="cost"/> would take at the player's rate; null when it has no
    /// marketplace town, 0 when nothing but gold is missing.
    /// </summary>
    private long? BuyingGold(PlayerId player, ResourceSet cost, ResourceSet have)
    {
        if (!MarketplaceRules.CanTrade(_state, player))
        {
            return null;
        }

        long gold = 0;
        foreach (var resource in Enum.GetValues<ResourceType>().Where(MarketplaceRules.IsTradable))
        {
            var missing = cost[resource] - have[resource];
            if (missing > 0)
            {
                gold += (long)missing * MarketplaceRules.TownRate(_state, player, resource)!.Value.BuyPrice;
            }
        }

        return gold;
    }

    /// <summary>Whether buying the missing resources makes <paramref name="cost"/> worth saving for (the gold within <see cref="AiWeights.SavingDays"/> days).</summary>
    private bool Buyable(PlayerId player, ResourceSet cost, ResourceSet have, ResourceSet income)
    {
        if (BuyingGold(player, cost, have) is not { } gold)
        {
            return false;
        }

        var treasury = (long)have.Gold + (income.Gold * _weights.SavingDays);
        return cost.Gold + gold <= treasury && gold * 100 <= treasury * _weights.TradeTreasuryPercent;
    }

    /// <summary>
    /// Trades so that <paramref name="cost"/> is affordable now, when it is worth it (the class remarks); true when the player can then pay it.
    /// </summary>
    private bool TradeFor(PlayerId player, ResourceSet cost, ResourceSet income)
    {
        var have = _state.GetPlayer(player).Resources;
        if (have.CanAfford(cost) || !MarketplaceRules.CanTrade(_state, player))
        {
            return have.CanAfford(cost);
        }

        var goods = Enum.GetValues<ResourceType>().Where(MarketplaceRules.IsTradable).ToList();
        if (goods.Any(r => have[r] < cost[r]))
        {
            // A resource the income brings soon is waited for; otherwise buy all that is missing, if the gold covers it within the limit.
            if (goods.All(r => have[r] + (income[r] * _weights.TradeWaitDays) >= cost[r]) || BuyingGold(player, cost, have) is not { } gold
                || cost.Gold + gold > have.Gold || gold * 100 > (long)have.Gold * _weights.TradeTreasuryPercent)
            {
                return false;
            }

            foreach (var resource in goods.Where(r => have[r] < cost[r]))
            {
                if (!_ai.Execute(new TradeResourceCommand(player, resource, TradeDirection.Buy, cost[resource] - have[resource])).IsAccepted)
                {
                    return false;
                }
            }

            return _state.GetPlayer(player).Resources.CanAfford(cost);
        }

        // Only gold is missing: sell the spare stock, if it covers the whole shortfall.
        var shortfall = cost.Gold - have.Gold;
        var sales = new List<(ResourceType Resource, int Units)>();
        foreach (var resource in goods.OrderByDescending(r => (long)Spare(r) * Rate(r)).ThenBy(r => r))
        {
            if (shortfall <= 0)
            {
                break;
            }

            var units = Math.Min(Spare(resource), (shortfall + Rate(resource) - 1) / Rate(resource));
            if (units > 0)
            {
                sales.Add((resource, units));
                shortfall -= units * Rate(resource);
            }
        }

        if (shortfall > 0)
        {
            return false;
        }

        foreach (var (resource, units) in sales)
        {
            if (!_ai.Execute(new TradeResourceCommand(player, resource, TradeDirection.Sell, units)).IsAccepted)
            {
                return false;
            }
        }

        return _state.GetPlayer(player).Resources.CanAfford(cost);

        int Spare(ResourceType resource) => Math.Max(0, have[resource] - cost[resource] - (MarketplaceRules.IsBasic(resource) ? _weights.KeepBasic : _weights.KeepMagic));
        int Rate(ResourceType resource) => Math.Max(1, MarketplaceRules.TownRate(_state, player, resource)!.Value.SellPrice);
    }
}
