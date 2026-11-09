using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Domain.Visibility;

namespace EternalWars.Core.Domain.Info;

public static partial class InfoSheets
{
    /// <summary>
    /// What a right click on a map cell shows <paramref name="viewer"/> (ADR-154): a hero it sees there, else the object covering the cell (live
    /// while in sight, as last seen under the fog), else the ground. Null for a cell the player has not explored.
    /// </summary>
    public static InfoSheet? MapCell(GameState state, PlayerId viewer, MapPosition cell, bool detailed)
    {
        if (!state.Map.Contains(cell) || !state.Fog.IsExplored(viewer, cell))
        {
            return null;
        }

        if (state.HeroAt(cell) is { } hero && state.CanSee(viewer, hero))
        {
            return Hero(state, hero, viewer, detailed);
        }

        var known = state.Memory.Of(viewer).FirstOrDefault(k => k.Cells().Contains(cell) || (k.Definition.Footprint.HasVisit && k.Position.Offset(
            k.Definition.Footprint.VisitOffset.Dx, k.Definition.Footprint.VisitOffset.Dy) == cell));
        if (known is not null)
        {
            var inSight = known.Cells().Any(c => state.Fog.IsInSight(viewer, c));
            return inSight && state.FindObject(known.Id) is { } live ? MapObject(state, viewer, live, detailed) : Remembered(state, known, detailed);
        }

        return Ground(state, cell);
    }

    /// <summary>A map object the player sees now.</summary>
    public static InfoSheet MapObject(GameState state, PlayerId viewer, MapObject mapObject, bool detailed)
    {
        var definition = mapObject.Definition;
        var lines = new List<string>();
        var owner = (mapObject as IFlaggable)?.Owner;
        if (mapObject is IFlaggable)
        {
            lines.Add(owner is { } o ? $"Owner: {(o == viewer ? "you" : state.GetPlayer(o).Name)}" : "No owner.");
        }

        switch (mapObject)
        {
            case Town town:
                return Town(state, viewer, town, detailed);
            case MonsterGuard monster:
                var shown = AdventureIntel.ScoutMonster(state, monster, viewer);
                var stack = shown.Stacks.FirstOrDefault();
                if (stack is not null)
                {
                    var sheet = Creature(state, stack.Creature, detailed, stack);
                    var disposition = $"Disposition: {monster.Disposition}{(monster.NeverFlees ? ", never flees" : "")}";
                    return sheet with { Kind = "Neutral stack", Lines = [.. sheet.Lines.Take(1), disposition, .. sheet.Lines.Skip(1)] };
                }

                break;
            case Mine mine:
                lines.Add($"Gives {N(mine.DailyYield)} {mine.Resource.ToString().ToLowerInvariant()} a day.");
                AddGuard(state, viewer, mine.Guard, mine.Position, lines);
                break;
            case CreatureDwelling dwelling:
                var creature = state.Creatures.Get(dwelling.Creature);
                lines.Add($"Recruits {creature.Name} (tier {N(creature.Tier)}), {Cost(creature.Cost)} each" + (owner == viewer ? $"; {N(dwelling.Available ?? 0)} waiting." : "."));
                AddGuard(state, viewer, dwelling.Guard, dwelling.Position, lines);
                break;
            case ArtifactPickup pickup:
                lines.Add(pickup.Artifact is { } artifact && state.Artifacts.TryGet(artifact, out var a) ? $"{a.Name} ({a.Tier}, {a.Slot})" : $"A {pickup.Tier.ToString().ToLowerInvariant()} artifact.");
                break;
            case ResourcePile pile when definition.Resource is { } resource:
                lines.Add($"A pile of {resource.ToString().ToLowerInvariant()}.");
                break;
            case ResourceProvider:
                lines.Add($"This week: {ResourceProviderRules.DescribeWeek(definition, state.Date)}.");
                break;
        }

        // An arena map's day of preparation shows what some objects hold (ARN-2 c).
        lines.AddRange(AdventureIntel.ArenaContents(state, mapObject).Select(line => char.ToUpperInvariant(line[0]) + line[1..] + (line.EndsWith('.') ? "" : ".")));
        AddVisits(state, viewer, mapObject, lines);
        if (detailed)
        {
            lines.Add($"Id {definition.Id}   Visits: {definition.VisitScope}   Size {N(definition.Footprint.Width)}×{N(definition.Footprint.Height)}");
        }

        if (lines.Count == 0)
        {
            lines.Add(definition.Footprint.HasVisit ? "Visit it to see what it holds." : "Scenery.");
        }

        return new InfoSheet(definition.Name, Title(definition.Kind.ToString()), lines);
    }

