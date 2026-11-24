namespace EternalWars.Core.Domain.Map;

/// <summary>The names of a map's levels (ADR-309), as the minimap's caption shows them.</summary>
public static class MapLevels
{
    /// <summary>"Surface" for level 0, "Underground" for level 1.</summary>
    public static string Name(int level) => level == 0 ? "Surface" : "Underground";
}
