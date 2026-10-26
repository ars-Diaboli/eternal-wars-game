namespace EternalWars.Core.Domain.Map;

/// <summary>
/// Rivers, drawn on top of terrain like roads (ADR-079, MAP-26). The kinds only change the look; every river slows a step the same way.
/// </summary>
public enum RiverType
{
    None,
    Clear,
    Icy,
    Muddy,
    Lava,
}

public static class RiverRules
{
    /// <summary>The opposite of a road: leaving a river tile costs this much more, unless the step goes road to road (a bridge).</summary>
    public const int PenaltyPercent = 25;

    /// <summary>The step's base cost (before the diagonal factor) with the river penalty applied.</summary>
    public static int WithPenalty(int baseCost) => baseCost * (100 + PenaltyPercent) / 100;

    /// <summary>The look the generator gives a river on a terrain: icy on snow and ice, lava on lava, muddy on swamp and the wastes.</summary>
    public static RiverType For(TerrainType terrain) => terrain switch
    {
        TerrainType.Snow or TerrainType.Ice => RiverType.Icy,
        TerrainType.Lava => RiverType.Lava,
        TerrainType.Swamp or TerrainType.Wasteland or TerrainType.CursedLands => RiverType.Muddy,
        _ => RiverType.Clear,
    };
}
