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
                var inPile = pile.Amount ?? ResourcePile.Roll(pile.Resource, state.Random);
                var amount = chance > 0 && state.Random.Next(100) < chance ? inPile + (inPile + 1) / 2 : inPile;
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
                var values = chest.FixedValues ?? WeightedRoll.Pick(TreasureChest.Odds, state.Random);
                if (values is null)
                {
                    // The artifact outcome (ADR-068): no choice, the chest is emptied; a full backpack loses it.
                    RemoveFromMap(chest);
                    var found = artifacts.Draw(ArtifactTier.Minor) is { } minor && artifacts.Grant(hero, minor, ArtifactSource.Found) ? minor : null;
                    events.Publish(new LootFound(hero.Id, chest.Id, ResourceSet.Empty, found));
                    break;
                }

                var decision = new TreasureChestDecision(hero.Id, chest.Id, values.Gold, values.Experience);
                state.PendingDecision = decision;
                events.Publish(new DecisionRequested(decision));
                break;

            case LootSite site:
                Search(hero, site);
                break;

            case BuffSite buff:
                Bless(hero, buff);
                break;

            case TrainingSite training:
                Train(hero, training);
                break;

            case SignPost sign:
                events.Publish(new SignRead(hero.Id, sign.Id, sign.Message));
                break;

            case Observatory observatory:
                // Every visit looks out (it only reveals what is still hidden); the log tells the AI which heroes have been up there.
                TryCollect(hero, observatory);
                events.Publish(new SurroundingsRevealed(hero.Id, observatory.Id, hero.Owner, observatory.VisitPosition, Observatory.Radius));
                break;

            case TownGate gate:
                OfferTowns(hero, gate);
                break;

            case Cartographer cartographer:
                OfferMap(hero, cartographer);
                break;

            case Sphinx sphinx:
                AskRiddle(hero, sphinx);
                break;

            case WitchHut hut:
                OfferSkills(hero, hut);
                break;

            case DenOfThieves den:
                events.Publish(new ThievesDenVisited(hero.Id, den.Id));
                break;

            case Prison prison:
                Free(hero, prison);
                break;

            case CreatureBank bank:
                EnterBank(hero, bank);
                break;

            case MonsterGuard monster:
                guards.Encounter(hero, monster);
                break;

            case ResourceProvider provider when provider.Owner != hero.Owner:
                Flag(hero, provider);
                break;

            case ResourceProvider provider when provider.Stored.IsEmpty:
                events.Publish(new MapObjectFoundEmpty(hero.Id, provider.Id));
                break;

            case ResourceProvider provider:
                var stored = provider.Stored;
                provider.Stored = ResourceSet.Empty;
                events.Publish(new ProviderStockChanged(provider.Id, ResourceSet.Empty));
                GiveResources(hero, provider, stored);
                break;

            case CreatureDwelling { Guard: not null } guardedDwelling:
                guards.Encounter(hero, guardedDwelling);
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
                TownService.Capture(state, events, town, hero.Owner);
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

    /// <summary>
    /// A creature bank (ADR-068): the Ruins show their surroundings on every visit; a full bank means a fight; a plundered one gives nothing, and the
    /// Churchyard's costs the next battle's morale.
    /// </summary>
    private void EnterBank(Hero hero, CreatureBank bank)
    {
        if (bank.Rule == BankRule.Ruins)
        {
            events.Publish(new SurroundingsRevealed(hero.Id, bank.Id, hero.Owner, bank.VisitPosition, CreatureBankRules.RuinsRevealRadius));
        }

        if (bank.IsGuarded)
        {
            battles.BeginBankBattle(hero, bank);
            return;
        }

        events.Publish(new MapObjectFoundEmpty(hero.Id, bank.Id));
        if (bank.Rule == BankRule.Churchyard)
        {
            progression.GrantStatus(hero, new HeroStatus(bank.Definition.Id, HeroStatusKind.Morale, CreatureBankRules.ChurchyardMorale, StatusExpiry.NextBattle));
        }
    }

    /// <summary>The town gate's picker: the owner's towns, an occupied entrance listed but unavailable (ADR-068, MAP-12).</summary>
    private void OfferTowns(Hero hero, TownGate gate)
    {
        var towns = state.ObjectsOf<Town>().Where(t => t.Owner == hero.Owner).OrderBy(t => t.Id.Value)
            .Select(t => new TownGateOption(t.Id, t.Name, state.HeroAt(t.VisitPosition) is null))
            .ToList();
        if (towns.Count == 0)
        {
            events.Publish(new MapObjectRefused(hero.Id, gate.Id, "You have no town to go to."));
            return;
        }

        var decision = new TownGateDecision(hero.Id, gate.Id, towns);
        state.PendingDecision = decision;
        events.Publish(new DecisionRequested(decision));
    }

    /// <summary>The visitor of a town gate goes to the town picked, at no movement cost, or stays (ADR-068).</summary>
    internal void ResolveTownGate(MapObjectId? townId)
    {
        if (state.PendingDecision is not TownGateDecision decision)
        {
            throw new InvalidOperationException("No town gate is open.");
        }

        var hero = state.GetHero(decision.Hero);
        Town? town = null;
        if (townId is { } id)
        {
            town = state.FindObject(id) as Town;
            if (town is null || town.Owner != hero.Owner || decision.Towns.All(t => t.Town != id))
            {
                throw new InvalidOperationException("That town is not on the list.");
            }

            if (state.HeroAt(town.VisitPosition) is not null)
            {
                throw new InvalidOperationException($"Someone stands at the entrance of {town.Name}.");
            }
        }

        state.PendingDecision = null;
        if (town is not null)
        {
            var from = hero.Position;
            hero.Position = town.VisitPosition;
            events.Publish(new HeroTeleported(hero.Id, from, hero.Position));
            TownService.AutoAppoint(state, events, town, hero);
        }

        events.Publish(new DecisionResolved(decision));
    }

    /// <summary>A prisoner joins the visitor's player where the prison stood, unless the player has all the heroes it may (ADR-068, MAP-5).</summary>
    private void Free(Hero hero, Prison prison)
    {
        if (state.HeroLimitReached(hero.Owner))
        {
            events.Publish(new MapObjectRefused(hero.Id, prison.Id, $"You may have at most {state.HeroLimit(hero.Owner)} heroes; the prisoner stays."));
            return;
        }

        if (prison.Hero is not { } heroId || !state.HeroRoster.Contains(heroId))
        {
            events.Publish(new MapObjectRefused(hero.Id, prison.Id, "The prison is empty."));
            return;
        }

        RemoveFromMap(prison);
        var level = Math.Max(1, prison.Level ?? 1);
        var freed = Hero.Create(state.NextUnusedHeroId(), hero.Owner, prison.Position, null, state.HeroRoster.Get(heroId));
        freed.Experience = ExperienceTable.For(level);
        state.AddHero(freed);
        progression.LevelUpAutomatically(freed);
        freed.MaxMovementPoints = MovementAllowance.Daily(freed, state.Creatures);
        freed.MovementPoints = 0; // it starts moving on its owner's next turn
        events.Publish(new HeroFreed(prison.Id, freed.Id, hero.Owner, heroId, freed.Level));
    }

    /// <summary>The hero on a hill fort's entrance upgrades one of its stacks of tiers 1–3 for 150 % of the price difference (ADR-068, MAP-11).</summary>
    internal void UpgradeAtHillFort(PlayerId player, MapObjectId fortId, int slot)
    {
        var hero = HeroAt<HillFort>(player, fortId, out var fort);
        var stack = (Army.IsValidSlot(slot) ? hero.Army[slot] : null) ?? throw new InvalidOperationException("The slot is empty.");
        var upgraded = HillFortRules.UpgradeOf(state.Creatures, stack.Creature)
                       ?? throw new InvalidOperationException("Only stacks of tiers 1–3 that have an upgrade are upgraded here.");
        if (!economy.TrySpend(player, HillFortRules.Cost(state.Creatures, stack)))
        {
            throw new InvalidOperationException("Not enough resources.");
        }

        hero.Army.Set(slot, new CreatureStack(upgraded.Id, stack.Count));
        events.Publish(new HillFortUpgraded(fort.Id, hero.Id, slot, stack.Creature, upgraded.Id, stack.Count));
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    /// <summary>The hero on a War Machine Factory's entrance buys a machine it lacks at the blacksmith's price (ADR-068, MAP-13).</summary>
    internal void BuyAtFactory(PlayerId player, MapObjectId factoryId, Battles.WarMachineSlot slot)
    {
        var hero = HeroAt<WarMachineFactory>(player, factoryId, out var factory);
        if (hero.WarMachines.Contains(slot))
        {
            throw new InvalidOperationException($"{hero.Name} already has that machine.");
        }

        if (!economy.TrySpend(player, Battles.WarMachines.Cost(slot)))
        {
            throw new InvalidOperationException("Not enough gold.");
        }

        hero.AddWarMachine(slot);
        events.Publish(new WarMachineBought(factory.Id, hero.Id, slot));
    }

    /// <summary>The player's hero standing on the entrance of an object of type <typeparamref name="T"/>, with nothing pending.</summary>
    private Hero HeroAt<T>(PlayerId player, MapObjectId id, out T mapObject)
        where T : MapObject
    {
        mapObject = state.FindObject(id) as T ?? throw new InvalidOperationException($"{id} is not a {typeof(T).Name}.");
        if (state.PendingDecision is not null)
        {
            throw new InvalidOperationException("Resolve the pending decision first.");
        }

        var hero = state.HeroAt(mapObject.VisitPosition);
        return hero is not null && hero.Owner == player ? hero : throw new InvalidOperationException($"No own hero stands at the {mapObject.Definition.Name}.");
    }

    /// <summary>
    /// A buff site (ADR-068): statuses for the next battle or the week, movement points for today, the Magic Well's mana. The Stables also upgrade
    /// Lancers on every visit, whether the hero may take the movement again or not.
    /// </summary>
    private void Bless(Hero hero, BuffSite site)
    {
        if (site.Rule == BuffRule.Stables)
        {
            UpgradeAtStables(hero);
        }

        if (!TryCollect(hero, site))
        {
            events.Publish(new MapObjectFoundEmpty(hero.Id, site.Id));
            return;
        }

        foreach (var status in BuffRules.Statuses(site.Definition, state.Date))
        {
            progression.GrantStatus(hero, status);
        }

        if (BuffRules.MovementNow(site.Rule) is > 0 and var points)
        {
            hero.MovementPoints += points;
            events.Publish(new HeroMovementGained(hero.Id, points, hero.MovementPoints));
        }

        if (site.Rule == BuffRule.MagicWell && hero.Mana < hero.MaxMana)
        {
            hero.Mana = hero.MaxMana;
            events.Publish(new HeroManaRestored(hero.Id, hero.Mana, hero.MaxMana));
        }
    }

    private void UpgradeAtStables(Hero hero)
    {
        if (!state.Creatures.TryGet(BuffRules.StablesCreature, out var lancer) || lancer.UpgradesTo is not { } upgrade)
        {
            return;
        }

        var stacks = hero.Army.Stacks.Where(s => s.Stack.Creature == lancer.Id).ToList();
        foreach (var (slot, stack) in stacks)
        {
            hero.Army.Set(slot, new CreatureStack(upgrade, stack.Count));
        }

        if (stacks.Count > 0)
        {
            events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        }
    }

    /// <summary>
    /// A training site (ADR-068): once per hero. The Learning Stone and the Library give at once (the Library only from level 10); the others ask
    /// through a <see cref="TrainingDecision"/>, and a paid one turns the hero away when the player cannot pay any price.
    /// </summary>
    private void Train(Hero hero, TrainingSite site)
    {
        if (!site.CanCollect(hero.Id, state.Date))
        {
            events.Publish(new MapObjectFoundEmpty(hero.Id, site.Id));
            return;
        }

        switch (site.Rule)
        {
            case TrainingRule.LearningStone:
                TryCollect(hero, site);
                progression.GrantExperience(hero, TrainingRules.LearningStoneExperience);
                return;
            case TrainingRule.LibraryOfEnlightenment when hero.Level < TrainingRules.LibraryLevel:
                events.Publish(new MapObjectRefused(hero.Id, site.Id, $"Only heroes of level {TrainingRules.LibraryLevel} or more may study here."));
                return;
            case TrainingRule.LibraryOfEnlightenment:
                TryCollect(hero, site);
                foreach (var stat in Enum.GetValues<PrimaryStat>())
                {
                    progression.RaiseStat(hero, stat, TrainingRules.LibraryStats);
                }

                return;
        }

        var options = TrainingRules.Options(site.Rule, state.GetPlayer(hero.Owner).Resources);
        if (options.Count == 0)
        {
            events.Publish(new MapObjectRefused(hero.Id, site.Id, "Not enough resources to pay."));
            return;
        }

        var decision = new TrainingDecision(hero.Id, site.Id, options, TrainingRules.CanDecline(site.Rule));
        state.PendingDecision = decision;
        events.Publish(new DecisionRequested(decision));
    }

    /// <summary>The visitor of a training site picks an option, pays for it and trains; or leaves, where the site allows it (ADR-068).</summary>
    internal void ResolveTraining(TrainingOption? option)
    {
        if (state.PendingDecision is not TrainingDecision decision)
        {
            throw new InvalidOperationException("No training is offered.");
        }

        if (option is not { } picked)
        {
            if (!decision.CanDecline)
            {
                throw new InvalidOperationException("Choose one of the options.");
            }

            state.PendingDecision = null;
            events.Publish(new DecisionResolved(decision));
            return;
        }

        if (!decision.Options.Contains(picked))
        {
            throw new InvalidOperationException($"{picked} is not offered.");
        }

        var hero = state.GetHero(decision.Hero);
        var site = (TrainingSite)state.GetObject(decision.Site);
        if (!economy.TrySpend(hero.Owner, TrainingRules.Price(site.Rule, picked)))
        {
            throw new InvalidOperationException("Not enough resources.");
        }

        state.PendingDecision = null;
        TryCollect(hero, site);
        if (TrainingRules.Stat(picked) is { } stat)
        {
            progression.RaiseStat(hero, stat, TrainingRules.StatBonus);
        }

        events.Publish(new DecisionResolved(decision));
        var experience = site.Rule switch
        {
            TrainingRule.Gazebo => TrainingRules.GazeboExperience,
            TrainingRule.TreeOfKnowledge => TrainingRules.ExperienceToNextLevel(hero),
            _ => 0,
        };
        progression.GrantExperience(hero, experience);
    }

    /// <summary>
    /// A loot site (ADR-068): the first visitor finds what it rolls, vanishing sites go; later visitors find nothing. The Warrior's Tomb costs
    /// every visitor morale for the next battle, the first one too.
    /// </summary>
    private void Search(Hero hero, LootSite site)
    {
        if (TryCollect(hero, site))
        {
            var roll = LootRules.Roll(site.Rule, state.Random);
            if (site.Vanishes)
            {
                RemoveFromMap(site);
            }

            economy.Grant(hero.Owner, roll.Resources);
            var found = roll.Artifact is { } tier && artifacts.Draw(tier) is { } artifact && artifacts.Grant(hero, artifact, ArtifactSource.Found) ? artifact : null;
            events.Publish(new LootFound(hero.Id, site.Id, roll.Resources, found));
        }
        else
        {
            events.Publish(new MapObjectFoundEmpty(hero.Id, site.Id));
        }

        if (site.Rule == LootRule.WarriorsTomb)
        {
            progression.GrantStatus(hero, new HeroStatus(site.Definition.Id, HeroStatusKind.Morale, LootRules.TombMorale, StatusExpiry.NextBattle));
        }
    }

    /// <summary>
    /// A hero flags a provider (ADR-079, TOWN-9): the first flagging of a neutral one brings a week's output at once; otherwise the hero takes
    /// whatever its former owner had left there.
    /// </summary>
    private void Flag(Hero hero, ResourceProvider provider)
    {
        var previous = provider.Owner;
        var taken = provider.WasClaimed ? provider.Stored : ResourceProviderRules.Roll(provider.Definition, state.Date, state.Random);
        provider.Owner = hero.Owner;
        provider.WasClaimed = true;
        events.Publish(new ProviderOwnerChanged(provider.Id, previous, hero.Owner));
        if (!provider.Stored.IsEmpty)
        {
            provider.Stored = ResourceSet.Empty;
            events.Publish(new ProviderStockChanged(provider.Id, ResourceSet.Empty));
        }

        if (!taken.IsEmpty)
        {
            GiveResources(hero, provider, taken);
        }
    }

    /// <summary>
    /// Game start (ADR-079): witch huts the map left open roll their skills, refugee camps get their first week and artifact merchants their stock.
    /// </summary>
    internal void PrepareObjects()
    {
        foreach (var hut in state.ObjectsOf<WitchHut>().Where(h => h.Skills.Count == 0).OrderBy(h => h.Id.Value).ToList())
        {
            hut.Skills = WitchHutRules.Roll(state.Random);
        }

        StartWeek(restock: true);
    }

    /// <summary>The first day of a week: new refugees in every camp; on the first day of a month also new merchant stock (ADR-079).</summary>
    internal void StartWeek(bool restock)
    {
        var candidates = RefugeeCampRules.Candidates(state.Creatures);
        foreach (var camp in state.ObjectsOf<RefugeeCamp>().OrderBy(c => c.Id.Value).ToList())
        {
            if (candidates.Count == 0)
            {
                break;
            }

            var creature = candidates[state.Random.Next(candidates.Count)];
            camp.Creature = creature.Id;
            camp.Available = creature.WeeklyGrowth;
            events.Publish(new RefugeesArrived(camp.Id, creature.Id, camp.Available));
        }

        if (!restock)
        {
            return;
        }

        foreach (var merchant in state.ObjectsOf<ArtifactMerchant>().OrderBy(m => m.Id.Value).ToList())
        {
            merchant.Stock.Clear();
            for (var i = 0; i < ArtifactMerchant.StockSize; i++)
            {
                merchant.Stock.Add(artifacts.Draw(state.Random.Next(2) == 0 ? ArtifactTier.Minor : ArtifactTier.Major));
            }

            events.Publish(new MerchantRestocked(merchant.Id, merchant.Stock.ToList()));
        }
    }

    /// <summary>The visitor's player buys the cartographer's map of its level, or leaves (ADR-079, MAP-31).</summary>
    internal void ResolveCartographer(bool buy)
    {
        if (state.PendingDecision is not CartographerDecision decision)
        {
            throw new InvalidOperationException("No cartographer is waiting for an answer.");
        }

        var hero = state.GetHero(decision.Hero);
        if (buy && !economy.TrySpend(hero.Owner, new ResourceSet(Gold: decision.Price)))
        {
            throw new InvalidOperationException("Not enough gold.");
        }

        state.PendingDecision = null;
        events.Publish(new DecisionResolved(decision));
        if (!buy)
        {
            return;
        }

        var cartographer = (Cartographer)state.GetObject(decision.Cartographer);
        cartographer.Buyers.Add(hero.Owner);
        events.Publish(new MapBought(cartographer.Id, hero.Id, hero.Owner, decision.Level));
        var map = state.Map;
        events.Publish(new SurroundingsRevealed(hero.Id, cartographer.Id, hero.Owner, new MapPosition(map.Width / 2, map.Height / 2, decision.Level), map.Width + map.Height));
    }

    /// <summary>
    /// The hero answers the sphinx (ADR-079, MAP-27): right brings a Major artifact or 5000 experience (even odds; a full backpack loses the
    /// artifact); wrong ends the hero's movement for the day and costs 3 morale in its next battle.
    /// </summary>
    internal void ResolveSphinx(int option)
    {
        if (state.PendingDecision is not SphinxDecision decision)
        {
            throw new InvalidOperationException("No sphinx is waiting for an answer.");
        }

        if (option < 0 || option >= decision.Options.Count)
        {
            throw new InvalidOperationException($"Option {option} is not offered.");
        }

        var hero = state.GetHero(decision.Hero);
        var sphinx = (Sphinx)state.GetObject(decision.Sphinx);
        state.PendingDecision = null;
        TryCollect(hero, sphinx);
        events.Publish(new DecisionResolved(decision));
        if (option != decision.Correct)
        {
            hero.MovementPoints = 0;
            events.Publish(new SphinxAnswered(sphinx.Id, hero.Id, Right: false));
            progression.GrantStatus(hero, new HeroStatus(sphinx.Definition.Id, HeroStatusKind.Morale, Sphinx.WrongMorale, StatusExpiry.NextBattle));
            return;
        }

        if (state.Random.Next(2) == 0 && artifacts.Draw(ArtifactTier.Major) is { } artifact)
        {
            var kept = artifacts.Grant(hero, artifact, ArtifactSource.Found);
            events.Publish(new SphinxAnswered(sphinx.Id, hero.Id, Right: true, Artifact: kept ? artifact : null));
            return;
        }

        events.Publish(new SphinxAnswered(sphinx.Id, hero.Id, Right: true, Experience: Sphinx.Experience));
        progression.GrantExperience(hero, Sphinx.Experience);
    }

    /// <summary>The hero learns or improves one of the witch hut's skills, or leaves without (ADR-079, MAP-34).</summary>
    internal void ResolveWitchHut(string? skill)
    {
        if (state.PendingDecision is not WitchHutDecision decision)
        {
            throw new InvalidOperationException("No witch hut is waiting for an answer.");
        }

        if (skill is not null && !decision.Options.Contains(skill))
        {
            throw new InvalidOperationException($"'{skill}' is not offered.");
        }

        var hero = state.GetHero(decision.Hero);
        state.PendingDecision = null;
        if (skill is not null)
        {
            hero.Skills.Raise(skill);
            hero.WitchHutLevel = hero.Level;
        }

        events.Publish(new DecisionResolved(decision));
        if (skill is not null)
        {
            events.Publish(new WitchHutSkillLearned(decision.Hut, hero.Id, skill, hero.Skills.Level(skill)));
        }
    }

    /// <summary>The hero on an artifact merchant's entrance buys the artifact of a slot for twice its value (ADR-079, MAP-33).</summary>
    internal void BuyArtifact(PlayerId player, MapObjectId merchantId, int slot)
    {
        var hero = HeroAt<ArtifactMerchant>(player, merchantId, out var merchant);
        var artifact = (slot >= 0 && slot < merchant.Stock.Count ? merchant.Stock[slot] : null)
                       ?? throw new InvalidOperationException("Nothing is for sale in that slot.");
        if (hero.Artifacts.BackpackFull)
        {
            throw new InvalidOperationException($"{hero.Name}'s backpack is full.");
        }

        var price = ArtifactMerchantRules.Price(state.Artifacts.Get(artifact));
        if (!economy.TrySpend(player, price))
        {
            throw new InvalidOperationException("Not enough gold.");
        }

        merchant.Stock[slot] = null;
        artifacts.Grant(hero, artifact, ArtifactSource.Bought);
        events.Publish(new ArtifactBought(merchant.Id, hero.Id, slot, artifact, price));
    }

    /// <summary>The hero on a refugee camp's entrance buys its creatures at the normal price (ADR-079, MAP-30; no Stewardship discount).</summary>
    internal void RecruitAtRefugeeCamp(PlayerId player, MapObjectId campId, int count)
    {
        var hero = HeroAt<RefugeeCamp>(player, campId, out var camp);
        if (camp.Creature is not { } id || count < 1 || count > camp.Available)
        {
            throw new InvalidOperationException($"Only {camp.Available} creatures wait here.");
        }

        var creature = state.Creatures.Get(id);
        if (!hero.Army.CanAccept(id))
        {
            throw new InvalidOperationException($"{hero.Name} has no free slot for {creature.Name}.");
        }

        if (!economy.TrySpend(player, creature.Cost * count))
        {
            throw new InvalidOperationException("Not enough resources.");
        }

        camp.Available -= count;
        hero.Army.TryAdd(new CreatureStack(id, count));
        events.Publish(new DwellingRecruited(camp.Id, hero.Id, id, count, camp.Available));
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
    }

    /// <summary>
    /// Creatures of an army slot go on the altar for twice their hit points in experience (ADR-079, MAP-29): Darkness and Wild heroes only, and the
    /// hero keeps at least one creature *(choice)*.
    /// </summary>
    internal void SacrificeCreatures(PlayerId player, MapObjectId altarId, int slot, int count)
    {
        var hero = HeroAt<SacrificialAltar>(player, altarId, out var altar);
        if (!SacrificeRules.For(ProgressionService.TownOf(state, hero)).HasFlag(SacrificeKind.Creatures))
        {
            throw new InvalidOperationException($"{hero.Name} cannot sacrifice creatures.");
        }

        var stack = (Army.IsValidSlot(slot) ? hero.Army[slot] : null) ?? throw new InvalidOperationException("The slot is empty.");
        if (count < 1 || count > stack.Count)
        {
            throw new InvalidOperationException($"The stack has {stack.Count} creatures.");
        }

        if (count == hero.Army.Stacks.Sum(s => s.Stack.Count))
        {
            throw new InvalidOperationException($"{hero.Name} must keep at least one creature.");
        }

        hero.Army.Set(slot, count == stack.Count ? null : new CreatureStack(stack.Creature, stack.Count - count));
        var experience = SacrificeRules.Experience(state.Creatures.Get(stack.Creature), count);
        events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
        events.Publish(new SacrificeMade(altar.Id, hero.Id, experience, stack.Creature, count));
        progression.GrantExperience(hero, experience);
    }

    /// <summary>An artifact the hero wears or carries goes on the altar for 2000 / 5000 / 10 000 experience by tier (Light and Wild heroes only).</summary>
    internal void SacrificeArtifact(PlayerId player, MapObjectId altarId, string artifact)
    {
        var hero = HeroAt<SacrificialAltar>(player, altarId, out var altar);
        if (!SacrificeRules.For(ProgressionService.TownOf(state, hero)).HasFlag(SacrificeKind.Artifacts))
        {
            throw new InvalidOperationException($"{hero.Name} cannot sacrifice artifacts.");
        }

        if (!hero.Artifacts.Has(artifact))
        {
            throw new InvalidOperationException($"{hero.Name} has no '{artifact}'.");
        }

        if (!hero.Artifacts.RemoveFromBackpack(artifact))
        {
            hero.Artifacts.RemoveWorn(artifact);
            state.RefreshArtifacts(hero);
        }

        var experience = SacrificeRules.Experience(state.Artifacts.Get(artifact));
        events.Publish(new HeroArtifactsChanged(hero.Id));
        events.Publish(new SacrificeMade(altar.Id, hero.Id, experience, Artifact: artifact));
        progression.GrantExperience(hero, experience);
    }

    private void OfferMap(Hero hero, Cartographer cartographer)
    {
        if (cartographer.Level >= state.Map.Levels)
        {
            events.Publish(new MapObjectRefused(hero.Id, cartographer.Id, "This map has no underground to chart."));
        }
        else if (cartographer.Buyers.Contains(hero.Owner))
        {
            events.Publish(new MapObjectFoundEmpty(hero.Id, cartographer.Id));
        }
        else if (!state.GetPlayer(hero.Owner).Resources.CanAfford(new ResourceSet(Gold: Cartographer.Price)))
        {
            events.Publish(new MapObjectRefused(hero.Id, cartographer.Id, $"The cartographer's map costs {Cartographer.Price} gold."));
        }
        else
        {
            Ask(new CartographerDecision(hero.Id, cartographer.Id, cartographer.Level, Cartographer.Price));
        }
    }

    /// <summary>Each hero gets one riddle per sphinx: the author's, or one from the table; the right answer's place among the three is rolled.</summary>
    private void AskRiddle(Hero hero, Sphinx sphinx)
    {
        if (!sphinx.CanCollect(hero.Id, state.Date))
        {
            events.Publish(new MapObjectFoundEmpty(hero.Id, sphinx.Id));
            return;
        }

        var riddle = sphinx.Riddle ?? RiddleTable.Default[state.Random.Next(RiddleTable.Default.Count)];
        var correct = state.Random.Next(3);
        var options = new List<string> { riddle.Wrong1, riddle.Wrong2 };
        options.Insert(correct, riddle.Answer);
        Ask(new SphinxDecision(hero.Id, sphinx.Id, riddle.Question, options, correct));
    }

    private void OfferSkills(Hero hero, WitchHut hut)
    {
        var options = WitchHutRules.Options(hero, hut);
        if (hero.WitchHutLevel == hero.Level)
        {
            events.Publish(new MapObjectRefused(hero.Id, hut.Id, $"{hero.Name} learned at a witch hut at this level already."));
        }
        else if (options.Count == 0)
        {
            events.Publish(new MapObjectRefused(hero.Id, hut.Id, $"The witch has nothing to teach {hero.Name}."));
        }
        else
        {
            Ask(new WitchHutDecision(hero.Id, hut.Id, options));
        }
    }

    private void Ask(PendingDecision decision)
    {
        state.PendingDecision = decision;
        events.Publish(new DecisionRequested(decision));
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
