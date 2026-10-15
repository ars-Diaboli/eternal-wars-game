namespace EternalWars.Core.Domain.Map;

public enum TerrainType
{
    Grass,
    Dirt,
    Sand,
    Snow,
    Swamp,
    Rough,
    Lava,
    Subterranean,
    Water,
    Rock,
}

/// <summary>Movement rules per terrain. Costs are in movement points for an orthogonal step.</summary>
public static class TerrainRules
{
    public const int ImpassableCost = int.MaxValue;

    public static int MovementCost(this TerrainType terrain) => terrain switch
    {
        TerrainType.Grass or TerrainType.Dirt or TerrainType.Lava or TerrainType.Subterranean => 100,
        TerrainType.Rough => 125,
        TerrainType.Sand or TerrainType.Snow => 150,
        TerrainType.Swamp => 175,
        TerrainType.Water or TerrainType.Rock => ImpassableCost,
        _ => throw new ArgumentOutOfRangeException(nameof(terrain), terrain, null),
    };

    public static bool IsPassableOnFoot(this TerrainType terrain) => terrain.MovementCost() != ImpassableCost;
}
