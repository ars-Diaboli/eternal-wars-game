using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Commands;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Objects;

/// <summary>The answers to the decisions map object visits and hero meetings open: each refuses unless its decision is pending.</summary>
public sealed class ObjectDecisionService(
    ObjectInteractionContext context,
    BattleService battles,
    ProgressionService progression,
    ArtifactService artifacts)
{
    private Game.GameState State => context.State;

    private Core.Events.IEventPublisher Events => context.Events;

    internal void ResolveTreasureChest(TreasureChestReward reward)
    {
        if (State.PendingDecision is not TreasureChestDecision decision)
        {
            throw new CommandRefusedException("No treasure chest decision is pending.");
        }

        // Everything the answer needs is found before anything changes, so a refusal leaves the state as it was.
        var chest = State.GetObject(decision.Chest);
        var hero = State.GetHero(decision.Hero);
        State.PendingDecision = null;
        context.RemoveFromMap(chest);
        if (reward == TreasureChestReward.Gold)
        {
            context.Economy.Grant(hero.Owner, new ResourceSet(Gold: decision.Gold));
        }

        Events.Publish(new DecisionResolved(decision));
        if (reward == TreasureChestReward.Experience)
        {
            progression.GrantExperience(hero, decision.Experience);
        }
    }

    /// <summary>Closes the exchange screen opened by a meeting of two own heroes.</summary>
    internal void CompleteExchange()
    {
        if (State.PendingDecision is not HeroExchangeDecision decision)
        {
            throw new CommandRefusedException("No hero exchange is open.");
        }

        State.PendingDecision = null;
        Events.Publish(new DecisionResolved(decision));
    }

    /// <summary>The visitor of a town gate goes to the town picked, at no movement cost, or stays (ADR-068).</summary>
    internal void ResolveTownGate(MapObjectId? townId)
    {
        if (State.PendingDecision is not TownGateDecision decision)
        {
            throw new CommandRefusedException("No town gate is open.");
        }

        var hero = State.GetHero(decision.Hero);
        Town? town = null;
        if (townId is { } id)
        {
            town = State.FindObject(id) as Town;
            if (town is null || town.Owner != hero.Owner || decision.Towns.All(t => t.Town != id))
            {
                throw new CommandRefusedException("That town is not on the list.");
            }

            if (State.HeroAt(town.VisitPosition) is not null)
            {
                throw new CommandRefusedException($"Someone stands at the entrance of {town.Name}.");
            }
        }

        State.PendingDecision = null;
        if (town is not null)
        {
            var from = hero.Position;
            hero.Position = town.VisitPosition;
            Events.Publish(new HeroTeleported(hero.Id, from, hero.Position));
            TownService.AutoAppoint(State, Events, town, hero);
            Magic.MagicRules.Visit(State, Events, hero, town);
        }

        Events.Publish(new DecisionResolved(decision));
    }

    /// <summary>The visitor of a training site picks an option, pays for it and trains; or leaves, where the site allows it (ADR-068).</summary>
    internal void ResolveTraining(TrainingOption? option)
    {
        if (State.PendingDecision is not TrainingDecision decision)
        {
            throw new CommandRefusedException("No training is offered.");
        }

        if (option is not { } picked)
        {
            if (!decision.CanDecline)
            {
                throw new CommandRefusedException("Choose one of the options.");
            }

            State.PendingDecision = null;
            Events.Publish(new DecisionResolved(decision));
            return;
        }

        if (!decision.Options.Contains(picked))
        {
            throw new CommandRefusedException($"{picked} is not offered.");
        }

        var hero = State.GetHero(decision.Hero);
        var site = (TrainingSite)State.GetObject(decision.Site);
        if (!context.Economy.TrySpend(hero.Owner, TrainingRules.Price(site.Rule, picked)))
        {
            throw new CommandRefusedException("Not enough resources.");
        }

        State.PendingDecision = null;
        context.TryCollect(hero, site);
        if (TrainingRules.Stat(picked) is { } stat)
        {
            progression.RaiseStat(hero, stat, TrainingRules.StatBonus);
        }

        Events.Publish(new DecisionResolved(decision));
        var experience = site.Rule switch
        {
            TrainingRule.Gazebo => TrainingRules.GazeboExperience,
            TrainingRule.TreeOfKnowledge => TrainingRules.ExperienceToNextLevel(hero),
            _ => 0,
        };
        progression.GrantExperience(hero, experience);
    }

    /// <summary>The visitor's player pays for the deposit, which becomes the mine of its kind with the same id, flagged for the player; or leaves.</summary>
    internal void ResolveDeposit(bool buy)
    {
        if (State.PendingDecision is not DepositDecision decision)
        {
            throw new CommandRefusedException("No deposit is waiting for an answer.");
        }

        // The mine is made before the price is paid, so nothing is spent on a deposit that cannot become one.
        var hero = State.GetHero(decision.Hero);
        var deposit = (Deposit)State.GetObject(decision.Deposit);
        var mine = buy ? new Mine(deposit.Id, State.ObjectCatalog.Get(deposit.Definition.Becomes!), deposit.Position, hero.Owner) : null;
        if (buy && !context.Economy.TrySpend(hero.Owner, decision.Price))
        {
            throw new CommandRefusedException("Not enough resources.");
        }

        State.PendingDecision = null;
        Events.Publish(new DecisionResolved(decision));
        if (mine is null)
        {
            return;
        }

        State.RemoveObject(deposit);
        State.AddObject(mine);
        Events.Publish(new DepositBought(mine.Id, hero.Id, hero.Owner, decision.Price));
        Events.Publish(new MapObjectChanged(mine.Id));
        Events.Publish(new MineOwnerChanged(mine.Id, null, hero.Owner));
    }

    /// <summary>The visitor's player buys the cartographer's map of its level, or leaves (ADR-079, MAP-31).</summary>
    internal void ResolveCartographer(bool buy)
    {
        if (State.PendingDecision is not CartographerDecision decision)
        {
            throw new CommandRefusedException("No cartographer is waiting for an answer.");
        }

        var hero = State.GetHero(decision.Hero);
        var cartographer = (Cartographer)State.GetObject(decision.Cartographer);
        if (buy && !context.Economy.TrySpend(hero.Owner, new ResourceSet(Gold: decision.Price)))
        {
            throw new CommandRefusedException("Not enough gold.");
        }

        State.PendingDecision = null;
        Events.Publish(new DecisionResolved(decision));
        if (!buy)
        {
            return;
        }

        cartographer.Buyers.Add(hero.Owner);
        Events.Publish(new MapBought(cartographer.Id, hero.Id, hero.Owner, decision.Level));
        var map = State.Map;
        Events.Publish(new SurroundingsRevealed(hero.Id, cartographer.Id, hero.Owner, new MapPosition(map.Width / 2, map.Height / 2, decision.Level), map.Width + map.Height));
    }

    /// <summary>
    /// The hero answers the sphinx (ADR-079, MAP-27): right brings a Major artifact or 5000 experience (even odds; a full backpack loses the
    /// artifact); wrong ends the hero's movement for the day and costs 3 morale in its next battle.
    /// </summary>
    internal void ResolveSphinx(int option)
    {
        if (State.PendingDecision is not SphinxDecision decision)
        {
            throw new CommandRefusedException("No sphinx is waiting for an answer.");
        }

        if (option < 0 || option >= decision.Options.Count)
        {
            throw new CommandRefusedException($"Option {option} is not offered.");
        }

        var hero = State.GetHero(decision.Hero);
        var sphinx = (Sphinx)State.GetObject(decision.Sphinx);
        State.PendingDecision = null;
        context.TryCollect(hero, sphinx);
        Events.Publish(new DecisionResolved(decision));
        if (option != decision.Correct)
        {
            hero.MovementPoints = 0;
            Events.Publish(new SphinxAnswered(sphinx.Id, hero.Id, Right: false));
            progression.GrantStatus(hero, new HeroStatus(sphinx.Definition.Id, HeroStatusKind.Morale, Sphinx.WrongMorale, StatusExpiry.NextBattle));
            return;
        }

        if (State.Random.Next(2) == 0 && artifacts.Draw(ArtifactTier.Major) is { } artifact)
        {
            var kept = artifacts.Grant(hero, artifact, ArtifactSource.Found);
            Events.Publish(new SphinxAnswered(sphinx.Id, hero.Id, Right: true, Artifact: kept ? artifact : null));
            return;
        }

        Events.Publish(new SphinxAnswered(sphinx.Id, hero.Id, Right: true, Experience: Sphinx.Experience));
        progression.GrantExperience(hero, Sphinx.Experience);
    }

    /// <summary>The hero opens the Pandora's Box, fighting its guard first if it has one, or leaves it closed (ADR-100, MAP-35).</summary>
    internal void ResolvePandorasBox(bool open)
    {
        if (State.PendingDecision is not PandorasBoxDecision decision)
        {
            throw new CommandRefusedException("No Pandora's Box is waiting for an answer.");
        }

        var hero = State.GetHero(decision.Hero);
        var box = (PandorasBox)State.GetObject(decision.Box);
        State.PendingDecision = null;
        Events.Publish(new DecisionResolved(decision));
        if (!open)
        {
            return;
        }

        if (box.IsGuarded)
        {
            battles.BeginBoxBattle(hero, box);
            return;
        }

        PandorasBoxRules.Open(State, Events, context.Economy, progression, artifacts, hero, box);
    }

    /// <summary>The hero learns or improves one of the witch hut's skills, or leaves without (ADR-079, MAP-34).</summary>
    internal void ResolveWitchHut(string? skill)
    {
        if (State.PendingDecision is not WitchHutDecision decision)
        {
            throw new CommandRefusedException("No witch hut is waiting for an answer.");
        }

        if (skill is not null && !decision.Options.Contains(skill))
        {
            throw new CommandRefusedException($"'{skill}' is not offered.");
        }

        var hero = State.GetHero(decision.Hero);
        if (skill is not null)
        {
            hero.Skills.Raise(skill); // refuses (a full set of skill groups) before anything has changed
            hero.WitchHutLevel = hero.Level;
        }

        State.PendingDecision = null;
        Events.Publish(new DecisionResolved(decision));
        if (skill is not null)
        {
            Events.Publish(new WitchHutSkillLearned(decision.Hut, hero.Id, skill, hero.Skills.Level(skill)));
        }
    }
}
