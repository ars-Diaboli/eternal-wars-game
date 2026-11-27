using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Generation;

/// <summary>
/// One map of a <see cref="TemplateBatch"/> and its checks, as <c>sim maps</c> makes them (ADR-274): the generator's problems (when every
/// attempt had some), whether the seed gives the same map twice, the rival start towns a player cannot walk to, and the map validator's
/// errors; with the fairness measures of every start (<see cref="StartMeasure"/>) and the map, to preview or open.
/// </summary>
public sealed record BatchMap(
    ulong Seed,
    EditorMap Map,
    bool SameTwice,
    IReadOnlyList<string> Unreachable,
    IReadOnlyList<string> MapErrors)
{
    public int Attempt => Map.Generation.Attempt;

    public IReadOnlyList<string> Problems => Map.Problems;

    public IReadOnlyList<StartMeasure> Starts => Map.Generation.Map.Fairness?.Starts ?? [];

    /// <summary>Every check passed.</summary>
    public bool Passed => Problems.Count == 0 && SameTwice && Unreachable.Count == 0 && MapErrors.Count == 0;

    /// <summary>What failed, one line each; empty when the map passed.</summary>
    public IReadOnlyList<string> Failures =>
    [
        .. Problems.Select(p => $"generator: {p}"),
        .. SameTwice ? [] : new[] { "the same seed generated two different maps" },
        .. Unreachable,
        .. MapErrors.Select(e => $"map validator: {e}"),
    ];

    /// <summary>
    /// How far apart the starts are on one measure: the largest less the smallest, in percent of their mean (0 with fewer than two starts or a
    /// mean of 0).
    /// </summary>
    public double Imbalance(FairnessMeasure measure)
    {
        var values = Starts.Select(s => (double)TemplateBatch.Value(s, measure)).ToList();
        if (values.Count < 2 || values.Average() is var mean && mean <= 0)
        {
            return 0;
        }

        return (values.Max() - values.Min()) * 100 / mean;
    }
}

/// <summary>The fairness measures of a start (<see cref="StartMeasure"/>) the batch compares.</summary>
public enum FairnessMeasure
{
    MineDistance,
    TreasureValue,
    GuardPower,
}

/// <summary>A number of the batch over its maps: the smallest, the mean and the largest.</summary>
public readonly record struct Spread(double Min, double Mean, double Max)
{
    public static Spread Of(IEnumerable<double> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? default : new Spread(list.Min(), list.Average(), list.Max());
    }
}

/// <summary>A batch's numbers: how many maps passed, and the spread over the maps of the attempts and of each measure's imbalance.</summary>
public sealed record BatchSummary(int Maps, int Passed, Spread Attempts, IReadOnlyDictionary<FairnessMeasure, Spread> Imbalance);

/// <summary>
/// "Generate many" in the template editor (ADR-274): the template generated at one size for N seeds (<see cref="DefaultCount"/>, from a first
/// seed up), every slot a player and towns drawn as the preview generates them (<see cref="Generate"/>), each map checked like <c>sim maps</c>
/// checks one (<see cref="Check"/>, without the AI's play). Off the main thread, at most a given number of maps at once, cancellable; the maps
/// come back in seed order and each one is the map a single run of its seed makes.
/// </summary>
public static class TemplateBatch
{
    public const int DefaultCount = 20;

    /// <summary>The most maps a batch runs, so a typo does not start a thousand.</summary>
    public const int MaxCount = 200;

    /// <summary>How many maps run at once by default: half the processors, so the editor stays responsive.</summary>
    public static int DefaultParallel => Math.Max(1, Environment.ProcessorCount / 2);

    /// <summary>
    /// The map the template editor generates (its preview and its batch): every start slot a player, towns drawn from the seed. Throws
    /// <see cref="ArgumentException"/> for a template with errors or a size it does not have (<see cref="EditorMapGenerator.Generate"/>).
    /// </summary>
    public static EditorMap Generate(MapTemplate template, int size, ulong seed, ContentCatalogs? content = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        return EditorMapGenerator.Generate(template, size, seed, template.MaxPlayers, new Dictionary<int, string>(), content);
    }

