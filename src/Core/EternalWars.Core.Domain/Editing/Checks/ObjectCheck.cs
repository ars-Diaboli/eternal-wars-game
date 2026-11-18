using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// The map objects: a passable entrance, then each kind's own problem (owners, gate links, known creatures, artifacts and heroes, prisons, garrisons,
/// towns, Pandora's Boxes, quest objects without a quest).
/// </summary>
internal sealed class ObjectCheck : IMapCheck
{
    public IEnumerable<MapIssue> Check(GameState scenario)
    {
        foreach (var mapObject in scenario.Objects)
        {
            if (mapObject.Definition.Footprint.HasVisit && !scenario.Terrains.IsPassable(scenario.Map.GetTerrain(mapObject.VisitPosition)))
            {
                yield return new(IssueSeverity.Error, $"{mapObject.Definition.Name} has an impassable entrance.", mapObject.VisitPosition);
            }

            var name = mapObject.Definition.Name;
            switch (mapObject)
            {
                case Mine { Owner: { } owner } mine when !scenario.HasPlayer(owner):
                    yield return new(IssueSeverity.Error, $"{name} belongs to a missing player.", mine.Position);
                    break;
                case TwoWayGate { LinkedGate: null } gate:
                    yield return new(IssueSeverity.Warning, $"{name} is not linked.", gate.Position);
                    break;
                case TwoWayGate { LinkedGate: { } link } gate
                    when scenario.Objects.FirstOrDefault(o => o.Id == link) is not TwoWayGate partner || partner.LinkedGate != gate.Id:
                    yield return new(IssueSeverity.Error, $"{name} link is not mutual.", gate.Position);
                    break;
                case OneWayGateEntrance { Exit: null } entrance:
                    yield return new(IssueSeverity.Warning, $"{name} has no exit.", entrance.Position);
                    break;
                case OneWayGateEntrance { Exit: { } exit } entrance
                    when scenario.Objects.FirstOrDefault(o => o.Id == exit) is not OneWayGateExit:
                    yield return new(IssueSeverity.Error, $"{name} points at something that is not a one-way exit.", entrance.Position);
                    break;
                case OneWayGateExit exit when !scenario.ObjectsOf<OneWayGateEntrance>().Any(e => e.Exit == exit.Id):
                    yield return new(IssueSeverity.Warning, $"{name} is not used by any entrance.", exit.Position);
                    break;
                case ArtifactPickup { Artifact: { } artifact } pickup when !scenario.Artifacts.Contains(artifact):
                    yield return new(IssueSeverity.Error, $"{name} holds the unknown artifact '{artifact}'.", pickup.Position);
                    break;
                case CreatureDwelling dwelling when !scenario.Creatures.Contains(dwelling.Creature):
                    yield return new(IssueSeverity.Error, $"{name} uses unknown creature '{dwelling.Creature}'.", dwelling.Position);
                    break;
                case CreatureDwelling dwelling when scenario.Creatures.IsUpgrade(dwelling.Creature):
                    yield return new(IssueSeverity.Error,
                        $"{name} sells the upgraded '{dwelling.Creature}'; map dwellings sell base creatures only ('{scenario.Creatures.BaseOf(dwelling.Creature)}').",
                        dwelling.Position);
                    break;
                case MonsterGuard monster when !scenario.Creatures.Contains(monster.Creature):
                    yield return new(IssueSeverity.Error, $"Monster uses unknown creature '{monster.Creature}'.", monster.Position);
                    break;
                case Prison { Hero: { } prisoner } prison when !scenario.HeroRoster.Contains(prisoner):
                    yield return new(IssueSeverity.Error, $"{name} holds the unknown hero '{prisoner}'.", prison.Position);
                    break;
                case Prison prison when !scenario.Terrains.IsPassable(scenario.Map.GetTerrain(prison.Position)):
                    yield return new(IssueSeverity.Error, $"{name}: the freed hero would stand on impassable terrain.", prison.Position);
                    break;
                case Prison prison when scenario.Heroes.Any(h => h.Definition is { } d && d == prison.Hero):
                    yield return new(IssueSeverity.Warning, $"{name} holds a hero who is already on the map.", prison.Position);
                    break;
                case Mine { Guard: { } guard } mine when !scenario.Creatures.Contains(guard.Creature):
                    yield return new(IssueSeverity.Error, $"{name} guard uses unknown creature '{guard.Creature}'.", mine.Position);
                    break;
                case MapGarrison { Owner: { } garrisonOwner } garrison when !scenario.HasPlayer(garrisonOwner):
                    yield return new(IssueSeverity.Error, $"{name} belongs to a missing player.", garrison.Position);
                    break;
                case MapGarrison garrison when garrison.Troops.Stacks.Any(s => !scenario.Creatures.Contains(s.Stack.Creature)):
                    yield return new(IssueSeverity.Error, $"{name} holds an unknown creature.", garrison.Position);
                    break;
                case MapGarrison { Owner: null, Troops.IsEmpty: true } garrison:
                    yield return new(IssueSeverity.Warning, $"{name} is neutral and empty: the first hero to arrive takes it.", garrison.Position);
                    break;
                case Town town:
                    foreach (var problem in TownProblems(scenario, town))
                    {
                        yield return new(IssueSeverity.Error, $"{town.Name} {problem}", town.Position);
                    }

                    break;
                case PandorasBox box:
                    foreach (var problem in BoxProblems(scenario, box))
                    {
                        yield return new(IssueSeverity.Error, $"{name} {problem}", box.Position);
                    }

                    break;
                case QuestSite { Quest: null } site:
                    yield return new(IssueSeverity.Warning, site.IsBarrier ? $"{name} has no quest: it bars every hero." : $"{name} has no quest: it stands empty.", site.Position);
                    break;
            }
        }
    }

