using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Generation;

/// <summary>
/// Stage 2b of the generator (ADR-079, MAP-25): the rock clumps of stage 2 become scenery. Each clump tile gets back the ground around it, then
/// the clump is filled with scenery made for that ground (<c>scenery.csv</c>), larger pieces first, so exactly the clump's tiles stay blocked and
/// no part of a zone is cut off. Deterministic from the seed (the <c>scenery</c> stream).
/// </summary>
public static class SceneryPlacer
{
    /// <summary>What fills a tile no other scenery fits.</summary>
    public const string Fallback = "scenery.rock.small";

    public static void Place(GeneratedMap map, IReadOnlyList<MapPosition> obstacles, ulong seed, MapObjectCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(obstacles);
        catalog ??= MapObjectCatalog.Default;
        var scenery = catalog.OfKind(MapObjectKind.Scenery).OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
        if (scenery.Count == 0)
        {
            return;
        }

        var random = new SeedStreams(seed).For("scenery");
        var left = obstacles.ToHashSet();
        foreach (var tile in obstacles)
        {
            map.Map.SetTerrain(tile, Ground(map, tile, left));
        }

        foreach (var tile in obstacles.OrderBy(t => t.Level).ThenBy(t => t.Y).ThenBy(t => t.X))
        {
            if (!left.Contains(tile))
            {
                continue;
            }

            var terrain = map.Map.GetTerrain(tile);
            var fitting = scenery.Where(d => d.Terrains.Contains(terrain) && Cells(d, Anchor(d, tile)).All(left.Contains)).ToList();
            var definition = fitting.Count == 0
                ? catalog.Get(Fallback)
                : Pick(fitting.GroupBy(d => d.Footprint.OccupiedCells.Count).MaxBy(g => g.Key)!.ToList(), random);
            var topLeft = Anchor(definition, tile);
            map.Add(MapObject.Create(map.NextId(), definition, topLeft));
            left.ExceptWith(Cells(definition, topLeft));
        }
    }

    private static MapObjectDefinition Pick(List<MapObjectDefinition> choices, GameRandom random) => choices[random.Next(choices.Count)];

    /// <summary>Where the definition's top-left goes so its first blocked cell (row by row) lands on <paramref name="tile"/>.</summary>
    private static MapPosition Anchor(MapObjectDefinition definition, MapPosition tile)
    {
        var (dx, dy, _) = definition.Footprint.OccupiedCells.OrderBy(c => c.Dy).ThenBy(c => c.Dx).First();
        return tile.Offset(-dx, -dy);
    }

    private static IEnumerable<MapPosition> Cells(MapObjectDefinition definition, MapPosition topLeft) =>
        definition.Footprint.OccupiedCells.Select(c => topLeft.Offset(c.Dx, c.Dy));

    /// <summary>The most common ground among the tile's neighbours in its zone (rock and other clump tiles aside; ties by terrain order).</summary>
    private static TerrainType Ground(GeneratedMap map, MapPosition tile, HashSet<MapPosition> clumps)
    {
        var zone = map.Layout.ZoneAt(tile.X, tile.Y, tile.Level);
        var around = new List<TerrainType>();
        for (var radius = 1; around.Count == 0 && radius <= 3; radius++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                for (var dx = -radius; dx <= radius; dx++)
                {
                    var next = tile.Offset(dx, dy);
                    if (map.Map.Contains(next) && !clumps.Contains(next) && map.Layout.ZoneAt(next.X, next.Y, next.Level) == zone
                        && map.Map.GetTerrain(next) is var terrain && terrain != TerrainPainter.WallTerrain && TerrainCatalog.Default.IsPassable(terrain))
                    {
                        around.Add(terrain);
                    }
                }
            }
        }

        return around.Count == 0 ? TerrainType.Dirt : around.GroupBy(t => t).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
    }
}
