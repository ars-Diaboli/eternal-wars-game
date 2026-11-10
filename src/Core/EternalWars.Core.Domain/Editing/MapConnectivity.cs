using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

/// <summary>
/// Where a hero can walk on a map before anything is cleared: passable terrain around blocked object cells, through guards (they can be beaten)
/// and pickups (they go away), via gates; any other object entrance ends a route. The map checks use it (the simulator's generated-map check;
/// <see cref="MapValidator"/> may too).
/// </summary>
public static class MapConnectivity
{
    /// <summary>The cells a hero standing at <paramref name="start"/> can reach.</summary>
    public static IReadOnlySet<MapPosition> Reachable(GameState state, MapPosition start)
    {
        var reached = new HashSet<MapPosition> { start };
        var landed = new HashSet<MapPosition> { start }; // stood on after a gate: the hero walks on from there (ADR-117 fix)
        var open = new Queue<MapPosition>([start]);
        while (open.TryDequeue(out var position))
        {
            if (!landed.Contains(position) && state.ObjectCellAt(position) is { Cell: FootprintCell.Visit, Object: var entered })
            {
                if (entered.TeleportTarget is not null && state.TeleportDestination(entered) is { } exit && landed.Add(exit))
                {
                    reached.Add(exit);
                    open.Enqueue(exit);
                    continue;
                }

                if (entered is not (MonsterGuard or ResourcePile or TreasureChest or ArtifactPickup) && entered.EndsRoute)
                {
                    continue;
                }
            }

            foreach (var next in DirectionExtensions.All.Select(position.Step))
            {
                if (state.Map.Contains(next) && state.Terrains.IsPassable(state.Map.GetTerrain(next))
                    && state.ObjectCellAt(next) is not { Cell: FootprintCell.Blocked } && reached.Add(next))
                {
                    open.Enqueue(next);
                }
            }
        }

        return reached;
    }

    /// <summary>
    /// The other players' owned towns each player's first hero cannot walk to (<see cref="Reachable"/>), in hero then town order: on a fair map
    /// every player can reach every rival.
    /// </summary>
    public static IEnumerable<(Player Player, Town Town)> UnreachableRivalTowns(GameState state)
    {
        foreach (var hero in state.Heroes.GroupBy(h => h.Owner).Select(g => g.First()))
        {
            var reached = Reachable(state, hero.Position);
            foreach (var town in state.ObjectsOf<Town>().Where(t => t.Owner is { } owner && owner != hero.Owner && !reached.Contains(t.VisitPosition)))
            {
                yield return (state.GetPlayer(hero.Owner), town);
            }
        }
    }
}
