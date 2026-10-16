using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>Starting point for a new map in the editor: plain terrain, two players in opposite corners.</summary>
public static class BlankScenario
{
    public static GameState Create(int width, int height, int levels = 1, TerrainType fill = TerrainType.Grass, CreatureCatalog? creatures = null, HeroCatalog? heroRoster = null, Towns.BuildingCatalog? buildings = null)
    {
        var map = new AdventureMap(width, height, levels, fill) { Name = "Untitled" };
        for (var level = 1; level < levels; level++)
        {
            foreach (var position in map.Positions(level))
            {
                map.SetTerrain(position, TerrainType.Subterranean);
            }
        }

        var players = new[]
        {
            new Player(new PlayerId(0), "Blue", isHuman: true),
            new Player(new PlayerId(1), "Red", isHuman: true),
        };
        var heroes = new[]
        {
            new Hero(new HeroId(1), "Blue hero", players[0].Id, new MapPosition(1, 1)),
            new Hero(new HeroId(2), "Red hero", players[1].Id, new MapPosition(width - 2, height - 2)),
        };
        return new GameState(map, players, heroes, creatures: creatures, heroRoster: heroRoster, buildings: buildings);
    }
}
