using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Towns;

/// <summary>Another building a level needs first: <see cref="Building"/> at <see cref="Level"/> or higher.</summary>
public sealed record BuildingRequirement(Common.BuildingId Building, int Level)
{
    public override string ToString() => $"{Building} {Level.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>
/// One level of a town building (ADR-046): what it costs, what it needs and what it pays every day. Level 1 is the
/// building itself, higher levels its upgrades. Dwellings are the buildings <c>dwelling-1</c> … <c>dwelling-5</c>,
/// shared by every creature line of that tier; their level 2 is the dwelling upgrade. <see cref="Town"/> is the town type a
/// row belongs to, or null for a row every town shares (ADR-070, TOWN-3).
/// </summary>
public sealed record BuildingLevel(Common.BuildingId Building, int Level, string Name)
{
    public string? Town { get; init; }

    /// <summary>The Hall's column the building stands in (ADR-296); a building's first level names it.</summary>
    public BuildingKind Kind { get; init; } = BuildingKind.Unique;

    public ResourceSet Cost { get; init; }

    public IReadOnlyList<BuildingRequirement> Requires { get; init; } = [];

    /// <summary>Paid every day to the town's owner while this is the highest level built.</summary>
    public ResourceSet Income { get; init; }

    /// <summary>
    /// A growth building's flat weekly bonus (ADR-071, TOWN-1): every built dwelling of <see cref="BuildingGrowth.Tier"/> grows this many more
    /// creatures a week while this level is the highest built; null for other buildings.
    /// </summary>
    public BuildingGrowth? Growth { get; init; }
}

/// <summary>A growth building's bonus: <see cref="Amount"/> more creatures a week for every dwelling of <see cref="Tier"/> (ADR-071, TOWN-1).</summary>
public sealed record BuildingGrowth(int Tier, int Amount)
{
    public override string ToString() => $"{BuiltInBuildings.Dwelling(Tier)} {Amount.ToString(CultureInfo.InvariantCulture)}";
}

/// <summary>Ids of the buildings code refers to; the levels and numbers live in <c>data/buildings.csv</c>.</summary>
public static class BuiltInBuildings
{
    public const string TownHall = "town-hall";
    public const string Fort = "fort";
    public const string Blacksmith = "blacksmith";
    public const string Marketplace = "marketplace";
    public const string MageGuild = "mage-guild";
    public const string Tavern = "tavern";
    public const string Caravan = "caravan";

    /// <summary>
    /// The Grail building (ADR-130, ADR-140): never built with gold, only by an own hero bringing the Grail into the town
    /// (<see cref="Objects.GrailRules"/>); each town type names its own.
    /// </summary>
    public const string Grail = "grail";

    /// <summary>
    /// Stronghold's and Swamp Bestiary's 3-level building instead of a mage guild (ADR-071, MAG-2; placeholder): a level teaches the warcry of that
    /// level (<see cref="Battles.PrimalFury.LevelOf"/>) to the owner's heroes in the town.
    /// </summary>
    public const string WarLodge = "war-lodge";

    /// <summary>
    /// The spell building of another school (ADR-072, MAG-12): <c>school-inferno</c> adds one Inferno spell per mage guild level. Each town on
    /// the ring has the two of its ring neighbours' schools (rows of its own in <c>buildings.csv</c>).
    /// </summary>
    public static Common.BuildingId SpellSchool(string school) => $"school-{school}";

    /// <summary>The school a <see cref="SpellSchool"/> building adds, or null for other buildings.</summary>
    public static string? SchoolOf(Common.BuildingId building) =>
        building.Value.StartsWith("school-", StringComparison.Ordinal) && building.Value.Length > "school-".Length ? building.Value["school-".Length..] : null;

    /// <summary>The growth building of a dwelling tier (ADR-071, TOWN-1): <c>growth-1</c>, <c>growth-2</c>; each town names its own.</summary>
    public static Common.BuildingId Growth(int tier) => $"growth-{tier.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The town hall level that is the Capitol: one per player (ADR-070, TOWN-3).</summary>
    public const int CapitolLevel = 4;

    /// <summary>Name of the embedded building table in this assembly.</summary>
    public const string ResourceName = "EternalWars.Buildings.csv";

    /// <summary>What a newly placed town has built (user decision): town hall 1, fort 1 and the tavern the fort needs.</summary>
    public static IReadOnlyDictionary<Common.BuildingId, int> DefaultStart { get; } =
        new Dictionary<Common.BuildingId, int> { [TownHall] = 1, [Fort] = 1, [Tavern] = 1 };

    /// <summary>The building whose levels price the dwellings of a tier.</summary>
    public static Common.BuildingId Dwelling(int tier) => $"dwelling-{tier.ToString(CultureInfo.InvariantCulture)}";

