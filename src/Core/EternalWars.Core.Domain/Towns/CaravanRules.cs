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
/// refunded when the caravan has to return. A resource caravan (ADR-079, TOWN-9) carries <see cref="Cargo"/> from an own resource
/// provider to the owner's treasury instead; its <see cref="Creature"/> is empty and <see cref="Count"/> 0.
/// </summary>
public sealed record Caravan(
    int Id, PlayerId Owner, MapObjectId Source, MapObjectId Destination, string Creature, int Count, int DaysLeft, ResourceSet Paid, ResourceSet? Cargo = null)
{
    public bool CarriesResources => Cargo is not null;
}

/// <summary>A route found for a caravan: what it costs in movement points and how many days it takes.</summary>
public sealed record CaravanRoute(int Cost, int Days);

/// <summary>
/// Caravan rules as queries (ADR-054), shared by <see cref="CaravanService"/>, views and the AI. A caravan moves
/// <see cref="DailyMovement"/> points a day, every step at the plain cost (terrain, roads and rivers ignored; ADR-079, TOWN-10), but
/// virtually: it never visits objects and cannot be seen or attacked. A route is open when no hostile hero, neutral monster or tile
/// guarded by one is on it.
/// <para>
/// Reach (ADR-073, TOWN-5): a source farther than <see cref="MaxReach"/> movement points cannot supply a town. The <b>raw</b> distance
/// ignores hostile heroes, monsters and guards: it decides which dwellings and providers a town's caravan lists at all
/// (<see cref="Dwellings"/>, <see cref="Providers"/>). Ordering checks the open route within the reach. A town has at most
/// <see cref="Limit"/> caravans coming to it (ADR-079, TOWN-10).
/// </para>
/// </summary>
public static class CaravanRules
{
    /// <summary>How far a caravan reaches, in movement points with every step at the plain cost (100, diagonals 141).</summary>
    public const int MaxReach = 7500;

    /// <summary>Movement points a caravan covers a day (ADR-077, TOWN-10): the reach takes at most 3 days.</summary>
    public const int DailyMovement = 2500;

    /// <summary>Caravans coming to a town at once, on their way or waiting at the gates (ADR-077, TOWN-10).</summary>
    public const int MaxCaravans = 6;

    /// <summary>The limit while the town has a governor.</summary>
    public const int GovernedMaxCaravans = 8;

    /// <summary>What a resource caravan costs (ADR-079, TOWN-9; placeholder), refunded when it has to return.</summary>
    public static ResourceSet ResourceCaravanCost { get; } = new(Gold: 100);

    /// <summary>The cheapest open route between two tiles, or null when there is none; a route takes at least a day.</summary>
    public static CaravanRoute? FindRoute(GameState state, PlayerId owner, MapPosition from, MapPosition to) =>
        from == to ? new CaravanRoute(0, 1)
        : Search(state, from, to, p => CanPass(state, owner, p), (_, _, d) => PlainStep(d), int.MaxValue) is { } cost
            ? new CaravanRoute(cost, Math.Max(1, (cost + DailyMovement - 1) / DailyMovement))
            : null;

    /// <summary>How many caravans may come to the town at once: <see cref="MaxCaravans"/>, <see cref="GovernedMaxCaravans"/> with a governor.</summary>
    public static int Limit(GameState state, Town town) => StewardshipRules.GovernorOf(state, town) is null ? MaxCaravans : GovernedMaxCaravans;

    /// <summary>Whether another caravan may be ordered or sent to the town; null when it may.</summary>
    public static string? CheckLimit(GameState state, Town town) =>
        Queue(state, town).Count >= Limit(state, town)
            ? $"{town.Name} already has {Limit(state, town)} caravans coming (the most it takes at once)."
            : null;

    /// <summary>The owner's flagged resource providers the town's caravan lists: within its raw reach, by id (ADR-079, TOWN-9).</summary>
    public static IReadOnlyList<ResourceProvider> Providers(GameState state, Town town) =>
        town.Owner is not { } owner ? []
        : state.ObjectsOf<ResourceProvider>().Where(p => p.Owner == owner && InReach(state, p, town)).OrderBy(p => p.Id.Value).ToList();

