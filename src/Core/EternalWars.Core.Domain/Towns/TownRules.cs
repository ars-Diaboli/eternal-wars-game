using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Towns;

/// <summary>
/// Town rules as queries (ADR-030, ADR-046), shared by <see cref="TownService"/>, the editor, views and the AI. The
/// <c>Check…</c> methods return why something is not allowed, or null when it is.
/// </summary>
public static class TownRules
{
    /// <summary>A town builds at most this many of its nine dwellings (ADR-030).</summary>
    public const int MaxDwellings = 7;

    /// <summary>The town type's creature lines (base creatures, tier order): its nine dwellings.</summary>
    public static IReadOnlyList<CreatureDefinition> Lines(GameState state, Town town) =>
        state.Creatures.BaseCreaturesOf(town.TownType).ToList();

    /// <summary>The line a creature is recruited from: its own line or, for an upgraded creature, its base line.</summary>
    public static CreatureDefinition? LineOf(GameState state, Town town, string creature) =>
        Lines(state, town).FirstOrDefault(l => l.Id == creature || l.UpgradesTo == creature);

    /// <summary>An own hero standing on the town's entrance: recruits and garrison stacks can go to it.</summary>
    public static Hero? VisitingHero(GameState state, Town town) =>
        state.HeroAt(town.VisitPosition) is { } hero && hero.Owner == town.Owner ? hero : null;

    /// <summary>The level a building has in the town; a dwelling id (<c>dwelling-3</c>) counts its best dwelling of that tier.</summary>
    public static int Level(GameState state, Town town, string building) =>
        BuiltInBuildings.DwellingTier(building) is { } tier
            ? Lines(state, town).Where(l => l.Tier == tier).Select(l => town.DwellingLevel(l.Id)).DefaultIfEmpty(0).Max()
            : town.Level(building);

    /// <summary>The next level of a building, or null when it is fully built.</summary>
    public static BuildingLevel? NextLevel(GameState state, Town town, string building)
    {
        var level = town.Level(building) + 1;
        return level <= state.Buildings.MaxLevel(building) ? state.Buildings.Get(building, level) : null;
    }

    /// <summary>The next level of a line's dwelling (build, then upgrade), or null when there is none.</summary>
    public static BuildingLevel? NextDwellingLevel(GameState state, Town town, CreatureDefinition line)
    {
        var level = town.DwellingLevel(line.Id) + 1;
        var building = BuiltInBuildings.Dwelling(line.Tier);
        return level > state.Buildings.MaxLevel(building) || (level >= 2 && line.UpgradesTo is null)
            ? null
            : state.Buildings.Get(building, level);
    }

    /// <summary>Whether the town may build the next level of <paramref name="building"/> today.</summary>
    public static string? CheckBuild(GameState state, Town town, string building)
    {
        if (BuiltInBuildings.DwellingTier(building) is not null || !state.Buildings.Contains(building))
        {
            return $"Unknown town building '{building}'.";
        }

        return NextLevel(state, town, building) is { } next ? CheckLevel(state, town, next) : $"{building} is fully built.";
    }

    /// <summary>Whether the town may build (or upgrade) the dwelling of <paramref name="line"/> today.</summary>
    public static string? CheckDwelling(GameState state, Town town, string line)
    {
        if (Lines(state, town).FirstOrDefault(l => l.Id == line) is not { } definition)
        {
            return $"{town.Name} has no dwelling for '{line}'.";
        }

        if (NextDwellingLevel(state, town, definition) is not { } next)
        {
            return $"The {definition.Name} dwelling is fully built.";
        }

        if (next.Level == 1 && CheckDwellingChoice(state, town, line, 1) is { } refusal)
        {
            return refusal;
        }

        return CheckLevel(state, town, next);
    }

