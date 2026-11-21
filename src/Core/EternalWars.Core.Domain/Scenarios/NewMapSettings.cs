using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>
/// What the editor's new-map wizard asks for a blank map (ADR-259): its size (<see cref="MapSizes.MinDimension"/> to
/// <see cref="MapSizes.MaxDimension"/> a side, square or not), its levels (the surface, or the surface and the underground), the terrain the
/// surface is painted in, and the players, by kind (<see cref="Humans"/>: true for a person, false for the computer, by default).
/// </summary>
public sealed record NewMapSettings(int Width, int Height, int Levels, TerrainType Terrain, IReadOnlyList<bool> Humans)
{
    /// <summary>The most levels the wizard makes: the surface and the underground.</summary>
    public const int MaxLevels = 2;

    /// <summary>The terrains a new map's surface may be painted in: those a hero can walk on, but the underground's.</summary>
    public static IReadOnlyList<TerrainType> Terrains { get; } =
        [.. Enum.GetValues<TerrainType>().Where(t => t != TerrainType.Subterranean && TerrainCatalog.Default.IsPassable(t))];

    /// <summary>The wizard's first choice: a square map of <paramref name="size"/>, one level of grass, two people.</summary>
    public static NewMapSettings Default(int size) => new(size, size, 1, TerrainType.Grass, [true, true]);

    /// <summary>What is wrong with the settings, or null when a map can be made of them.</summary>
    public string? Problem() =>
        Width is < MapSizes.MinDimension or > MapSizes.MaxDimension || Height is < MapSizes.MinDimension or > MapSizes.MaxDimension
            ? $"A map is {MapSizes.MinDimension} to {MapSizes.MaxDimension} tiles a side."
        : Levels is < 1 or > MaxLevels ? $"A new map has 1 to {MaxLevels} levels."
        : !Terrains.Contains(Terrain) ? "A new map's ground is a terrain a hero can walk on."
        : Humans.Count is < 1 or > GameState.MaxPlayers ? $"A map has 1 to {GameState.MaxPlayers} players."
        : null;
}
