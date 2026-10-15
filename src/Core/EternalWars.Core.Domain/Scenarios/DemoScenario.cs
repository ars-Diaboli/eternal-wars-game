using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>
/// Small deterministic sandbox used by the game, the 2D harness and tests until real maps exist.
/// </summary>
public static class DemoScenario
{
    public static GameState Create(int width = 24, int height = 18, int seed = 1)
    {
        var map = new AdventureMap(width, height) { Name = "Demo Valley" };
        var random = new Random(seed);

        foreach (var position in map.Positions(level: 0))
        {
            var roll = random.Next(100);
            var terrain = roll switch
            {
                < 6 => TerrainType.Water,
                < 10 => TerrainType.Rock,
                < 18 => TerrainType.Rough,
                < 24 => TerrainType.Sand,
                < 28 => TerrainType.Swamp,
                < 40 => TerrainType.Dirt,
                _ => TerrainType.Grass,
            };
            map.SetTerrain(position, terrain);
        }

        var blueStart = new MapPosition(2, 2);
        var redStart = new MapPosition(width - 3, height - 3);
        map.SetTerrain(blueStart, TerrainType.Grass);
        map.SetTerrain(redStart, TerrainType.Grass);

        var players = new[]
        {
            new Player(new PlayerId(0), "Blue", isHuman: true),
            new Player(new PlayerId(1), "Red", isHuman: true),
        };

        var heroes = new[]
        {
            new Hero(new HeroId(1), "Orrin", players[0].Id, blueStart),
            new Hero(new HeroId(2), "Sandro", players[1].Id, redStart),
        };

        return new GameState(map, players, heroes);
    }
}
