using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>
/// Hero levels (ADR-048): every <see cref="ExperienceTable"/> level gives one skill point, spent on one of the skills
/// <see cref="SkillOffers"/> proposes, and one primary stat point (<see cref="StatGrowth"/>, ADR-055). Human players choose through a <see cref="LevelUpDecision"/>, during their own turn
/// (a defender who levels up in the attacker's turn chooses when its turn starts); computer players take the first offer.
/// </summary>
public sealed class ProgressionService(GameState state, IEventPublisher events)
{
    /// <summary>Adds experience and offers any level-ups that are due now.</summary>
    internal void GrantExperience(Hero hero, int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        hero.Experience += amount;
        events.Publish(new HeroExperienceGained(hero.Id, amount, hero.Experience));
        OfferNext();
    }

    /// <summary>A map object raises a primary stat (ADR-068).</summary>
    internal void RaiseStat(Hero hero, PrimaryStat stat, int amount)
    {
        for (var i = 0; i < amount; i++)
        {
            hero.Raise(stat);
        }

        events.Publish(new HeroPrimaryStatRaised(hero.Id, stat, amount, hero.Stat(stat)));
    }

    /// <summary>Gives a hero a temporary status (ADR-068); one of the same source and kind is replaced.</summary>
    internal void GrantStatus(Hero hero, HeroStatus status)
    {
        hero.AddStatus(status);
        events.Publish(new HeroStatusGained(hero.Id, status));
    }

    /// <summary>Ends the statuses of the given heroes that last until <paramref name="expiry"/>; quiet for heroes without any.</summary>
    internal void ExpireStatuses(IEnumerable<Hero> heroes, StatusExpiry expiry)
    {
        foreach (var hero in heroes.OrderBy(h => h.Id.Value).ToList())
        {
            if (hero.RemoveStatuses(expiry) is { Count: > 0 } ended)
            {
                events.Publish(new HeroStatusesExpired(hero.Id, ended));
            }
        }
    }

    /// <summary>
    /// Resolves computer heroes' level-ups at once and, if nothing else is pending, asks the current human player
    /// about the first of its heroes with a level-up due.
    /// </summary>
    internal void OfferNext()
    {
        if (state.PendingDecision is not null)
        {
            return;
        }

        foreach (var hero in state.Heroes.Where(h => h.PendingLevelUps > 0 && !state.GetPlayer(h.Owner).IsHuman).ToList())
        {
            while (hero.PendingLevelUps > 0)
            {
                var offers = Offers(hero);
                LevelUp(hero, offers.FirstOrDefault(), RollStat(hero));
            }
        }

        var player = state.CurrentPlayer;
        if (!state.IsStarted || !player.IsHuman)
        {
            return;
        }

        foreach (var hero in state.HeroesOf(player.Id).Where(h => h.PendingLevelUps > 0).ToList())
        {
            var options = Offers(hero);
            var stat = RollStat(hero);
            if (options.Count == 0)
            {
                // Everything is learned: the level and its stat point count, there is nothing to choose.
                LevelUp(hero, null, stat);
                continue;
            }

            var decision = new LevelUpDecision(hero.Id, hero.Level + 1, options, stat);
            state.PendingDecision = decision;
            events.Publish(new DecisionRequested(decision));
            return;
        }
    }

    internal void Choose(string skill)
    {
        if (state.PendingDecision is not LevelUpDecision decision)
        {
            throw new InvalidOperationException("No level-up is pending.");
        }

        if (!decision.Options.Contains(skill))
        {
            throw new InvalidOperationException($"'{skill}' is not one of the offered skills.");
        }

        state.PendingDecision = null;
        LevelUp(state.GetHero(decision.Hero), skill, decision.Stat);
        events.Publish(new DecisionResolved(decision));
        OfferNext();
    }

    /// <summary>The town whose school counts as the hero's own: its roster town, else its preferred creature's town.</summary>
    public static string? TownOf(GameState state, Hero hero) =>
        hero.Definition is { } id && state.HeroRoster.Contains(id) ? state.HeroRoster.Get(id).Town
        : hero.PreferredCreature is { } line && state.Creatures.TryGet(line, out var creature) ? creature.Town
        : null;

    private IReadOnlyList<string> Offers(Hero hero) => SkillOffers.Generate(hero, TownOf(state, hero), state.Random);

    /// <summary>The stat the next level raises, rolled after the skill offers so the prompt can show it (ADR-055).</summary>
    private PrimaryStat RollStat(Hero hero) => StatGrowth.Roll(hero.Class, hero.Level + 1, state.Random);

    private void LevelUp(Hero hero, string? skill, PrimaryStat stat)
    {
        hero.Level++;
        if (skill is not null)
        {
            hero.Skills.Raise(skill);
        }

        hero.Raise(stat);
        events.Publish(new HeroLeveledUp(hero.Id, hero.Level, skill, skill is null ? SkillLevel.None : hero.Skills.Level(skill), stat, hero.Stat(stat)));
    }
}
