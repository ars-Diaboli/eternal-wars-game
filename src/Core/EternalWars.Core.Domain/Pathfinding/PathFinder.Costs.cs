using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Pathfinding;

public sealed partial class PathFinder
{
    /// <summary>
    /// The costs of the hero's cheapest routes from <paramref name="from"/> (default: where the hero stands) to every tile, for weighing many
    /// targets with one search: <see cref="CostMap.CostTo"/> gives what <see cref="FindPath"/> would give as the route's
    /// <see cref="HeroPath.TotalCost"/>, while the game does not change. <paramref name="reuse"/>: a map no longer needed, whose memory is taken
    /// over.
    /// </summary>
    public CostMap CostsFrom(HeroId heroId, MapPosition? from = null, bool respectFog = true, CostMap? reuse = null)
    {
        var map = reuse ?? new CostMap();
        map.Begin(this, state.GetHero(heroId), from ?? state.GetHero(heroId).Position, respectFog);
        return map;
    }

    /// <summary>
    /// <see cref="CostsFrom"/> for a hero that need not be on the map: the map analysis's scout (ADR-263). With
    /// <paramref name="passThrough"/>, a route goes on past what would end it in the game: a guarded tile and a monster (as if fought), a
    /// pickup or another visit (as if taken or done); the costs of the steps are the game's.
    /// </summary>
    public CostMap CostsFor(Hero hero, MapPosition from, bool respectFog, bool passThrough = false, CostMap? reuse = null)
    {
        ArgumentNullException.ThrowIfNull(hero);
        var map = reuse ?? new CostMap();
        map.Begin(this, hero, from, respectFog, passThrough);
        return map;
    }

    /// <summary>
    /// The costs of the hero's cheapest routes to <paramref name="target"/> from every tile, for weighing many starting points with one search
    /// (the AI's Dimension Door landings): <see cref="ReverseCostMap.CostFrom"/> gives what <see cref="FindPath"/> with that <c>from</c> would
    /// give as the route's <see cref="HeroPath.TotalCost"/>, while the game does not change.
    /// </summary>
    public ReverseCostMap CostsTo(HeroId heroId, MapPosition target, bool respectFog = true, ReverseCostMap? reuse = null)
    {
        var map = reuse ?? new ReverseCostMap();
        map.Begin(this, state.GetHero(heroId), target, respectFog);
        return map;
    }

    /// <summary>
    /// Working memory of a Dijkstra search that runs only as far as the questions asked so far need (all costs are at least 0, so a node is
    /// final once nothing cheaper waits in the queue). A node's cost counts only when its stamp is this search's generation, so starting
    /// again clears nothing but the queue and the terrain costs.
    /// </summary>
    public abstract class Flood
    {
        private int[] _cost = [];
        private int[] _reached = [];
        private int[] _closed = [];
        private int _generation;
        private long _started;
        private int _expanded;

        private protected PathFinder Finder { get; private set; } = null!;

        private protected Hero Hero { get; private set; } = null!;

        private protected bool RespectFog { get; private set; }

        private protected PriorityQueue<int, int> Open { get; } = new();

        private protected int[] TerrainCosts { get; } = new int[MovementRules.TerrainTypeCount];

        private protected Domain.Game.GameState State => Finder.State;

        private protected void Begin(PathFinder finder, Hero hero, bool respectFog)
        {
            (Finder, Hero, RespectFog) = (finder, hero, respectFog);
            var map = finder.State.Map;
            var nodes = map.Levels * map.Width * map.Height * 2;
            if (_cost.Length < nodes)
            {
                (_cost, _reached, _closed) = (new int[nodes], new int[nodes], new int[nodes]);
                _generation = 0;
                Grow(nodes);
            }

            if (++_generation == int.MaxValue)
            {
                Array.Clear(_reached);
                Array.Clear(_closed);
                Forget();
                _generation = 1;
            }

            Open.Clear();
            Array.Fill(TerrainCosts, -1);
            _counters = _counters with { Floods = _counters.Floods + 1 };
        }

        private protected int Generation => _generation;

        /// <summary>The arrays of a derived search grow to <paramref name="nodes"/> with the base's.</summary>
        private protected virtual void Grow(int nodes)
        {
        }

        /// <summary>The generation wrapped around: a derived search's stamps are cleared.</summary>
        private protected virtual void Forget()
        {
        }

        private protected int CostOf(int node) => _reached[node] == _generation ? _cost[node] : int.MaxValue;

        private protected bool IsClosed(int node) => _closed[node] == _generation;

        private protected void Relax(int node, int cost)
        {
            if (IsClosed(node) || cost >= CostOf(node))
            {
                return;
            }

            _reached[node] = _generation;
            _cost[node] = cost;
            Open.Enqueue(node, cost);
        }

        /// <summary>Settles nodes while one cheaper than <paramref name="bound"/> waits; false when none does (the answer is final).</summary>
        private protected bool SettleOneBelow(int bound)
        {
            while (Open.TryPeek(out var node, out var cost))
            {
                if (cost >= bound)
                {
                    return false;
                }

                Open.Dequeue();
                if (IsClosed(node))
                {
                    continue;
                }

                _closed[node] = _generation;
                _expanded++;
                Expand(node, cost);
                return true;
            }

            return false;
        }

