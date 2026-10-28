using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Turns;

/// <summary>What a week brings (ADR-073, FLOW-2).</summary>
public enum WeekEventKind
{
    None,

    /// <summary>A weekly event: the creature's line grows twice as fast this week.</summary>
    WeekOfCreature,

    /// <summary>A monthly event: the creature's line grows twice as fast this week, and stacks of it appear next to map objects.</summary>
    MonthOfCreature,

    /// <summary>A monthly event: no growth this week, and the creatures waiting in every town are halved.</summary>
    Plague,

    /// <summary>A monthly event: no growth this week.</summary>
    Starvation,
}

/// <summary>The event of one week (<see cref="Game.GameState.WeekEvent"/>); <see cref="Creature"/> names the creature of a week or month of a creature.</summary>
public sealed record WeekEvent(WeekEventKind Kind, string? Creature = null)
{
    public static WeekEvent None { get; } = new(WeekEventKind.None);

    public bool IsMonthly => Kind is WeekEventKind.MonthOfCreature or WeekEventKind.Plague or WeekEventKind.Starvation;

    /// <summary>Plague and starvation: no dwelling grows this week.</summary>
    public bool StopsGrowth => Kind is WeekEventKind.Plague or WeekEventKind.Starvation;

    /// <summary>
    /// What a dwelling of <paramref name="line"/> adds this week instead of <paramref name="normal"/>: nothing under plague and starvation, twice as
    /// much in the week or month of a creature of its line (base or upgrade named, the line counts).
    /// </summary>
    public int Growth(CreatureCatalog creatures, string line, int normal)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        if (StopsGrowth)
        {
            return 0;
        }

        return Creature is { } creature && creatures.BaseOf(creature) == creatures.BaseOf(line) ? normal * 2 : normal;
    }

    /// <summary>"Week of the Pikeman", "Plague", "An ordinary week" for the announcement and the Astrologer's Tower.</summary>
    public string Describe(CreatureCatalog creatures)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        var name = Creature is { } id && creatures.TryGet(id, out var creature) ? creature.Name : Creature;
        return Kind switch
        {
            WeekEventKind.WeekOfCreature => $"Week of the {name}: its growth doubles.",
            WeekEventKind.MonthOfCreature => $"Month of the {name}: its growth doubles, and its stacks appear across the land.",
            WeekEventKind.Plague => "Plague: no creature grows this week, and those waiting in the towns are halved.",
            WeekEventKind.Starvation => "Starvation: no creature grows this week.",
            _ => "An ordinary week.",
        };
    }
}

/// <summary>
/// The map's (or template's) week and month events (ADR-073, ADR-075): on or off, how many weeks pass before the first one (by default the first
/// week has none), and the factions whose creatures may be picked, for weekly and monthly events apart. Factions are town ids, or
/// <see cref="Neutral"/> for creatures of no town; null = the playable towns.
/// </summary>
public sealed record WeekEventSettings(
    bool Enabled = true,
    int SkipWeeks = WeekEventSettings.DefaultSkipWeeks,
    IReadOnlyList<string>? WeeklyFactions = null,
    IReadOnlyList<string>? MonthlyFactions = null)
{
    public const int DefaultSkipWeeks = 1;

    /// <summary>At most two months without events (ADR-075).</summary>
    public const int MaxSkipWeeks = 8;

    /// <summary>The faction id of creatures that belong to no town.</summary>
    public const string Neutral = "neutral";

    public static WeekEventSettings Default { get; } = new();

    /// <summary>Every faction id a setting may name: the towns with creatures (Conflux and Dragon Utopia too) and <see cref="Neutral"/>.</summary>
    public static IReadOnlyList<string> Factions { get; } =
    [
        .. BuiltInMapObjects.PlayableTowns, Towns.TownIds.Conflux, Towns.TownIds.DragonUtopia, Neutral,
    ];

    public IReadOnlyList<string> Weekly => WeeklyFactions ?? BuiltInMapObjects.PlayableTowns;

    public IReadOnlyList<string> Monthly => MonthlyFactions ?? BuiltInMapObjects.PlayableTowns;

    public bool IsDefault => Enabled && SkipWeeks == DefaultSkipWeeks && WeeklyFactions is null && MonthlyFactions is null;

    /// <summary>What is wrong with the settings, or null: the skip out of 0–8, or an unknown faction.</summary>
    public string? Problem()
    {
        if (SkipWeeks is < 0 or > MaxSkipWeeks)
        {
            return $"Week events may start after 0 to {MaxSkipWeeks} weeks, not {SkipWeeks}.";
        }

        return (WeeklyFactions ?? []).Concat(MonthlyFactions ?? []).FirstOrDefault(f => !Factions.Contains(f)) is { } unknown
            ? $"Week events name an unknown faction '{unknown}'."
            : null;
    }

    // Lists compare by content, so an edited copy equals the original (records compare lists by reference).
    public bool Equals(WeekEventSettings? other) =>
        other is not null && Enabled == other.Enabled && SkipWeeks == other.SkipWeeks
        && Same(WeeklyFactions, other.WeeklyFactions) && Same(MonthlyFactions, other.MonthlyFactions);

    public override int GetHashCode() => HashCode.Combine(Enabled, SkipWeeks, WeeklyFactions?.Count, MonthlyFactions?.Count);

    private static bool Same(IReadOnlyList<string>? a, IReadOnlyList<string>? b) => a is null ? b is null : b is not null && a.SequenceEqual(b);
}

