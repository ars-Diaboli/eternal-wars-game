using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Generation;

/// <summary>
/// Stage 2b of the generator (ADR-079, MAP-25): the rock clumps of stage 2 become scenery. Each clump tile gets back the ground around it, then
/// the clump is filled with scenery made for that ground (<c>scenery.csv</c>): a kind drawn by weight (trees and rocks most, ADR-157), then its
/// largest piece that fits, so exactly the clump's tiles stay blocked and no part of a zone is cut off. The scenery is that of the map's object table
/// (<see cref="GeneratedMap.Content"/>). Deterministic from the seed (the <c>scenery</c> stream).
/// </summary>
public static class SceneryPlacer
{
    /// <summary>What fills a tile no other scenery fits.</summary>
    public const string Fallback = "scenery.rock.small";

    public static void Place(GeneratedMap map, IReadOnlyList<MapPosition> obstacles, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(obstacles);
        var catalog = map.Content.Objects;
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
            var definition = fitting.Count == 0 ? catalog.Get(Fallback) : Pick(fitting, terrain, random);
            var topLeft = Anchor(definition, tile);
            map.Add(MapObject.Create(map.NextId(), definition, topLeft));
            left.ExceptWith(Cells(definition, topLeft));
        }
    }

    /// <summary>The kind of a scenery id (<c>scenery.tree.pair</c> → <c>tree</c>).</summary>
    public static string KindOf(MapObjectDefinition definition) => definition.Id.Split('.') is { Length: > 1 } parts ? parts[1] : definition.Id;

    /// <summary>
    /// How often a kind is drawn on a terrain (ADR-157): trees and rocks most, the terrain's own kind (trees in forests, rocks on broken
    /// ground, dead trees on blighted land) far more; walls and ruins rarely. Placeholder numbers.
    /// </summary>
    public static int Weight(string kind, TerrainType terrain) => kind switch
    {
        "tree" => terrain is TerrainType.ForestFloor ? 24 : terrain is TerrainType.Grass or TerrainType.Highlands or TerrainType.Snow ? 10 : 6,
        "rock" => terrain is TerrainType.Rough or TerrainType.Highlands or TerrainType.RockyGround or TerrainType.Lava or TerrainType.Subterranean
            or TerrainType.Snow ? 16 : 6,
        "dead-tree" => terrain is TerrainType.Wasteland or TerrainType.CursedLands or TerrainType.Swamp ? 12 : 4,
        "mushrooms" => terrain is TerrainType.Subterranean or TerrainType.Swamp ? 6 : 2,
        "bush" or "mound" => 3,
        "wall" or "ruins" => 1,
        _ => 2,
    };

    /// <summary>A kind drawn by <see cref="Weight"/> among those with a piece that fits, then that kind's largest fitting piece.</summary>
    private static MapObjectDefinition Pick(List<MapObjectDefinition> fitting, TerrainType terrain, GameRandom random)
    {
        var kinds = fitting.GroupBy(KindOf).OrderBy(g => g.Key, StringComparer.Ordinal).ToList();
        var roll = random.Next(kinds.Sum(g => Weight(g.Key, terrain)));
        var kind = kinds.First(g => (roll -= Weight(g.Key, terrain)) < 0).ToList();
        var largest = kind.Max(d => d.Footprint.OccupiedCells.Count);
        var choices = kind.Where(d => d.Footprint.OccupiedCells.Count == largest).ToList();
        return choices[random.Next(choices.Count)];
    }

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
                        && map.Map.GetTerrain(next) is var terrain && terrain != TerrainPainter.WallTerrain && map.Terrains.IsPassable(terrain))
                    {
                        around.Add(terrain);
                    }
                }
            }
        }

        return around.Count == 0 ? TerrainType.Dirt : around.GroupBy(t => t).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
    }
}
