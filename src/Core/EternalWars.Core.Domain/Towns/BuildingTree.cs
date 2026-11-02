using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Domain.Towns;

/// <summary>Where a building of the town stands today (ADR-114), for the town screen.</summary>
public enum BuildingStatus
{
    /// <summary>Its next level can be built now.</summary>
    Available,

    /// <summary>Every level is built.</summary>
    Complete,

    /// <summary>The town built something today already (one building a day).</summary>
    BuiltToday,

    /// <summary>Another building (or level) is missing.</summary>
    NeedsBuildings,

    /// <summary>The owner cannot pay for it.</summary>
    CannotAfford,

    /// <summary>Refused for another reason (a second Capitol).</summary>
    Refused,
}

/// <summary>One requirement of a building's next level and whether the town has it.</summary>
public sealed record RequirementState(BuildingRequirement Requirement, string Name, bool Met);

/// <summary>
/// A building of the town on the town screen's tree (ADR-114): its built level, its next level with the cost after Stewardship, that level's
/// requirements (met or not), the status, the refusal text, and its <see cref="Depth"/>: the column of the tree, the longest chain of
/// requirements below it (0 for a building that needs nothing).
/// </summary>
public sealed record BuildingNode(
    string Building, string Name, int Level, int MaxLevel, BuildingLevel? Next, ResourceSet? Cost, IReadOnlyList<RequirementState> Requires,
    BuildingStatus Status, string? Refusal, int Depth);

/// <summary>A dwelling the town could still build, and the tiers that building it would leave without room (the 7-of-9 rule, ADR-030).</summary>
public sealed record DwellingChoice(CreatureDefinition Line, IReadOnlyList<int> TiersLeftOut, IReadOnlyList<CreatureDefinition> ClosedLines);

/// <summary>The town screen's building tree and dwelling warnings (ADR-114): read-only queries over <see cref="TownRules"/>.</summary>
public static class BuildingTree
{
    /// <summary>The town's buildings (no dwellings) in tree order: by <see cref="BuildingNode.Depth"/>, then in table order.</summary>
    public static IReadOnlyList<BuildingNode> For(GameState state, Town town)
    {
        var catalog = TownRules.Buildings(state, town);
        var depths = new Dictionary<string, int>(StringComparer.Ordinal);
        var nodes = new List<BuildingNode>();
        foreach (var building in catalog.TownBuildings)
        {
            var level = town.Level(building);
            var next = TownRules.NextLevel(state, town, building);
            var requires = (next?.Requires ?? []).Select(r => new RequirementState(
                r, NameOf(catalog, r), TownRules.Level(state, town, r.Building) >= r.Level)).ToList();
            var refusal = TownRules.CheckBuild(state, town, building);
            var name = next?.Name ?? catalog.Get(building, level).Name;
            nodes.Add(new BuildingNode(
                building, name, level, catalog.MaxLevel(building), next, next is null ? null : TownRules.BuildCost(state, town, next), requires,
                Status(state, town, next, requires, refusal), refusal, Depth(catalog, building, depths, [])));
        }

        return [.. nodes.OrderBy(n => n.Depth)];
    }

    /// <summary>"Fort 2 (Citadel)" style name of a requirement; a dwelling requirement names the tier.</summary>
    private static string NameOf(BuildingCatalog catalog, BuildingRequirement requirement) =>
        BuiltInBuildings.DwellingTier(requirement.Building) is { } tier
            ? requirement.Level == 1 ? $"a tier {tier} dwelling" : $"an upgraded tier {tier} dwelling"
            : catalog.Get(requirement.Building, requirement.Level).Name;

    private static BuildingStatus Status(GameState state, Town town, BuildingLevel? next, IReadOnlyList<RequirementState> requires, string? refusal) =>
        next is null ? BuildingStatus.Complete
        : refusal is null ? BuildingStatus.Available
        : requires.Any(r => !r.Met) ? BuildingStatus.NeedsBuildings
        : town.LastBuilt == state.Date ? BuildingStatus.BuiltToday
        : TownRules.CheckCapitol(state, town, next) is not null ? BuildingStatus.Refused
        : BuildingStatus.CannotAfford;

    /// <summary>
    /// The longest requirement chain under the building's first level (where it enters the tree; upgrades needing more stay in its column); a
    /// dwelling requirement counts the dwellings' own requirements.
    /// </summary>
    private static int Depth(BuildingCatalog catalog, string building, Dictionary<string, int> known, HashSet<string> visiting)
    {
        if (known.TryGetValue(building, out var depth))
        {
            return depth;
        }

        if (!visiting.Add(building))
        {
            return 0; // a loop in a designer's table: the catalog allows it, the tree just stops there
        }

        depth = catalog.Get(building, 1).Requires
            .Where(r => r.Building != building)
            .Select(r => 1 + Depth(catalog, r.Building, known, visiting))
            .DefaultIfEmpty(0)
            .Max();
        visiting.Remove(building);
        known[building] = depth;
        return depth;
    }

    /// <summary>
    /// For each dwelling the town has not built: the tiers that would be left without room for a dwelling if it were built now, and the lines
    /// that could no longer be built afterwards (the 7-of-9 rule). Empty lists when the choice closes nothing. A line whose building is refused by
    /// the rule itself lists the tier it would leave out.
    /// </summary>
    public static IReadOnlyList<DwellingChoice> DwellingChoices(GameState state, Town town)
    {
        var lines = TownRules.Lines(state, town);
        var choices = new List<DwellingChoice>();
        foreach (var line in lines.Where(l => town.DwellingLevel(l.Id) == 0))
        {
            var built = lines.Where(l => l == line || town.DwellingLevel(l.Id) > 0).ToList();
            var room = TownRules.MaxDwellings - built.Count;
            var empty = lines.Select(l => l.Tier).Distinct().Where(t => built.All(b => b.Tier != t)).Order().ToList();
            var leftOut = empty.Count > room ? empty.Skip(Math.Max(0, room)).ToList() : [];

            // With this line built, another unbuilt line closes when building it as well would break the rule.
            var closed = lines.Where(other => other != line && town.DwellingLevel(other.Id) == 0 && !Fits(lines, built.Append(other).ToList())).ToList();
            choices.Add(new DwellingChoice(line, leftOut, closed));
        }

        return choices;
    }

    private static bool Fits(IReadOnlyList<CreatureDefinition> lines, IReadOnlyList<CreatureDefinition> built) =>
        built.Count <= TownRules.MaxDwellings
        && lines.Select(l => l.Tier).Distinct().Count(t => built.All(b => b.Tier != t)) <= TownRules.MaxDwellings - built.Count;
}