    /// <summary>The tier of a <see cref="Dwelling"/> id, or null for other buildings.</summary>
    public static int? DwellingTier(Common.BuildingId building) =>
        building.Value.StartsWith("dwelling-", StringComparison.Ordinal)
        && int.TryParse(building.Value.AsSpan("dwelling-".Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var tier)
            ? tier
            : null;

    /// <summary>Text of the embedded building table (what the build shipped).</summary>
    public static string EmbeddedTable()
    {
        using var stream = typeof(BuiltInBuildings).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"The building table '{ResourceName}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

/// <summary>The Hall's columns (ADR-296): what a building is for, by the <c>kind</c> column of <c>buildings.csv</c>.</summary>
public enum BuildingKind
{
    /// <summary>The town hall, the tavern, the marketplace.</summary>
    Core,

    /// <summary>The fort, the blacksmith, the caravan.</summary>
    Military,

    /// <summary>The mage guild or war lodge, the spell buildings and other magic.</summary>
    Magic,

    /// <summary>The dwellings and the growth buildings.</summary>
    Dwelling,

    /// <summary>The town's own buildings and the Grail building; a row without a kind.</summary>
    Unique,
}

/// <summary>
/// The building table (<c>src/Core/data/buildings.csv</c>), one row per building level, in the
/// <see cref="CsvTable"/> conventions: <c>id, town, level, name, gold, resources, requires, income, growth, kind, notes</c>. Requirements read
/// <c>fort 2; tavern 1</c>; a dwelling requirement (<c>dwelling-3 1</c>) is met by any dwelling of that tier. <c>growth</c> reads
/// <c>dwelling-1 5</c>: every dwelling of the tier grows 5 more a week (ADR-071). An empty <c>town</c> is a row every town shares; a town
/// type's own rows of a building replace the shared ones for that town, or add a building only that town has (ADR-070, TOWN-3). A town's
/// row at level 0 takes a shared building away from that town (Stronghold and Swamp Bestiary have no mage guild, ADR-071).
/// </summary>
public static class BuildingTable
{
    public const string Id = "id";
    public const string Town = "town";
    public const string Level = "level";
    public const string Name = "name";
    public const string Gold = "gold";
    public const string Resources = "resources";
    public const string Requires = "requires";
    public const string Income = "income";
    public const string Growth = "growth";
    public const string Kind = "kind";
    public const string Notes = "notes";

    /// <summary>The columns of the shipped table, in order.</summary>
    public static IReadOnlyList<string> Columns { get; } = [Id, Town, Level, Name, Gold, Resources, Requires, Income, Growth, Kind, Notes];

    private static readonly string[] Required = [Id, Level, Name];

    public static IReadOnlyList<BuildingLevel> Parse(string text) =>
        CsvTable.Parse(text, "Building", Required)
            .Read(row => row.Text(Id) is { Length: > 0 } id ? Read(row, id) : null)
            .OfType<BuildingLevel>()
            .ToList();

    private static BuildingLevel Read(CsvTable.Row row, string id) =>
        new(id, row.Int(Level), row.Text(Name))
        {
            Town = row.Optional(Town),
            Cost = new ResourceSet(Gold: row.Int(Gold, 0)) + CreatureTable.ParseResources(row, Resources),
            Requires = row.List(Requires).Select(r => ParseRequirement(row, r)).ToList(),
            Income = CreatureTable.ParseResources(row, Income),
            Growth = row.Optional(Growth) is { } growth ? ParseGrowth(row, growth) : null,
            Kind = row.Optional(Kind) is { } kind ? ParseKind(row, kind) : BuildingKind.Unique,
        };

    private static BuildingKind ParseKind(CsvTable.Row row, string text) =>
        Enum.TryParse<BuildingKind>(text, ignoreCase: true, out var kind) && Enum.IsDefined(kind) && !int.TryParse(text, out _)
            ? kind
            : throw row.Error(Kind, $"'{text}' is not one of core, military, magic, dwelling, unique");

    private static BuildingRequirement ParseRequirement(CsvTable.Row row, string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var building, var level]
        && int.TryParse(level, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? new BuildingRequirement(building, number)
            : throw row.Error(Requires, $"'{text}' is not '<building> <level>', e.g. 'fort 2'");

    private static BuildingGrowth ParseGrowth(CsvTable.Row row, string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries) is [var building, var amount]
        && BuiltInBuildings.DwellingTier(building) is { } tier and >= CreatureDefinition.MinTier and <= CreatureDefinition.MaxTier
        && int.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) && number > 0
            ? new BuildingGrowth(tier, number)
            : throw row.Error(Growth, $"'{text}' is not '<dwelling> <amount>', e.g. 'dwelling-1 5'");
}

/// <summary>
/// Building levels by building id. Levels of a building run 1, 2, … without gaps; requirements name known levels. The catalog
/// itself is the shared list; <see cref="For"/> gives a town type's own list (ADR-070, TOWN-3), where the town's rows of a
/// building replace the shared ones. Start from <see cref="Default"/> (the embedded table) or parse a designer's copy.
/// </summary>
public sealed class BuildingCatalog
{
    private readonly Dictionary<Common.BuildingId, List<BuildingLevel>> _buildings;
    private readonly Dictionary<string, BuildingCatalog> _towns = new(StringComparer.Ordinal);

    public BuildingCatalog(IEnumerable<BuildingLevel> levels)
    {
        var all = levels.ToList();
        _buildings = Group(all.Where(l => l.Town is null));
        Validate(_buildings, "");
        foreach (var town in all.Select(l => l.Town).OfType<string>().Distinct(StringComparer.Ordinal))
        {
            var removed = all.Where(l => l.Town == town && l.Level == 0).Select(l => l.Building).ToHashSet();
            var own = Group(all.Where(l => l.Town == town && l.Level != 0));
            var merged = new Dictionary<Common.BuildingId, List<BuildingLevel>>();
            foreach (var (id, list) in _buildings.Where(b => !removed.Contains(b.Key)))
            {
                merged[id] = own.TryGetValue(id, out var replaced) ? replaced : list;
            }

            foreach (var (id, list) in own.Where(o => !merged.ContainsKey(o.Key)))
            {
                merged[id] = list;
            }

            Validate(merged, $" ({town})");
            _towns[town] = new BuildingCatalog(merged, town);
        }
    }

    private BuildingCatalog(Dictionary<Common.BuildingId, List<BuildingLevel>> buildings, string town)
    {
        _buildings = buildings;
        Town = town;
    }

    public static BuildingCatalog Default { get; } = new(BuildingTable.Parse(BuiltInBuildings.EmbeddedTable()));

    /// <summary>The town type of a list from <see cref="For"/>; null for the shared list.</summary>
    public string? Town { get; }

    /// <summary>The town types that have rows of their own.</summary>
    public IEnumerable<string> TownTypes => _towns.Keys;

    /// <summary>A town type's buildings: the shared list with that town's own rows; the shared list when it has none.</summary>
    public BuildingCatalog For(string? town) => town is not null && _towns.TryGetValue(town, out var own) ? own : this;

    /// <summary>Every building id in table order, dwellings included.</summary>
    public IEnumerable<Common.BuildingId> Buildings => _buildings.Keys;

    /// <summary>The buildings a town builds by id (everything except the per-tier dwellings).</summary>
    public IEnumerable<Common.BuildingId> TownBuildings => _buildings.Keys.Where(b => BuiltInBuildings.DwellingTier(b) is null);

    public bool Contains(Common.BuildingId building) => _buildings.ContainsKey(building);

    /// <summary>Highest level of a building; 0 for unknown ids.</summary>
    public int MaxLevel(Common.BuildingId building) => _buildings.TryGetValue(building, out var list) ? list.Count : 0;

    /// <summary>The Hall's column of a building (ADR-296): its first level's kind; a dwelling is <see cref="BuildingKind.Dwelling"/>.</summary>
    public BuildingKind KindOf(Common.BuildingId building) =>
        BuiltInBuildings.DwellingTier(building) is not null ? BuildingKind.Dwelling
        : _buildings.TryGetValue(building, out var list) && list.Count > 0 ? list[0].Kind
        : BuildingKind.Unique;

    public BuildingLevel Get(Common.BuildingId building, int level) =>
        _buildings.TryGetValue(building, out var list) && level >= 1 && level <= list.Count
            ? list[level - 1]
            : throw new KeyNotFoundException($"Unknown building level '{building} {level}'.");

    private static Dictionary<Common.BuildingId, List<BuildingLevel>> Group(IEnumerable<BuildingLevel> levels)
    {
        var buildings = new Dictionary<Common.BuildingId, List<BuildingLevel>>();
        foreach (var level in levels)
        {
            if (!buildings.TryGetValue(level.Building, out var list))
            {
                list = [];
                buildings[level.Building] = list;
            }

            list.Add(level);
        }

        foreach (var list in buildings.Values)
        {
            list.Sort((a, b) => a.Level.CompareTo(b.Level));
        }

        return buildings;
    }

    private static void Validate(Dictionary<Common.BuildingId, List<BuildingLevel>> buildings, string where)
    {
        int MaxLevel(Common.BuildingId building) => buildings.TryGetValue(building, out var list) ? list.Count : 0;

        foreach (var (id, list) in buildings)
        {
            if (list.Select(l => l.Level).Where((level, i) => level != i + 1).Any())
            {
                throw new FormatException($"Building '{id}'{where} needs levels 1, 2, … without gaps or repeats.");
            }
        }

        foreach (var level in buildings.Values.SelectMany(l => l))
        {
            foreach (var requirement in level.Requires)
            {
                if (requirement.Level < 1 || requirement.Level > MaxLevel(requirement.Building))
                {
                    throw new FormatException($"Building '{level.Building}'{where} level {level.Level} requires unknown '{requirement}'.");
                }
            }
        }

        for (var tier = CreatureDefinition.MinTier; tier <= CreatureDefinition.MaxTier; tier++)
        {
            if (MaxLevel(BuiltInBuildings.Dwelling(tier)) < 2)
            {
                throw new FormatException($"Building table{where} needs '{BuiltInBuildings.Dwelling(tier)}' levels 1 (dwelling) and 2 (upgrade).");
            }
        }
    }
}
