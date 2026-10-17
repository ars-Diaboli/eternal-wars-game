using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Generation;

/// <summary>A generated map and how it was made: the attempt that gave it (0 = the seed itself) and that attempt's seed.</summary>
public sealed record GenerationResult(GeneratedMap Map, int Attempt, ulong AttemptSeed)
{
    public IReadOnlyList<string> Problems => Map.Problems;
}

/// <summary>
/// The generator's entry point (ADR-064): runs the stages in order on one seed. <see cref="Generate"/> makes the map (layout, terrain, connections,
/// towns and mines, roads, treasure); <see cref="GenerateBest"/> retries on problems (RMG-5); <see cref="Build"/> also assembles the scenario for the
/// players set up at game start, whose towns are an input so their start zones are painted native (RMG-7).
/// </summary>
public static class MapGenerator
{
    /// <summary>
    /// Bumped whenever a change makes a template, size, players and seed give another map; saved with generated games and shown with the seed, so a
    /// shared seed from another version can be warned about.
    /// </summary>
    public const int Version = 1;

    /// <summary>How many seeds <see cref="GenerateBest"/> tries: the seed, then sub-seeds of it.</summary>
    public const int Attempts = 10;

    public static GeneratedMap Generate(
        MapTemplate template, int size, ulong seed, LayoutStrategy strategy = LayoutStrategy.Voronoi, CreatureCatalog? creatures = null, ArtifactCatalog? artifacts = null,
        IReadOnlyDictionary<int, string>? startTowns = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        var layout = ZoneLayouter.Layout(template, size, seed, strategy);
        var map = GeneratedMap.From(TerrainPainter.Paint(template, layout, seed, startTowns));
        foreach (var (from, to) in layout.UnmetConnections)
        {
            map.Problem($"Zones {from} and {to} do not touch.");
        }

        ConnectionPlacer.Place(template, map, seed, creatures);
        SettlementPlacer.Place(template, map, seed, creatures, startTowns);
        RoadPlacer.Place(template, map);
        TreasurePlacer.Place(template, map, seed, creatures, artifacts);
        return map;
    }

    /// <summary>The seed of attempt <paramref name="attempt"/>: the seed itself first, then sub-seeds derived from it.</summary>
    public static ulong AttemptSeed(ulong seed, int attempt) =>
        attempt == 0 ? seed : seed ^ ((ulong)attempt * 0x9E3779B97F4A7C15UL) ^ ((ulong)attempt << 47);

    /// <summary>
    /// The first map without problems among <see cref="Attempts"/> tries (the seed, then its sub-seeds); if every one has problems, the one with
    /// the fewest (the earliest of those). Deterministic: the same inputs pick the same attempt.
    /// </summary>
    public static GenerationResult GenerateBest(
        MapTemplate template, int size, ulong seed, LayoutStrategy strategy = LayoutStrategy.Voronoi, CreatureCatalog? creatures = null, ArtifactCatalog? artifacts = null,
        IReadOnlyDictionary<int, string>? startTowns = null)
    {
        GenerationResult? best = null;
        for (var attempt = 0; attempt < Attempts; attempt++)
        {
            var attemptSeed = AttemptSeed(seed, attempt);
            var result = new GenerationResult(Generate(template, size, attemptSeed, strategy, creatures, artifacts, startTowns), attempt, attemptSeed);
            if (result.Problems.Count == 0)
            {
                return result;
            }

            if (best is null || result.Problems.Count < best.Problems.Count)
            {
                best = result;
            }
        }

        return best!;
    }

    /// <summary>
    /// Resolves "Random" towns, generates the best map for the players' towns and assembles the scenario. The problems of the map used (if every
    /// attempt had some) are in the result for the new-game screen to show.
    /// </summary>
    public static (GameState State, GenerationResult Generation) Build(
        MapTemplate template, int size, ulong seed, IReadOnlyList<PlayerSetup> players, GameOptions? options = null, CreatureCatalog? creatures = null,
        HeroCatalog? heroRoster = null, ArtifactCatalog? artifacts = null, BuildingCatalog? buildings = null)
    {
        var resolved = ScenarioAssembler.ResolveTowns(players, seed);
        var generation = GenerateBest(template, size, seed, creatures: creatures, artifacts: artifacts, startTowns: ScenarioAssembler.StartTowns(resolved));
        return (ScenarioAssembler.Build(template, generation.Map, resolved, seed, options, creatures, heroRoster, buildings, artifacts), generation);
    }
}