        private protected abstract void Expand(int node, int cost);

        /// <summary>Starts timing a question for <see cref="Counters"/>.</summary>
        private protected void StartAnswer()
        {
            _started = System.Diagnostics.Stopwatch.GetTimestamp();
            _expanded = 0;
        }

        /// <summary>Counts the question's work in <see cref="Counters"/> (tiles expanded and time, with the searches').</summary>
        private protected int? Answer(int cost)
        {
            _counters = _counters with
            {
                Expanded = _counters.Expanded + _expanded,
                Time = _counters.Time + System.Diagnostics.Stopwatch.GetElapsedTime(_started),
            };
            return cost == int.MaxValue ? null : cost;
        }

        private protected int StepCost(MapPosition from, MapPosition to, Direction direction) =>
            MovementRules.StepCost(State, Hero, from, to, direction, TerrainCosts);

        private protected bool CanStandOn(MapPosition position) => Finder.CanStandOn(Hero, position, RespectFog);
    }

    /// <summary>
    /// <see cref="CostsFrom"/>: Dijkstra from the origin under the route rules of <see cref="FindPath"/> with no target. Routes into a tile only
    /// a target may be entered (a hero or barring quest object bumped into, a monster stepped onto from the tile it guards) end there and go
    /// no further, so they are kept apart and count only when that tile is asked for, as in a search for it.
    /// </summary>
    public sealed class CostMap : Flood
    {
        private int[] _bump = []; // per tile: the cheapest step into it from a settled node when it stands in the way (an encounter target)
        private int[] _guardEnd = []; // per tile: the cheapest step onto the monster there from a tile it guards
        private int[] _endStamp = [];
        private int _start;
        private MapPosition _origin;
        private bool _passThrough;

        internal void Begin(PathFinder finder, Hero hero, MapPosition origin, bool respectFog, bool passThrough = false)
        {
            Begin(finder, hero, respectFog);
            _origin = origin;
            _passThrough = passThrough;
            _start = finder.Node(origin, teleported: false);
            Relax(_start, 0);
        }

        /// <summary>What <see cref="FindPath"/> to <paramref name="target"/> would give as its <see cref="HeroPath.TotalCost"/>, or null for no route.</summary>
        public int? CostTo(MapPosition target)
        {
            if (target == _origin || Finder.TargetKindOf(Hero, target, RespectFog) is not { } kind)
            {
                return null;
            }

            StartAnswer();
            var walk = Finder.Node(target, teleported: false);
            var tile = walk >> 1;
            int best;
            do
            {
                best = Math.Min(CostOf(walk), CostOf(walk + 1));
                if (_endStamp[tile] == Generation)
                {
                    best = Math.Min(best, _guardEnd[tile]);
                    if (kind == TargetKind.Encounter)
                    {
                        best = Math.Min(best, _bump[tile]);
                    }
                }
            }
            while (SettleOneBelow(best));

            return Answer(best);
        }

        private protected override void Grow(int nodes) =>
            (_bump, _guardEnd, _endStamp) = (new int[nodes / 2], new int[nodes / 2], new int[nodes / 2]);

        private protected override void Forget() => Array.Clear(_endStamp);

        private protected override void Expand(int node, int cost)
        {
            var (position, teleported) = Finder.Decode(node);

            // A guarded tile ends a route; only the step onto a monster guarding it goes on, to a route ending there (ADR-040).
            if (node != _start && State.IsGuarded(position))
            {
                foreach (var monster in State.GuardsOf(position))
                {
                    if (Finder.GuardEdge(position, monster.VisitPosition) is { } toMonster)
                    {
                        End(_guardEnd, monster.VisitPosition, cost + StepCost(position, monster.VisitPosition, toMonster));
                    }
                }

                if (!_passThrough) // the analysis's scout fights and walks on (ADR-263)
                {
                    return;
                }
            }

            if (node != _start && !teleported && Finder.VisitAt(Hero, position) is { } visit)
            {
                if (visit.Teleports)
                {
                    if (Finder.TeleportExit(Hero, visit.Object, RespectFog) is { } exit)
                    {
                        Relax(Finder.Node(exit, teleported: true), cost);
                    }

                    return;
                }

                if (visit.EndsRoute && !_passThrough)
                {
                    return;
                }
            }

            foreach (var direction in DirectionExtensions.All)
            {
                var next = position.Step(direction);
                if (CanStandOn(next))
                {
                    Relax(Finder.Node(next, teleported: false), cost + StepCost(position, next, direction));
                }
                else if (State.Map.Contains(next) && StandsInTheWay(next))
                {
                    End(_bump, next, cost + StepCost(position, next, direction));
                }
            }
        }

        /// <summary>A tile a route may step into only as its target: a hero the owner knows of, or a barring quest object (EVT-5).</summary>
        private bool StandsInTheWay(MapPosition position) =>
            (State.HeroAt(position) is { } other && Finder.IsKnown(Hero, other, RespectFog)) || QuestRules.Barring(State, Hero, position) is not null;

