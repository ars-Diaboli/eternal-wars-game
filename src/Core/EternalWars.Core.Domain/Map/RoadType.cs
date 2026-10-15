namespace EternalWars.Core.Domain.Map;

/// <summary>HoMM3-style roads, drawn on top of terrain (docs/decisions.md ADR-024).</summary>
public enum RoadType
{
    None,
    Dirt,
    Gravel,
    Cobblestone,
}

public static class RoadRules
{
    /// <summary>Orthogonal step cost on a road; replaces terrain cost including native/rough penalties.</summary>
    public static int MovementCost(this RoadType road) => road switch
    {
        RoadType.Dirt => 50,
        RoadType.Gravel => 65,
        RoadType.Cobblestone => 75,
        _ => throw new ArgumentOutOfRangeException(nameof(road), road, "No road."),
    };
}
