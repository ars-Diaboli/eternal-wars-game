using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Generation;

public sealed record TemplateIssue(IssueSeverity Severity, string Message);

/// <summary>
/// Checks a <see cref="MapTemplate"/> before anything is generated from it: errors make it unusable (a start zone without an
/// owner, a zone nobody can reach), warnings point at things that will probably play badly (start zones that differ).
/// </summary>
public static class TemplateValidator
{
    /// <summary>The largest zone count a template may have (the layout needs room for every zone on the smallest map).</summary>
    public const int MaxZones = 64;

    /// <summary>The most passages (or gate pairs) one connection may make.</summary>
    public const int MaxPassages = 8;

    /// <summary>At least this many tiles per unit of zone weight per level on the smallest allowed size, before a warning.</summary>
    private const int MinTilesPerZone = 200;

    public static IReadOnlyList<TemplateIssue> Validate(MapTemplate template, TerrainCatalog? terrains = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        terrains ??= TerrainCatalog.Default;
        var issues = new List<TemplateIssue>();
        void Error(string message) => issues.Add(new(IssueSeverity.Error, message));
        void Warn(string message) => issues.Add(new(IssueSeverity.Warning, message));

        if (string.IsNullOrWhiteSpace(template.Name))
        {
            Error("The template needs a name.");
        }

        Players(template, Error);
        Sizes(template, Error, Warn);

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var zone in template.Zones)
        {
            if (string.IsNullOrWhiteSpace(zone.Id))
            {
                Error("A zone needs an id.");
            }
            else if (!ids.Add(zone.Id))
            {
                Error($"Zone id '{zone.Id}' is used twice.");
            }

            ValidateZone(template, zone, terrains, Error, Warn);
        }

        if (template.Zones.Count == 0)
        {
            Error("The template has no zones.");
        }

        if (template.Zones.Count > MaxZones)
        {
            Error($"At most {MaxZones} zones.");
        }

        for (var level = 0; level < template.Levels && template.Zones.Count > 0; level++)
        {
            if (!template.Zones.Any(z => z.Level == level))
            {
                Error($"Level {level} has no zone: the map would be empty there.");
            }
        }

