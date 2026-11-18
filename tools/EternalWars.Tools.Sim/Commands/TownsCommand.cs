using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim towns [--runs N] [--upgraded]</c>: every playable town against every other with a one-week army of seven dwellings (ADR-030): both
/// lines of tiers 1–2, one line each of tiers 3 and 4, and tier 5, one week of growth each. The four possible builds rotate between runs and
/// each pairing fights from both sides. Prints army power and win rates.
/// </summary>
internal sealed class TownsCommand : SimCommand
{
    public override string Name => "towns";

    public override IReadOnlyList<OptionSpec> Options => [OptionSpec.Number("--runs", min: 1), OptionSpec.Flag("--upgraded")];

    public override int Run(CommandLine line, SimContext sim)
    {
        var (runs, upgraded) = (line.Number("--runs", 32), line.Has("--upgraded"));
        var towns = SimContext.PlayableTowns;
        var builds = towns.ToDictionary(t => t, t => sim.WeekArmies(t, upgraded));
        foreach (var (town, armies) in builds)
        {
            var powers = armies.Select(Power).ToList();
            Console.WriteLine($"{town,-15} power {powers.Min(),6}–{powers.Max(),-6} " +
                              string.Join("  ", sim.Catalog.BaseCreaturesOf(town).Select(c => $"T{c.Tier} {c.Name} {c.WeeklyGrowth}×{sim.Catalog.Get(upgraded ? c.UpgradesTo! : c.Id).FightValue}")));
        }

        Console.WriteLine($"Win rate of the row town over the column town ({runs} battles per side, {(upgraded ? "upgraded" : "base")} one-week armies)");
        Console.WriteLine("".PadRight(16) + string.Concat(towns.Select(t => t[..Math.Min(7, t.Length)].PadLeft(8))) + "   total");
        foreach (var row in towns)
        {
            var wins = 0;
            var text = row.PadRight(16);
            foreach (var column in towns)
            {
                if (row == column)
                {
                    text += "-".PadLeft(8);
                    continue;
                }

                var won = 0;
                for (var run = 0; run < runs; run++)
                {
                    var (mine, theirs) = (builds[row][run % 4], builds[column][run / 4 % 4]);
                    won += Fights.FightOnce(sim, mine, theirs, (ulong)run).Ended.Winner == BattleSide.Attacker ? 1 : 0;
                    won += Fights.FightOnce(sim, theirs, mine, (ulong)(run + 1000)).Ended.Winner == BattleSide.Defender ? 1 : 0;
                }

                wins += won;
                text += $"{100 * won / (2 * runs)}%".PadLeft(8);
            }

            Console.WriteLine(text + $"{100 * wins / (2 * runs * (towns.Length - 1))}%".PadLeft(8));
        }

        return 0;

        long Power(Army army) => army.Stacks.Sum(s => (long)s.Stack.Count * sim.Catalog.Get(s.Stack.Creature).FightValue);
    }
}