    /// <summary>
    /// The 7-of-9 rule (ADR-030): at most <see cref="MaxDwellings"/> dwellings, and never so many that a tier could no
    /// longer get one. Checks the town as it would be with <paramref name="line"/> at <paramref name="level"/>.
    /// </summary>
    public static string? CheckDwellingChoice(GameState state, Town town, string line, int level)
    {
        var lines = Lines(state, town);
        var built = lines.Where(l => l.Id == line ? level > 0 : town.DwellingLevel(l.Id) > 0).ToList();
        if (built.Count > MaxDwellings)
        {
            return $"A town builds at most {MaxDwellings} of its {lines.Count} dwellings.";
        }

        var emptyTiers = lines.Select(l => l.Tier).Distinct().Where(t => built.All(b => b.Tier != t)).ToList();
        return emptyTiers.Count > MaxDwellings - built.Count
            ? $"That would leave no room for a tier {emptyTiers[0]} dwelling."
            : null;
    }

    /// <summary>Gold and resources for <paramref name="count"/> creatures.</summary>
    public static ResourceSet RecruitCost(CreatureDefinition creature, int count) => creature.Cost * count;

    /// <summary>How many of something costing <paramref name="each"/> the resources pay for, at most <paramref name="limit"/>.</summary>
    public static int MaxAffordable(ResourceSet resources, ResourceSet each, int limit) =>
        Math.Max(0, Enum.GetValues<ResourceType>().Where(t => each[t] > 0).Select(t => resources[t] / each[t]).Append(limit).Min());

    /// <summary>The price difference for upgrading <paramref name="count"/> creatures of a line.</summary>
    public static ResourceSet UpgradeCost(GameState state, CreatureDefinition line, int count) =>
        (state.Creatures.Get(line.UpgradesTo!).Cost - line.Cost) * count;

    /// <summary>What an owned town pays every day: the income of the highest level of every building.</summary>
    public static ResourceSet DailyIncome(GameState state, Town town) =>
        town.Buildings
            .Where(b => state.Buildings.Contains(b.Key))
            .Aggregate(ResourceSet.Empty, (total, b) => total + state.Buildings.Get(b.Key, Math.Min(b.Value, state.Buildings.MaxLevel(b.Key))).Income);

    /// <summary>
    /// The first requirement a built level lacks (every level up to the built one counts), or null when the town is
    /// consistent. Checks the town as it would be with <paramref name="building"/> at <paramref name="level"/>: the editor
    /// asks before pre-building or removing something, the validator for whole maps.
    /// </summary>
    public static string? UnmetRequirement(GameState state, Town town, string? building = null, int level = 0)
    {
        int LevelOf(string id) => id == building ? level : Level(state, town, id);

        var built = state.Buildings.TownBuildings.Select(b => (Building: b, Level: Math.Min(LevelOf(b), state.Buildings.MaxLevel(b))))
            .Concat(Lines(state, town).Select(l => (Building: BuiltInBuildings.Dwelling(l.Tier), Level: town.DwellingLevel(l.Id))));
        foreach (var (id, top) in built)
        {
            for (var l = 1; l <= top; l++)
            {
                var row = state.Buildings.Get(id, l);
                if (row.Requires.FirstOrDefault(r => LevelOf(r.Building) < r.Level) is { } missing)
                {
                    return $"{row.Name} needs {state.Buildings.Get(missing.Building, missing.Level).Name}.";
                }
            }
        }

        return null;
    }

    private static string? CheckLevel(GameState state, Town town, BuildingLevel level)
    {
        if (town.LastBuilt == state.Date)
        {
            return $"{town.Name} has already built something today.";
        }

        if (level.Requires.FirstOrDefault(r => Level(state, town, r.Building) < r.Level) is { } missing)
        {
            return $"{level.Name} needs {state.Buildings.Get(missing.Building, missing.Level).Name} first.";
        }

        return town.Owner is { } owner && !state.GetPlayer(owner).Resources.CanAfford(level.Cost)
            ? $"Not enough resources for {level.Name}."
            : null;
    }
}
