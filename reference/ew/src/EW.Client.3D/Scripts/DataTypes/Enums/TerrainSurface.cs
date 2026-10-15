namespace EW.Scripts.DataTypes.Enums;

public enum TerrainSurface
{
    Underground,
    Grassland,
    Highlands,
    Forest,
    Dirt,
    Swamp,
    Beach,
    Desert,
    Wastelands,
    Rough,
    Rocks,
    Snow,
    Ice,
    ShallowWater,
    CursedLands,
    Lava,
}

public static class TerrainExtensions
{
    public static float GetWeightMultiplier(this TerrainSurface surface, Faction faction = Faction.Neutral)
    {
        if (faction == Faction.Temple && surface is TerrainSurface.Grassland or TerrainSurface.Highlands) return 1f;
        if (faction == Faction.Grove && surface is TerrainSurface.Forest or TerrainSurface.Grassland) return 1f;
        if (faction == Faction.Academy && surface is TerrainSurface.Snow or TerrainSurface.Ice) return 1f;

        if (faction == Faction.Necropolis && surface == TerrainSurface.CursedLands) return 1f;
        if (faction == Faction.Inferno && surface == TerrainSurface.Lava) return 1f;
        if (faction == Faction.Dungeon && surface == TerrainSurface.Underground) return 1f;

        if (faction == Faction.Stronghold && surface is TerrainSurface.Rough or TerrainSurface.Wastelands) return 1f;
        if (faction == Faction.Bestiary && surface is TerrainSurface.Swamp or TerrainSurface.ShallowWater) return 1f;

        if (faction == Faction.Conflux && surface == TerrainSurface.Highlands) return 1f;
        if (faction == Faction.DragonUtopia && surface == TerrainSurface.Rocks) return 1f;

        switch (surface)
        {
            case TerrainSurface.Grassland: return 1.25f;
            case TerrainSurface.Highlands: return 1.25f;
            case TerrainSurface.Forest: return 1.25f;
            
            case TerrainSurface.Snow: return 2.0f;
            case TerrainSurface.Ice: return 2.0f;
            
            case TerrainSurface.CursedLands: return 1.25f;
            case TerrainSurface.Lava: return 1.5f;
            case TerrainSurface.Underground: return 1.25f;

            case TerrainSurface.Rough: return 1.5f;
            case TerrainSurface.Wastelands: return 1.5f;
            
            case TerrainSurface.Swamp: return 2.0f;
            case TerrainSurface.ShallowWater: return 2.0f;

            case TerrainSurface.Dirt: return 1.5f;
            case TerrainSurface.Beach: return 1.25f;
            case TerrainSurface.Desert: return 1.25f;
            case TerrainSurface.Rocks: return 1.5f;
        }

        return 1.0f;
    }
}

