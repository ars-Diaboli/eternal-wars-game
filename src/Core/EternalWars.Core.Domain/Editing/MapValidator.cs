using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

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
            else if (scenario.ObjectCellAt(hero.Position) is { } cell && !IsOwnTownEntrance(cell, hero.Owner))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} stands inside an object.", hero.Position));
            }

            if (hero.Army.IsEmpty)
            {
                issues.Add(new(IssueSeverity.Warning, $"Hero {hero.Name} has no army and cannot fight.", hero.Position));
            }

            if (hero.Definition is { } type && !scenario.HeroRoster.Contains(type))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} is of unknown type '{type}'.", hero.Position));
            }

            foreach (var (_, stack) in hero.Army.Stacks.Where(s => !scenario.Creatures.Contains(s.Stack.Creature)))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} has unknown creature '{stack.Creature}'.", hero.Position));
            }

            foreach (var artifact in hero.Artifacts.AllWorn.Select(w => w.Id).Concat(hero.Artifacts.Backpack).Where(a => !scenario.Artifacts.Contains(a)))
            {
                issues.Add(new(IssueSeverity.Error, $"Hero {hero.Name} has unknown artifact '{artifact}'.", hero.Position));
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
                case ArtifactPickup { Artifact: { } artifact } pickup when !scenario.Artifacts.Contains(artifact):
                    issues.Add(new(IssueSeverity.Error, $"{name} holds the unknown artifact '{artifact}'.", pickup.Position));
                    break;
                case CreatureDwelling dwelling when !scenario.Creatures.Contains(dwelling.Creature):
                    issues.Add(new(IssueSeverity.Error, $"{name} uses unknown creature '{dwelling.Creature}'.", dwelling.Position));
                    break;
                case MonsterGuard monster when !scenario.Creatures.Contains(monster.Creature):
                    issues.Add(new(IssueSeverity.Error, $"Monster uses unknown creature '{monster.Creature}'.", monster.Position));
                    break;
                case Mine { Guard: { } guard } mine when !scenario.Creatures.Contains(guard.Creature):
                    issues.Add(new(IssueSeverity.Error, $"{name} guard uses unknown creature '{guard.Creature}'.", mine.Position));
                    break;
                case Town town:
                    issues.AddRange(ValidateTown(scenario, town).Select(m => new MapIssue(IssueSeverity.Error, $"{town.Name} {m}", town.Position)));
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

        foreach (var player in scenario.Players.Where(p => !scenario.HeroesOf(p.Id).Any() && !scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id)))
        {
            issues.Add(new(IssueSeverity.Warning, $"Player {player.Name} has no starting hero or town."));
        }

        // The editor refuses this; a map made elsewhere loses the surplus when the game starts (ADR-068).
        foreach (var player in scenario.Players.Where(p => scenario.HeroesOf(p.Id).Count() > scenario.HeroLimit(p.Id)))
        {
            issues.Add(new(IssueSeverity.Warning,
                $"Player {player.Name} starts with more heroes than it may have ({scenario.HeroLimit(player.Id)}); the newest are removed when the game starts."));
        }

        issues.AddRange(ValidateRules(scenario));
        return issues;
    }

    /// <summary>Victory and defeat conditions (ADR-057): what they point at must exist, and a game should be able to end.</summary>
    private static IEnumerable<MapIssue> ValidateRules(GameState scenario)
    {
        var rules = scenario.Rules;
        foreach (var condition in rules.Victory.Concat(rules.Defeat))
        {
            var name = ScenarioRules.Describe(scenario, condition);
            if (condition.Player is { } player && !scenario.HasPlayer(player))
            {
                yield return new(IssueSeverity.Error, $"Condition '{condition.Kind}' belongs to a missing player.");
                continue;
            }

            var town = condition.Town is { } townId ? scenario.FindObject(townId) as Town : null;
            var hero = condition.Hero is { } heroId ? scenario.Heroes.FirstOrDefault(h => h.Id == heroId) : null;
            switch (condition.Kind)
            {
                case ConditionKind.CaptureTown or ConditionKind.LoseTown when town is null:
                    yield return new(IssueSeverity.Error, $"Condition '{condition.Kind}' points at a town that does not exist.");
                    break;
                case ConditionKind.DefeatHero or ConditionKind.LoseHero when hero is null:
                    yield return new(IssueSeverity.Error, $"Condition '{condition.Kind}' points at a hero that does not exist.");
                    break;
                case ConditionKind.LoseTown or ConditionKind.LoseHero when condition.Player is null:
                    yield return new(IssueSeverity.Error, $"'{name}' needs the player who loses.");
                    break;
                case ConditionKind.CaptureTown when town!.Owner is { } owner && (condition.Player is null ? scenario.HasPlayer(owner) : owner == condition.Player):
                    yield return new(IssueSeverity.Warning, $"'{name}': the town is owned from the start, so the game would end at once.", town.Position);
                    break;
                case ConditionKind.LoseTown when town!.Owner != condition.Player:
                    yield return new(IssueSeverity.Warning, $"'{name}': the player does not own the town at the start, so it would lose at once.", town.Position);
                    break;
                case ConditionKind.DefeatHero when condition.Player is { } winner && (winner == hero!.Owner || scenario.AreAllies(winner, hero.Owner)):
                    yield return new(IssueSeverity.Warning, $"'{name}': the hero is on the same team.", hero.Position);
                    break;
                case ConditionKind.GatherResources when condition.Amount < 1:
                    yield return new(IssueSeverity.Error, "A resource condition needs an amount above zero.");
                    break;
                case ConditionKind.SurviveUntilDay when condition.Day < 2:
                    yield return new(IssueSeverity.Error, "Surviving needs a day after the first.");
                    break;
                case ConditionKind.TimeLimit when condition.Day < 1:
                    yield return new(IssueSeverity.Error, "A time limit needs a day of 1 or more.");
                    break;
            }
        }

        if (!rules.CanBeWon)
        {
            yield return new(IssueSeverity.Warning, "Nobody can win this scenario: the standard victory is off and there is no victory condition.");
        }
        else if (rules.StandardVictory && rules.Victory.Count == 0
                 && scenario.Players.Where(p => scenario.HeroesOf(p.Id).Any() || scenario.ObjectsOf<Town>().Any(t => t.Owner == p.Id)).Select(p => p.Team).Distinct().Count() < 2)
        {
            yield return new(IssueSeverity.Warning, "Fewer than two teams start with a hero or a town, so the game cannot end by defeating enemies.");
        }
    }

    /// <summary>A hero may start in the entrance of its own town, visiting it (HoMM3; generated maps put the start hero there).</summary>
    internal static bool IsOwnTownEntrance((MapObject Object, FootprintCell Cell) cell, PlayerId owner) =>
        cell is { Object: Town town, Cell: FootprintCell.Visit } && town.Owner == owner;

    private static IEnumerable<string> ValidateTown(GameState scenario, Town town)
    {
        if (town.Owner is { } owner && !scenario.HasPlayer(owner))
        {
            yield return "belongs to a missing player.";
        }

        foreach (var (building, level) in town.Buildings.Where(b => b.Value > scenario.Buildings.MaxLevel(b.Key)))
        {
            yield return $"has {building} level {level}, which does not exist.";
        }

        if (TownRules.UnmetRequirement(scenario, town) is { } requirement)
        {
            yield return $"breaks a building requirement: {requirement}";
        }

        var lines = TownRules.Lines(scenario, town);
        foreach (var (line, level) in town.Dwellings)
        {
            if (lines.FirstOrDefault(l => l.Id == line) is not { } definition)
            {
                yield return $"has a dwelling for '{line}', which is not one of its creature lines.";
            }
            else if (level > 1 && definition.UpgradesTo is null)
            {
                yield return $"has an upgraded {definition.Name} dwelling, but that line has no upgrade.";
            }
        }

        if (town.Dwellings.Keys.FirstOrDefault() is { } any && TownRules.CheckDwellingChoice(scenario, town, any, town.DwellingLevel(any)) is { } choice)
        {
            yield return $"breaks the dwelling rule: {choice}";
        }

        foreach (var (_, stack) in town.Garrison.Stacks.Where(s => !scenario.Creatures.Contains(s.Stack.Creature)))
        {
            yield return $"garrison has unknown creature '{stack.Creature}'.";
        }
    }
}
