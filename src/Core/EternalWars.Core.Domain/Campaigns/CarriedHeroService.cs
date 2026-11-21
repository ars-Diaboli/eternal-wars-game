using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Campaigns;

/// <summary>Puts a campaign map's carried heroes on the map when the game starts (ADR-241), by the rules of <see cref="CampaignRules"/>.</summary>
public sealed class CarriedHeroService(GameState state, IEventPublisher events)
{
    /// <summary>
    /// Puts the carried heroes (<see cref="GameOptions.CarriedHeroes"/>) on the map when the game starts, before the placeholders are drawn: each
    /// becomes the campaign person's hero of the same roster hero, else the person's first hero placeholder not yet used. A carried hero the map has
    /// no place for, or whose roster hero is another player's or in a prison, waits for a later map (<see cref="GameState.CarriedWaiting"/>).
    /// </summary>
    internal void Place()
    {
        if (state.Options.CarriedHeroes is not { Count: > 0 } carried)
        {
            return;
        }

        if (CampaignRules.PlayerOf(state) is not { } player)
        {
            state.CarriedWaiting = [.. carried];
            return;
        }

        var elsewhere = state.Heroes.Concat(state.GarrisonHeroes).Where(h => h.Owner != player.Id).Select(h => h.Definition)
            .Concat(state.ObjectsOf<Prison>().Select(p => p.Hero))
            .Concat(state.RetiredHeroes.Select(h => h.Definition))
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var waiting = new List<CarriedHero>();
        var used = new HashSet<Hero>();
        foreach (var hero in carried)
        {
            var mine = state.AllHeroesOf(player.Id).Where(h => !used.Contains(h)).OrderBy(h => h.Id.Value).ToList();
            var target = mine.FirstOrDefault(h => !h.IsPlaceholder && h.Definition == hero.Hero) ?? mine.FirstOrDefault(h => h.IsPlaceholder);
            if (elsewhere.Contains(hero.Hero) || !state.HeroRoster.Contains(hero.Hero) || target is null)
            {
                waiting.Add(hero);
                continue;
            }

            used.Add(target);
            Apply(target, hero);
            if (state.Heroes.Contains(target))
            {
                events.Publish(new HeroRemoved(target.Id));
                events.Publish(new HeroPlaced(target.Id, target.Owner, target.Position));
            }
        }

        state.CarriedWaiting = waiting;
    }

    /// <summary>Makes <paramref name="hero"/> the carried hero: its roster hero, then what it gained; its army and artifacts when carried.</summary>
    private void Apply(Hero hero, CarriedHero carried)
    {
        if (hero.Definition != carried.Hero || hero.IsPlaceholder)
        {
            hero.Apply(state.HeroRoster.Get(carried.Hero));
        }

        hero.IsPlaceholder = false;
        (hero.Biography, hero.Portrait) = (null, null); // the carried hero, not the map's (ADR-257)
        hero.Name = carried.Name;
        hero.Level = carried.Level;
        hero.Experience = carried.Experience;
        hero.Attack = carried.Attack;
        hero.Defense = carried.Defense;
        hero.SpellPower = carried.SpellPower;
        hero.Knowledge = carried.Knowledge;
        hero.Skills.ReplaceWith(carried.Skills);
        hero.SetSpells(carried.SpellBook, carried.Spells);
        hero.SetWarMachines(carried.WarMachines);
        if (carried.Artifacts is not null || carried.Backpack is not null)
        {
            hero.Artifacts.ReplaceWith(carried.Artifacts ?? [], carried.Backpack ?? [], state.Artifacts);
        }

        if (carried.Army is { } army)
        {
            for (var slot = 0; slot < Army.SlotCount; slot++)
            {
                hero.Army.Set(slot, null);
            }

            foreach (var stack in army.Where(s => s.Slot is >= 0 and < Army.SlotCount && s.Count > 0))
            {
                hero.Army.Set(stack.Slot, new CreatureStack(stack.Creature, stack.Count));
            }
        }

        if (hero.Army.IsEmpty && TavernRules.StarterStack(state, state.HeroRoster.Get(carried.Hero)) is { } starter)
        {
            hero.Army.TryAdd(starter);
        }

        state.RefreshArtifacts(hero);
    }
}