    /// <summary>Whether the town may send a caravan for what waits at the provider; null when it may.</summary>
    public static string? CheckFetch(GameState state, Town town, ResourceProvider provider)
    {
        if (town.Level(BuiltInBuildings.Caravan) < 1)
        {
            return $"{town.Name} has no caravan.";
        }

        if (provider.Owner is null || provider.Owner != town.Owner)
        {
            return $"The {provider.Definition.Name} is not yours.";
        }

        if (provider.Stored.IsEmpty)
        {
            return $"Nothing waits at the {provider.Definition.Name}.";
        }

        if (!state.GetPlayer(town.Owner!.Value).Resources.CanAfford(ResourceCaravanCost))
        {
            return $"A caravan costs {ResourceProviderRules.Describe(ResourceCaravanCost)}.";
        }

        RouteBetween(state, provider, town, out var refusal);
        return refusal ?? CheckLimit(state, town);
    }

    /// <summary>
    /// The distance a caravan's reach is measured in (every step at the plain cost), or null when it is beyond
    /// <see cref="MaxReach"/> or there is no way. With an <paramref name="owner"/> only open routes count; without one hostile
    /// heroes, monsters and guards are ignored (the raw distance).
    /// </summary>
    public static int? Distance(GameState state, PlayerId? owner, MapPosition from, MapPosition to) =>
        Search(state, from, to, p => owner is { } player ? CanPass(state, player, p) : MovementRules.CheckTile(state, p) == MoveResult.Moved,
            (_, _, direction) => PlainStep(direction), MaxReach);

    /// <summary>Whether a source is within the caravan's raw reach of a town: the town lists it at all (ADR-073).</summary>
    public static bool InReach(GameState state, MapObject source, Town town) => Distance(state, null, source.VisitPosition, town.VisitPosition) is not null;

    /// <summary>The owner's map dwellings the town's caravan lists: within its raw reach, by id.</summary>
    public static IReadOnlyList<CreatureDwelling> Dwellings(GameState state, Town town) =>
        town.Owner is not { } owner ? []
        : state.ObjectsOf<CreatureDwelling>().Where(d => d.Owner == owner && InReach(state, d, town)).OrderBy(d => d.Id.Value).ToList();

    /// <summary>
    /// The town's caravan queue (ADR-073): the caravans coming to it, those on their way first (fewest days left, then the order
    /// they were sent), then those waiting at the gates for room in the garrison.
    /// </summary>
    public static IReadOnlyList<Caravan> Queue(GameState state, Town town) =>
        state.Caravans.Where(c => c.Destination == town.Id && c.Owner == town.Owner)
            .OrderBy(c => c.DaysLeft == 0).ThenBy(c => c.DaysLeft).ThenBy(c => c.Id).ToList();

    /// <summary>Open route from a source's entrance to a town's entrance, within <see cref="MaxReach"/>; null refusal text otherwise via <paramref name="refusal"/>.</summary>
    public static CaravanRoute? RouteBetween(GameState state, MapObject source, Town destination, out string? refusal)
    {
        var owner = destination.Owner!.Value;
        refusal = !InReach(state, source, destination)
            ? $"{source.Definition.Name} is beyond the reach of {destination.Name}'s caravan ({MaxReach} movement points)."
            : Distance(state, owner, source.VisitPosition, destination.VisitPosition) is null
                ? $"No open route from {source.Definition.Name} to {destination.Name} within the caravan's reach."
                : null;
        return refusal is null ? FindRoute(state, owner, source.VisitPosition, destination.VisitPosition) : null;
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

        if (!InReach(state, dwelling, town))
        {
            return $"The {dwelling.Definition.Name} is beyond the reach of {town.Name}'s caravan ({MaxReach} movement points).";
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

        RouteBetween(state, dwelling, town, out var refusal);
        return refusal ?? CheckLimit(state, town);
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

        RouteBetween(state, from, to, out var refusal);
        return refusal ?? CheckLimit(state, to);
    }

    /// <summary>Dijkstra over the map's tiles; the target may be entered even when it would not pass. Null beyond <paramref name="limit"/>.</summary>
    private static int? Search(
        GameState state, MapPosition from, MapPosition to, Func<MapPosition, bool> canPass, Func<MapPosition, MapPosition, Direction, int> stepCost, int limit)
    {
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
                return cost[current];
            }

            foreach (var direction in DirectionExtensions.All)
            {
                var next = position.Step(direction);
                if (!map.Contains(next) || (next != to && !canPass(next)))
                {
                    continue;
                }

                var candidate = cost[current] + stepCost(position, next, direction);
                if (candidate <= limit && candidate < cost[Node(next)])
                {
                    cost[Node(next)] = candidate;
                    open.Enqueue(next, candidate);
                }
            }
        }

        return null;
    }

    private static int PlainStep(Direction direction) =>
        direction.IsDiagonal() ? (int)Math.Round(TerrainCatalog.NativeCost * HeroMovementService.DiagonalMultiplier) : TerrainCatalog.NativeCost;

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
}
