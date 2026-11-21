using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Analysis;
using EternalWars.Core.Generation;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim fairness [--template T] [--seeds N] [--seed S] [--size N] [--verbose]</c>: the whole-map check (ADR-071, ADR-117) over N seeds per
/// template and size: how many single attempts pass, how many seeds <see cref="MapGenerator.GenerateBest"/> gets a map without problems for,
/// and which check fails most. With --verbose, each seed's map is also measured by the map analysis (ADR-263), every slot taken.
/// </summary>
internal sealed class FairnessCommand : SimCommand
{
    public override string Name => "fairness";

    public override IReadOnlyList<OptionSpec> Options =>
        [OptionSpec.Text("--template"), OptionSpec.Number("--seeds", min: 1), OptionSpec.Seed("--seed"), OptionSpec.Flag("--verbose"), OptionSpec.Number("--size", min: 1)];

    public override int Run(CommandLine line, SimContext sim)
    {
        var (only, seeds, first, verbose) = (line.Text("--template"), line.Number("--seeds", 20), line.Seed("--seed", 1), line.Has("--verbose"));
        int? onlySize = line.Number("--size", 0) is > 0 and var size ? size : null;
        var templates = only is null ? BuiltInTemplates.All : [SimContext.LoadTemplate(only)];
        var failedSeeds = 0;
        foreach (var template in templates)
        {
            foreach (var templateSize in template.Sizes.Where(s => onlySize is null || s == onlySize))
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var firstTry = 0;
                var best = 0;
                var attempts = 0;
                var reasons = new Dictionary<string, int>();
                for (var i = 0; i < seeds; i++)
                {
                    var seed = first + (ulong)i;
                    var single = MapGenerator.Generate(template, templateSize, seed, content: sim.Content);
                    firstTry += single.Problems.Count == 0 ? 1 : 0;
                    foreach (var problem in single.Problems)
                    {
                        var key = problem.StartsWith("Unfair", StringComparison.Ordinal) ? "unfair: " + string.Concat(problem[(problem.IndexOf(':') + 2)..].TakeWhile(c => !char.IsDigit(c))).Trim()
                            : problem.Contains("walked to", StringComparison.Ordinal) || problem.Contains("cannot walk", StringComparison.Ordinal) ? "unreachable" : "other: " + problem;
                        reasons[key] = reasons.GetValueOrDefault(key) + 1;
                    }

                    if (verbose)
                    {
                        Console.WriteLine($"  seed {seed}: {string.Join(" | ", single.Fairness!.Starts.Select(s => $"{s.Zone} mines {s.MineDistance} treasure {s.TreasureValue} guard {s.GuardPower}"))}");
                        single.Problems.ToList().ForEach(p => Console.WriteLine($"    {p}"));
                        clock.Stop(); // the analysis is no part of a seed's generation time
                        PrintAnalysis(sim, template, templateSize, seed);
                        clock.Start();
                    }

                    var result = single.Problems.Count == 0 ? new GenerationResult(single, 0, seed) : MapGenerator.GenerateBest(template, templateSize, seed, content: sim.Content);
                    best += result.Problems.Count == 0 ? 1 : 0;
                    attempts += result.Attempt + 1;
                    failedSeeds += result.Problems.Count == 0 ? 0 : 1;
                }

                Console.WriteLine($"{template.Name} {templateSize}×{templateSize}: first try {firstTry}/{seeds}, with retries {best}/{seeds}, {(double)attempts / seeds:0.0} attempts a seed, {clock.ElapsedMilliseconds / seeds} ms a seed");
                foreach (var (reason, count) in reasons.OrderByDescending(r => r.Value).Take(6))
                {
                    Console.WriteLine($"    {count,4} × {reason}");
                }
            }
        }

        return failedSeeds == 0 ? 0 : 1;
    }

    /// <summary>
    /// The map analysis's numbers (EDIT-9, ADR-263) for the game of <paramref name="seed"/> with every slot taken (built as the new-game
    /// screen builds it, so it may be another attempt than the single map measured above): days to the nearest town, mines and provider; treasure and guards within 3 and 7 days; its warnings.
    /// </summary>
    private static void PrintAnalysis(SimContext sim, MapTemplate template, int size, ulong seed)
    {
        var state = SimGames.GeneratedGame(sim, template, size, template.MaxPlayers, seed).State;
        var fairness = new MapAnalysis(state).Fairness();
        foreach (var start in fairness.Starts)
        {
            static string Days(int? days) => days is { } d ? $"{d}d" : "-";
            Console.WriteLine($"    analysis player {start.Player.Value + 1}: town {Days(start.DaysToTown)}, mines "
                + $"{string.Join(" ", start.DaysToMine.Select(m => $"{m.Key.ToString().ToLowerInvariant()} {Days(m.Value)}"))}, provider {Days(start.DaysToProvider)}, "
                + $"treasure {start.TreasureWithin3}/{start.TreasureWithin7}, guards {start.GuardsWithin3}/{start.GuardsWithin7} (within {MapAnalysis.NearDays}/{MapAnalysis.FarDays} days)");
        }

        fairness.Warnings.ToList().ForEach(w => Console.WriteLine($"    analysis: {w}"));
    }
}
