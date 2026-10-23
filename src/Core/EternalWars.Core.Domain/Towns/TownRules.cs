using EternalWars.Core.Domain.Common;
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

    /// <summary>
    /// Extra weekly growth of a town's dwellings from its fort (ADR-066, HoMM3): +50 % with the Citadel (fort 2), +100 %
    /// with the Castle (fort 3), none below. Map dwellings (<see cref="Objects.CreatureDwelling"/>) never get it.
    /// </summary>
    public static int GrowthBonusPercent(Town town) => town.Level(BuiltInBuildings.Fort) switch
    {
        >= 3 => 100,
        2 => 50,
        _ => 0,
    };

    /// <summary>
    /// How many creatures of <paramref name="line"/> the town's dwelling adds every Monday and opens with: the creature's
    /// weekly growth plus the fort bonus (<see cref="GrowthBonusPercent"/>), the bonus rounded down (growth 1 stays 1 with
    /// a Citadel). Whether the dwelling is built is not checked.
    /// </summary>
    public static int WeeklyGrowth(Town town, CreatureDefinition line) =>
        line.WeeklyGrowth + (line.WeeklyGrowth * GrowthBonusPercent(town) / 100);

    /// <summary>
    /// The first requirement the town lacks for a line's dwelling at <paramref name="level"/> (every level up to it counts),
    /// or null: the editor asks before pre-building a dwelling (every dwelling needs the fort, ADR-066).
    /// </summary>
    public static string? UnmetDwellingRequirement(GameState state, Town town, CreatureDefinition line, int level)
    {
        var building = BuiltInBuildings.Dwelling(line.Tier);
        for (var l = 1; l <= Math.Min(level, Buildings(state, town).MaxLevel(building)); l++)
        {
            var row = Buildings(state, town).Get(building, l);
            if (row.Requires.FirstOrDefault(r => Level(state, town, r.Building) < r.Level) is { } missing)
            {
                return $"{row.Name} needs {Buildings(state, town).Get(missing.Building, missing.Level).Name}.";
            }
        }

        return null;
    }

    /// <summary>The buildings of the town's type: the shared list with its own rows (ADR-070, TOWN-3).</summary>
    public static BuildingCatalog Buildings(GameState state, Town town) => state.Buildings.For(town.TownType);

    /// <summary>
    /// Why the town may not have this level because it is a second Capitol of its owner (ADR-070, TOWN-3), or null. A town
    /// without an owner never refuses.
    /// </summary>
    public static string? CheckCapitol(GameState state, Town town, BuildingLevel level) =>
        level.Building == BuiltInBuildings.TownHall && level.Level >= BuiltInBuildings.CapitolLevel && town.Owner is { } owner
        && CapitolOf(state, owner, except: town) is { } other
            ? $"{other.Name} is already your Capitol; a player has only one."
            : null;

    /// <summary>The owner's town with a Capitol (town hall 4), other than <paramref name="except"/>, or null.</summary>
    public static Town? CapitolOf(GameState state, PlayerId owner, Town? except = null) =>
        state.ObjectsOf<Town>().Where(t => t.Owner == owner && t != except && t.Level(BuiltInBuildings.TownHall) >= BuiltInBuildings.CapitolLevel)
            .OrderBy(t => t.Id.Value).FirstOrDefault();

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
        return level <= Buildings(state, town).MaxLevel(building) ? Buildings(state, town).Get(building, level) : null;
    }

    /// <summary>The next level of a line's dwelling (build, then upgrade), or null when there is none.</summary>
    public static BuildingLevel? NextDwellingLevel(GameState state, Town town, CreatureDefinition line)
    {
        var level = town.DwellingLevel(line.Id) + 1;
        var building = BuiltInBuildings.Dwelling(line.Tier);
        return level > Buildings(state, town).MaxLevel(building) || (level >= 2 && line.UpgradesTo is null)
            ? null
            : Buildings(state, town).Get(building, level);
    }

    /// <summary>Whether the town may build the next level of <paramref name="building"/> today.</summary>
    public static string? CheckBuild(GameState state, Town town, string building)
    {
        if (BuiltInBuildings.DwellingTier(building) is not null || !Buildings(state, town).Contains(building))
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

    /// <summary>
    /// Whether the visiting hero may buy a war machine here (ADR-056): the town has a blacksmith, an own hero is on the
    /// entrance, it does not own that machine yet, and the owner can pay.
    /// </summary>
    public static string? CheckBuyWarMachine(GameState state, Town town, Battles.WarMachineSlot slot)
    {
        if (town.Level(BuiltInBuildings.Blacksmith) < 1)
        {
            return $"{town.Name} has no blacksmith.";
        }

        if (VisitingHero(state, town) is not { } hero)
        {
            return $"No own hero is visiting {town.Name}.";
        }

        if (hero.WarMachines.Contains(slot))
        {
            return $"{hero.Name} already has a {WarMachineName(state, hero, slot)}.";
        }

        return town.Owner is { } owner && !state.GetPlayer(owner).Resources.CanAfford(Battles.WarMachines.Cost(slot))
            ? $"Not enough gold for a {WarMachineName(state, hero, slot)}."
            : null;
    }

    /// <summary>The machine a hero gets for a slot: its own town's offensive machine, wherever it is bought.</summary>
    public static string WarMachineName(GameState state, Hero hero, Battles.WarMachineSlot slot) =>
        Battles.WarMachines.Definition(Battles.WarMachines.KindOf(slot, ProgressionService.TownOf(state, hero))).Name;

    /// <summary>What a building level costs this town: its price less the governor's Stewardship discount (ADR-063).</summary>
    public static ResourceSet BuildCost(GameState state, Town town, BuildingLevel level) =>
        StewardshipRules.DiscountBuilding(state, town, level.Cost);

    /// <summary>What one creature costs to recruit in this town, after the governor's Stewardship discount.</summary>
    public static ResourceSet CreatureCost(GameState state, Town town, CreatureDefinition creature) =>
        StewardshipRules.DiscountCreatures(state, town, creature.Cost);

    /// <summary>Gold and resources for <paramref name="count"/> creatures recruited in this town.</summary>
    public static ResourceSet RecruitCost(GameState state, Town town, CreatureDefinition creature, int count) =>
        CreatureCost(state, town, creature) * count;

    /// <summary>How many of something costing <paramref name="each"/> the resources pay for, at most <paramref name="limit"/>.</summary>
    public static int MaxAffordable(ResourceSet resources, ResourceSet each, int limit) =>
        Math.Max(0, Enum.GetValues<ResourceType>().Where(t => each[t] > 0).Select(t => resources[t] / each[t]).Append(limit).Min());

    /// <summary>The price difference for upgrading <paramref name="count"/> creatures of a line.</summary>
    public static ResourceSet UpgradeCost(GameState state, Town town, CreatureDefinition line, int count) =>
        StewardshipRules.DiscountCreatures(state, town, state.Creatures.Get(line.UpgradesTo!).Cost - line.Cost) * count;

    /// <summary>What an owned town pays every day: the income of the highest level of every building.</summary>
    public static ResourceSet DailyIncome(GameState state, Town town) =>
        town.Buildings
            .Where(b => Buildings(state, town).Contains(b.Key))
            .Aggregate(ResourceSet.Empty, (total, b) => total + Buildings(state, town).Get(b.Key, Math.Min(b.Value, Buildings(state, town).MaxLevel(b.Key))).Income);

    /// <summary>
    /// The first requirement a built level lacks (every level up to the built one counts), or null when the town is
    /// consistent. Checks the town as it would be with <paramref name="building"/> at <paramref name="level"/>: the editor
    /// asks before pre-building or removing something, the validator for whole maps.
    /// </summary>
    public static string? UnmetRequirement(GameState state, Town town, string? building = null, int level = 0)
    {
        int LevelOf(string id) => id == building ? level : Level(state, town, id);

        var built = Buildings(state, town).TownBuildings.Select(b => (Building: b, Level: Math.Min(LevelOf(b), Buildings(state, town).MaxLevel(b))))
            .Concat(Lines(state, town).Select(l => (Building: BuiltInBuildings.Dwelling(l.Tier), Level: town.DwellingLevel(l.Id))));
        foreach (var (id, top) in built)
        {
            for (var l = 1; l <= top; l++)
            {
                var row = Buildings(state, town).Get(id, l);
                if (row.Requires.FirstOrDefault(r => LevelOf(r.Building) < r.Level) is { } missing)
                {
                    return $"{row.Name} needs {Buildings(state, town).Get(missing.Building, missing.Level).Name}.";
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
            return $"{level.Name} needs {Buildings(state, town).Get(missing.Building, missing.Level).Name} first.";
        }

        if (CheckCapitol(state, town, level) is { } capitol)
        {
            return capitol;
        }

        return town.Owner is { } owner && !state.GetPlayer(owner).Resources.CanAfford(BuildCost(state, town, level))
            ? $"Not enough resources for {level.Name}."
            : null;
    }
}