        private void End(int[] costs, MapPosition position, int cost)
        {
            var tile = Finder.Node(position, teleported: false) >> 1;
            if (_endStamp[tile] != Generation)
            {
                _endStamp[tile] = Generation;
                _bump[tile] = int.MaxValue;
                _guardEnd[tile] = int.MaxValue;
            }

            costs[tile] = Math.Min(costs[tile], cost);
        }
    }

    /// <summary>
    /// <see cref="CostsTo"/>: Dijkstra backwards from the target, over the steps <see cref="FindPath"/> may take, so a node's cost is that of
    /// the cheapest route from it to the target. The first step from an origin follows the origin's own rules (it may leave a guarded tile or a
    /// visit tile), so <see cref="CostFrom"/> adds it on top.
    /// </summary>
    public sealed class ReverseCostMap : Flood
    {
        private readonly List<(MapPosition Entrance, MapPosition Exit)> _gates = [];
        private MapPosition _target;
        private TargetKind? _kind;

        internal void Begin(PathFinder finder, Hero hero, MapPosition target, bool respectFog)
        {
            Begin(finder, hero, respectFog);
            _target = target;
            _kind = finder.TargetKindOf(hero, target, respectFog);
            _gates.Clear();
            if (_kind is null)
            {
                return;
            }

            // Every gate entrance a route may walk into, and where it lands.
            foreach (var gate in State.ObjectsOf<TwoWayGate>().Cast<MapObject>().Concat(State.ObjectsOf<OneWayGateEntrance>()))
            {
                if (VisitAt(gate.VisitPosition) is { Teleports: true } visit && Finder.TeleportExit(Hero, visit.Object, RespectFog) is { } exit
                    && !_gates.Exists(g => g.Entrance == gate.VisitPosition))
                {
                    _gates.Add((gate.VisitPosition, exit));
                }
            }

            var walk = finder.Node(target, teleported: false);
            Relax(walk, 0);
            Relax(walk + 1, 0);

            // A guarded tile next to a monster target steps onto it, and nowhere else.
            foreach (var direction in DirectionExtensions.All)
            {
                var guarded = target.Step(direction);
                if (CanStandOn(guarded) && State.IsGuarded(guarded) && Finder.GuardEdge(guarded, target) is { } toMonster)
                {
                    var cost = StepCost(guarded, target, toMonster);
                    Relax(finder.Node(guarded, teleported: false), cost);
                    Relax(finder.Node(guarded, teleported: true), cost);
                }
            }
        }

        /// <summary>
        /// What <see cref="FindPath"/> to the target with <c>from</c> <paramref name="origin"/> would give as its <see cref="HeroPath.TotalCost"/>,
        /// or null for no route.
        /// </summary>
        public int? CostFrom(MapPosition origin)
        {
            if (_kind is null || origin == _target)
            {
                return null;
            }

            // A route could come back over an origin with a visit tile and then use it (a gate's jump), which a search from there cannot.
            if (VisitAt(origin) is not null)
            {
                return Finder.FindPath(Hero.Id, _target, RespectFog, origin)?.TotalCost;
            }

            StartAnswer();
            int best;
            do
            {
                best = int.MaxValue;
                foreach (var direction in DirectionExtensions.All)
                {
                    var next = origin.Step(direction);
                    if (Enterable(next) && CostOf(Finder.Node(next, teleported: false)) is var rest and < int.MaxValue)
                    {
                        best = Math.Min(best, StepCost(origin, next, direction) + rest);
                    }
                }
            }
            while (SettleOneBelow(best));

            return Answer(best);
        }

        private protected override void Expand(int node, int cost)
        {
            var (position, teleported) = Finder.Decode(node);
            if (teleported)
            {
                // Arriving here by gate: from each entrance that leads here, for nothing.
                foreach (var (entrance, exit) in _gates)
                {
                    if (exit == position && entrance != _target && !State.IsGuarded(entrance))
                    {
                        Relax(Finder.Node(entrance, teleported: false), cost);
                    }
                }

                return;
            }

            if (!Enterable(position))
            {
                return;
            }

            // Walking in from each neighbour that walks on from where it stands.
            foreach (var direction in DirectionExtensions.All)
            {
                var from = position.Step(direction);
                // A route passes only over tiles it may stand on (the origin's own first step is CostFrom's).
                if (from == _target || !CanStandOn(from) || State.IsGuarded(from))
                {
                    continue;
                }

                var step = StepCost(from, position, DirectionExtensions.FromOffset(position.X - from.X, position.Y - from.Y)!.Value);
                Relax(Finder.Node(from, teleported: true), cost + step);
                if (VisitAt(from) is not { } visit || !(visit.Teleports || visit.EndsRoute))
                {
                    Relax(Finder.Node(from, teleported: false), cost + step);
                }
            }
        }

        private bool Enterable(MapPosition position) => CanStandOn(position) || (_kind == TargetKind.Encounter && position == _target);

        private Visit? VisitAt(MapPosition position) => Finder.VisitAt(Hero, position);
    }
}