    /// <summary>One seed's map and its checks: generated twice to see that it comes out the same.</summary>
    public static BatchMap Check(MapTemplate template, int size, ulong seed, ContentCatalogs? content = null)
    {
        var map = Generate(template, size, seed, content);
        var again = Generate(template, size, seed, content);
        var state = map.Scenario;
        var unreachable = state.Rules.Tournament
            ? [] // a tournament's players never reach each other (ADR-366); MapFairness checks they stay apart
            : MapConnectivity.UnreachableRivalTowns(state)
            .Select(u => $"{u.Player.Name} cannot reach the town of {state.GetPlayer(u.Town.Owner!.Value).Name} at {u.Town.VisitPosition}")
            .ToList();
        var errors = map.Issues.Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message).ToList();
        return new BatchMap(seed, map, GameStateDigest.Compute(state) == GameStateDigest.Compute(again.Scenario), unreachable, errors);
    }

    /// <summary>The seeds of a batch: <paramref name="count"/> from <paramref name="first"/> up (wrapping past the largest).</summary>
    public static IReadOnlyList<ulong> Seeds(ulong first, int count) =>
        [.. Enumerable.Range(0, count).Select(i => unchecked(first + (ulong)i))];

    /// <summary>
    /// Checks the template at <paramref name="size"/> for <paramref name="count"/> seeds from <paramref name="first"/>, at most
    /// <paramref name="parallel"/> at once (<see cref="DefaultParallel"/> when null), off the calling thread. <paramref name="done"/> hears of
    /// each map as it is checked, on a worker thread, in any order. Cancelling stops starting maps and throws
    /// <see cref="OperationCanceledException"/>. Refuses a template with errors or a size it does not have up front (<see cref="ArgumentException"/>).
    /// </summary>
    public static async Task<IReadOnlyList<BatchMap>> RunAsync(
        MapTemplate template,
        int size,
        ulong first,
        int count = DefaultCount,
        ContentCatalogs? content = null,
        int? parallel = null,
        Action<BatchMap>? done = null,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, MaxCount);
        var workers = parallel ?? DefaultParallel;
        ArgumentOutOfRangeException.ThrowIfLessThan(workers, 1, nameof(parallel));
        content ??= ContentCatalogs.Default;
        if (TemplateValidator.Validate(template, content).FirstOrDefault(i => i.Severity == IssueSeverity.Error) is { } error)
        {
            throw new ArgumentException($"The template has errors: {error.Message}", nameof(template));
        }

        if (!template.Sizes.Contains(size))
        {
            throw new ArgumentOutOfRangeException(nameof(size), size, $"{template.Name} supports the sizes {string.Join(", ", template.Sizes)}.");
        }

        var seeds = Seeds(first, count);
        var maps = new BatchMap[count];
        var options = new ParallelOptions { MaxDegreeOfParallelism = workers, CancellationToken = cancel };
        await Parallel.ForEachAsync(Enumerable.Range(0, count), options, (i, token) =>
        {
            token.ThrowIfCancellationRequested();
            maps[i] = Check(template, size, seeds[i], content);
            done?.Invoke(maps[i]);
            return ValueTask.CompletedTask;
        }).ConfigureAwait(false);
        return maps;
    }

    /// <summary>A start's number on a measure.</summary>
    public static int Value(StartMeasure start, FairnessMeasure measure)
    {
        ArgumentNullException.ThrowIfNull(start);
        return measure switch
        {
            FairnessMeasure.MineDistance => start.MineDistance,
            FairnessMeasure.TreasureValue => start.TreasureValue,
            FairnessMeasure.GuardPower => start.GuardPower,
            _ => throw new ArgumentOutOfRangeException(nameof(measure), measure, null),
        };
    }

    /// <summary>The batch's numbers over the maps checked so far.</summary>
    public static BatchSummary Summarize(IReadOnlyCollection<BatchMap> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        return new BatchSummary(
            maps.Count,
            maps.Count(m => m.Passed),
            Spread.Of(maps.Select(m => (double)m.Attempt)),
            Enum.GetValues<FairnessMeasure>().ToDictionary(m => m, m => Spread.Of(maps.Select(b => b.Imbalance(m)))));
    }
}
