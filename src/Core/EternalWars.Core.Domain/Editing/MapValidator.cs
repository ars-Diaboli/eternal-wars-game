using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

public enum IssueSeverity
{
    Warning,
    Error,
}

public sealed record MapIssue(IssueSeverity Severity, string Message, MapPosition? Position = null);

/// <summary>
/// Checks a scenario for problems that individual edits cannot prevent (e.g. painting water under a
/// hero). The editor shows these; errors should block "play this map".
/// </summary>
public static class MapValidator
{
    public static IReadOnlyList<MapIssue> Validate(GameState scenario)
    {
        var issues = new List<MapIssue>();
        var map = scenario.Map;

        foreach (var hero in scenario.Heroes)
        {
            if (!scenario.HasPlayer(hero.Owner))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} belongs to a missing player.", hero.Position));
            }

            if (!map.Contains(hero.Position) || !scenario.Terrains.IsPassable(map.GetTerrain(hero.Position)))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} stands on impassable terrain.", hero.Position));
            }
            else if (scenario.ObjectCellAt(hero.Position) is not null)
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} stands inside an object.", hero.Position));
            }

            if (hero.Army.IsEmpty)
            {
                issues.Add(new(IssueSeverity.Warning, $"Hero {hero.Name} has no army and cannot fight.", hero.Position));
            }

            foreach (var (_, stack) in hero.Army.Stacks.Where(s => !scenario.Creatures.Contains(s.Stack.Creature)))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} has unknown creature '{stack.Creature}'.", hero.Position));
            }
        }

        foreach (var mapObject in scenario.Objects)
        {
            if (!scenario.Terrains.IsPassable(map.GetTerrain(mapObject.VisitPosition)))
            {
                issues.Add(new(IssueSeverity.Error, $"{mapObject.Definition.Name} has an impassable entrance.", mapObject.VisitPosition));
            }

            var name = mapObject.Definition.Name;
            switch (mapObject)
            {
                case Mine { Owner: { } owner } mine when !scenario.HasPlayer(owner):
                    issues.Add(new(IssueSeverity.Error, $"{name} belongs to a missing player.", mine.Position));
                    break;
                case TwoWayGate { LinkedGate: null } gate:
                    issues.Add(new(IssueSeverity.Warning, $"{name} is not linked.", gate.Position));
                    break;
                case TwoWayGate { LinkedGate: { } link } gate
                    when scenario.Objects.FirstOrDefault(o => o.Id == link) is not TwoWayGate partner || partner.LinkedGate != gate.Id:
                    issues.Add(new(IssueSeverity.Error, $"{name} link is not mutual.", gate.Position));
                    break;
                case OneWayGateEntrance { Exit: null } entrance:
                    issues.Add(new(IssueSeverity.Warning, $"{name} has no exit.", entrance.Position));
                    break;
                case OneWayGateEntrance { Exit: { } exit } entrance
                    when scenario.Objects.FirstOrDefault(o => o.Id == exit) is not OneWayGateExit:
                    issues.Add(new(IssueSeverity.Error, $"{name} points at something that is not a one-way exit.", entrance.Position));
                    break;
                case OneWayGateExit exit when !scenario.ObjectsOf<OneWayGateEntrance>().Any(e => e.Exit == exit.Id):
                    issues.Add(new(IssueSeverity.Warning, $"{name} is not used by any entrance.", exit.Position));
                    break;
                case MonsterGuard monster when !scenario.Creatures.Contains(monster.Creature):
                    issues.Add(new(IssueSeverity.Error, $"Monster uses unknown creature '{monster.Creature}'.", monster.Position));
                    break;
                case Mine { Guard: { } guard } mine when !scenario.Creatures.Contains(guard.Creature):
                    issues.Add(new(IssueSeverity.Error, $"{name} guard uses unknown creature '{guard.Creature}'.", mine.Position));
                    break;
            }
        }

        for (var level = 0; level < map.Levels; level++)
        {
            foreach (var position in map.Positions(level))
            {
                if (map.GetRoad(position) != RoadType.None && !scenario.Terrains.IsPassable(map.GetTerrain(position)))
                {
                    issues.Add(new(IssueSeverity.Warning, "Road on impassable terrain.", position));
                }
            }
        }

        foreach (var player in scenario.Players.Where(p => !scenario.HeroesOf(p.Id).Any()))
        {
            issues.Add(new(IssueSeverity.Warning, $"Player {player.Name} has no starting hero."));
        }

        return issues;
    }
}
