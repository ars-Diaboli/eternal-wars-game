using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Towns;

/// <summary>
/// What happens when a hero comes into a town and when a town changes hands: a hero with Stewardship takes over a town without a governor
/// (<see cref="AutoAppoint"/>), an own hero raises the Grail building, takes the unique buildings' bonuses and learns the guild's spells
/// (<see cref="Visit"/>), and a captured town loses its garrison and governor (<see cref="Capture"/>). The rules it applies are the pure
/// queries of <see cref="MagicRules"/>, <see cref="StewardshipRules"/>, <see cref="UniqueBuildings"/> and <see cref="TownRules"/>.
/// </summary>
public sealed class TownVisitService(GameState state, IEventPublisher events, ProgressionService progression)
{
    /// <summary>A hero walks, teleports or is portalled into an own or allied town: it may become governor, then it visits.</summary>
    internal void Enter(Hero hero, Town town)
    {
        AutoAppoint(town, hero);
        Visit(hero, town);
    }

    /// <summary>
    /// A hero in an own town (on the entrance) with a mage guild learns the offered spells it may and does not know yet, and the offered spells it
    /// knows rise, free, straight to the rank its Mastery allows (ADR-072, ADR-122; a spell learned elsewhere rises too). Its mana fills up only
    /// after a night there (<paramref name="night"/>: the start of the owner's turn), not on entering. Nothing happens without a guild, and no
    /// learning without a book. A war lodge instead teaches the warcries up to its level (<see cref="Hero.WarcriesLearned"/>). Every town visit
    /// comes through here, so the hero first raises the Grail building with a Grail it carries (ADR-130) and the town's unique buildings give
    /// their bonuses (ADR-138).
    /// </summary>
    internal void Visit(Hero hero, Town town, bool night = false)
    {
        if (town.Owner != hero.Owner)
        {
            return;
        }

        RaiseGrailBuilding(hero, town);
        TakeUniqueBonuses(hero, town);

        // Stronghold's and Swamp Bestiary's war lodge (ADR-071, placeholder) teaches the warcries up to its level instead of spells.
        if (town.Level(BuiltInBuildings.WarLodge) is var lodge and > 0 && lodge > hero.WarcriesLearned)
        {
            hero.WarcriesLearned = lodge;
            events.Publish(new WarcriesLearned(hero.Id, town.Id, lodge));
        }

        if (town.Level(BuiltInBuildings.MageGuild) < 1)
        {
            return;
        }

        var offered = MagicRules.Offered(state, town).ToList();
        var learned = offered.Where(id => state.Spells.TryGet(id, out var spell) && MagicRules.CanLearn(hero, spell) && hero.Learn(id)).ToList();
        if (learned.Count > 0)
        {
            events.Publish(new SpellsLearned(hero.Id, town.Id, learned));
        }

        if (hero.HasSpellBook)
        {
            var raised = offered.Distinct(StringComparer.Ordinal)
                .Where(id => state.Spells.TryGet(id, out var spell) && hero.RaiseSpell(id, MagicRules.MaxRank(hero, spell)))
                .Select(id => new SpellRank(id, hero.Spells[id]))
                .ToList();
            if (raised.Count > 0)
            {
                events.Publish(new SpellsRaised(hero.Id, town.Id, raised));
            }
        }

        var max = hero.MaxMana;
        if (night && hero.Mana < max)
        {
            hero.Mana = max;
            events.Publish(new HeroManaRestored(hero.Id, hero.Mana, max));
        }
    }

    /// <summary>Visits the town's own hero on the entrance, if any (after a guild or unique building is built or a book bought there).</summary>
    internal void VisitBy(Town town)
    {
        if (TownRules.VisitingHero(state, town) is { } hero)
        {
            Visit(hero, town);
        }
    }

    /// <summary>A visiting hero with Stewardship takes over a town without a governor, unless it governs another town.</summary>
    internal void AutoAppoint(Town town, Hero hero)
    {
        if (town.Owner == hero.Owner && StewardshipRules.GovernorOf(state, town) is null
            && StewardshipRules.CheckAppoint(state, town, hero) is null && StewardshipRules.TownGovernedBy(state, hero) is null)
        {
            SetGovernor(town, hero);
        }
    }

    /// <summary>The town's governor becomes <paramref name="hero"/> (null: none); the hero leaves any other town it governed.</summary>
    internal void SetGovernor(Town town, Hero? hero)
    {
        if (hero is not null && StewardshipRules.TownGovernedBy(state, hero) is { } other && other != town)
        {
            other.Governor = null;
            events.Publish(new GovernorChanged(other.Id, hero.Id, null));
        }

        var previous = StewardshipRules.GovernorOf(state, town)?.Id;
        town.Governor = hero?.Id;
        events.Publish(new GovernorChanged(town.Id, previous, hero?.Id));
    }

