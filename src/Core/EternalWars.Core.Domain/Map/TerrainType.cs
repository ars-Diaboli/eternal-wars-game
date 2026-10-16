namespace EternalWars.Core.Domain.Map;

/// <summary>
/// Terrain identities. Rules (passability, cost, native towns) are data: see <see cref="TerrainCatalog"/>.
/// New terrains are appended, so existing values keep their numbers.
/// </summary>
public enum TerrainType
{
    Grass,
    Dirt,
    Rough,
    Highlands,
    ForestFloor,
    Wasteland,
    Snow,
    Lava,
    Subterranean,
    Sand,
    Swamp,
    Water,
    Rock,
    Beach,
    Ice,
    ShallowWater,
    CursedLands,

    /// <summary>Walkable rocky ground (Dragon Utopia); <see cref="Rock"/> is the impassable cliff.</summary>
    RockyGround,
}
