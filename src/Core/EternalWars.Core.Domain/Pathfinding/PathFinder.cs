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
/// <item>8-way movement; step cost from <see cref="MovementRules.StepCost(GameState, Hero, MapPosition, MapPosition, Direction)"/> (road → road, else terrain
/// being left; diagonal ×1.41).</item>
/// <item>Impassable terrain, blocked object cells and heroes the owner can see are obstacles. Unseen heroes
/// are unknown to the planner; movement runs into them.</item>
/// <item>A route may <b>end</b> on an own or visible enemy hero (meeting or battle), never pass through one.
/// Allied heroes simply block.</item>
/// <item>An object's visit tile ends the route (visiting is mandatory, ADR-013) unless the object is
/// harmless to walk over (<see cref="MapObject.EndsRoute"/>), or it is a usable gate, in which case the
/// route continues from the gate's target (two-way partner or one-way exit).</item>
/// <item>With <c>respectFog</c>, only tiles the hero's owner has explored are used.</item>
/// <item>Border guards and gates are walls to heroes without their key and open ground to key holders (ADR-071); own and allied garrisons are
/// passes (ADR-070).</item>
/// <item>A Quest Guard, and a Quest Gate whose quest the hero does not meet, are walls that a route may end on (the bump shows the quest);
/// a gate is open ground to a hero that meets its quest (EVT-5).</item>
/// <item>Tiles guarded by a monster end the route (the encounter happens there), like the monster itself.</item>
/// </list>
/// Search state is (tile, arrived-by-teleport): a gate entrance reached on foot must teleport,
/// but the same tile reached by teleport may be walked away from.
/// </remarks>
public sealed partial class PathFinder(GameState state)
{
    private const int CheapestOrthogonal = 50; // dirt road

    /// <summary>A diagonal dirt-road step costs 70 (50 × 1.41 = 70.5, rounded to even): the estimate must never count more, or routes come out dearer.</summary>
    private const int CheapestDiagonalExtra = 20;

    [ThreadStatic]
    private static PathSearchCounters _counters;

    private GameState State => state;

    [ThreadStatic]
    private static SearchBuffers? _spare;

    /// <summary>
    /// The searches this thread has run so far (diagnostics for <c>sim game --profile</c>; rules never read it). Per thread, so a game on one
    /// thread counts only its own searches.
    /// </summary>
    public static PathSearchCounters Counters => _counters;

    /// <summary>
    /// The cheapest route for the hero to <paramref name="target"/>, or null. <paramref name="from"/>: plan as if the hero stood there instead (the
    /// AI weighing a Dimension Door landing); the costs are still the hero's.
    /// </summary>
    public HeroPath? FindPath(HeroId heroId, MapPosition target, bool respectFog = true, MapPosition? from = null) =>
        FindPathFor(state.GetHero(heroId), target, respectFog, from);

    /// <summary>
    /// <see cref="FindPath"/> for a hero that need not be on the map: the editor's Measure tool
    /// (<see cref="Analysis.MapMeasure"/>). The days of the steps count from the hero's movement points as they are.
    /// </summary>
    public HeroPath? FindPathFor(Hero hero, MapPosition target, bool respectFog, MapPosition? from = null)
    {
        ArgumentNullException.ThrowIfNull(hero);
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        _counters = _counters with { Searches = _counters.Searches + 1 };
        var map = state.Map;
        var origin = from ?? hero.Position;
        if (target == origin || TargetKindOf(hero, target, respectFog) is not { } kind)
        {
            return null;
        }

        var targetIsEncounter = kind == TargetKind.Encounter;

        // The buffers this thread keeps between searches; a search started inside another (none today) gets its own.
        var search = _spare ?? new SearchBuffers();
        _spare = null;
        search.Begin(map.Levels * map.Width * map.Height * 2);
        try
        {
            return Search(hero, origin, target, targetIsEncounter, respectFog, search, started);
        }
        finally
        {
            _spare = search;
        }
    }

