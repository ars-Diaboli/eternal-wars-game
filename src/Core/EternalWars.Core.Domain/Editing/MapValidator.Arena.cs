using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing;

public static partial class MapValidator
{
    /// <summary>
    /// An arena map (ADR-097, ARN-2 e): without a combat place it is faulty (an error); warnings when two enemies can meet on day 1 (their heroes
    /// reach the same ground, <see cref="DayOneReach"/>), for map events that do something only on a later day (day 2 is the battle, and there is
    /// no turn after it), for victory and defeat conditions (the arena ignores them) and when fewer than two teams start with a hero or a town. A
    /// combat place on a map without the switch does nothing.
    /// </summary>
    private static IEnumerable<MapIssue> ValidateArena(GameState scenario)
    {
        var places = scenario.ObjectsOf<CombatPlace>().OrderBy(p => p.Id.Value).ToList();
        if (!scenario.Rules.Arena)
        {
            foreach (var place in places)
            {
                yield return new(IssueSeverity.Warning, $"{place.Definition.Name} does nothing on a map that is not an arena (Conditions tab).", place.Position);
            }

            yield break;
        }

        if (places.Count == 0)
        {
            yield return new(IssueSeverity.Error, "An arena map needs a combat place: the battle of day 2 is fought there.");
        }

        foreach (var place in places.Where(p => !scenario.Map.Contains(p.AttackCell) || !scenario.Terrains.IsPassable(scenario.Map.GetTerrain(p.AttackCell))))
        {
            yield return new(IssueSeverity.Error, $"{place.Definition.Name}: the attacker's cell is on impassable ground, where no battle can be fought.", place.Position);
        }

        var rules = scenario.Rules;
        if (rules.Victory.Count + rules.Defeat.Count > 0)
        {
            yield return new(IssueSeverity.Warning, "The arena ignores the victory and defeat conditions: the battle of day 2 decides the game.");
        }

        var sides = scenario.Players.Where(p => scenario.HeroesOf(p.Id).Any() || scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id)).ToList();
        if (sides.Select(p => p.Team).Distinct().Count() < 2)
        {
            yield return new(IssueSeverity.Warning, "Fewer than two teams start with a hero or a town: nobody can fight the arena battle.");
        }

        var reach = sides.ToDictionary(p => p.Id, p => DayOneReach(scenario, p.Id));
        for (var i = 0; i < sides.Count; i++)
        {
            for (var j = i + 1; j < sides.Count; j++)
            {
                var (a, b) = (sides[i], sides[j]);
                var common = scenario.AreEnemies(a.Id, b.Id)
                    ? reach[a.Id].Where(reach[b.Id].Contains).OrderBy(p => p.Level).ThenBy(p => p.Y).ThenBy(p => p.X).Take(1).ToList()
                    : [];
                if (common.Count > 0)
                {
                    yield return new(IssueSeverity.Warning,
                        $"{a.Name} and {b.Name} can meet on day 1, when movement is unlimited: keep them apart with impassable ground or objects.", common[0]);
                }
            }
        }

        foreach (var definition in scenario.MapEvents.Set.Events.Where(e => MapEvents.MapEventChecks.RunsOnlyAfter(e, ArenaRules.PreparationDay)))
        {
            yield return new(IssueSeverity.Warning,
                $"Event '{definition.Name}' does something only after day 1; an arena game ends with the battle at the start of day 2.", null, definition.Name);
        }
    }

    /// <summary>
    /// The ground the player's heroes can reach on an arena's day 1 (unlimited movement): from its heroes and its towns' entrances (where hired
    /// heroes appear), over passable cells that no object blocks, in 8 directions, and through gates. Monsters, garrisons, border and quest barriers
    /// are taken as passable: a hero may beat or open them that day.
    /// </summary>
    internal static HashSet<MapPosition> DayOneReach(GameState scenario, PlayerId player)
    {
        var reached = new HashSet<MapPosition>();
        var open = new Queue<MapPosition>();
        foreach (var start in scenario.HeroesOf(player).Select(h => h.Position)
                     .Concat(scenario.ObjectsOf<Town>().Where(t => t.Owner == player).Select(t => t.VisitPosition)))
        {
            Visit(start);
        }

        while (open.TryDequeue(out var position))
        {
            if (scenario.ObjectCellAt(position) is { Cell: FootprintCell.Visit, Object: var visited } && scenario.TeleportDestination(visited) is { } exit)
            {
                Visit(exit);
            }

            foreach (var direction in DirectionExtensions.All)
            {
                Visit(position.Step(direction));
            }
        }

        return reached;

        void Visit(MapPosition position)
        {
            if (MovementRules.CheckTile(scenario, position) == MoveResult.Moved && reached.Add(position))
            {
                open.Enqueue(position);
            }
        }
    }
}