        StartZones(template, Error);
        Connections(template, Error, Warn);
        Fairness(template, Warn);
        return issues;
    }

    public static bool IsValid(MapTemplate template, TerrainCatalog? terrains = null) =>
        Validate(template, terrains).All(i => i.Severity != IssueSeverity.Error);

    private static void Players(MapTemplate template, Action<string> error)
    {
        if (template.MinPlayers < 2 || template.MaxPlayers > Domain.Game.GameState.MaxPlayers || template.MinPlayers > template.MaxPlayers)
        {
            error($"The player count must go from 2 up to {Domain.Game.GameState.MaxPlayers}, with the minimum not above the maximum.");
        }

        if (template.MaxHeroes is < 1 or > ScenarioRules.HeroLimit)
        {
            error($"MaxHeroes must be 1 to {ScenarioRules.HeroLimit}.");
        }

        if (template.MaxHumans is < 1)
        {
            error("MaxHumans must be at least 1 (or left out).");
        }
    }

    private static void Sizes(MapTemplate template, Action<string> error, Action<string> warn)
    {
        if (template.Sizes.Count == 0)
        {
            error("The template needs at least one map size.");
            return;
        }

        foreach (var size in template.Sizes.Where(s => !MapSizes.Standard.Contains(s)))
        {
            error($"Map size {size} is not a standard size ({string.Join(", ", MapSizes.Standard)}).");
        }

        var smallest = template.Sizes.Min();
        var perZone = (long)smallest * smallest * template.Levels / Math.Max(1, template.Zones.Count);
        if (template.Zones.Count > 0 && perZone < MinTilesPerZone)
        {
            warn($"On a {smallest}×{smallest} map a zone averages {perZone} tiles: too small for a town, mines and treasure. Drop the small sizes or some zones.");
        }
    }

    private static void ValidateZone(MapTemplate template, TemplateZone zone, TerrainCatalog terrains, Action<string> error, Action<string> warn)
    {
        var name = $"Zone '{zone.Id}'";
        var isStart = zone.Type is ZoneType.PlayerStart or ZoneType.AiStart;
        if (zone.Size < 1)
        {
            error($"{name} needs a size of at least 1.");
        }

        if (zone.Level < 0 || zone.Level >= template.Levels)
        {
            error($"{name} is on level {zone.Level}, but the map has {template.Levels} level(s).");
        }

        if (isStart && (zone.Owner is not { } owner || owner < 0 || owner >= template.MaxPlayers))
        {
            error($"{name} is a start zone and needs an owner slot from 0 to {template.MaxPlayers - 1}.");
        }

        if (!isStart && zone.Owner is not null)
        {
            error($"{name} is not a start zone, so it has no owner.");
        }

        if (zone.Team is { } team && (!isStart || team < 0))
        {
            error($"{name} has team {team}: only start zones have a team, numbered from 0.");
        }

        Range(zone.PlayerTowns, $"{name} player towns", error);
        Range(zone.NeutralTowns, $"{name} neutral towns", error);
        if (isStart && zone.PlayerTowns.Min < 1)
        {
            error($"{name} is a start zone and needs at least one player town.");
        }

        if (!isStart && zone.PlayerTowns.Max > 0)
        {
            error($"{name} is not a start zone, so it cannot have player towns.");
        }

        foreach (var town in zone.TownTypes ?? [])
        {
            if (!BuiltInMapObjects.PlayableTowns.Contains(town))
            {
                error($"{name} allows the unknown town type '{town}'.");
            }
        }

        foreach (var (resource, count) in zone.Mines)
        {
            if (count < 0)
            {
                error($"{name} wants a negative number of {resource} mines.");
            }
        }

        if (zone.ExtraPiles < 0)
        {
            error($"{name} has a negative number of extra piles.");
        }

        foreach (var band in zone.Treasure)
        {
            if (band.MinValue < 1 || band.MaxValue < band.MinValue || band.Count < 1)
            {
                error($"{name} has a treasure band {band.MinValue}–{band.MaxValue} ×{band.Count}: values start at 1, the maximum is not below the minimum, and there is at least one.");
            }
        }

        foreach (var terrain in zone.Terrains ?? [])
        {
            if (!Enum.IsDefined(terrain) || !terrains.IsPassable(terrain))
            {
                error($"{name} lists {terrain}, which cannot be walked on.");
            }
        }

        StartBuildings(zone, name, isStart, error);

        if (zone.Type == ZoneType.Junction && (zone.PlayerTowns.Max > 0 || zone.Treasure.Count > 0))
        {
            warn($"{name} is a junction but holds towns or treasure.");
        }
    }

    /// <summary>Start-zone buildings: known non-dwelling buildings within their levels; dwellings by tier, as many as the 7-of-9 rule allows (ADR-030).</summary>
    private static void StartBuildings(TemplateZone zone, string name, bool isStart, Action<string> error)
    {
        if (!isStart && (zone.StartBuildings is { Count: > 0 } || zone.StartDwellings is { Count: > 0 }))
        {
            error($"{name} is not a start zone, so it has no start buildings.");
        }

        var buildings = BuildingCatalog.Default;
        foreach (var (building, level) in zone.StartBuildings ?? new Dictionary<string, int>())
        {
            if (building.StartsWith("dwelling-", StringComparison.Ordinal))
            {
                error($"{name} lists '{building}' as a start building; dwellings go in the start dwellings (by tier).");
            }
            else if (!buildings.Contains(building) || level < 1 || level > buildings.MaxLevel(building))
            {
                error($"{name} starts with '{building}' at level {level}, which does not exist.");
            }
        }

        var tiers = zone.StartDwellings ?? [];
        if (tiers.Any(t => t is < 1 or > 5))
        {
            error($"{name} starts with a dwelling of a tier outside 1–5.");
        }

        var withTier1 = tiers.Contains(1) ? tiers : [.. tiers, 1]; // the tier-1 dwelling picked at setup
        if (withTier1.GroupBy(t => t).Any(g => g.Count() > (g.Key == 5 ? 1 : 2))
            || 5 - withTier1.Where(t => t is >= 1 and <= 5).Distinct().Count() > TownRules.MaxDwellings - withTier1.Count)
        {
            error($"{name} starts with more dwellings than a town can build (two per tier 1–4, one of tier 5, seven in all with every tier left reachable).");
        }
    }

    private static void Range(CountRange range, string what, Action<string> error)
    {
        if (range.Min < 0 || range.Max < range.Min)
        {
            error($"{what}: the minimum is not below 0 and the maximum is not below the minimum.");
        }
    }

    private static void StartZones(MapTemplate template, Action<string> error)
    {
        var starts = template.Zones.Where(z => z.Type is ZoneType.PlayerStart or ZoneType.AiStart).ToList();
        if (starts.Count != template.MaxPlayers)
        {
            error($"The template is for {template.MaxPlayers} players, so it needs {template.MaxPlayers} start zones, not {starts.Count}.");
        }

        foreach (var slot in starts.Where(z => z.Owner is not null).GroupBy(z => z.Owner).Where(g => g.Count() > 1))
        {
            error($"Player slot {slot.Key} owns more than one start zone.");
        }

        var humanSlots = starts.Count(z => z.Type == ZoneType.PlayerStart);
        if (humanSlots < Math.Min(template.MinPlayers, template.MaxHumans ?? int.MaxValue))
        {
            error("Too few player start zones: with the minimum number of players the humans would have no place to start.");
        }
    }

    private static void Connections(MapTemplate template, Action<string> error, Action<string> warn)
    {
        var pairs = new HashSet<(string, string)>();
        foreach (var c in template.Connections)
        {
            var name = $"Connection {c.From} – {c.To}";
            if (template.Zone(c.From) is not { } from || template.Zone(c.To) is not { } to)
            {
                error($"{name} names a zone that does not exist.");
                continue;
            }

            if (c.From == c.To)
            {
                error($"{name} joins a zone to itself.");
                continue;
            }

            if (!pairs.Add(string.CompareOrdinal(c.From, c.To) < 0 ? (c.From, c.To) : (c.To, c.From)))
            {
                error($"{name} is listed twice.");
            }

            if (c.Passages is < 1 or > MaxPassages)
            {
                error($"{name} makes {c.Passages} passages; it makes from 1 to {MaxPassages}.");
            }

            if (c.GuardValue < 0)
            {
                error($"{name} has a negative guard value.");
            }

            if (c.Kind == ConnectionKind.Wide && c.GuardValue > 0)
            {
                warn($"{name} is wide (unguarded): its guard value is ignored.");
            }

            if (c.Kind != ConnectionKind.Gate && from.Level != to.Level)
            {
                error($"{name} joins two levels, which only a gate can do.");
            }
        }

        if (template.Zones.Count > 1)
        {
            foreach (var zone in template.Zones.Where(z => !template.Connections.Any(c => c.From == z.Id || c.To == z.Id)))
            {
                error($"Zone '{zone.Id}' has no connection.");
            }

            var unreachable = Unreachable(template);
            if (unreachable.Count > 0)
            {
                error($"The zone graph is not connected: {string.Join(", ", unreachable)} cannot be reached from '{template.Zones[0].Id}'.");
            }
        }
    }

    /// <summary>Zones not reachable from the first zone.</summary>
    private static List<string> Unreachable(MapTemplate template)
    {
        var seen = new HashSet<string> { template.Zones[0].Id };
        var queue = new Queue<string>([template.Zones[0].Id]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var next in template.Connections.Where(c => c.From == current || c.To == current).Select(c => c.From == current ? c.To : c.From))
            {
                if (template.Zone(next) is not null && seen.Add(next))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return template.Zones.Where(z => !seen.Contains(z.Id)).Select(z => z.Id).ToList();
    }

    /// <summary>Warnings about start zones that are not alike: a competitive map should treat every player the same.</summary>
    private static void Fairness(MapTemplate template, Action<string> warn)
    {
        var starts = template.Zones.Where(z => z.Type is ZoneType.PlayerStart or ZoneType.AiStart).ToList();
        if (starts.Count < 2)
        {
            return;
        }

        var first = starts[0];
        foreach (var other in starts.Skip(1))
        {
            if (other.Size != first.Size)
            {
                warn($"Start zones '{first.Id}' and '{other.Id}' differ in size.");
            }

            if (!SameMines(first, other) || first.ExtraPiles != other.ExtraPiles || TreasureValue(first) != TreasureValue(other)
                || first.PlayerTowns != other.PlayerTowns || first.Monsters != other.Monsters)
            {
                warn($"Start zones '{first.Id}' and '{other.Id}' differ in towns, mines, treasure or monsters.");
            }
        }

        var distances = starts.Select(z => (Zone: z, Others: Distances(template, z.Id, starts))).ToList();
        if (distances.Select(d => d.Others.Count == 0 ? 0 : d.Others.Min()).Distinct().Count() > 1)
        {
            warn("Start zones are not the same number of connections away from their nearest rival.");
        }
    }

    private static bool SameMines(TemplateZone a, TemplateZone b) =>
        a.Mines.Where(m => m.Value > 0).OrderBy(m => m.Key).SequenceEqual(b.Mines.Where(m => m.Value > 0).OrderBy(m => m.Key));

    private static long TreasureValue(TemplateZone zone) => zone.Treasure.Sum(t => (long)(t.MinValue + t.MaxValue) * t.Count);

    /// <summary>Connection counts from a start zone to each other start zone.</summary>
    private static List<int> Distances(MapTemplate template, string from, List<TemplateZone> starts)
    {
        var distance = new Dictionary<string, int> { [from] = 0 };
        var queue = new Queue<string>([from]);
        while (queue.TryDequeue(out var current))
        {
            foreach (var next in template.Connections.Where(c => c.From == current || c.To == current).Select(c => c.From == current ? c.To : c.From))
            {
                if (distance.TryAdd(next, distance[current] + 1))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return starts.Where(s => s.Id != from && distance.ContainsKey(s.Id)).Select(s => distance[s.Id]).ToList();
    }
}
