using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Game;

namespace EternalWars.Tools.Sim.Commands;

/// <summary><c>sim replay &lt;file&gt;</c>: replays a record (or a save) and checks it reaches the state it records.</summary>
internal sealed class ReplayCommand : SimCommand
{
    public override string Name => "replay";

    public override IReadOnlyList<string> Positionals => ["<file>"];

    public override int Run(CommandLine line, SimContext sim)
    {
        var record = GameRecordSerializer.Load(line.Positionals[0]);
        var session = GameReplay.Replay(record, sim.Content);
        Console.WriteLine($"replayed {record.Commands.Count} commands: {session.State.Date}, digest {GameStateDigest.Compute(session.State)} (matches the record)");
        return 0;
    }
}
