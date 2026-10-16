using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Towns;

/// <summary>
/// A caravan on its way (ADR-054): <see cref="Count"/> creatures of <see cref="Creature"/> from <see cref="Source"/> (an own map
/// dwelling, or the town whose troops were sent) to the garrison of the town <see cref="Destination"/>. They arrive when
/// <see cref="DaysLeft"/> runs out at the start of the owner's turns. <see cref="Paid"/> is what a dwelling's creatures cost,
/// refunded when the caravan has to return.
/// </summary>
public sealed record Caravan(int Id, PlayerId Owner, MapObjectId Source, MapObjectId Destination, string Creature, int Count, int DaysLeft, ResourceSet Paid);

/// <summary>A route found for a caravan: what it costs in movement points and how many days it takes.</summary>
public sealed record CaravanRoute(int Cost, int Days);

/// <summary>
/// Caravan rules as queries (ADR-054), shared by <see cref="CaravanService"/>, views and the AI. A caravan walks like an
/// army made of its creatures (their map speed, terrain and roads; no hero skills), but virtually: it never visits objects and
/// cannot be seen or attacked. A route is open when no hostile hero, neutral monster or tile guarded by one is on it; more than
/// <see cref="MaxDays"/> days of travel is too far.
/// </summary>
public static class CaravanRules
{
    /// <summary>A source farther away than this many days of travel cannot supply a town.</summary>
    public const int MaxDays = 5;

    /// <summary>The cheapest open route between two tiles for creatures of the given speed, or null when there is none.</summary>
    public static CaravanRoute? FindRoute(GameState state, PlayerId owner, MapPosition from, MapPosition to, CreatureDefinition creature)
    {
        if (from == to)
        {
            return new CaravanRoute(0, 1);
        }

        var map = state.Map;
        int Node(MapPosition p) => (((p.Level * map.Height) + p.Y) * map.Width) + p.X;
        var cost = new int[map.Levels * map.Width * map.Height];
        Array.Fill(cost, int.MaxValue);
        var closed = new bool[cost.Length];
        var open = new PriorityQueue<MapPosition, int>();
        cost[Node(from)] = 0;
        open.Enqueue(from, 0);

        while (open.TryDequeue(out var position, out _))
        {
            var current = Node(position);
            if (closed[current])
            {
                continue;
            }

            closed[current] = true;
            if (position == to)
            {
                return new CaravanRoute(cost[current], Math.Max(1, DaysFor(cost[current], creature)));
            }

            foreach (var direction in DirectionExtensions.All)
            {
                var next = position.Step(direction);
                if (!map.Contains(next) || (next != to && !CanPass(state, owner, next)))
                {
                    continue;
                }

                var candidate = cost[current] + StepCost(state, creature, position, next, direction);
                if (candidate < cost[Node(next)])
                {
                    cost[Node(next)] = candidate;
                    open.Enqueue(next, candidate);
                }
            }
        }

        return null;
    }

    /// <summary>Open route from a source's entrance to a town's entrance, within <see cref="MaxDays"/>; null refusal text otherwise via <paramref name="refusal"/>.</summary>
    public static CaravanRoute? RouteBetween(GameState state, MapObject source, Town destination, CreatureDefinition creature, out string? refusal)
    {
        var route = FindRoute(state, destination.Owner!.Value, source.VisitPosition, destination.VisitPosition, creature);
        refusal = route is null ? $"No open route from {source.Definition.Name} to {destination.Name}."
            : route.Days > MaxDays ? $"{destination.Name} is {route.Days} days away from {source.Definition.Name}; a caravan travels at most {MaxDays}."
            : null;
        return refusal is null ? route : null;
    }

    /// <summary>Whether the town may hire <paramref name="count"/> creatures from the dwelling; null when it may.</summary>
    public static string? CheckOrder(GameState state, Town town, CreatureDwelling dwelling, int count)
    {
        if (town.Level(BuiltInBuildings.Caravan) < 1)
        {
            return $"{town.Name} has no caravan.";
        }

        if (dwelling.Owner is null || dwelling.Owner != town.Owner)
        {
            return $"The {dwelling.Definition.Name} is not yours.";
        }

        var available = dwelling.Available ?? 0;
        if (count < 1 || count > available)
        {
            return $"Only {available} creatures wait in the dwelling.";
        }

        var creature = state.Creatures.Get(dwelling.Creature);
        if (!town.Garrison.CanAccept(creature.Id))
        {
            return $"{town.Name}'s garrison has no free slot for {creature.Name}.";
        }

        if (!state.GetPlayer(town.Owner!.Value).Resources.CanAfford(TownRules.RecruitCost(state, town, creature, count)))
        {
            return $"Not enough resources for {count} {creature.Name}.";
        }

        RouteBetween(state, dwelling, town, creature, out var refusal);
        return refusal;
    }

    /// <summary>Whether troops of <paramref name="from"/>'s garrison slot may be sent to another own town; null when they may.</summary>
    public static string? CheckSend(GameState state, Town from, int slot, Town to, int? count)
    {
        if (from.Level(BuiltInBuildings.Caravan) < 1)
        {
            return $"{from.Name} has no caravan.";
        }

        if (to.Owner is null || to.Owner != from.Owner || to == from)
        {
            return "Troops can only be sent to another own town.";
        }

        var stack = Armies.Army.IsValidSlot(slot) ? from.Garrison[slot] : null;
        if (stack is null)
        {
            return "The slot is empty.";
        }

        if (count is { } n && (n < 1 || n > stack.Count))
        {
            return $"The stack has {stack.Count} creatures.";
        }

        if (!to.Garrison.CanAccept(stack.Creature))
        {
            return $"{to.Name}'s garrison has no free slot.";
        }

        RouteBetween(state, from, to, state.Creatures.Get(stack.Creature), out var refusal);
        return refusal;
    }

    private static int DaysFor(int cost, CreatureDefinition creature) =>
        (cost + MovementAllowance.Daily(creature.MapSpeed) - 1) / MovementAllowance.Daily(creature.MapSpeed);

    private static bool CanPass(GameState state, PlayerId owner, MapPosition position)
    {
        if (MovementRules.CheckTile(state, position) != MoveResult.Moved || state.IsGuarded(position))
        {
            return false;
        }

        if (state.ObjectCellAt(position)?.Object is MonsterGuard)
        {
            return false;
        }

        return state.HeroAt(position) is not { } hero || !state.AreEnemies(owner, hero.Owner);
    }

    /// <summary>Like a hero's step (road to road, else the terrain being left, diagonals ×1.41), without hero skills; native terrain counts for the creature's own town.</summary>
    private static int StepCost(GameState state, CreatureDefinition creature, MapPosition from, MapPosition to, Direction direction)
    {
        var map = state.Map;
        var fromRoad = map.GetRoad(from);
        var baseCost = fromRoad != RoadType.None && map.GetRoad(to) != RoadType.None
            ? fromRoad.MovementCost()
            : state.Terrains.MovementCost(map.GetTerrain(from), creature.Town);
        return direction.IsDiagonal() ? (int)Math.Round(baseCost * HeroMovementService.DiagonalMultiplier) : baseCost;
    }
}
