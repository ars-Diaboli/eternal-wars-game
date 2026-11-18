namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// One command of the sim (<c>sim &lt;name&gt; ...</c>): its positionals and options, checked strictly by <see cref="CommandLine"/> before it runs.
/// Returns the process exit code: 0 when everything checked out, 1 when something failed.
/// </summary>
internal abstract class SimCommand
{
    /// <summary>The word after <c>sim</c>.</summary>
    public abstract string Name { get; }

    /// <summary>The positional arguments, all required, as <c>&lt;name&gt;</c> for messages.</summary>
    public virtual IReadOnlyList<string> Positionals => [];

    /// <summary>The options it takes, besides <c>--creatures</c> (every command takes that).</summary>
    public virtual IReadOnlyList<OptionSpec> Options => [];

    /// <summary>The command takes <c>--weight Name=value</c> (it plays the adventure AI).</summary>
    public virtual bool TakesWeights => false;

    /// <summary>Checks <paramref name="args"/> (after the command name, global options taken out) against <see cref="Positionals"/> and <see cref="Options"/>.</summary>
    public CommandLine Parse(IReadOnlyList<string> args) => CommandLine.Parse(Name, args, Positionals, Options);

    public abstract int Run(CommandLine line, SimContext sim);

    /// <summary>Every command, by name.</summary>
    public static IReadOnlyList<SimCommand> All { get; } =
    [
        new BattleCommand(), new RecordCommand(), new ReplayCommand(), new PlayGameCommand(), new MapsCommand(), new TownsCommand(), new AiCommand(),
        new CreaturesCommand(), new LinesCommand(), new LayoutCommand(), new GenerateCommand(), new ExportCommand(), new FairnessCommand(), new SelfTestCommand(),
    ];

    public static SimCommand? Find(string name) => All.FirstOrDefault(c => c.Name == name);
}
