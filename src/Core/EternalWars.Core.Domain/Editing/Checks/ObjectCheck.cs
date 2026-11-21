using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Editing.Checks;

/// <summary>
/// The map objects: a passable entrance, then each kind's own problem (owners, gate links, known creatures, artifacts and heroes, prisons, garrisons,
/// towns, Pandora's Boxes, quest objects without a quest). Where the answer is clear an issue carries its fix (ADR-265): a gate is linked with
/// the only gate it can pair with, and what belongs to a missing player becomes neutral.
/// </summary>
internal sealed class ObjectCheck : IMapCheck
{
    public IssueKind Kind => IssueKind.Objects;

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
                    yield return new(IssueSeverity.Error, $"{name} belongs to a missing player.", mine.Position) { Fix = Neutral(mine, owner) };
                    break;
                case TwoWayGate { LinkedGate: null } gate:
                    yield return new(IssueSeverity.Warning, $"{name} is not linked.", gate.Position) { Fix = PairFor(scenario, gate) };
                    break;
                case TwoWayGate { LinkedGate: { } link } gate
                    when scenario.Objects.FirstOrDefault(o => o.Id == link) is not TwoWayGate partner || partner.LinkedGate != gate.Id:
                    yield return new(IssueSeverity.Error, $"{name} link is not mutual.", gate.Position)
                    {
                        // The gate it points at, of its kind, is linked to nothing and pointed at by it alone: linking that one back is the clear answer.
                        Fix = scenario.FindObject(link) is TwoWayGate { LinkedGate: null } back && back.Definition.Id == gate.Definition.Id
                            && scenario.ObjectsOf<TwoWayGate>().Count(g => g.LinkedGate == back.Id) == 1
                            ? new LinkGatesFix($"Link the {back.Definition.Name.ToLowerInvariant()} at {At(back.Position)} back to it", gate.Id, back.Id)
                            : null,
                    };
                    break;
                case OneWayGateEntrance { Exit: null } entrance:
                    yield return new(IssueSeverity.Warning, $"{name} has no exit.", entrance.Position) { Fix = ExitFor(scenario) };
                    break;
                case OneWayGateEntrance { Exit: { } exit } entrance
                    when scenario.Objects.FirstOrDefault(o => o.Id == exit) is not OneWayGateExit:
                    yield return new(IssueSeverity.Error, $"{name} points at something that is not a one-way exit.", entrance.Position);
                    break;
                case OneWayGateExit exit when !scenario.ObjectsOf<OneWayGateEntrance>().Any(e => e.Exit == exit.Id):
                    yield return new(IssueSeverity.Warning, $"{name} is not used by any entrance.", exit.Position) { Fix = ExitFor(scenario) };
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
                    yield return new(IssueSeverity.Error, $"{name} belongs to a missing player.", garrison.Position) { Fix = Neutral(garrison, garrisonOwner) };
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
                        yield return new(IssueSeverity.Error, $"{town.Name} {problem}", town.Position)
                        {
                            Fix = problem == MissingOwner && town.Owner is { } missing ? Neutral(town, missing) : null,
                        };
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

    private const string MissingOwner = "belongs to a missing player.";

    /// <summary>The fix of a thing whose owner is not a player of the map: it becomes neutral.</summary>
    private static OwnerFix Neutral(MapObject mapObject, PlayerId missing) =>
        new($"Make the {mapObject.Definition.Name.ToLowerInvariant()} neutral", mapObject.Id, missing, null);

    /// <summary>
    /// The gate an unlinked two-way gate clearly pairs with, among the gates of its kind (a subterranean gate pairs with a subterranean gate):
    /// the one gate that points at it, else the one other unlinked gate when no gate points at either; null when there is no such gate or more
    /// than one.
    /// </summary>
    private static LinkGatesFix? PairFor(GameState scenario, TwoWayGate gate)
    {
        var gates = scenario.ObjectsOf<TwoWayGate>().Where(g => g.Definition.Id == gate.Definition.Id).ToList();
        if (scenario.ObjectsOf<TwoWayGate>().Any(g => g.LinkedGate == gate.Id && g.Definition.Id != gate.Definition.Id))
        {
            return null; // a gate of another kind points at it: not clear
        }

        var pointing = gates.Where(g => g.LinkedGate == gate.Id).ToList();
        var partner = pointing.Count switch
        {
            1 => pointing[0],
            0 => gates.Where(g => g.LinkedGate is null && !gates.Any(o => o.LinkedGate == g.Id)).ToList() is [var a, var b] && (a == gate || b == gate)
                ? (a == gate ? b : a)
                : null,
            _ => null,
        };
        return partner is null ? null : new LinkGatesFix($"Link it with the {partner.Definition.Name.ToLowerInvariant()} at {At(partner.Position)}", gate.Id, partner.Id);
    }

    /// <summary>The fix of a one-way entrance without an exit and an exit no entrance uses, when the map has exactly one of each: link them.</summary>
    private static GateExitFix? ExitFor(GameState scenario)
    {
        var exits = scenario.ObjectsOf<OneWayGateExit>();
        var entrances = scenario.ObjectsOf<OneWayGateEntrance>();
        return entrances.Where(e => e.Exit is null).ToList() is [var entrance] && exits.Where(x => !entrances.Any(e => e.Exit == x.Id)).ToList() is [var exit]
            ? new GateExitFix($"Point the entrance at {At(entrance.Position)} at the exit at {At(exit.Position)}", entrance.Id, exit.Id)
            : null;
    }

    /// <summary>A tile as the fixes name it: x, y, and "underground" below the surface.</summary>
    internal static string At(MapPosition position) => $"{position.X}, {position.Y}{(position.Level > 0 ? " underground" : "")}";

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
            yield return MissingOwner;
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

        if (town.Dwellings.Count > 0 && town.Dwellings.Keys.First() is var any && TownRules.CheckDwellingChoice(scenario, town, any, town.DwellingLevel(any)) is { } choice)
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
