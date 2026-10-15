namespace EternalWars.Core.Domain.Game;

public enum FogMode
{
    /// <summary>HoMM3: once explored, terrain, objects and units stay visible.</summary>
    PermanentReveal,

    /// <summary>
    /// Terrain and objects stay revealed once explored, but other players' units are only visible
    /// while their tile is in sight of one of your observers (heroes, owned mines).
    /// </summary>
    ShroudWithRefog,
}

/// <summary>Rules chosen when a game is created (not part of the map file).</summary>
/// <param name="FogMode">How exploration and unit visibility work.</param>
/// <param name="RefogMargin">
/// <see cref="FogMode.ShroudWithRefog"/> only: hysteresis in tiles. A tile comes into sight within an
/// observer's scouting radius, but only drops out of sight once every observer is farther than
/// scouting radius + margin, so units at the edge of vision don't flicker in and out.
/// </param>
public sealed record GameOptions(FogMode FogMode = FogMode.PermanentReveal, int RefogMargin = 2)
{
    public static GameOptions Default { get; } = new();
}
