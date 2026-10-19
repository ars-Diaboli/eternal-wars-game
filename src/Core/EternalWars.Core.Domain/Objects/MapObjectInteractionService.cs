using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Objects;

/// <summary>What happens when a hero visits a map object or meets a hero, and the follow-up decisions it opens.</summary>
public sealed class MapObjectInteractionService(
    GameState state,
    IEventPublisher events,
    EconomyService economy,
    BattleService battles,
    GuardService guards,
    ProgressionService progression,
    ArtifactService artifacts)
{
    internal void ResolveTreasureChest(TreasureChestReward reward)
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
        events.Publish(new DecisionResolved(decision));
        if (reward == TreasureChestReward.Experience)
        {
            progression.GrantExperience(hero, decision.Experience);
        }
    }

    /// <summary>The visitor of a warehouse takes one of the offered options (ADR-068).</summary>
    internal void ResolveResourceChoice(int option)
    {
        if (state.PendingDecision is not ResourceChoiceDecision decision)
        {
            throw new InvalidOperationException("No resource choice is pending.");
        }

        if (option < 0 || option >= decision.Options.Count)
        {
            throw new InvalidOperationException($"Option {option} is not offered.");
        }

        state.PendingDecision = null;
        var hero = state.GetHero(decision.Hero);
        GiveResources(hero, state.GetObject(decision.Provider), decision.Options[option]);
        events.Publish(new DecisionResolved(decision));
    }

    /// <summary>
    /// Takes the reward of an object with a limited <see cref="VisitScope"/> for the hero (ADR-068): false when the hero may not collect now,
    /// else the visit is recorded and <see cref="MapObjectCollected"/> published, before the caller hands out the reward.
    /// </summary>
    internal bool TryCollect(Hero hero, MapObject mapObject)
    {
        if (!mapObject.CanCollect(hero.Id, state.Date))
        {
            return false;
        }

        mapObject.Visits.Record(hero.Id, state.Date);
        events.Publish(new MapObjectCollected(hero.Id, mapObject.Id));
        return true;
    }

    internal void Visit(Hero hero, MapObject mapObject)
    {
        events.Publish(new MapObjectVisited(hero.Id, mapObject.Id));
        switch (mapObject)
        {
            case ResourcePile pile:
                RemoveFromMap(pile);
                var chance = SkillEffects.ProspectingPileChancePercent(hero.SkillLevelOf(BuiltInSkills.Prospecting));
                var amount = chance > 0 && state.Random.Next(100) < chance ? pile.Amount + (pile.Amount + 1) / 2 : pile.Amount;
                economy.Grant(hero.Owner, ResourceSet.Of(pile.Resource, amount));
                events.Publish(new ResourcePileCollected(hero.Id, pile.Id, pile.Resource, amount));
                break;

            case Mine { Guard: not null } guarded:
                guards.Encounter(hero, guarded);
                break;

            case Mine mine when mine.Owner != hero.Owner:
                var previous = mine.Owner;
                mine.Owner = hero.Owner;
                events.Publish(new MineOwnerChanged(mine.Id, previous, hero.Owner));
                break;

            case TreasureChest chest:
                var values = chest.FixedValues ?? TreasureChest.RollTable[state.Random.Next(TreasureChest.RollTable.Count)];
                var decision = new TreasureChestDecision(hero.Id, chest.Id, values.Gold, values.Experience);
                state.PendingDecision = decision;
                events.Publish(new DecisionRequested(decision));
                break;

            case MonsterGuard monster:
                guards.Encounter(hero, monster);
                break;

            case ResourceProvider provider when !provider.CanCollect(hero.Id, state.Date):
                events.Publish(new MapObjectFoundEmpty(hero.Id, provider.Id));
                break;

            case ResourceProvider provider:
                var options = ResourceProviderRules.Roll(provider.Definition, state.Date, state.Random);
                if (options.Count == 1)
                {
                    GiveResources(hero, provider, options[0]);
                }
                else
                {
                    // The week's reward is taken only once the visitor chooses (the decision blocks everything else meanwhile).
                    var choice = new ResourceChoiceDecision(hero.Id, provider.Id, options);
                    state.PendingDecision = choice;
                    events.Publish(new DecisionRequested(choice));
                }

                break;

            case CreatureDwelling dwelling when dwelling.Owner != hero.Owner:
                var before = dwelling.Owner;
                dwelling.Owner = hero.Owner;
                events.Publish(new DwellingOwnerChanged(dwelling.Id, before, hero.Owner));
                break;

            case ArtifactPickup { Artifact: { } found } pickup:
                if (artifacts.Grant(hero, found, ArtifactSource.Pickup))
                {
                    RemoveFromMap(pickup);
                }

                break;

            // Own or allied towns: the hero just stands in the town (an own hero can take recruits and garrison stacks).
            case Town town when town.Owner is { } owner && !state.AreEnemies(hero.Owner, owner):
                TownService.AutoAppoint(state, events, town, hero);
                break;

            case Town { Garrison.IsEmpty: false } town:
                battles.BeginTownBattle(hero, town);
                break;

            case Town town:
                TownService.Capture(events, town, hero.Owner);
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

    /// <summary>A hero on a map dwelling's entrance buys creatures into its army, discounted by its own Stewardship (ADR-054).</summary>
    internal void RecruitAtDwelling(PlayerId player, MapObjectId dwellingId, int count)
    {
        var dwelling = state.FindObject(dwellingId) as CreatureDwelling ?? throw new InvalidOperationException($"{dwellingId} is not a dwelling.");
        if (state.PendingDecision is not null)
        {
            throw new InvalidOperationException("Resolve the pending decision first.");
        }

        var hero = state.HeroAt(dwelling.VisitPosition);
        if (hero is null || hero.Owner != player)
        {
            throw new InvalidOperationException($"No own hero stands at the {dwelling.Definition.Name}.");
        }

        var available = dwelling.Available ?? 0;
        if (count < 1 || count > available)
        {
            throw new InvalidOperationException($"Only {available} creatures wait here.");
        }

        var creature = state.Creatures.Get(dwelling.Creature);
        if (!hero.Army.CanAccept(creature.Id))
        {
            throw new InvalidOperationException($"{hero.Name} has no free slot for {creature.Name}.");
        }

        var price = StewardshipRules.DiscountCreatures(creature.Cost, hero.SkillLevelOf(BuiltInSkills.Stewardship)) * count;
        if (!economy.TrySpend(player, price))
        {
            throw new InvalidOperationException("Not enough resources.");
        }

        dwelling.Available = available - count;
        hero.Army.TryAdd(new CreatureStack(creature.Id, count));
        events.Publish(new DwellingRecruited(dwelling.Id, hero.Id, creature.Id, count, dwelling.Available.Value));
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    /// <summary>Closes the exchange screen opened by a meeting of two own heroes.</summary>
    internal void CompleteExchange()
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

    /// <summary>A hero attacks an enemy hero: <see cref="BattleTriggered"/>, then the battle itself.</summary>
    internal void StartBattle(Hero attacker, Hero defender, bool isAmbush)
    {
        events.Publish(new BattleTriggered(attacker.Id, defender.Id, isAmbush));
        battles.BeginHeroBattle(attacker, defender, isAmbush);
    }

    private void GiveResources(Hero hero, MapObject provider, ResourceSet resources)
    {
        TryCollect(hero, provider);
        economy.Grant(hero.Owner, resources);
        events.Publish(new ResourcesFound(hero.Id, provider.Id, resources));
    }

    private void RemoveFromMap(MapObject mapObject)
    {
        state.RemoveObject(mapObject);
        events.Publish(new MapObjectRemoved(mapObject.Id));
    }
}
