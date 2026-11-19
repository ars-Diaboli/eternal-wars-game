using EternalWars.Core.Domain.AI;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Tools.Sim.Commands;

namespace EternalWars.Tools.Sim;

/// <summary>
/// Headless simulator (ADR-034): drives the core through commands only, exactly like a front-end or the AI. One class per command
/// (<see cref="SimCommand.All"/>, in <c>Commands/</c>):
/// <code>
/// sim battle haven.pikeman:20,haven.archer:10 inferno.hell-hound:12 [--runs 200] [--seed 1]
/// sim record demo.ewreplay.json [--days 7] [--seed 1]
/// sim replay demo.ewreplay.json
/// sim game [--map towns|demo|ember|file.ewmap.json] [--days 40] [--seed 1] [--runs 1] [--record file] [--difficulty normal] [--fog refog] [--profile]  (the adventure AI plays every slot; --fog refog: shroud with refog instead of permanent reveal; --profile: time per turn, path searches, battles)
/// sim game --template duel|file.ewtemplate.json [--size N] [--players N] [...]  (the same on a map generated per run, random towns)
/// sim maps [--template name] [--maps 3] [--days 14] [--seed 1]  (CI: generates maps of every shipped template and checks they are playable)
/// sim towns [--runs 32] [--upgraded]  (round robin of one-week town armies, for roster balance)
/// sim ai [--runs 8] [--upgraded] [--level L]  (the battle AI's level L against the level before it, ADR-188; one-week town armies)
/// sim creatures [--write table.csv]  (per-line weekly power against the tier median; optionally rewrite the table)
/// sim lines [--runs 4] [--upgraded]  (every creature line against the other lines of its tier, one week each)
/// sim layout duel|file.ewtemplate.json [--size 96] [--seed 1] [--rects] [--terrain] [--seeds N]  (zone layout, or with --terrain the painted terrain with walls, of a random map template as text)
/// sim export towns|demo|ember --out map.ewmap.json  (a built-in map saved as a file, for the editor)
/// sim generate duel|file.ewtemplate.json --out map.ewmap.json [--size N] [--seed 1] [--players N]  (a generated map saved as a file, human players)
/// sim fairness [--template name] [--seeds 20] [--seed 1] [--size N] [--verbose]  (per start: mines, treasure, guards; fairness problems)
/// sim selftest                      (CI: determinism and replay round trip)
/// </code>
/// Any command takes <c>--creatures path/to/creatures.csv</c> to use an edited creature table without rebuilding; game, maps and selftest take
/// <c>--weight Name=value</c> (repeatable) to play the adventure AI with that <see cref="AiWeights"/> weight changed. Options are strict
/// (<see cref="CommandLine"/>): an unknown option, a missing value or a bad value is an error naming it, exit code 2.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var (rest, creatures, weights) = TakeGlobalOptions(args);
            if (rest.Count == 0 || SimCommand.Find(rest[0]) is not { } command)
            {
                return Usage();
            }

            if (weights.Count > 0 && !command.TakesWeights)
            {
                throw new UsageException($"{command.Name}: --weight is for the commands that play the adventure AI ({string.Join(", ", SimCommand.All.Where(c => c.TakesWeights).Select(c => c.Name))}).");
            }

            var line = command.Parse(rest.Skip(1).ToList());
            var aiWeights = AiWeights.Default;
            foreach (var weight in weights)
            {
                try
                {
                    aiWeights = aiWeights.With(weight);
                }
                catch (FormatException e)
                {
                    throw new UsageException($"--weight: {e.Message}");
                }
            }

            var content = creatures is null ? ContentCatalogs.Default : new ContentCatalogs(new CreatureCatalog(CreatureTable.Parse(File.ReadAllText(creatures))));
            return command.Run(line, new SimContext(content, aiWeights, weights));
        }
        catch (UsageException e)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 2;
        }
        catch (Exception e) when (e is FormatException or InvalidOperationException or ArgumentException or KeyNotFoundException or IOException)
        {
            Console.Error.WriteLine($"error: {e.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Takes the options every command shares out of the arguments, wherever they are: <c>--creatures path</c> (once) and every
    /// <c>--weight Name=value</c>, in order. Returns the rest: the command name and its own arguments.
    /// </summary>
    internal static (List<string> Arguments, string? Creatures, List<string> Weights) TakeGlobalOptions(IReadOnlyList<string> args)
    {
        var rest = new List<string>();
        string? creatures = null;
        var weights = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            if (args[i] is not ("--creatures" or "--weight"))
            {
                rest.Add(args[i]);
                continue;
            }

            if (i + 1 >= args.Count || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new UsageException(args[i] == "--weight" ? $"--weight needs a value (Name=value, names: {string.Join(", ", AiWeights.Names)})." : "--creatures needs a value (a creature table file).");
            }

            if (args[i] == "--weight")
            {
                weights.Add(args[++i]);
            }
            else if (creatures is null)
            {
                creatures = args[++i];
            }
            else
            {
                throw new UsageException("--creatures is given twice.");
            }
        }

        return (rest, creatures, weights);
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: sim battle <army> <army> [--runs N] [--seed S] | record <file> [--days D] [--seed S] | replay <file> | game [--map towns|demo|file | --template T [--size N] [--players N]] [--days D] [--seed S] [--runs N] [--record F] [--difficulty C] [--fog refog] [--profile] [--weight Name=value]... | maps [--template T] [--maps N] [--days D] [--seed S] | towns [--runs N] [--upgraded] | ai [--runs N] [--upgraded] [--level L] | creatures [--write F] | lines [--runs N] [--upgraded] | layout <template> [--size N] [--seed S] [--rects] [--terrain] [--seeds N] | generate <template> --out <file> [--size N] [--seed S] [--players N] | selftest");
        Console.Error.WriteLine("       [--creatures table.csv] with any command uses that creature table");
        Console.Error.WriteLine($"       [--weight Name=value]... plays the adventure AI with those weights ({string.Join(", ", AiWeights.Names)})");
        Console.Error.WriteLine("       army = creature:count[,creature:count...], e.g. haven.pikeman:20,haven.archer:10");
        return 2;
    }
}
