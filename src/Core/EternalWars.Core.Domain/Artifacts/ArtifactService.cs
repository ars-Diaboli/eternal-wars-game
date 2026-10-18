using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Artifacts;

/// <summary>
/// Artifacts in play (ADR-061): wearing, taking off and giving between heroes (owner's turn, free of movement points),
/// pickups from the map, loot from a defeated hero, and rolling the pickups the map left open.
/// </summary>
public sealed class ArtifactService(GameState state, IEventPublisher events)
{
    /// <summary>The artifacts every pickup of a tier may be, minus those already placed on the map or carried (relics stay unique).</summary>
    internal void RollUnsetPickups()
    {
        var taken = state.Heroes.SelectMany(h => h.Artifacts.AllWorn.Select(w => w.Id).Concat(h.Artifacts.Backpack))
            .Concat(state.ObjectsOf<ArtifactPickup>().Select(p => p.Artifact).OfType<string>())
            .ToHashSet(StringComparer.Ordinal);
        foreach (var pickup in state.ObjectsOf<ArtifactPickup>().Where(p => p.Artifact is null).OrderBy(p => p.Id.Value).ToList())
        {
            var tier = state.Artifacts.OfTier(pickup.Tier).OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
            var free = tier.Where(a => !taken.Contains(a.Id)).ToList();
            var pool = free.Count > 0 ? free : tier;
            if (pool.Count == 0)
            {
                continue;
            }

            pickup.Artifact = pool[state.Random.Next(pool.Count)].Id;
            taken.Add(pickup.Artifact);
        }
    }

    /// <summary>Puts a found artifact in the hero's backpack; false when it is full (nothing happens).</summary>
    internal bool Grant(Hero hero, string artifact, ArtifactSource source)
    {
        if (!hero.Artifacts.AddToBackpack(artifact))
        {
            events.Publish(new ArtifactBackpackFull(hero.Id, artifact));
            return false;
        }

        events.Publish(new ArtifactGained(hero.Id, artifact, source));
        return true;
    }

    /// <summary>
    /// A defeated hero's artifacts, worn and carried, all go into the winner's backpack. When they do not all fit, the most
    /// valuable ones come first (<see cref="ArtifactValue.BestFirst"/>) and the rest are lost.
    /// </summary>
    internal void Loot(Hero from, Hero to)
    {
        var all = ArtifactValue.BestFirst(state.Artifacts, from.Artifacts.AllWorn.Select(w => w.Id).Concat(from.Artifacts.Backpack)).ToList();
        foreach (var artifact in all)
        {
            if (to.Artifacts.AddToBackpack(artifact))
            {
                events.Publish(new ArtifactGained(to.Id, artifact, ArtifactSource.Loot));
            }
            else
            {
                events.Publish(new ArtifactLost(to.Id, artifact));
            }
        }
    }

    internal void Equip(PlayerId player, HeroId heroId, string artifact)
    {
        var hero = Own(player, heroId, exchangeAllowed: true);
        var definition = state.Artifacts.Get(artifact);
        if (!hero.Artifacts.Backpack.Contains(artifact))
        {
            throw new InvalidOperationException($"{hero.Name} does not carry '{definition.Name}'.");
        }

        hero.Artifacts.RemoveFromBackpack(artifact);
        if (!hero.Artifacts.HasRoomToWear(definition.Slot))
        {
            // The one worn there goes to the backpack, which has the place just freed.
            var replaced = hero.Artifacts.Worn(definition.Slot)[0];
            hero.Artifacts.RemoveWorn(replaced);
            hero.Artifacts.AddToBackpack(replaced);
            events.Publish(new ArtifactUnequipped(hero.Id, replaced));
        }

        hero.Artifacts.Wear(definition);
        state.RefreshArtifacts(hero);
        events.Publish(new ArtifactEquipped(hero.Id, artifact, definition.Slot));
    }

    internal void Unequip(PlayerId player, HeroId heroId, string artifact)
    {
        var hero = Own(player, heroId, exchangeAllowed: true);
        if (!hero.Artifacts.IsWorn(artifact))
        {
            throw new InvalidOperationException($"{hero.Name} does not wear '{artifact}'.");
        }

        if (hero.Artifacts.BackpackFull)
        {
            throw new InvalidOperationException($"{hero.Name}'s backpack is full.");
        }

        hero.Artifacts.RemoveWorn(artifact);
        hero.Artifacts.AddToBackpack(artifact);
        state.RefreshArtifacts(hero);
        events.Publish(new ArtifactUnequipped(hero.Id, artifact));
    }

    /// <summary>Hands an artifact (from the backpack first, else off the body) to another own hero whose exchange is open.</summary>
    internal void Give(PlayerId player, HeroId fromId, string artifact, HeroId toId)
    {
        var from = Own(player, fromId, exchangeAllowed: true);
        var to = Own(player, toId, exchangeAllowed: true);
        if (from == to)
        {
            throw new InvalidOperationException("A hero cannot give an artifact to itself.");
        }

        if (state.PendingDecision is not HeroExchangeDecision exchange
            || !((exchange.Hero == from.Id && exchange.Other == to.Id) || (exchange.Hero == to.Id && exchange.Other == from.Id)))
        {
            throw new InvalidOperationException("Artifacts change hands only while the exchange of the two heroes is open.");
        }

        var carried = from.Artifacts.Backpack.Contains(artifact);
        if (!carried && !from.Artifacts.IsWorn(artifact))
        {
            throw new InvalidOperationException($"{from.Name} does not have '{artifact}'.");
        }

        if (to.Artifacts.BackpackFull)
        {
            throw new InvalidOperationException($"{to.Name}'s backpack is full.");
        }

        if (carried)
        {
            from.Artifacts.RemoveFromBackpack(artifact);
        }
        else
        {
            from.Artifacts.RemoveWorn(artifact);
            state.RefreshArtifacts(from);
        }

        to.Artifacts.AddToBackpack(artifact);
        events.Publish(new ArtifactGiven(from.Id, to.Id, artifact));
    }

    private Hero Own(PlayerId player, HeroId id, bool exchangeAllowed)
    {
        var hero = state.Heroes.FirstOrDefault(h => h.Id == id) ?? throw new InvalidOperationException($"Unknown hero {id}.");
        if (hero.Owner != player)
        {
            throw new InvalidOperationException($"{hero.Name} does not belong to {player}.");
        }

        if (state.PendingDecision is not null && !(exchangeAllowed && state.PendingDecision is HeroExchangeDecision))
        {
            throw new InvalidOperationException("Resolve the pending decision first.");
        }

        return hero;
    }
}
