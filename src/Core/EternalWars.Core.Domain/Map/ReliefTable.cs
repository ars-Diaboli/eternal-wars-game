namespace EternalWars.Core.Domain.Map;

/// <summary>
/// A terrain's preferred ground height (ADR-182 c), in eighths of a cell width. <see cref="Base"/> is used when shaping
/// from terrain; <see cref="Min"/> … <see cref="Max"/> is the range the generator's noise may move it in.
/// </summary>
public readonly record struct TerrainHeight(int Base, int Min, int Max)
{
    public static TerrainHeight At(int eighths) => new(eighths, eighths, eighths);
}

/// <summary>
/// Heights by terrain (ADR-182 c), one table for the generator, the map reader's fill and the editor's "Heights from
/// terrain". Placeholders, tuned once the look-dev spike shows them. Looks only: no rule reads it.
/// </summary>
public static class ReliefTable
{
    public static TerrainHeight Of(TerrainType terrain) => terrain switch
    {
        // Very low and mostly flat: 0–0.25.
        TerrainType.Swamp or TerrainType.ShallowWater or TerrainType.Ice => new(1, 0, 2),
        TerrainType.Beach => TerrainHeight.At(4),
        TerrainType.Sand => TerrainHeight.At(6),
        TerrainType.Grass or TerrainType.Dirt or TerrainType.CursedLands => TerrainHeight.At(8),
        TerrainType.ForestFloor or TerrainType.Subterranean => TerrainHeight.At(10),
        TerrainType.Wasteland => TerrainHeight.At(12),
        TerrainType.Snow => TerrainHeight.At(16),
        TerrainType.Rough => TerrainHeight.At(20),
        TerrainType.Highlands => TerrainHeight.At(24),
        TerrainType.Lava => TerrainHeight.At(26),
        TerrainType.RockyGround => TerrainHeight.At(28),
        TerrainType.Water => TerrainHeight.At(-4),

        // Impassable rock stands above the walkable limit, 4.5–6 (ADR-075's cliffs).
        TerrainType.Rock => new(42, 36, 48),
        _ => throw new ArgumentOutOfRangeException(nameof(terrain), terrain, "No height for this terrain."),
    };

    /// <summary>Terrains kept flat: no noise, and corners inside them keep their own height when shaping.</summary>
    public static bool IsFlat(TerrainType terrain) => terrain is TerrainType.ShallowWater or TerrainType.Ice;
}