    private HeroPath? Search(Hero hero, MapPosition origin, MapPosition target, bool targetIsEncounter, bool respectFog, SearchBuffers search, long started)
    {
        var useHeuristic = !state.HasTeleporters;
        var terrainCosts = search.TerrainCosts;
        var open = search.Open;
        var start = Node(origin, teleported: false);
        search.Reach(start, 0, -1);
        open.Enqueue(start, 0);
        var expanded = 0;

        while (open.TryDequeue(out var current, out _))
        {
            if (search.IsClosed(current))
            {
                continue;
            }

            search.Close(current);
            expanded++;
            var (position, teleported) = Decode(current);
            if (position == target)
            {
                Count(expanded, found: true, started);
                return BuildPath(hero, current, search);
            }

            // Entering a guarded tile starts an encounter, so a route can end there but never pass through (ADR-040).
            if (current != start && state.IsGuarded(position))
            {
                // Except towards the guarding monster itself: clicking a monster routes up to it (HoMM3).
                if (GuardEdge(position, target) is { } toMonster)
                {
                    Relax(current, Node(target, teleported: false), MovementRules.StepCost(state, hero, position, target, toMonster, terrainCosts), target);
                }

                continue;
            }

            if (current != start && !teleported && VisitAt(hero, position) is { } visit)
            {
                if (visit.Teleports)
                {
                    if (TeleportExit(hero, visit.Object, respectFog) is { } exit)
                    {
                        Relax(current, Node(exit, teleported: true), 0, exit);
                    }

                    continue;
                }

                if (visit.EndsRoute)
                {
                    continue;
                }
            }

            foreach (var direction in DirectionExtensions.All)
            {
                var next = position.Step(direction);
                if (CanStandOn(next) || (targetIsEncounter && next == target))
                {
                    Relax(current, Node(next, teleported: false), MovementRules.StepCost(state, hero, position, next, direction, terrainCosts), next);
                }
            }
        }

        Count(expanded, found: false, started);
        return null;

        bool CanStandOn(MapPosition position) => this.CanStandOn(hero, position, respectFog);

        void Relax(int from, int to, int stepCost, MapPosition toPosition)
        {
            var candidate = search.CostOf(from) + stepCost;
            if (search.IsClosed(to) || candidate >= search.CostOf(to))
            {
                return;
            }

            search.Reach(to, candidate, from);
            open.Enqueue(to, candidate + (useHeuristic ? Heuristic(toPosition, target) : 0));
        }
    }

    /// <summary>
    /// Whether a route may end on <paramref name="target"/>, and how: null when it may not (a tile no hero can stand on, unexplored with
    /// <paramref name="respectFog"/>, or an allied hero on it); <see cref="TargetKind.Encounter"/> when the last step bumps into what stands
    /// there (a hero the owner knows of: a meeting or a battle; or a barring quest object, EVT-5).
    /// </summary>
    private TargetKind? TargetKindOf(Hero hero, MapPosition target, bool respectFog)
    {
        if (MovementRules.CheckTile(state, target) != MoveResult.Moved || (respectFog && !state.Fog.IsExplored(hero.Owner, target)))
        {
            return null;
        }

        var kind = TargetKind.Plain;
        if (state.HeroAt(target) is { } targetHero && IsKnown(hero, targetHero, respectFog))
        {
            if (MovementRules.Classify(state, hero.Owner, targetHero) == HeroEncounter.Ally)
            {
                return null;
            }

            kind = TargetKind.Encounter;
        }

        return QuestRules.Barring(state, hero, target) is not null ? TargetKind.Encounter : kind;
    }

    private bool IsKnown(Hero hero, Hero other, bool respectFog) => !respectFog || state.CanSee(hero.Owner, other);

    /// <summary>Whether a route may pass over <paramref name="position"/> (not only end there).</summary>
    private bool CanStandOn(Hero hero, MapPosition position, bool respectFog) =>
        MovementRules.CheckTile(state, position) == MoveResult.Moved &&
        BorderRules.Barring(state, hero.Owner, position) is null &&
        QuestRules.Barring(state, hero, position) is null &&
        (!respectFog || state.Fog.IsExplored(hero.Owner, position)) &&
        (state.HeroAt(position) is not { } other || !IsKnown(hero, other, respectFog));

    /// <summary>
    /// The step from a guarded <paramref name="position"/> onto <paramref name="monster"/>, when a monster guarding the position is visited
    /// there: the one step a route may take out of a guarded tile (clicking a monster routes up to it, HoMM3).
    /// </summary>
    private Direction? GuardEdge(MapPosition position, MapPosition monster) =>
        state.GuardsOf(position).Any(m => m.VisitPosition == monster) &&
        DirectionExtensions.FromOffset(monster.X - position.X, monster.Y - position.Y) is { } toMonster &&
        monster.Level == position.Level
            ? toMonster
            : null;

    /// <summary>The object visited on <paramref name="position"/> and what it does to a route that walks onto it, or null.</summary>
    private Visit? VisitAt(Hero hero, MapPosition position) =>
        state.ObjectCellAt(position) is { Cell: FootprintCell.Visit } visit
            ? new Visit(
                visit.Object,
                visit.Object.TeleportTarget is not null,
                // Own and allied garrisons are passes (ADR-070).
                visit.Object.EndsRoute && !(visit.Object is MapGarrison garrison && garrison.LetsThrough(state, hero.Owner)))
            : null;