/// <summary>
/// The rolls of the week and month events (ADR-073, FLOW-2; draft chances), from <see cref="Game.GameState.Random"/>. A month's first week rolls the
/// monthly table: 45 % month of a creature, 15 % plague, 15 % starvation, 25 % nothing, and then the weekly roll is made as usual. Every other week
/// rolls the weekly table: 75 % week of a creature, 25 % nothing.
/// </summary>
public static class WeekEventRules
{
    public const int WeekOfCreaturePercent = 75;

    public const int MonthOfCreaturePercent = 45;

    public const int PlaguePercent = 15;

    public const int StarvationPercent = 15;

    /// <summary>One stack of the month's creature per this many objects it could guard (at least one, at most <see cref="MaxSpawnedStacks"/>).</summary>
    public const int ObjectsPerSpawnedStack = 10;

    public const int MaxSpawnedStacks = 16;

    /// <summary>
    /// The event of the week that starts on <paramref name="weekStart"/>. Nothing is drawn when events are off or the week is among the skipped
    /// ones, so maps without events keep their random sequence.
    /// </summary>
    public static WeekEvent Roll(WeekEventSettings settings, CreatureCatalog creatures, GameDate weekStart, GameRandom random)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(random);
        if (!settings.Enabled || weekStart.AbsoluteWeek <= settings.SkipWeeks)
        {
            return WeekEvent.None;
        }

        if (weekStart.Week == 1)
        {
            var monthly = random.Next(100);
            if (monthly < MonthOfCreaturePercent)
            {
                return Pick(Candidates(creatures, settings.Monthly), WeekEventKind.MonthOfCreature, random);
            }

            if (monthly < MonthOfCreaturePercent + PlaguePercent)
            {
                return new WeekEvent(WeekEventKind.Plague);
            }

            if (monthly < MonthOfCreaturePercent + PlaguePercent + StarvationPercent)
            {
                return new WeekEvent(WeekEventKind.Starvation);
            }
        }

        return random.Next(100) < WeekOfCreaturePercent
            ? Pick(Candidates(creatures, settings.Weekly), WeekEventKind.WeekOfCreature, random)
            : WeekEvent.None;
    }

    /// <summary>The creatures of the factions that grow (weekly growth above 0), base and upgrades alike, by id.</summary>
    public static IReadOnlyList<CreatureDefinition> Candidates(CreatureCatalog creatures, IReadOnlyList<string> factions)
    {
        ArgumentNullException.ThrowIfNull(creatures);
        return creatures.All.Where(c => c.WeeklyGrowth > 0 && factions.Contains(c.Town ?? WeekEventSettings.Neutral))
            .OrderBy(c => c.Id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>How many stacks a month of a creature brings for <paramref name="sites"/> objects that could take a guard.</summary>
    public static int SpawnedStacks(int sites) => sites == 0 ? 0 : Math.Clamp(sites / ObjectsPerSpawnedStack, 1, MaxSpawnedStacks);

    private static WeekEvent Pick(IReadOnlyList<CreatureDefinition> candidates, WeekEventKind kind, GameRandom random) =>
        candidates.Count == 0 ? WeekEvent.None : new WeekEvent(kind, candidates[random.Next(candidates.Count)].Id);
}
