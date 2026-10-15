using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Objects;

/// <summary>What happens when a hero visits a map object, and the follow-up decisions it opens.</summary>
public sealed class MapObjectInteractionService(GameState state, IEventPublisher events, EconomyService economy)
{
    public void ResolveTreasureChest(TreasureChestReward reward)
    {
        if (state.PendingDecision is not TreasureChestDecision decision)
        {
            throw new InvalidOperationException("No treasure chest decision is pending.");
        }

        state.PendingDecision = null;
        RemoveFromMap(state.GetObject(decision.Chest));

        var hero = state.GetHero(decision.Hero);
        if (reward == TreasureChestReward.Gold)
        {
            economy.Grant(hero.Owner, new ResourceSet(Gold: decision.Gold));
        }
        else
        {
            hero.Experience += decision.Experience;
            events.Publish(new HeroExperienceGained(hero.Id, decision.Experience, hero.Experience));
        }

        events.Publish(new DecisionResolved(decision));
    }

    internal void Visit(Hero hero, MapObject mapObject)
    {
        events.Publish(new MapObjectVisited(hero.Id, mapObject.Id));
        switch (mapObject)
        {
            case ResourcePile pile:
                RemoveFromMap(pile);
                economy.Grant(hero.Owner, ResourceSet.Of(pile.Resource, pile.Amount));
                events.Publish(new ResourcePileCollected(hero.Id, pile.Id, pile.Resource, pile.Amount));
                break;

            case Mine mine when mine.Owner != hero.Owner:
                var previous = mine.Owner;
                mine.Owner = hero.Owner;
                events.Publish(new MineOwnerChanged(mine.Id, previous, hero.Owner));
                break;

            case TreasureChest chest:
                var decision = new TreasureChestDecision(hero.Id, chest.Id, chest.Gold, chest.Experience);
                state.PendingDecision = decision;
                events.Publish(new DecisionRequested(decision));
                break;

            case { TeleportTarget: not null }:
                // Two-way partner or one-way exit; a hero standing on the landing tile blocks the jump.
                if (state.TeleportDestination(mapObject) is { } exit && state.HeroAt(exit) is null)
                {
                    var from = hero.Position;
                    hero.Position = exit;
                    events.Publish(new HeroTeleported(hero.Id, from, exit));
                }

                break;
        }
    }

    /// <summary>Closes the exchange screen opened by a meeting of two own heroes.</summary>
    public void CompleteExchange()
    {
        if (state.PendingDecision is not HeroExchangeDecision decision)
        {
            throw new InvalidOperationException("No hero exchange is open.");
        }

        state.PendingDecision = null;
        events.Publish(new DecisionResolved(decision));
    }

    internal void Meet(Hero hero, Hero other)
    {
        var decision = new HeroExchangeDecision(hero.Id, other.Id);
        state.PendingDecision = decision;
        events.Publish(new HeroesMet(hero.Id, other.Id));
        events.Publish(new DecisionRequested(decision));
    }

    /// <summary>Battle hook (Phase 2 resolves it); nothing on the map changes yet.</summary>
    internal void StartBattle(Hero attacker, Hero defender, bool isAmbush) =>
        events.Publish(new BattleTriggered(attacker.Id, defender.Id, isAmbush));

    private void RemoveFromMap(MapObject mapObject)
    {
        state.RemoveObject(mapObject);
        events.Publish(new MapObjectRemoved(mapObject.Id));
    }
}
