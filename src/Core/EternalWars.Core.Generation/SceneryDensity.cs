using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Generation;

/// <summary>
/// How much scenery a terrain carries on random maps (ADR-157): <see cref="Clumps"/> scales the zone's obstacle density (percent, 100 = as the
/// template says), <see cref="ExtraSize"/> adds tiles to the clumps that start on it. Forests are thick with trees, rough ground and highlands
/// with rocks, sand and ice stay open. Placeholder numbers.
/// </summary>
public sealed record SceneryDensity(int Clumps, int ExtraSize)
{
    public static SceneryDensity For(TerrainType terrain) => terrain switch
    {
        TerrainType.ForestFloor => new(250, 4),
        TerrainType.Highlands => new(175, 2),
        TerrainType.Rough or TerrainType.Swamp or TerrainType.RockyGround => new(150, 1),
        TerrainType.Grass or TerrainType.Snow or TerrainType.Subterranean => new(125, 1),
        TerrainType.Dirt or TerrainType.Wasteland or TerrainType.CursedLands => new(100, 0),
        TerrainType.Lava => new(75, 0),
        _ => new(50, 0), // sand, beach, ice and anything else
    };
}
