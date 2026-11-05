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
    /// <summary>The artifacts of a tier the rolls may give, by id: without the map's bans, unless they ban the whole tier (ADR-126).</summary>
    private IReadOnlyList<ArtifactDefinition> OfTier(ArtifactTier tier) =>
        MapBans.Allowed(state.Artifacts.OfTier(tier).OrderBy(a => a.Id, StringComparer.Ordinal).ToList(), a => state.Rules.Bans.BansArtifact(a.Id));

    /// <summary>The artifacts every pickup of a tier may be, minus those already placed on the map or carried (relics stay unique).</summary>
    internal void RollUnsetPickups()
    {
        var taken = state.Heroes.SelectMany(h => h.Artifacts.AllWorn.Select(w => w.Id).Concat(h.Artifacts.Backpack))
            .Concat(state.ObjectsOf<ArtifactPickup>().Select(p => p.Artifact).OfType<string>())
            .ToHashSet(StringComparer.Ordinal);
        foreach (var pickup in state.ObjectsOf<ArtifactPickup>().Where(p => p.Artifact is null).OrderBy(p => p.Id.Value).ToList())
        {
            var tier = OfTier(pickup.Tier);
            var free = tier.Where(a => !taken.Contains(a.Id)).ToList();
            var pool = free.Count > 0 ? free : [.. tier];
            if (pool.Count == 0)
            {
                continue;
            }

            pickup.Artifact = pool[state.Random.Next(pool.Count)].Id;
            taken.Add(pickup.Artifact);
        }
    }

    /// <summary>
    /// Draws an artifact of a tier for a map reward (ADR-068). Minor and Major artifacts may repeat. Relics come from a pool: neither one drawn
    /// before nor one lying on the map or carried by a hero; when none is left, the pool is refilled and the draw starts anew, so a hero may end
    /// up with two of one relic. Null only when the catalog has no artifact of the tier.
    /// </summary>
    internal string? Draw(ArtifactTier tier)
    {
        var all = OfTier(tier);
        if (all.Count == 0)
        {
            return null;
        }

        if (tier != ArtifactTier.Relic)
        {
            return all[state.Random.Next(all.Count)].Id;
        }

        var present = state.Heroes.SelectMany(h => h.Artifacts.AllWorn.Select(w => w.Id).Concat(h.Artifacts.Backpack))
            .Concat(state.ObjectsOf<ArtifactPickup>().Select(p => p.Artifact).OfType<string>())
            .ToHashSet(StringComparer.Ordinal);
        var pool = all.Where(a => !state.DrawnRelics.Contains(a.Id) && !present.Contains(a.Id)).ToList();
        if (pool.Count == 0)
        {
            state.DrawnRelics.Clear();
            pool = all.Where(a => !present.Contains(a.Id)).ToList();
            pool = pool.Count > 0 ? pool : [.. all];
        }

        var drawn = pool[state.Random.Next(pool.Count)].Id;
        state.DrawnRelics.Add(drawn);
        return drawn;
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
    /// valuable ones come first (<see cref="ArtifactValue.BestFirst"/>) and the rest are lost. An assembled relic (ADR-121) goes over whole when
    /// the winner can wear it (its slot and the slots it blocks are free), otherwise as its pieces into the backpack.
    /// </summary>
    internal void Loot(Hero from, Hero to)
    {
        var all = ArtifactValue.BestFirst(state.Artifacts, from.Artifacts.AllWorn.Select(w => w.Id).Concat(from.Artifacts.Backpack)).ToList();
        foreach (var artifact in all)
        {
            var definition = state.Artifacts.TryGet(artifact, out var known) ? known : null;
            if (definition is { IsAssembled: true })
            {
                if (to.Artifacts.Wear(definition, state.Artifacts.BlockedSlots(definition)))
                {
                    state.RefreshArtifacts(to);
                    events.Publish(new ArtifactGained(to.Id, artifact, ArtifactSource.Loot));
                    events.Publish(new ArtifactEquipped(to.Id, artifact, definition.Slot));
                    continue;
                }

                foreach (var piece in definition.Pieces)
                {
                    Take(piece);
                }

                continue;
            }

            Take(artifact);
        }

        void Take(string artifact)
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

    /// <summary>The assembled relics whose every piece the hero wears (ADR-121), in id order.</summary>
    public static IEnumerable<ArtifactDefinition> Assemblable(ArtifactCatalog catalog, Hero hero) =>
        catalog.Relics.Where(r => r.Pieces.All(hero.Artifacts.IsWorn));

    /// <summary>Joins the worn pieces into the relic (ADR-121): the pieces go, the relic sits in the first piece's slot and blocks the others'.</summary>
    internal void Assemble(PlayerId player, HeroId heroId, string relic)
    {
        var hero = Own(player, heroId, exchangeAllowed: true);
        var definition = state.Artifacts.Get(relic);
        if (!definition.IsAssembled)
        {
            throw new InvalidOperationException($"{definition.Name} is not an assembled relic.");
        }

        if (definition.Pieces.FirstOrDefault(p => !hero.Artifacts.IsWorn(p)) is { } missing)
        {
            throw new InvalidOperationException($"{hero.Name} must wear {state.Artifacts.Get(missing).Name} to assemble {definition.Name}.");
        }

        foreach (var piece in definition.Pieces)
        {
            hero.Artifacts.RemoveWorn(piece);
        }

        hero.Artifacts.Wear(definition, state.Artifacts.BlockedSlots(definition));
        state.RefreshArtifacts(hero);
        events.Publish(new RelicAssembled(hero.Id, relic, definition.Pieces));
    }

    /// <summary>
    /// Takes the relic apart (ADR-121): a worn one into its pieces, worn in their slots again; a carried one into pieces in the backpack, which needs
    /// the room.
    /// </summary>
    internal void TakeApart(PlayerId player, HeroId heroId, string relic)
    {
        var hero = Own(player, heroId, exchangeAllowed: true);
        var definition = state.Artifacts.Get(relic);
        if (!definition.IsAssembled)
        {
            throw new InvalidOperationException($"{definition.Name} is not an assembled relic.");
        }

        if (hero.Artifacts.IsWorn(relic))
        {
            hero.Artifacts.RemoveWorn(relic);
            foreach (var piece in definition.Pieces)
            {
                hero.Artifacts.Wear(state.Artifacts.Get(piece));
            }

            state.RefreshArtifacts(hero);
            events.Publish(new RelicTakenApart(hero.Id, relic, definition.Pieces, Worn: true));
            return;
        }

        if (!hero.Artifacts.Backpack.Contains(relic))
        {
            throw new InvalidOperationException($"{hero.Name} has no {definition.Name}.");
        }

        if (hero.Artifacts.Backpack.Count - 1 + definition.Pieces.Count > ArtifactSlots.BackpackSize)
        {
            throw new InvalidOperationException($"{hero.Name}'s backpack has no room for the pieces of {definition.Name}.");
        }

        hero.Artifacts.RemoveFromBackpack(relic);
        foreach (var piece in definition.Pieces)
        {
            hero.Artifacts.AddToBackpack(piece);
        }

        events.Publish(new RelicTakenApart(hero.Id, relic, definition.Pieces, Worn: false));
    }

    internal void Equip(PlayerId player, HeroId heroId, string artifact)
    {
        var hero = Own(player, heroId, exchangeAllowed: true);
        var definition = state.Artifacts.Get(artifact);
        if (!hero.Artifacts.Backpack.Contains(artifact))
        {
            throw new InvalidOperationException($"{hero.Name} does not carry '{definition.Name}'.");
        }

        // A full slot frees one place by sending what is worn there to the backpack; the slots an assembled relic blocks must be free already.
        var blocks = state.Artifacts.BlockedSlots(definition);
        var swap = !hero.Artifacts.HasRoomToWear(definition.Slot) && hero.Artifacts.Worn(definition.Slot).Count > 0;
        if (blocks.Append(definition.Slot).GroupBy(s => s)
                .FirstOrDefault(g => hero.Artifacts.Free(g.Key) + (swap && g.Key == definition.Slot ? 1 : 0) < g.Count()) is { } full)
        {
            throw new InvalidOperationException(full.Key == definition.Slot && !swap
                ? $"The {definition.Slot} slot is blocked by an assembled relic."
                : $"{definition.Name} also needs a free {full.Key} slot.");
        }

        hero.Artifacts.RemoveFromBackpack(artifact);
        if (swap)
        {
            // The one worn there goes to the backpack, which has the place just freed.
            var replaced = hero.Artifacts.Worn(definition.Slot)[0];
            hero.Artifacts.RemoveWorn(replaced);
            hero.Artifacts.AddToBackpack(replaced);
            events.Publish(new ArtifactUnequipped(hero.Id, replaced));
        }

        if (!hero.Artifacts.Wear(definition, blocks))
        {
            hero.Artifacts.AddToBackpack(artifact);
            state.RefreshArtifacts(hero);
            throw new InvalidOperationException($"{definition.Name} does not fit.");
        }

        state.RefreshArtifacts(hero);
        events.Publish(new ArtifactEquipped(hero.Id, artifact, definition.Slot));
        if (state.Artifacts.RelicOf(artifact) is { } relic && Assemblable(state.Artifacts, hero).Any(r => r.Id == relic))
        {
            events.Publish(new RelicReady(hero.Id, relic));
        }
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
