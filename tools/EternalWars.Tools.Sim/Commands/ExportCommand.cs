using EternalWars.Core.Data.Maps;

namespace EternalWars.Tools.Sim.Commands;

/// <summary><c>sim export towns|demo|ember --out &lt;file&gt;</c>: saves a built-in map as a map file the editor and the game open.</summary>
internal sealed class ExportCommand : SimCommand
{
    public override string Name => "export";

    public override IReadOnlyList<string> Positionals => ["<map>"];

    public override IReadOnlyList<OptionSpec> Options => [OptionSpec.Text("--out", required: true)];

    public override int Run(CommandLine line, SimContext sim)
    {
        var (map, path) = (line.Positionals[0], line.Text("--out")!);
        MapSerializer.Save(SimGames.HandMadeGame(sim, map, 1).Map, path);
        Console.WriteLine($"{map} → {path}");
        return 0;
    }
}
