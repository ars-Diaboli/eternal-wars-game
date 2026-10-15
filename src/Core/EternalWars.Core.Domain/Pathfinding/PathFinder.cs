using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Pathfinding;

/// <summary>
/// Cheapest-route search on the adventure map (A*, falling back to Dijkstra when gates make the
/// distance heuristic unsafe).
/// </summary>
/// <remarks>
/// Rules:
/// <list type="bullet">
/// <item>8-way movement; step cost from <see cref="MovementRules.StepCost"/> (road → road, else terrain
/// being left; diagonal ×1.41).</item>
/// <item>Impassable terrain, blocked object cells and heroes the owner can see are obstacles. Unseen heroes
/// are unknown to the planner; movement runs into them.</item>
/// <item>A route may <b>end</b> on an own or visible enemy hero (meeting or battle), never pass through one.
/// Allied heroes simply block.</item>
/// <item>An object's visit tile ends the route (visiting is mandatory, ADR-013) unless the object is
/// harmless to walk over (<see cref="MapObject.EndsRoute"/>), or it is a usable gate, in which case the
/// route continues from the gate's target (two-way partner or one-way exit).</item>
/// <item>With <c>respectFog</c>, only tiles the hero's owner has explored are used.</item>
/// </list>
/// Search state is (tile, arrived-by-teleport): a gate entrance reached on foot must teleport,
/// but the same tile reached by teleport may be walked away from.
/// </remarks>
public sealed class PathFinder(GameState state)
{
    private const int CheapestOrthogonal = 50; // dirt road
    private const int CheapestDiagonalExtra = 21;

    public HeroPath? FindPath(HeroId heroId, MapPosition target, bool respectFog = true)
    {
        var hero = state.GetHero(heroId);
        var map = state.Map;
        if (target == hero.Position || MovementRules.CheckTile(state, target) != MoveResult.Moved ||
            (respectFog && !state.Fog.IsExplored(hero.Owner, target)))
        {
            return null;
        }

        // A hero on the target turns the route into a meeting/battle, or makes it impossible (ally).
        var targetIsEncounter = false;
        if (state.HeroAt(target) is { } targetHero && IsKnown(targetHero))
        {
            if (MovementRules.Classify(state, hero.Owner, targetHero) == HeroEncounter.Ally)
            {
                return null;
            }

            targetIsEncounter = true;
        }

        var nodeCount = map.Levels * map.Width * map.Height * 2;
        var cost = new int[nodeCount];
        var parent = new int[nodeCount];
        var closed = new bool[nodeCount];
        Array.Fill(cost, int.MaxValue);
        Array.Fill(parent, -1);

        var useHeuristic = !state.Objects.Any(o => o.TeleportTarget is not null);
        var start = Node(hero.Position, teleported: false);
        cost[start] = 0;
        var open = new PriorityQueue<int, int>();
        open.Enqueue(start, 0);

        while (open.TryDequeue(out var current, out _))
        {
            if (closed[current])
            {
                continue;
            }

            closed[current] = true;
            var (position, teleported) = Decode(current);
            if (position == target)
            {
                return BuildPath(hero, current, cost, parent);
            }

            if (current != start && !teleported && state.ObjectCellAt(position) is { Cell: FootprintCell.Visit } visit)
            {
                if (visit.Object.TeleportTarget is not null)
                {
                    if (state.TeleportDestination(visit.Object) is { } exit && CanStandOn(exit))
                    {
                        Relax(current, Node(exit, teleported: true), 0, exit);
                    }

                    continue;
                }

                if (visit.Object.EndsRoute)
                {
                    continue;
                }
            }

            foreach (var direction in DirectionExtensions.All)
            {
                var next = position.Step(direction);
                if (CanStandOn(next) || (targetIsEncounter && next == target))
                {
                    Relax(current, Node(next, teleported: false), MovementRules.StepCost(state, hero, position, next, direction), next);
                }
            }
        }

        return null;

        bool IsKnown(Hero other) => !respectFog || state.CanSee(hero.Owner, other);

        bool CanStandOn(MapPosition position) =>
            MovementRules.CheckTile(state, position) == MoveResult.Moved &&
            (!respectFog || state.Fog.IsExplored(hero.Owner, position)) &&
            (state.HeroAt(position) is not { } other || !IsKnown(other));

        void Relax(int from, int to, int stepCost, MapPosition toPosition)
        {
            var candidate = cost[from] + stepCost;
            if (closed[to] || candidate >= cost[to])
            {
                return;
            }

            cost[to] = candidate;
            parent[to] = from;
            open.Enqueue(to, candidate + (useHeuristic ? Heuristic(toPosition, target) : 0));
        }
    }

    /// <summary>Admissible lower bound: octile distance at the cheapest possible (road) cost.</summary>
    private static int Heuristic(MapPosition from, MapPosition to)
    {
        if (from.Level != to.Level)
        {
            return 0;
        }

        var dx = Math.Abs(from.X - to.X);
        var dy = Math.Abs(from.Y - to.Y);
        return (CheapestOrthogonal * Math.Max(dx, dy)) + (CheapestDiagonalExtra * Math.Min(dx, dy));
    }

    private HeroPath BuildPath(Hero hero, int end, int[] cost, int[] parent)
    {
        var nodes = new List<int>();
        for (var node = end; parent[node] != -1; node = parent[node])
        {
            nodes.Add(node);
        }

        nodes.Reverse();

        var steps = new List<PathStep>(nodes.Count);
        var movementLeft = hero.MovementPoints;
        var day = 0;
        var previousCost = 0;
        foreach (var node in nodes)
        {
            var (position, teleported) = Decode(node);
            var stepCost = cost[node] - previousCost;
            previousCost = cost[node];

            // Teleports happen during the previous step's visit, so they never start a new day.
            if (!teleported && !HeroMovementService.CanAffordStep(movementLeft))
            {
                day++;
                movementLeft = MovementAllowance.Daily(hero);
            }

            movementLeft = Math.Max(0, movementLeft - stepCost);
            steps.Add(new PathStep(position, teleported ? PathStepKind.Teleport : PathStepKind.Walk, stepCost, cost[node], day));
        }

        return new HeroPath(hero.Id, steps);
    }

    private int Node(MapPosition position, bool teleported) =>
        ((((position.Level * state.Map.Height) + position.Y) * state.Map.Width) + position.X) * 2 + (teleported ? 1 : 0);

    private (MapPosition Position, bool Teleported) Decode(int node)
    {
        var teleported = (node & 1) == 1;
        var tile = node >> 1;
        var x = tile % state.Map.Width;
        var rest = tile / state.Map.Width;
        return (new MapPosition(x, rest % state.Map.Height, rest / state.Map.Height), teleported);
    }
}
