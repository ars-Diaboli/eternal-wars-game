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
/// <item>8-way movement; step cost from <see cref="HeroMovementService.StepCost"/> (terrain being left, diagonal ×1.41).</item>
/// <item>Impassable terrain, blocked object cells and other heroes the owner can see are obstacles.
/// Unseen heroes are unknown to the planner; movement stops when it bumps into them.</item>
/// <item>An object's visit tile ends the route (visiting is mandatory), except a linked gate, whose
/// visit continues from the partner gate's entrance on the other side.</item>
/// <item>With <c>respectFog</c>, only tiles the hero's owner has explored are used.</item>
/// </list>
/// Search state is (tile, arrived-by-teleport): a gate entrance reached on foot must teleport,
/// but the same tile reached by teleport may be walked away from.
/// </remarks>
public sealed class PathFinder(GameState state)
{
    private const int StraightCost = 100;
    private const int DiagonalExtra = 41;

    public HeroPath? FindPath(HeroId heroId, MapPosition target, bool respectFog = true)
    {
        var hero = state.GetHero(heroId);
        var map = state.Map;
        PlayerId? viewer = respectFog ? hero.Owner : null;
        if (target == hero.Position || MovementRules.CheckEnter(state, target, viewer) != MoveResult.Moved ||
            (respectFog && !state.Fog.IsExplored(hero.Owner, target)))
        {
            return null;
        }

        var nodeCount = map.Levels * map.Width * map.Height * 2;
        var cost = new int[nodeCount];
        var parent = new int[nodeCount];
        var closed = new bool[nodeCount];
        Array.Fill(cost, int.MaxValue);
        Array.Fill(parent, -1);

        var useHeuristic = !state.ObjectsOf<SubterraneanGate>().Any(g => g.LinkedGate is not null);
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
                // Entering an object's entrance visits it, so the route can only continue through a gate.
                if (visit.Object is SubterraneanGate { LinkedGate: { } link })
                {
                    var exit = state.GetObject(link).VisitPosition;
                    if (CanUse(exit))
                    {
                        Relax(current, Node(exit, teleported: true), 0, exit);
                    }
                }

                continue;
            }

            foreach (var direction in DirectionExtensions.All)
            {
                var next = position.Step(direction);
                if (CanUse(next))
                {
                    Relax(current, Node(next, teleported: false), HeroMovementService.StepCost(map.GetTerrain(position), direction), next);
                }
            }
        }

        return null;

        bool CanUse(MapPosition position) =>
            MovementRules.CheckEnter(state, position, viewer) == MoveResult.Moved &&
            (!respectFog || state.Fog.IsExplored(hero.Owner, position));

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

    /// <summary>Admissible lower bound: octile distance at the cheapest terrain cost.</summary>
    private static int Heuristic(MapPosition from, MapPosition to)
    {
        if (from.Level != to.Level)
        {
            return 0;
        }

        var dx = Math.Abs(from.X - to.X);
        var dy = Math.Abs(from.Y - to.Y);
        return (StraightCost * Math.Max(dx, dy)) + (DiagonalExtra * Math.Min(dx, dy));
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