    /// <summary>A town: its owner, type and garrison as the player's Scouting shows it (ADR-125); its own town's buildings in full.</summary>
    public static InfoSheet Town(GameState state, PlayerId viewer, Town town, bool detailed)
    {
        var intel = AdventureIntel.ScoutTown(state, town, viewer);
        var lines = new List<string> { town.Owner is { } o ? $"Owner: {(o == viewer ? "you" : state.GetPlayer(o).Name)}" : "No owner." };
        lines.Add($"Garrison: {ShownArmyText(state, intel.Garrison)}");
        foreach (var (label, hero) in new[] { ("In the garrison", intel.GarrisonHero), ("At the gate", intel.VisitingHero) })
        {
            if (hero is not null)
            {
                lines.Add($"{label}: {hero.Name} with {ShownArmyText(state, hero.Stacks)}");
            }
        }

        if (town.Owner == viewer)
        {
            var catalog = TownRules.Buildings(state, town);
            var built = town.Buildings.Where(b => b.Value > 0 && catalog.Contains(b.Key)).Select(b => catalog.Get(b.Key, b.Value).Name).ToList();
            lines.Add($"Buildings: {(built.Count == 0 ? "none" : string.Join(", ", built))}");
            lines.Add($"Built today: {(town.LastBuilt == state.Date ? town.LastBuiltName ?? "a building" : "nothing yet")}");
        }
        else if (intel.Available is { } available)
        {
            lines.Add($"Waiting in the dwellings: {ShownArmyText(state, available)}");
        }

        if (detailed)
        {
            lines.Add($"Sees {N(town.ScoutingRadius)} cells around it.");
        }

        return new InfoSheet(town.Name, $"Town, {Title(town.TownType)}", lines);
    }

    /// <summary>An object under the fog, as the player last saw it.</summary>
    private static InfoSheet Remembered(GameState state, KnownObject known, bool detailed)
    {
        var lines = new List<string> { "Last seen here; it may have changed since." };
        if (known.Owner is { } owner)
        {
            lines.Add($"Owner then: {state.GetPlayer(owner).Name}");
        }

        if (detailed)
        {
            lines.Add($"Id {known.Definition.Id}");
        }

        return new InfoSheet(known.Definition.Name, Title(known.Definition.Kind.ToString()), lines);
    }

    private static InfoSheet Ground(GameState state, MapPosition cell)
    {
        var lines = new List<string>();
        if (state.Map.GetRoad(cell) is var road and not RoadType.None)
        {
            lines.Add($"{Title(road.ToString())} road.");
        }

        if (state.Map.GetRiver(cell) is var river and not RiverType.None)
        {
            lines.Add($"{Title(river.ToString())} river.");
        }

        if (lines.Count == 0)
        {
            lines.Add("Open ground.");
        }

        return new InfoSheet(Title(state.Map.GetTerrain(cell).ToString()), "Terrain", lines);
    }

    private static void AddGuard(GameState state, PlayerId viewer, GuardStack? guard, MapPosition at, List<string> lines)
    {
        if (guard is not { Count: { } count and > 0 })
        {
            return;
        }

        var shown = AdventureIntel.ScoutStacks(state, [new CreatureStack(guard.Creature, count)], at, null, viewer);
        lines.Add($"Guarded by {ShownArmyText(state, shown)}.");
    }

    /// <summary>Which of the viewer's heroes took the object's reward, for objects that give it once (to each hero or to the first).</summary>
    private static void AddVisits(GameState state, PlayerId viewer, MapObject mapObject, List<string> lines)
    {
        var scope = mapObject.Definition.VisitScope;
        if (scope == VisitScope.Unlimited)
        {
            return;
        }

        var own = mapObject.Visits.Heroes.Select(h => state.Heroes.FirstOrDefault(x => x.Id == h)).OfType<Heroes.Hero>().Where(h => h.Owner == viewer).Select(h => h.Name).ToList();
        if (scope == VisitScope.FirstVisitor)
        {
            lines.Add(own.Count > 0 ? "Already taken by your hero." : "Its reward goes to the first visitor.");
            return;
        }

        lines.Add(own.Count > 0 ? $"Visited by {string.Join(", ", own)}." : "None of your heroes has visited it.");
    }
}
