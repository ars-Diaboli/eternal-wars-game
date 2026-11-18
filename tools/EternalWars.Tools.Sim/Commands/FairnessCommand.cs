using EternalWars.Core.Data.Templates;
using EternalWars.Core.Generation;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim fairness [--template T] [--seeds N] [--seed S] [--size N] [--verbose]</c>: the whole-map check (ADR-071, ADR-117) over N seeds per
/// template and size: how many single attempts pass, how many seeds <see cref="MapGenerator.GenerateBest"/> gets a map without problems for,
/// and which check fails most.
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
}