    /// <summary>Where the gate takes a hero who walks in, when the hero may stand there.</summary>
    private MapPosition? TeleportExit(Hero hero, MapObject gate, bool respectFog) =>
        state.TeleportDestination(gate) is { } exit && CanStandOn(hero, exit, respectFog) ? exit : null;

    private enum TargetKind
    {
        Plain,
        Encounter,
    }

    /// <summary>An object's visit tile on a route: a gate moves the hero on; otherwise, unless it is harmless to walk over, the route ends there.</summary>
    private readonly record struct Visit(MapObject Object, bool Teleports, bool EndsRoute);

    private static void Count(int expanded, bool found, long started) => _counters = _counters with
    {
        Found = _counters.Found + (found ? 1 : 0),
        Expanded = _counters.Expanded + expanded,
        Time = _counters.Time + System.Diagnostics.Stopwatch.GetElapsedTime(started),
    };

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

    private HeroPath BuildPath(Hero hero, int end, SearchBuffers search)
    {
        var nodes = new List<int>();
        for (var node = end; search.ParentOf(node) != -1; node = search.ParentOf(node))
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
            var cost = search.CostOf(node);
            var stepCost = cost - previousCost;
            previousCost = cost;

            // Teleports happen during the previous step's visit, so they never start a new day.
            if (!teleported && !HeroMovementService.CanAffordStep(movementLeft))
            {
                day++;
                movementLeft = MovementAllowance.Daily(state, hero);
            }

            movementLeft = Math.Max(0, movementLeft - stepCost);
            steps.Add(new PathStep(position, teleported ? PathStepKind.Teleport : PathStepKind.Walk, stepCost, cost, day));
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

    /// <summary>
    /// A search's working memory, kept by its thread from one search to the next. A node's cost and parent count only when its
    /// <see cref="_reached"/> stamp is this search's generation, and it is closed only when its <see cref="_closed"/> stamp is; so a new search
    /// clears nothing but the queue and the terrain costs.
    /// </summary>
    private sealed class SearchBuffers
    {
        private int[] _cost = [];
        private int[] _parent = [];
        private int[] _reached = [];
        private int[] _closed = [];
        private int _generation;

        public PriorityQueue<int, int> Open { get; } = new();

        /// <summary>The hero's cost per terrain type, worked out on first use in each search (-1 = not yet).</summary>
        public int[] TerrainCosts { get; } = new int[MovementRules.TerrainTypeCount];

        public void Begin(int nodeCount)
        {
            if (_cost.Length < nodeCount)
            {
                (_cost, _parent, _reached, _closed) = (new int[nodeCount], new int[nodeCount], new int[nodeCount], new int[nodeCount]);
                _generation = 0;
            }

            if (++_generation == int.MaxValue)
            {
                Array.Clear(_reached);
                Array.Clear(_closed);
                _generation = 1;
            }

            Open.Clear();
            Array.Fill(TerrainCosts, -1);
        }

        public int CostOf(int node) => _reached[node] == _generation ? _cost[node] : int.MaxValue;

        public int ParentOf(int node) => _reached[node] == _generation ? _parent[node] : -1;

        public void Reach(int node, int cost, int parent)
        {
            _reached[node] = _generation;
            _cost[node] = cost;
            _parent[node] = parent;
        }

        public bool IsClosed(int node) => _closed[node] == _generation;

        public void Close(int node) => _closed[node] = _generation;
    }
}

/// <summary>
/// What the <see cref="PathFinder"/> has done on a thread: the searches started (those that stopped at once included), the ones that found a
/// route, the tiles they expanded and the time the searches took (wall clock; searches that stopped at once are not timed), and the cost
/// maps started (<see cref="PathFinder.CostsFrom"/>, <see cref="PathFinder.CostsTo"/>), whose tiles and time are counted with the searches'.
/// </summary>
public readonly record struct PathSearchCounters(long Searches, long Found, long Expanded, TimeSpan Time, long Floods = 0)
{
    public static PathSearchCounters operator +(PathSearchCounters a, PathSearchCounters b) =>
        new(a.Searches + b.Searches, a.Found + b.Found, a.Expanded + b.Expanded, a.Time + b.Time, a.Floods + b.Floods);

    public static PathSearchCounters operator -(PathSearchCounters a, PathSearchCounters b) =>
        new(a.Searches - b.Searches, a.Found - b.Found, a.Expanded - b.Expanded, a.Time - b.Time, a.Floods - b.Floods);
}
