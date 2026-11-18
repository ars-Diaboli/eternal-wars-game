using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim lines [--runs N] [--upgraded]</c>: every creature line of the playable towns against every other line of its tier: one week of
/// growth each, both sides, N seeds. Prints each line's win rate, weakest first, per tier.
/// </summary>
internal sealed class LinesCommand : SimCommand
{
    public override string Name => "lines";

    public override IReadOnlyList<OptionSpec> Options => [OptionSpec.Number("--runs", min: 1), OptionSpec.Flag("--upgraded")];

    public override int Run(CommandLine line, SimContext sim)
    {
        var (runs, upgraded) = (line.Number("--runs", 4), line.Has("--upgraded"));
        var lines = SimContext.PlayableTowns.SelectMany(sim.Catalog.BaseCreaturesOf).ToList();
        foreach (var tier in lines.Select(c => c.Tier).Distinct().Order())
        {
            var group = lines.Where(c => c.Tier == tier).ToList();
            var wins = group.ToDictionary(c => c.Id, _ => 0);
            foreach (var (a, i) in group.Select((c, i) => (c, i)))
            {
                foreach (var b in group.Skip(i + 1))
                {
                    var (armyA, armyB) = (Week(a), Week(b));
                    for (var run = 0; run < runs; run++)
                    {
                        var first = Fights.FightOnce(sim, armyA, armyB, (ulong)run).Ended.Winner == BattleSide.Attacker;
                        var second = Fights.FightOnce(sim, armyB, armyA, (ulong)(run + 1000)).Ended.Winner == BattleSide.Defender;
                        wins[a.Id] += (first ? 1 : 0) + (second ? 1 : 0);
                        wins[b.Id] += (first ? 0 : 1) + (second ? 0 : 1);
                    }
                }
            }

            Console.WriteLine($"Tier {tier} ({(upgraded ? "upgraded" : "base")}, one week of growth, {runs} battles per side):");
            foreach (var (id, won) in wins.OrderBy(w => w.Value))
            {
                Console.WriteLine($"  {id,-30} {100.0 * won / (2 * runs * (group.Count - 1)),5:0}%");
            }
        }

        return 0;

        Army Week(CreatureDefinition creature) => Army.Of(new CreatureStack(upgraded ? creature.UpgradesTo! : creature.Id, creature.WeeklyGrowth));
    }
}
