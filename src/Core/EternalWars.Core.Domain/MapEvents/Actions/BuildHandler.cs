using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.MapEvents.Actions;

/// <summary>
/// Builds a town building, or a line's dwelling, up to its level (EVT-4 a): no cost, no day's build, and every requirement built first.
/// </summary>
internal sealed class BuildHandler : MapEventActionHandler<BuildAction>
{
    protected override bool Apply(MapEventActionContext context, BuildAction action)
    {
        if (context.Town is { } town)
        {
            Build(context, town, action.Building, action.Level);
        }

        return true;
    }

    private static void Build(MapEventActionContext context, Town town, string building, int level, int depth = 0)
    {
        if (depth > 32)
        {
            return;
        }

        var (state, construction) = (context.State, context.Service.Construction);
        var catalog = TownRules.Buildings(state, town);
        if (TownRules.LineOf(state, town, building) is { } line && line.Id == building)
        {
            var dwelling = BuiltInBuildings.Dwelling(line.Tier);
            var top = Math.Min(level, line.UpgradesTo is null ? 1 : catalog.MaxLevel(dwelling));
            for (var l = town.DwellingLevel(line.Id) + 1; l <= top; l++)
            {
                BuildRequirements(context, town, catalog.Get(dwelling, l), depth);
                construction.RaiseDwelling(town, line, l);
            }

            return;
        }

        if (BuiltInBuildings.DwellingTier(building) is { } tier)
        {
            // A requirement on a tier ("dwelling-3"): the town's first line of that tier.
            if (TownRules.Lines(state, town).Where(l => l.Tier == tier).OrderBy(l => l.Id, StringComparer.Ordinal).FirstOrDefault() is { } first)
            {
                Build(context, town, first.Id, level, depth + 1);
            }

            return;
        }

        if (!catalog.Contains(building))
        {
            return;
        }

        for (var l = town.Level(building) + 1; l <= Math.Min(level, catalog.MaxLevel(building)); l++)
        {
            BuildRequirements(context, town, catalog.Get(building, l), depth);
            construction.Raise(town, building, l);
        }
    }

    private static void BuildRequirements(MapEventActionContext context, Town town, BuildingLevel level, int depth)
    {
        foreach (var requirement in level.Requires.Where(r => TownRules.Level(context.State, town, r.Building) < r.Level))
        {
            Build(context, town, requirement.Building, requirement.Level, depth + 1);
        }
    }
}
