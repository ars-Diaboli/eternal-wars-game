using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Generation;

/// <summary>
/// The generator's entry point (ADR-064): runs the stages in order on one seed. <see cref="Generate"/> makes the map (layout, terrain, connections,
/// towns and mines, roads, treasure); <see cref="Build"/> also assembles the scenario for the players set up at game start.
/// </summary>
public static class MapGenerator
{
    public static GeneratedMap Generate(
        MapTemplate template, int size, ulong seed, LayoutStrategy strategy = LayoutStrategy.Voronoi, CreatureCatalog? creatures = null, ArtifactCatalog? artifacts = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        var layout = ZoneLayouter.Layout(template, size, seed, strategy);
        var map = GeneratedMap.From(TerrainPainter.Paint(template, layout, seed));
        foreach (var (from, to) in layout.UnmetConnections)
        {
            map.Problem($"Zones {from} and {to} do not touch.");
        }

        ConnectionPlacer.Place(template, map, seed, creatures);
        SettlementPlacer.Place(template, map, seed, creatures);
        RoadPlacer.Place(template, map);
        TreasurePlacer.Place(template, map, seed, creatures, artifacts);
        return map;
    }

    public static GameState Build(
        MapTemplate template, int size, ulong seed, IReadOnlyList<PlayerSetup> players, GameOptions? options = null, CreatureCatalog? creatures = null,
        HeroCatalog? heroRoster = null, ArtifactCatalog? artifacts = null) =>
        ScenarioAssembler.Build(template, Generate(template, size, seed, creatures: creatures, artifacts: artifacts), players, seed, options, creatures, heroRoster);
}
