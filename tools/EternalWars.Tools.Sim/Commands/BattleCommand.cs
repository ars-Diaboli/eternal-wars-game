using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Battles;

namespace EternalWars.Tools.Sim.Commands;

/// <summary><c>sim battle &lt;army&gt; &lt;army&gt; [--runs N] [--seed S]</c>: two computer heroes meet N times with different seeds; prints win rate and average losses.</summary>
internal sealed class BattleCommand : SimCommand
{
    public override string Name => "battle";

    public override IReadOnlyList<string> Positionals => ["<attacker army>", "<defender army>"];

    public override IReadOnlyList<OptionSpec> Options => [OptionSpec.Number("--runs", min: 1), OptionSpec.Seed("--seed")];

    public override int Run(CommandLine line, SimContext sim) =>
        Battle(sim, sim.ParseArmy(line.Positionals[0]), sim.ParseArmy(line.Positionals[1]), line.Number("--runs", 100), line.Seed("--seed", 1));

    private static int Battle(SimContext sim, Army attacker, Army defender, int runs, ulong seed)
    {
        var wins = 0;
        var rounds = 0L;
        var losses = new Dictionary<(BattleSide, string), long>();
        for (var run = 0; run < runs; run++)
        {
            var result = Fights.FightOnce(sim, attacker, defender, seed + (ulong)run);
            wins += result.Ended.Winner == BattleSide.Attacker ? 1 : 0;
            rounds += result.Rounds;
            foreach (var casualty in result.Ended.Casualties)
            {
                losses[(casualty.Side, casualty.Creature)] = losses.GetValueOrDefault((casualty.Side, casualty.Creature)) + casualty.Lost;
            }
        }

        Console.WriteLine($"{runs} battles: attacker won {wins} ({100.0 * wins / runs:0.#} %), {(double)rounds / runs:0.#} rounds on average");
        foreach (var ((side, creature), lost) in losses.OrderBy(l => l.Key.Item1).ThenBy(l => l.Key.Item2))
        {
            Console.WriteLine($"  {side,-8} {creature,-28} lost {(double)lost / runs:0.##} per battle");
        }

        return 0;
    }
}