    /// <summary>
    /// A town falls to <paramref name="owner"/>: an empty garrison was walked into, or the garrison was beaten. A Capitol drops to
    /// town hall 3 when the new owner already has one (ADR-070, TOWN-3).
    /// </summary>
    internal void Capture(Town town, PlayerId owner)
    {
        if (!town.Garrison.IsEmpty)
        {
            for (var slot = 0; slot < Army.SlotCount; slot++)
            {
                town.Garrison.Set(slot, null);
            }

            events.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
        }

        var previous = town.Owner;
        town.Owner = owner;
        town.Governor = null;
        events.Publish(new TownOwnerChanged(town.Id, previous, owner));

        if (town.Level(BuiltInBuildings.TownHall) >= BuiltInBuildings.CapitolLevel && TownRules.CapitolOf(state, owner, except: town) is not null)
        {
            town.SetLevel(BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel - 1);
            events.Publish(new TownBuildingDowngraded(town.Id, BuiltInBuildings.TownHall, BuiltInBuildings.CapitolLevel - 1));
        }
    }

    /// <summary>
    /// An own hero in a town without a Grail building raises it with the Grail it carries (ADR-130, ADR-140): the Grail is given up, the building
    /// stands at once and does not count as the town's building of the day.
    /// </summary>
    private void RaiseGrailBuilding(Hero hero, Town town)
    {
        if (town.Owner != hero.Owner || town.Level(BuiltInBuildings.Grail) > 0 || !TownRules.Buildings(state, town).Contains(BuiltInBuildings.Grail)
            || state.Artifacts.Grail is not { } grail || !hero.Artifacts.Has(grail.Id))
        {
            return;
        }

        if (!hero.Artifacts.RemoveFromBackpack(grail.Id))
        {
            hero.Artifacts.RemoveWorn(grail.Id);
            state.RefreshArtifacts(hero);
        }

        events.Publish(new ArtifactLost(hero.Id, grail.Id));
        events.Publish(new HeroArtifactsChanged(hero.Id));
        town.SetLevel(BuiltInBuildings.Grail, 1);
        events.Publish(new TownBuilt(town.Id, BuiltInBuildings.Grail, 1));
        events.Publish(new GrailBuildingRaised(hero.Id, town.Id, TownRules.Buildings(state, town).Get(BuiltInBuildings.Grail, 1).Name));
    }

    /// <summary>An own hero in the town takes the bonuses of its built unique buildings that it may take today (<see cref="UniqueBuildings.CanTake"/>).</summary>
    private void TakeUniqueBonuses(Hero hero, Town town)
    {
        foreach (var building in UniqueBuildings.Built(town).Where(b => UniqueBuildings.CanTake(state, town, b, hero)).ToList())
        {
            town.Visits.Record(hero.Id, state.Date);
            events.Publish(new UniqueBuildingVisited(hero.Id, town.Id, building.Id));
            Give(hero, building.Bonus);
        }
    }

    private void Give(Hero hero, UniqueBonus bonus)
    {
        switch (bonus)
        {
            case UniqueBonus.Stables:
                // The same status as the map's Stables, so the two do not add up (ADR-126: without the free Lancer upgrade).
                progression.GrantStatus(hero, new HeroStatus(BuiltInMapObjects.Stables, HeroStatusKind.Movement, BuffRules.StablesMovement, StatusExpiry.EndOfWeek));
                hero.MovementPoints += BuffRules.StablesMovement;
                events.Publish(new HeroMovementGained(hero.Id, BuffRules.StablesMovement, hero.MovementPoints));
                break;
            case UniqueBonus.Attack:
                progression.RaiseStat(hero, PrimaryStat.Attack, 1);
                break;
            case UniqueBonus.Defense:
                progression.RaiseStat(hero, PrimaryStat.Defense, 1);
                break;
            case UniqueBonus.Knowledge:
                progression.RaiseStat(hero, PrimaryStat.Knowledge, 1);
                break;
            case UniqueBonus.SpellPower:
                progression.RaiseStat(hero, PrimaryStat.SpellPower, 1);
                break;
            case UniqueBonus.Experience:
                progression.GrantExperience(hero, UniqueBuildings.ExperienceBonus);
                break;
            case UniqueBonus.ManaSpring:
                var before = hero.Mana;
                hero.Mana = before + (hero.MaxMana * UniqueBuildings.ManaSpringPercent / 100);
                if (hero.Mana > before)
                {
                    events.Publish(new HeroManaRestored(hero.Id, hero.Mana, hero.MaxMana));
                }

                break;
        }
    }
}
