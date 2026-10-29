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

/// <summary>Scenario complexity (ADR-063): how rich the players start. Chosen when a game is configured.</summary>
public enum Complexity
{
    Beginner,
    Easy,
    Normal,
    Hard,
    Expert,
}

/// <summary>Starting resources per <see cref="Complexity"/> (draft numbers; the hardest is the designer's, the rest are on a HoMM3-like scale).</summary>
public static class StartingResources
{
    public static Economy.ResourceSet For(Complexity complexity) => complexity switch
    {
        Complexity.Beginner => new(Gold: 10_000, Lumber: 30, Ore: 30, Crystal: 10, Mercury: 10, Sulfur: 10),
        Complexity.Easy => new(Gold: 7_500, Lumber: 20, Ore: 20, Crystal: 6, Mercury: 6, Sulfur: 6),
        Complexity.Normal => new(Gold: 5_000, Lumber: 15, Ore: 15, Crystal: 3, Mercury: 3, Sulfur: 3),
        Complexity.Hard => new(Gold: 3_500, Lumber: 12, Ore: 12, Crystal: 1, Mercury: 1, Sulfur: 1),
        _ => new(Gold: 2_500, Lumber: 10, Ore: 10),
    };
}

/// <summary>Rules chosen when a game is created (not part of the map file).</summary>
/// <param name="FogMode">How exploration and unit visibility work.</param>
/// <param name="RefogMargin">
/// <see cref="FogMode.ShroudWithRefog"/> only: hysteresis in tiles. A tile comes into sight within an
/// observer's scouting radius, but only drops out of sight once every observer is farther than
/// scouting radius + margin, so units at the edge of vision don't flicker in and out.
/// </param>
/// <param name="Seed">
/// Seed of the game's <see cref="Common.GameRandom"/>. Together with the map and the command log it fully
/// determines the game (replays). Front-ends pick a random seed; tests use the default.
/// </param>
/// <param name="Complexity">
/// Every player starts with the <see cref="StartingResources"/> of this level instead of what the map gives; null keeps the
/// map's own resources.
/// </param>
/// <param name="TurnTimerMinutes">
/// The turn timer (ADR-091): a human player's turn lasts this many minutes (1 to <see cref="Turns.TurnTimer.MaxMinutes"/>); null is off. The
/// clock is the front-end's (<see cref="Turns.TurnTimer"/>); what it does is ordinary commands in the log, so replays need no clock.
/// </param>
public sealed record GameOptions(
    FogMode FogMode = FogMode.PermanentReveal, int RefogMargin = 2, ulong Seed = 1, Complexity? Complexity = null, int? TurnTimerMinutes = null)
{
    public static GameOptions Default { get; } = new();
}
