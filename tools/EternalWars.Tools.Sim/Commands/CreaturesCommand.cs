using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim creatures [--write table.csv]</c>: the roster overview for balancing, per creature line its value
/// (<see cref="CreatureDefinition.FightValue"/>), weekly growth, weekly power (growth × value) relative to the median line of that tier in the
/// playable towns, the upgrade's value relative to the base, and gold per point of value. With <c>--write</c> it writes the catalog (built-in or
/// <c>--creatures</c>) as a creature table instead; the notes column stays empty.
/// </summary>
internal sealed class CreaturesCommand : SimCommand
{
    public override string Name => "creatures";

    public override IReadOnlyList<OptionSpec> Options => [OptionSpec.Text("--write")];

    public override int Run(CommandLine line, SimContext sim) => line.Text("--write") is { } path ? WriteTable(sim, path) : Overview(sim);

    private static int WriteTable(SimContext sim, string path)
    {
        File.WriteAllText(path, CreatureTable.Format(sim.Catalog.All));
        Console.WriteLine($"{sim.Catalog.All.Count()} creatures → {path}");
        return 0;
    }

    private static int Overview(SimContext sim)
    {
        var catalog = sim.Catalog;
        var towns = SimContext.PlayableTowns;
        var upgrades = catalog.All.Where(c => c.UpgradesTo is not null).Select(c => c.UpgradesTo!).ToHashSet();
        var bases = catalog.All.Where(c => !upgrades.Contains(c.Id)).ToList();
        var medians = Enumerable.Range(CreatureDefinition.MinTier, CreatureDefinition.MaxTier).ToDictionary(t => t, t =>
        {
            var powers = bases.Where(c => c.Tier == t && towns.Contains(c.Town)).Select(Week).Order().ToList();
            return powers.Count == 0 ? 1 : powers[powers.Count / 2];
        });

        Console.WriteLine($"{"town",-15} T {"creature",-18} {"value",6} {"grow",4} {"week",7} {"×med",5}  {"upgrade",-18} {"value",6} {"×base",5} {"gold/val",8}");
        foreach (var creature in bases.OrderBy(c => c.Town is { } t && towns.Contains(t) ? Array.IndexOf(towns, t) : towns.Length)
                     .ThenBy(c => c.Town).ThenBy(c => c.Tier).ThenBy(c => c.Name))
        {
            var upgrade = creature.UpgradesTo is { } id ? catalog.Get(id) : null;
            Console.WriteLine($"{creature.Town ?? "neutral",-15} {creature.Tier} {creature.Name,-18} {creature.FightValue,6} {creature.WeeklyGrowth,4} {Week(creature),7:0} " +
                              $"{Week(creature) / medians[creature.Tier],5:0.00}  {upgrade?.Name ?? "",-18} {upgrade?.FightValue.ToString() ?? "",6} " +
                              $"{(upgrade is null ? "" : $"{(double)upgrade.FightValue / creature.FightValue:0.00}"),5} {(double)creature.Cost.Gold / creature.FightValue,8:0.00}");
        }

        Console.WriteLine();
        Console.WriteLine("One-week army power (mean of the four seven-dwelling builds):");
        foreach (var town in towns)
        {
            Console.WriteLine($"  {town,-15} {sim.WeekArmies(town, upgraded: false).Average(a => a.Stacks.Sum(s => (double)s.Stack.Count * catalog.Get(s.Stack.Creature).FightValue)),8:0}");
        }

        return 0;

        static double Week(CreatureDefinition creature) => (double)creature.WeeklyGrowth * creature.FightValue;
    }
}
