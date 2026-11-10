using EternalWars.Core.Domain.Commands;
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

    /// <summary>
    /// A blank spell scroll with a spell written on it (ADR-136): a random spell of its level from the game's random source, without the map's
    /// banned spells unless they ban the whole level. Any other artifact, or a written scroll, comes back as it is.
    /// </summary>
    internal string Inscribe(string artifact)
    {
        if (!IsBlankScroll(artifact) || state.Artifacts.Get(artifact).ScrollLevel is not { } level)
        {
            return artifact;
        }

        var spells = MapBans.Allowed(state.Artifacts.ScrollSpells(level), state.Rules.Bans.BansSpell);
        return spells.Count == 0 ? artifact : ArtifactCatalog.WithSpell(artifact, spells[state.Random.Next(spells.Count)]);
    }

    /// <summary>
    /// Game start: every pickup the map left open gets an artifact of its tier, minus those already placed on the map or carried (relics stay
    /// unique); then every blank scroll on a pickup or a hero gets its spell (<see cref="Inscribe"/>; the generator and the editor's chosen spells
    /// are kept, ADR-136).
    /// </summary>
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

            pickup.Artifact = ArtifactRolls.Pick(pool, state.Random).Id;
            taken.Add(pickup.Artifact);
        }

        foreach (var pickup in state.ObjectsOf<ArtifactPickup>().Where(p => p.Artifact is not null).OrderBy(p => p.Id.Value))
        {
            pickup.Artifact = Inscribe(pickup.Artifact!);
        }

        foreach (var hero in state.Heroes.OrderBy(h => h.Id.Value))
        {
            InscribeCarried(hero);
        }
    }

    private bool IsBlankScroll(string artifact) => state.Artifacts.TryGet(artifact, out var definition) && definition is { IsScroll: true, Spell: null };

    /// <summary>Writes the spells of the blank scrolls the hero wears and carries.</summary>
    private void InscribeCarried(Hero hero)
    {
        foreach (var blank in hero.Artifacts.AllWorn.Select(w => w.Id).Where(IsBlankScroll).ToList())
        {
            hero.Artifacts.RemoveWorn(blank);
            hero.Artifacts.Wear(state.Artifacts.Get(Inscribe(blank)));
        }

        foreach (var blank in hero.Artifacts.Backpack.Where(IsBlankScroll).ToList())
        {
            hero.Artifacts.RemoveFromBackpack(blank);
            hero.Artifacts.AddToBackpack(Inscribe(blank));
        }

        state.RefreshArtifacts(hero);
    }

    /// <summary>
    /// Draws an artifact of a tier for a map reward (ADR-068): the tier's scroll in <see cref="ArtifactRolls.ScrollPercent"/> of the draws, with its
    /// spell written (ADR-136). Minor and Major artifacts may repeat. Relics come from a pool: neither one drawn
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
            return Inscribe(ArtifactRolls.Pick(all, state.Random).Id);
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

        var drawn = ArtifactRolls.Pick(pool, state.Random).Id;
        state.DrawnRelics.Add(drawn);
        return Inscribe(drawn);
    }

    /// <summary>
    /// Puts a found artifact in the hero's backpack, a blank scroll (a map event's, a Pandora's Box's) with its spell written (ADR-136); false when the
    /// backpack is full (nothing happens).
    /// </summary>
    internal bool Grant(Hero hero, string artifact, ArtifactSource source)
    {
        artifact = Inscribe(artifact);
        if (!hero.Artifacts.AddToBackpack(artifact))
        {
            events.Publish(new ArtifactBackpackFull(hero.Id, artifact));
            return false;
        }

        events.Publish(new ArtifactGained(hero.Id, artifact, source));
        return true;
    }

    /// <summary>
    /// Takes an artifact away from the hero (given up or taken by the map): from the backpack, else off the body, or the other way round with
    /// <paramref name="wornFirst"/>. False when the hero has none. The caller publishes what it means.
    /// </summary>
    internal bool Remove(Hero hero, string artifact, bool wornFirst = false)
    {
        var removed = wornFirst
            ? hero.Artifacts.RemoveWorn(artifact) || hero.Artifacts.RemoveFromBackpack(artifact)
            : hero.Artifacts.RemoveFromBackpack(artifact) || hero.Artifacts.RemoveWorn(artifact);
        if (removed)
        {
            state.RefreshArtifacts(hero);
        }

        return removed;
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
            throw new CommandRefusedException($"{definition.Name} is not an assembled relic.");
        }

        if (definition.Pieces.FirstOrDefault(p => !hero.Artifacts.IsWorn(p)) is { } missing)
        {
            throw new CommandRefusedException($"{hero.Name} must wear {state.Artifacts.Get(missing).Name} to assemble {definition.Name}.");
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
            throw new CommandRefusedException($"{definition.Name} is not an assembled relic.");
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
            throw new CommandRefusedException($"{hero.Name} has no {definition.Name}.");
        }

        if (hero.Artifacts.Backpack.Count - 1 + definition.Pieces.Count > ArtifactSlots.BackpackSize)
        {
            throw new CommandRefusedException($"{hero.Name}'s backpack has no room for the pieces of {definition.Name}.");
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
            throw new CommandRefusedException($"{hero.Name} does not carry '{definition.Name}'.");
        }

        var (blocks, swap) = Fit(hero, definition);
        hero.Artifacts.RemoveFromBackpack(artifact);
        PutOn(hero, definition, blocks, swap);
    }

    /// <summary>
    /// Where the artifact would go on the hero: the slots an assembled relic blocks (they must be free already), and whether the one worn in its
    /// full slot goes to the backpack to make room. Throws when it cannot be worn.
    /// </summary>
    private (IReadOnlyList<ArtifactSlot> Blocks, bool Swap) Fit(Hero hero, ArtifactDefinition definition)
    {
        var blocks = state.Artifacts.BlockedSlots(definition);
        var swap = !hero.Artifacts.HasRoomToWear(definition.Slot) && hero.Artifacts.Worn(definition.Slot).Count > 0;
        if (blocks.Append(definition.Slot).GroupBy(s => s)
                .FirstOrDefault(g => hero.Artifacts.Free(g.Key) + (swap && g.Key == definition.Slot ? 1 : 0) < g.Count()) is { } full)
        {
            throw new CommandRefusedException(full.Key == definition.Slot && !swap
                ? $"The {definition.Slot} slot is blocked by an assembled relic."
                : $"{definition.Name} also needs a free {full.Key} slot.");
        }

        return (blocks, swap);
    }

    /// <summary>Wears an artifact that is in neither the backpack nor a slot (checked by <see cref="Fit"/>); a swapped one goes to the backpack.</summary>
    private void PutOn(Hero hero, ArtifactDefinition definition, IReadOnlyList<ArtifactSlot> blocks, bool swap)
    {
        if (swap)
        {
            var replaced = hero.Artifacts.Worn(definition.Slot)[0];
            hero.Artifacts.RemoveWorn(replaced);
            hero.Artifacts.AddToBackpack(replaced);
            events.Publish(new ArtifactUnequipped(hero.Id, replaced));
        }

        if (!hero.Artifacts.Wear(definition, blocks))
        {
            hero.Artifacts.AddToBackpack(definition.Id);
            state.RefreshArtifacts(hero);
            throw new CommandRefusedException($"{definition.Name} does not fit.");
        }

        state.RefreshArtifacts(hero);
        events.Publish(new ArtifactEquipped(hero.Id, definition.Id, definition.Slot));
        if (state.Artifacts.RelicOf(definition.Id) is { } relic && Assemblable(state.Artifacts, hero).Any(r => r.Id == relic))
        {
            events.Publish(new RelicReady(hero.Id, relic));
        }
    }

    internal void Unequip(PlayerId player, HeroId heroId, string artifact)
    {
        var hero = Own(player, heroId, exchangeAllowed: true);
        if (!hero.Artifacts.IsWorn(artifact))
        {
            throw new CommandRefusedException($"{hero.Name} does not wear '{artifact}'.");
        }

        if (hero.Artifacts.BackpackFull)
        {
            throw new CommandRefusedException($"{hero.Name}'s backpack is full.");
        }

        hero.Artifacts.RemoveWorn(artifact);
        hero.Artifacts.AddToBackpack(artifact);
        state.RefreshArtifacts(hero);
        events.Publish(new ArtifactUnequipped(hero.Id, artifact));
    }

    /// <summary>
    /// Hands an artifact to another own hero whose exchange is open: the worn one when <paramref name="worn"/>, else from the backpack first, else
    /// off the body. It goes into the receiver's backpack, or with <paramref name="wear"/> onto its body (the one worn in a full slot goes to its
    /// backpack); nothing changes when it cannot.
    /// </summary>
    internal void Give(PlayerId player, HeroId fromId, string artifact, HeroId toId, bool worn = false, bool wear = false)
    {
        var from = Own(player, fromId, exchangeAllowed: true);
        var to = Own(player, toId, exchangeAllowed: true);
        if (from == to)
        {
            throw new CommandRefusedException("A hero cannot give an artifact to itself.");
        }

        if (state.PendingDecision is not HeroExchangeDecision exchange
            || !((exchange.Hero == from.Id && exchange.Other == to.Id) || (exchange.Hero == to.Id && exchange.Other == from.Id)))
        {
            throw new CommandRefusedException("Artifacts change hands only while the exchange of the two heroes is open.");
        }

        var carried = !worn && from.Artifacts.Backpack.Contains(artifact);
        if (!carried && !from.Artifacts.IsWorn(artifact))
        {
            throw new CommandRefusedException(worn ? $"{from.Name} does not wear '{artifact}'." : $"{from.Name} does not have '{artifact}'.");
        }

        var definition = state.Artifacts.Get(artifact);
        var fit = wear ? Fit(to, definition) : default;
        if ((!wear || fit.Swap) && to.Artifacts.BackpackFull)
        {
            throw new CommandRefusedException($"{to.Name}'s backpack is full.");
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

        if (wear)
        {
            PutOn(to, definition, fit.Blocks, fit.Swap);
        }
        else
        {
            to.Artifacts.AddToBackpack(artifact);
        }

        events.Publish(new ArtifactGiven(from.Id, to.Id, artifact));
    }

    private Hero Own(PlayerId player, HeroId id, bool exchangeAllowed)
    {
        var hero = state.Heroes.FirstOrDefault(h => h.Id == id) ?? throw new CommandRefusedException($"Unknown hero {id}.");
        if (hero.Owner != player)
        {
            throw new CommandRefusedException($"{hero.Name} does not belong to {player}.");
        }

        if (state.PendingDecision is not null && !(exchangeAllowed && state.PendingDecision is HeroExchangeDecision))
        {
            throw new CommandRefusedException("Resolve the pending decision first.");
        }

        return hero;
    }
}