    /// <summary>A Pandora's Box (ADR-100): it must hold something (or run a hero event), only gains, and only what exists.</summary>
    private static IEnumerable<string> BoxProblems(GameState scenario, PandorasBox box)
    {
        if (box.Contents.IsEmpty && box.Event is null)
        {
            yield return "is empty; give it something to hold or an event to run.";
        }

        // An event that is not a hero event is reported with the map events.
        if (box.Contents.Problem() is { } problem)
        {
            yield return problem;
        }

        foreach (var artifact in box.Contents.Artifacts.Where(a => !scenario.Artifacts.Contains(a)))
        {
            yield return $"holds the unknown artifact '{artifact}'.";
        }

        foreach (var stack in box.Contents.Creatures.Concat(box.Guard).Where(s => !scenario.Creatures.Contains(s.Creature)))
        {
            yield return $"has unknown creature '{stack.Creature}'.";
        }
    }

    /// <summary>A town's problems: its owner, buildings and their requirements, one Capitol, its dwellings, garrison and guild spells.</summary>
    private static IEnumerable<string> TownProblems(GameState scenario, Town town)
    {
        if (town.Owner is { } owner && !scenario.HasPlayer(owner))
        {
            yield return "belongs to a missing player.";
        }

        foreach (var (building, level) in town.Buildings.Where(b => b.Value > TownRules.Buildings(scenario, town).MaxLevel(b.Key)))
        {
            yield return $"has {building} level {level}, which does not exist.";
        }

        if (TownRules.UnmetRequirement(scenario, town) is { } requirement)
        {
            yield return $"breaks a building requirement: {requirement}";
        }

        if (town.Owner is { } capitolOwner && town.Level(BuiltInBuildings.TownHall) >= BuiltInBuildings.CapitolLevel
            && TownRules.CapitolOf(scenario, capitolOwner, except: town) is { } first && first.Id.Value < town.Id.Value)
        {
            yield return $"is a second Capitol of its owner ({first.Name} is one); a player has only one.";
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

        if (Magic.MagicRules.GuildSpellsProblem(scenario.Spells, town.TownType, town.FixedGuildSpells) is { } spells)
        {
            yield return $"has wrong guild spells: {spells}";
        }
    }
}
