using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>
/// Starting point for a new map in the editor (ADR-259): plain terrain (the underground below the surface), and the players, named by colour,
/// each with a hero: the first two in opposite corners, then the other corners, then the middles of the sides.
/// </summary>
public static class BlankScenario
{
    /// <summary>Two people on one level of <paramref name="fill"/>.</summary>
    public static GameState Create(int width, int height, int levels = 1, TerrainType fill = TerrainType.Grass, ContentCatalogs? content = null) =>
        Build(width, height, levels, fill, [true, true], content);

    /// <summary>The new-map wizard's map; settings with a <see cref="NewMapSettings.Problem"/> throw <see cref="ArgumentException"/>.</summary>
    public static GameState Create(NewMapSettings settings, ContentCatalogs? content = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.Problem() is { } problem)
        {
            throw new ArgumentException(problem, nameof(settings));
        }

        return Build(settings.Width, settings.Height, settings.Levels, settings.Terrain, settings.Humans, content);
    }

    /// <summary>Where the heroes of the players start, in slot order, on a map of the size.</summary>
    public static IReadOnlyList<MapPosition> HeroStarts(int width, int height) =>
    [
        new(1, 1), new(width - 2, height - 2), new(width - 2, 1), new(1, height - 2),
        new(width / 2, 1), new(width / 2, height - 2), new(1, height / 2), new(width - 2, height / 2),
    ];

    private static GameState Build(int width, int height, int levels, TerrainType fill, IReadOnlyList<bool> humans, ContentCatalogs? content)
    {
        content ??= ContentCatalogs.Default;
        var map = new AdventureMap(width, height, levels, fill) { Name = "Untitled" };
        for (var level = 1; level < levels; level++)
        {
            foreach (var position in map.Positions(level))
            {
                map.SetTerrain(position, TerrainType.Subterranean);
            }
        }

        var starts = HeroStarts(width, height);
        var players = humans.Select((human, slot) => new Player(new PlayerId(slot), Player.ColourNames[slot], isHuman: human)).ToList();
        var heroes = players.Select((player, slot) => new Hero(new HeroId(slot + 1), $"{player.Name} hero", player.Id, starts[slot])).ToList();
        ReliefShaper.FromTerrain(map);
        return new GameState(map, players, heroes, content: content);
    }
}
