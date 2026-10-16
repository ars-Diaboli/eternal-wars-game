using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Events;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Towns;

/// <summary>
/// Caravans (ADR-054): hiring creatures from owned map dwellings for a town, and sending troops between own towns. The
/// creatures are taken (and paid for) at once and arrive at the start of the owner's turn after the travel time. Each day
/// the route is checked again: when it is blocked the caravan returns, when the destination changed hands the creatures
/// scatter (user decisions, 2026-10-17).
/// </summary>
public sealed class CaravanService(GameState state, IEventPublisher events, EconomyService economy)
{
    /// <summary>Hires creatures from an owned dwelling for the town (its governor's Stewardship discount applies).</summary>
    internal void Order(PlayerId player, MapObjectId townId, MapObjectId dwellingId, int count)
    {
        var town = OwnTown(player, townId);
        var dwelling = state.FindObject(dwellingId) as CreatureDwelling ?? throw new InvalidOperationException($"{dwellingId} is not a dwelling.");
        Refuse(CaravanRules.CheckOrder(state, town, dwelling, count));

        var creature = state.Creatures.Get(dwelling.Creature);
        var route = CaravanRules.RouteBetween(state, dwelling, town, creature, out _)!;
        var price = TownRules.RecruitCost(state, town, creature, count);
        if (!economy.TrySpend(player, price))
        {
            throw new InvalidOperationException("Not enough resources.");
        }

        dwelling.Available = (dwelling.Available ?? 0) - count;
        Dispatch(new Caravan(state.NextCaravanId(), player, dwelling.Id, town.Id, creature.Id, count, route.Days, price));
        events.Publish(new DwellingRecruited(dwelling.Id, null, creature.Id, count, dwelling.Available.Value));
    }

    /// <summary>Takes a stack (or part of it) out of a garrison and sends it to another own town.</summary>
    internal void Send(PlayerId player, MapObjectId fromId, int slot, MapObjectId toId, int? count)
    {
        var from = OwnTown(player, fromId);
        var to = OwnTown(player, toId);
        Refuse(CaravanRules.CheckSend(state, from, slot, to, count));

        var stack = from.Garrison[slot]!;
        var number = count ?? stack.Count;
        var route = CaravanRules.RouteBetween(state, from, to, state.Creatures.Get(stack.Creature), out _)!;
        from.Garrison.Set(slot, number == stack.Count ? null : new CreatureStack(stack.Creature, stack.Count - number));
        events.Publish(new GarrisonChanged(from.Id, from.Garrison.Slots.ToList()));
        Dispatch(new Caravan(state.NextCaravanId(), player, from.Id, to.Id, stack.Creature, number, route.Days, ResourceSet.Empty));
    }

    /// <summary>Start of the owner's turn: every caravan of theirs checks its route, travels a day and may arrive.</summary>
    internal void Advance(PlayerId player)
    {
        foreach (var caravan in state.Caravans.Where(c => c.Owner == player).OrderBy(c => c.Id).ToList())
        {
            if (state.FindObject(caravan.Destination) is not Town { Owner: { } owner } destination || owner != player)
            {
                Lose(caravan, "its destination was lost");
                continue;
            }

            if (caravan.DaysLeft > 0)
            {
                var source = state.GetObject(caravan.Source);
                var creature = state.Creatures.Get(caravan.Creature);
                if (CaravanRules.FindRoute(state, player, source.VisitPosition, destination.VisitPosition, creature) is null)
                {
                    GoBack(caravan);
                    continue;
                }

                if (caravan.DaysLeft > 1)
                {
                    state.ReplaceCaravan(caravan, caravan with { DaysLeft = caravan.DaysLeft - 1 });
                    continue;
                }
            }

            // Arriving; with no free slot it waits at the gates until there is one.
            if (destination.Garrison.TryAdd(new CreatureStack(caravan.Creature, caravan.Count)))
            {
                state.RemoveCaravan(caravan);
                events.Publish(new GarrisonChanged(destination.Id, destination.Garrison.Slots.ToList()));
                events.Publish(new CaravanArrived(caravan.Id, destination.Id, caravan.Creature, caravan.Count));
            }
            else if (caravan.DaysLeft != 0)
            {
                state.ReplaceCaravan(caravan, caravan with { DaysLeft = 0 });
            }
        }
    }

    private void Dispatch(Caravan caravan)
    {
        state.AddCaravan(caravan);
        events.Publish(new CaravanDispatched(caravan.Id, caravan.Owner, caravan.Source, caravan.Destination, caravan.Creature, caravan.Count, caravan.DaysLeft));
    }

    /// <summary>The route is blocked: creatures go back to a dwelling (with the price refunded) or a town's garrison.</summary>
    private void GoBack(Caravan caravan)
    {
        state.RemoveCaravan(caravan);
        switch (state.FindObject(caravan.Source))
        {
            case CreatureDwelling dwelling:
                dwelling.Available = (dwelling.Available ?? 0) + caravan.Count;
                economy.Grant(caravan.Owner, caravan.Paid);
                events.Publish(new CaravanReturned(caravan.Id, dwelling.Id, caravan.Creature, caravan.Count));
                break;
            case Town { Owner: { } owner } town when owner == caravan.Owner && town.Garrison.TryAdd(new CreatureStack(caravan.Creature, caravan.Count)):
                events.Publish(new GarrisonChanged(town.Id, town.Garrison.Slots.ToList()));
                events.Publish(new CaravanReturned(caravan.Id, town.Id, caravan.Creature, caravan.Count));
                break;
            default:
                events.Publish(new CaravanLost(caravan.Id, caravan.Destination, caravan.Creature, caravan.Count, "the way back was closed"));
                break;
        }
    }

    private void Lose(Caravan caravan, string reason)
    {
        state.RemoveCaravan(caravan);
        events.Publish(new CaravanLost(caravan.Id, caravan.Destination, caravan.Creature, caravan.Count, reason));
    }

    private Town OwnTown(PlayerId player, MapObjectId id)
    {
        if (state.FindObject(id) is not Town town || town.Owner != player)
        {
            throw new InvalidOperationException($"{id} is not an own town.");
        }

        if (state.PendingDecision is not null)
        {
            throw new InvalidOperationException("Resolve the pending decision first.");
        }

        return town;
    }

    private static void Refuse(string? refusal)
    {
        if (refusal is not null)
        {
            throw new InvalidOperationException(refusal);
        }
    }
}
