using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// The danger enemy heroes pose to the player's heroes and towns: the visible enemy heroes, their army power as the player knows it and the
/// tiles each can reach in a day (ADR-184); whether a place holds against the strongest of them (<see cref="AiWeights.SafeShare"/>), and when
/// a hero stays to defend a town or moves into its garrison (ADR-112). On an arena's preparation day nothing is in danger (ADR-160).
/// </summary>
internal sealed class ThreatMap(AiContext ai)
{
    private readonly GameState _state = ai.State;
    private readonly Dictionary<PlayerId, List<(Hero Enemy, long Power, IReadOnlySet<MapPosition> Reach)>> _threats = [];
    private int _threatsAt = -1;
    private readonly Dictionary<(HeroId, MapPosition), IReadOnlySet<MapPosition>> _reach = [];

    /// <summary>Forgets the enemies' reach at the start of a turn (it holds while the enemy does not move).</summary>
    public void BeginTurn() => _reach.Clear();

    /// <summary>
    /// Visible enemy heroes, their army power and the tiles each can reach in a day. Kept until the next command runs (only commands change the
    /// game), as every candidate target and flooded tile asks for it.
    /// </summary>
    public IReadOnlyList<(Hero Enemy, long Power, IReadOnlySet<MapPosition> Reach)> Threats(PlayerId owner)
    {
        if (_threatsAt != ai.Session.CommandLog.Count)
        {
            _threats.Clear();
            _threatsAt = ai.Session.CommandLog.Count;
        }

        if (!_threats.TryGetValue(owner, out var threats))
        {
            threats = [.. _state.Heroes
                .Where(h => _state.AreEnemies(owner, h.Owner) && _state.CanSee(owner, h) && _state.Fog.IsExplored(owner, h.Position))
                .Select(h => (h, ai.ArmyPower(ai.Seen(h)), ReachOf(h)))];
            _threats[owner] = threats;
        }

        return threats;
    }

    /// <summary>
    /// The tiles an enemy hero can step onto with a day's full movement (ADR-184): a flood with its own step costs (roads, terrain, its native
    /// ground), through open ground; a hero, an object's entrance or a guarded tile can be reached but not passed. Kept for the turn (the enemy does
    /// not move during it).
    /// </summary>
    public IReadOnlySet<MapPosition> ReachOf(Hero enemy)
    {
        if (_reach.TryGetValue((enemy.Id, enemy.Position), out var known))
        {
            return known;
        }

        var budget = Math.Max(1, MovementAllowance.Daily(_state, enemy)); // its next day: what its army allows
        var cost = new Dictionary<MapPosition, int> { [enemy.Position] = 0 };
        var open = new PriorityQueue<MapPosition, int>();
        open.Enqueue(enemy.Position, 0);
        while (open.TryDequeue(out var position, out var spent))
        {
            if (spent != cost[position] || spent >= budget || (position != enemy.Position && !PassesThrough(position)))
            {
                continue; // a stale entry, out of movement (a step needs some left), or a tile that ends the walk
            }

            foreach (var direction in DirectionExtensions.All)
            {
                var next = position.Step(direction);
                if (MovementRules.CheckTile(_state, next) != MoveResult.Moved)
                {
                    continue;
                }

                var total = spent + MovementRules.StepCost(_state, enemy, position, next, direction);
                if (!cost.TryGetValue(next, out var old) || total < old)
                {
                    cost[next] = total;
                    open.Enqueue(next, total);
                }
            }
        }

        var reach = cost.Keys.ToHashSet();
        _reach[(enemy.Id, enemy.Position)] = reach;
        return reach;

        bool PassesThrough(MapPosition position) =>
            _state.HeroAt(position) is null && !_state.IsGuarded(position) && _state.ObjectCellAt(position) is not { Cell: FootprintCell.Visit, Object.EndsRoute: true };
    }

    /// <summary>The strongest enemy army that can reach the place within a day, leaving out the hero at <paramref name="except"/> (the one being attacked); 0 when none can.</summary>
    public long Danger(PlayerId owner, MapPosition position, MapPosition? except = null) =>
        ai.Preparing ? 0 : Threats(owner).Where(t => t.Enemy.Position != except && t.Reach.Contains(position))
            .Select(t => t.Power).DefaultIfEmpty(0).Max();

    /// <summary>What defends a place for the player: the hero's army, plus the garrison when it is the entrance of an own town.</summary>
    public long DefenceAt(Hero hero, MapPosition position) =>
        ai.ArmyPower(hero.Army) + (_state.ObjectCellAt(position) is { Object: Town town, Cell: FootprintCell.Visit } && town.Owner == hero.Owner ? ai.ArmyPower(town.Garrison) : 0);

    /// <summary>Whether <paramref name="defence"/> holds against <paramref name="danger"/>: no danger, or at least <see cref="AiWeights.SafeShare"/> of it.</summary>
    public bool Holds(long defence, long danger) => danger == 0 || defence >= danger * ai.Weights.SafeShare;

    /// <summary>
    /// Whether a hero defending its town should move into the garrison (ADR-112): only a secondary hero while the main hero is out (a garrison hero
    /// never retreats, so the player's last hero does not go in), and only when the hero and the garrison together at least match the danger.
    /// </summary>
    public bool ShouldHoldGarrison(Hero hero, Town town) =>
        hero.Id != ai.Main && ai.Main is { } main && _state.Heroes.Any(h => h.Id == main) && Defending(hero)
        && DefenceAt(hero, hero.Position) >= Danger(hero.Owner, hero.Position) && TownRules.CheckEnterGarrison(_state, town) is null;

    /// <summary>A hero in an own town that an enemy can reach stays there when the garrison alone could not hold it but the two together can.</summary>
    public bool Defending(Hero hero)
    {
        if (_state.ObjectCellAt(hero.Position) is not { Object: Town town, Cell: FootprintCell.Visit } || town.Owner != hero.Owner)
        {
            return false;
        }

        var danger = Danger(hero.Owner, hero.Position);
        return danger > 0 && !Holds(ai.ArmyPower(town.Garrison), danger) && Holds(DefenceAt(hero, hero.Position), danger);
    }
}
