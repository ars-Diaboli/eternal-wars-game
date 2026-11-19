using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Commands;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim record &lt;file&gt; [--days D] [--seed S] [--computer]</c>: plays the demo scenario with scripted heroes and writes a replay file; with
/// <c>--computer</c> Red is a computer player, whose battles the core fights within the command that starts them (ADR-233).
/// </summary>
internal sealed class RecordCommand : SimCommand
{
    public override string Name => "record";

    public override IReadOnlyList<string> Positionals => ["<file>"];

    public override IReadOnlyList<OptionSpec> Options => [OptionSpec.Number("--days", min: 0), OptionSpec.Seed("--seed"), OptionSpec.Flag("--computer")];

    public override int Run(CommandLine line, SimContext sim)
    {
        var (path, days) = (line.Positionals[0], line.Number("--days", 7));
        var (map, session) = DemoGame.Play(sim, days, line.Seed("--seed", 1), computerRed: line.Has("--computer"));
        var record = GameReplay.Record(map, session);
        GameRecordSerializer.Save(record, path);
        var battles = session.CommandLog.Count(c => c is AutoResolveBattleCommand);
        Console.WriteLine($"{session.CommandLog.Count} commands ({battles} battles quick-resolved) over {days} days → {path} (digest {record.FinalDigest})");
        return 0;
    }
}
