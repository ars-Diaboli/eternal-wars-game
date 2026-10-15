using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Armies;

/// <summary>
/// Rearranging armies: within one hero at any time on the owner's turn, between two heroes while their
/// exchange is open. A hero always keeps at least one stack.
/// </summary>
public sealed class ArmyService(GameState state, IEventPublisher events)
{
    /// <summary>
    /// Moves <paramref name="count"/> creatures (null = the whole stack) from one slot to another. Into an empty
    /// slot or onto the same creature the stacks merge; a whole stack onto a different creature swaps them.
    /// Throws <see cref="InvalidOperationException"/> when the move is not allowed.
    /// </summary>
    internal void MoveStack(PlayerId player, HeroId fromHeroId, int fromSlot, HeroId toHeroId, int toSlot, int? count)
    {
        var from = state.GetHero(fromHeroId);
        var to = state.GetHero(toHeroId);
        if (from.Owner != player || to.Owner != player)
        {
            throw new InvalidOperationException("Both heroes must belong to the player.");
        }

        if (from != to && !(state.PendingDecision is HeroExchangeDecision exchange && IsBetween(exchange, from.Id, to.Id)))
        {
            throw new InvalidOperationException("Stacks move between heroes only while their exchange is open.");
        }

        if (from == to && state.PendingDecision is not null and not HeroExchangeDecision)
        {
            throw new InvalidOperationException("Resolve the pending decision first.");
        }

        if (!Army.IsValidSlot(fromSlot) || !Army.IsValidSlot(toSlot) || (from == to && fromSlot == toSlot))
        {
            throw new InvalidOperationException("Invalid army slot.");
        }

        var source = from.Army[fromSlot] ?? throw new InvalidOperationException("The source slot is empty.");
        var moving = count ?? source.Count;
        if (moving < 1 || moving > source.Count)
        {
            throw new InvalidOperationException($"Cannot move {moving} of {source.Count} creatures.");
        }

        var target = to.Army[toSlot];
        var whole = moving == source.Count;
        if (target is not null && target.Creature != source.Creature && !whole)
        {
            throw new InvalidOperationException("A split stack can only go to an empty slot or the same creature.");
        }

        var swap = target is not null && target.Creature != source.Creature;
        if (whole && !swap && from != to && from.Army.StackCount == 1)
        {
            throw new InvalidOperationException("A hero must keep at least one stack.");
        }

        if (swap)
        {
            from.Army.Set(fromSlot, target);
            to.Army.Set(toSlot, source);
        }
        else
        {
            from.Army.Set(fromSlot, whole ? null : new CreatureStack(source.Creature, source.Count - moving));
            to.Army.Set(toSlot, new CreatureStack(source.Creature, (target?.Count ?? 0) + moving));
        }

        events.Publish(new ArmyChanged(from.Id, from.Army.Slots.ToList()));
        if (to != from)
        {
            events.Publish(new ArmyChanged(to.Id, to.Army.Slots.ToList()));
        }

        static bool IsBetween(HeroExchangeDecision decision, HeroId a, HeroId b) =>
            (decision.Hero == a && decision.Other == b) || (decision.Hero == b && decision.Other == a);
    }

    internal void Publish(Hero hero) => events.Publish(new ArmyChanged(hero.Id, hero.Army.Slots.ToList()));
}
