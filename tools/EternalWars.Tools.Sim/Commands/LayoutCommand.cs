using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim layout &lt;template&gt; [--size N] [--seed S] [--rects] [--terrain] [--seeds N]</c>: the zone layout of a template as text, one character
/// per tile and one letter per zone; with <c>--terrain</c> the generated map; with <c>--seeds N</c> a batch check of N seeds with timing.
/// </summary>
internal sealed class LayoutCommand : SimCommand
{
    public override string Name => "layout";

    public override IReadOnlyList<string> Positionals => ["<template>"];

    public override IReadOnlyList<OptionSpec> Options =>
        [OptionSpec.Number("--size", min: 1), OptionSpec.Seed("--seed"), OptionSpec.Flag("--rects"), OptionSpec.Flag("--terrain"), OptionSpec.Number("--seeds", min: 0)];

    public override int Run(CommandLine line, SimContext sim)
    {
        var (template, size, seed, rectangles) = (line.Positionals[0], line.Number("--size", 96), line.Seed("--seed", 1), line.Has("--rects"));
        return line.Number("--seeds", 0) is > 0 and var many
            ? LayoutBatch(sim, template, size, seed, many, rectangles)
            : Layout(sim, template, size, seed, rectangles, line.Has("--terrain"));
    }

    private static int Layout(SimContext sim, string template, int size, ulong seed, bool rectangles, bool terrain)
    {
        var loaded = SimContext.LoadTemplate(template);
        var strategy = rectangles ? LayoutStrategy.Rectangles : LayoutStrategy.Voronoi;
        var built = terrain ? MapGenerator.Generate(loaded, size, seed, strategy, sim.Content) : null;
        var layout = built?.Layout ?? ZoneLayouter.Layout(loaded, size, seed, strategy);

        for (var level = 0; level < layout.Levels; level++)
        {
            Console.WriteLine($"level {level}:");
            if (built is null)
            {
                Console.WriteLine(layout.ToAscii(level));
                continue;
            }

            var marks = built.Objects.SelectMany(o => o.OccupiedCells()).Where(c => c.Position.Level == level).ToDictionary(c => c.Position, c => c);
            var kinds = built.Objects.ToDictionary(o => o.VisitPosition, o => o switch
            {
                MonsterGuard { Mode: GuardMode.Area } => 'A', MonsterGuard => 'M', Town => 'T', Mine => 'm', ResourcePile => 'p', TreasureChest => 'c', ArtifactPickup => 'a', ResourceProvider => 'r', LootSite => 'l', BuffSite => 'b', TrainingSite => 't', Scenery => '#', _ => 'G',
            });
            Console.WriteLine(string.Join(System.Environment.NewLine, Enumerable.Range(0, size).Select(y => string.Concat(Enumerable.Range(0, size).Select(x =>
            {
                var p = new MapPosition(x, y, level);
                return kinds.TryGetValue(p, out var mark) ? mark : marks.ContainsKey(p) ? 'g' : built.Map.GetRoad(p) != RoadType.None ? '+' : built.Map.GetRiver(p) != RiverType.None ? '%' : TerrainCodes.ToCode(built.Map.GetTerrain(p));
            })))));
        }

        if (built is not null)
        {
            foreach (var monster in built.Objects.OfType<MonsterGuard>())
            {
                Console.WriteLine($"  guard at {monster.Position}: {monster.Count} {monster.Creature} ({monster.Count * sim.Catalog.Get(monster.Creature).FightValue} power)");
            }

            foreach (var mine in built.Objects.OfType<Mine>())
            {
                Console.WriteLine($"  {mine.Definition.Name} at {mine.Position}{(mine.Guard is { } g ? $" guarded by {g.Count} {g.Creature}" : "")}");
            }

            foreach (var item in built.Objects.Where(o => o is TreasureChest or ArtifactPickup or ResourcePile))
            {
                Console.WriteLine($"  {item switch { TreasureChest c => $"chest {c.FixedValues?.Gold} gold", ArtifactPickup a => $"artifact {a.Artifact}", ResourcePile p => $"{p.Amount} {p.Resource}", _ => "" }} at {item.Position}");
            }

            foreach (var town in built.Towns)
            {
                Console.WriteLine($"  {town.Town.TownType} town at {town.Town.Position}, {(town.Slot is { } slot ? $"player slot {slot}" : $"neutral, garrison {string.Join(", ", town.Town.Garrison.Stacks.Select(t => $"{t.Stack.Count} {t.Stack.Creature}"))}")}");
            }

            built.Problems.ToList().ForEach(p => Console.WriteLine($"  problem: {p}"));
        }

        for (var i = 0; i < layout.ZoneIds.Count; i++)
        {
            Console.WriteLine($"  {layout.ZoneIds[i],-12} area {layout.Area(i)}");
        }

        foreach (var (from, to) in layout.UnmetConnections)
        {
            Console.WriteLine($"  not touching: {from} - {to}");
        }

        return layout.UnmetConnections.Count == 0 ? 0 : 1;
    }

    /// <summary>Generates a template for N seeds in a row (layout, terrain, connections) and reports the ones with problems, and the time.</summary>
    private static int LayoutBatch(SimContext sim, string template, int size, ulong first, int count, bool rectangles)
    {
        var loaded = SimContext.LoadTemplate(template);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var bad = 0;
        for (var i = 0; i < count; i++)
        {
            var seed = first + (ulong)i;
            var built = MapGenerator.Generate(loaded, size, seed, rectangles ? LayoutStrategy.Rectangles : LayoutStrategy.Voronoi, sim.Content);
            var problems = built.Problems.ToList();
            if (problems.Count > 0)
            {
                bad++;
                Console.WriteLine($"seed {seed}: {string.Join("; ", problems)}");
            }
        }

        Console.WriteLine($"{count} maps of {loaded.Name} at {size}: {bad} with problems, {clock.ElapsedMilliseconds / count} ms each");
        return bad == 0 ? 0 : 1;
    }
}
