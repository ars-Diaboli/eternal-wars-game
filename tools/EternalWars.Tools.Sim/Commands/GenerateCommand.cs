using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Generation;

namespace EternalWars.Tools.Sim.Commands;

/// <summary>
/// <c>sim generate &lt;template&gt; --out &lt;file&gt; [--size N] [--seed S] [--players N]</c>: generates a map from a template and saves it as a
/// map file, with human players in the start slots and random towns (to open in the game or the editor; the smoke scenario
/// <c>game-generated-connections</c> uses it).
/// </summary>
internal sealed class GenerateCommand : SimCommand
{
    public override string Name => "generate";

    public override IReadOnlyList<string> Positionals => ["<template>"];

    public override IReadOnlyList<OptionSpec> Options =>
        [OptionSpec.Text("--out", required: true), OptionSpec.Number("--size", min: 1), OptionSpec.Seed("--seed"), OptionSpec.Number("--players", min: 1)];

    public override int Run(CommandLine line, SimContext sim)
    {
        var loaded = SimContext.LoadTemplate(line.Positionals[0]);
        var path = line.Text("--out")!;
        var size = line.Number("--size", loaded.Sizes[0]);
        var seed = line.Seed("--seed", 1);
        var players = line.Number("--players", loaded.MaxPlayers);
        var setups = Enumerable.Range(0, players).Select(slot => new PlayerSetup(slot, $"Player {slot + 1}", IsHuman: true, PlayerSetup.RandomTown)).ToList();
        var (state, generation) = MapGenerator.Build(loaded, size, seed, setups, new GameOptions(Seed: seed), sim.Content);
        MapSerializer.Save(MapConverter.ToDocument(state), path);
        Console.WriteLine($"{loaded.Name} at {size}, seed {seed}, attempt {generation.Attempt} -> {path}");
        generation.Problems.ToList().ForEach(p => Console.WriteLine($"  problem: {p}"));
        return generation.Problems.Count == 0 ? 0 : 1;
    }
}
