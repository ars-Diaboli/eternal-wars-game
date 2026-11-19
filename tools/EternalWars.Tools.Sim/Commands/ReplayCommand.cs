using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim replay &lt;file&gt;</c>: replays a record (or a save) and checks it reaches the state it records; prints how long reading and
/// replaying took, which is what loading the save costs.
/// </summary>
internal sealed class ReplayCommand : SimCommand
{
    public override string Name => "replay";

    public override IReadOnlyList<string> Positionals => ["<file>"];

    public override int Run(CommandLine line, SimContext sim)
    {
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var record = GameRecordSerializer.Load(line.Positionals[0]);
        var read = clock.Elapsed;
        var session = GameReplay.Replay(record, sim.Content);
        var replayed = clock.Elapsed - read;
        Console.WriteLine($"replayed {record.Commands.Count} commands: {session.State.Date}, digest {GameStateDigest.Compute(session.State)} (matches the record)");
        Console.WriteLine($"read in {read.TotalSeconds:0.00} s, replayed in {replayed.TotalSeconds:0.00} s");
        return 0;
    }
}
