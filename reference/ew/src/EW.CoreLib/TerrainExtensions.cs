namespace EW.CoreLib.Types;

public static class TerrainExtensions
{
    public static float GetWeightMultiplier(this TerrainSurface surface, Faction faction = Faction.Neutral)
    {
        if (FactionMatchesSurface(surface, faction)) return 1f;

        return WeightMultiplierForSurface(surface);
    }

    private static float WeightMultiplierForSurface(TerrainSurface surface)
    {
        return surface switch
        {
            TerrainSurface.Grassland => 1.25f,
            TerrainSurface.Highlands => 1.25f,
            TerrainSurface.Forest => 1.25f,
            TerrainSurface.Snow => 2.0f,
            TerrainSurface.Ice => 1.75f,
            TerrainSurface.CursedLands => 1.25f,
            TerrainSurface.Lava => 1.5f,
            TerrainSurface.Underground => 1.25f,
            TerrainSurface.Rough => 1.5f,
            TerrainSurface.Wastelands => 1.5f,
            TerrainSurface.Swamp => 2.0f,
            TerrainSurface.ShallowWater => 1.75f,
            TerrainSurface.Dirt => 1.5f,
            TerrainSurface.Beach => 1.25f,
            TerrainSurface.Desert => 1.25f,
            TerrainSurface.Rocks => 1.5f,
            _ => 1f
        };
    }

    private static bool FactionMatchesSurface(TerrainSurface surface, Faction faction)
    {
        switch (faction)
        {
            case Faction.Temple when surface is TerrainSurface.Grassland or TerrainSurface.Highlands:
            case Faction.Grove when surface is TerrainSurface.Forest or TerrainSurface.Grassland:
            case Faction.Academy when surface is TerrainSurface.Snow or TerrainSurface.Ice:
            case Faction.Necropolis when surface == TerrainSurface.CursedLands:
            case Faction.Inferno when surface == TerrainSurface.Lava:
            case Faction.Dungeon when surface == TerrainSurface.Underground:
            case Faction.Stronghold when surface is TerrainSurface.Rough or TerrainSurface.Wastelands:
            case Faction.Bestiary when surface is TerrainSurface.Swamp or TerrainSurface.ShallowWater:
            case Faction.Conflux when surface == TerrainSurface.Highlands:
            case Faction.DragonUtopia when surface == TerrainSurface.Rocks:
                return true;
            default:
                return false;
        }
    }
}