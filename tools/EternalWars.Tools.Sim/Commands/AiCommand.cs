using EternalWars.Core.Domain.Battles;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim ai [--runs N] [--upgraded] [--level L] [--spells] [--siege]</c>: the battle AI's level L against the level before it (ADR-188): every
/// playable town's one-week army (the four builds in turn) against every town's, itself included, from both sides, N seeds a pairing. The newer
/// level commands the row town. With <c>--spells</c> both heroes cast the same few battle spells, with <c>--siege</c> the defender holds a
/// Castle-level town against the attacker's siege engine and a
/// <see cref="SiegeAttackerPercent"/> army (<see cref="FightSetup"/>). Prints the newer level's win rate per row, in the mirror
/// matches and overall; 50 % means the step changes nothing.
/// </summary>
internal sealed class AiCommand : SimCommand
{
    /// <summary>A besieger's army is this much bigger, in percent, so that sieges are not all won by the town (a guess, for measuring only).</summary>
    public const int SiegeAttackerPercent = 175;

    public override string Name => "ai";

    public override IReadOnlyList<OptionSpec> Options => [OptionSpec.Number("--runs", min: 1), OptionSpec.Flag("--upgraded"), OptionSpec.Number("--level"), OptionSpec.Flag("--spells"), OptionSpec.Flag("--siege")];

    public override int Run(CommandLine line, SimContext sim)
    {
        var (runs, upgraded, level) = (line.Number("--runs", 8), line.Has("--upgraded"), (BattleAiLevel)line.Number("--level", (int)BattleAiLevel.Latest));
        if (level <= BattleAiLevel.Greedy || level > BattleAiLevel.Latest)
        {
            throw new FormatException($"--level is {(int)BattleAiLevel.Greedy + 1}–{(int)BattleAiLevel.Latest} (the level measured against the one before it).");
        }

        var setup = new FightSetup(line.Has("--spells"), line.Has("--siege"), line.Has("--siege") ? SiegeAttackerPercent : 100);
        var towns = SimContext.PlayableTowns;
        var previous = level - 1;
        var builds = towns.ToDictionary(t => t, t => sim.WeekArmies(t, upgraded));
        Console.WriteLine($"Win rate of {level} (row town) over {previous} (column town), {runs} battles a side, {(upgraded ? "upgraded" : "base")} one-week armies" +
                          $"{(setup.Spells ? ", casting heroes" : "")}{(setup.Siege ? ", sieges" : "")}");
        Console.WriteLine("".PadRight(16) + string.Concat(towns.Select(t => t[..Math.Min(7, t.Length)].PadLeft(8))) + "   total");
        var (total, mirror) = (0, 0);
        foreach (var row in towns)
        {
            var wins = 0;
            var text = row.PadRight(16);
            foreach (var column in towns)
            {
                var won = 0;
                for (var run = 0; run < runs; run++)
                {
                    var (mine, theirs) = (builds[row][run % 4], builds[column][run / 4 % 4]);
                    won += Fights.FightOnce(sim, mine, theirs, (ulong)run, (level, previous), setup).Ended.Winner == BattleSide.Attacker ? 1 : 0;
                    won += Fights.FightOnce(sim, theirs, mine, (ulong)(run + 1000), (previous, level), setup).Ended.Winner == BattleSide.Defender ? 1 : 0;
                }

                wins += won;
                mirror += row == column ? won : 0;
                text += $"{100 * won / (2 * runs)}%".PadLeft(8);
            }

            total += wins;
            Console.WriteLine(text + $"{100 * wins / (2 * runs * towns.Length)}%".PadLeft(8));
        }

        Console.WriteLine($"{level} won {100.0 * total / (2 * runs * towns.Length * towns.Length):0.#} % overall, " +
                          $"{100.0 * mirror / (2 * runs * towns.Length):0.#} % of the mirror matches");
        return 0;
    }
}
