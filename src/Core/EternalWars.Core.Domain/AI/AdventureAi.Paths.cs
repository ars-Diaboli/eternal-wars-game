using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Pathfinding;

namespace EternalWars.Core.Domain.AI;

/// <summary>
/// Route costs for weighing targets. One cost map per hero and game moment answers every target's cost, instead of one route search per
/// target; only the target chosen is searched in full, by the move. The costs are those of <see cref="HeroMovementService.FindPath"/>.
/// </summary>
public sealed partial class AdventureAi
{
    private PathFinder? _paths;
    private PathFinder.CostMap? _costs;
    private (HeroId Hero, long Stamp) _costsFor;
    private PathFinder.ReverseCostMap? _costsTo;

    private PathFinder Paths => _paths ??= new PathFinder(_state);

    /// <summary>The cost of the hero's cheapest route to <paramref name="target"/>, or null when there is none.</summary>
    private int? WalkCost(Hero hero, MapPosition target)
    {
        // The game changes only through commands (and moves): until one runs, the same map answers.
        var key = (hero.Id, session.ChangeStamp);
        if (_costs is null || _costsFor != key)
        {
            _costs = Paths.CostsFrom(hero.Id, reuse: _costs);
            _costsFor = key;
        }

        return _costs.CostTo(target);
    }

    /// <summary>
    /// The costs of the hero's cheapest routes to <paramref name="target"/> from tiles it does not stand on (Dimension Door landings); good
    /// until the game changes.
    /// </summary>
    private PathFinder.ReverseCostMap CostsTo(Hero hero, MapPosition target) => _costsTo = Paths.CostsTo(hero.Id, target, reuse: _costsTo);
}
