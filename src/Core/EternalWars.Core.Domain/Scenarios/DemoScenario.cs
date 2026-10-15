using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>
/// Small deterministic sandbox used by the game, the 2D harness and tests until real maps exist:
/// two players, resource piles, mines, a chest, and an underground level reached through linked gates.
/// </summary>
public static class DemoScenario
{
    public static GameState Create(int width = 24, int height = 18, int seed = 1, bool withUnderground = true)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 16);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 12);

        var map = new AdventureMap(width, height, withUnderground ? 2 : 1) { Name = "Demo Valley" };
        var random = new Random(seed);
        foreach (var position in map.Positions(level: 0))
        {
            map.SetTerrain(position, random.Next(100) switch
            {
                < 6 => TerrainType.Water,
                < 10 => TerrainType.Rock,
                < 18 => TerrainType.Rough,
                < 24 => TerrainType.Sand,
                < 28 => TerrainType.Swamp,
                < 40 => TerrainType.Dirt,
                _ => TerrainType.Grass,
            });
        }

        if (withUnderground)
        {
            foreach (var position in map.Positions(level: 1))
            {
                map.SetTerrain(position, random.Next(100) < 25 ? TerrainType.Rock : TerrainType.Subterranean);
            }
        }

        var players = new[]
        {
            new Player(new PlayerId(0), "Blue", isHuman: true),
            new Player(new PlayerId(1), "Red", isHuman: true),
        };

        var blueStart = new MapPosition(2, 2);
        var redStart = new MapPosition(width - 3, height - 3);
        var heroes = new[]
        {
            new Hero(new HeroId(1), "Orrin", players[0].Id, blueStart),
            new Hero(new HeroId(2), "Sandro", players[1].Id, redStart),
        };

        var catalog = MapObjectCatalog.Default;
        var objects = new List<MapObject>();
        Place(BuiltInMapObjects.PileId(ResourceType.Lumber), new MapPosition(4, 3));
        Place(BuiltInMapObjects.PileId(ResourceType.Gold), new MapPosition(3, 5));
        Place(BuiltInMapObjects.PileId(ResourceType.Ore), redStart.Offset(-2, -1));
        Place(BuiltInMapObjects.PileId(ResourceType.Sulfur), redStart.Offset(-1, -3));
        Place(BuiltInMapObjects.MineId(ResourceType.Lumber), new MapPosition(6, 1));
        Place(BuiltInMapObjects.MineId(ResourceType.Ore), redStart.Offset(-6, -1));
        Place(BuiltInMapObjects.MineId(ResourceType.Gold), new MapPosition((width / 2) - 1, (height / 2) - 1));
        Place(BuiltInMapObjects.TreasureChest, new MapPosition(width / 2, 2));

        if (withUnderground)
        {
            var surfaceGate = Place(BuiltInMapObjects.SubterraneanGate, new MapPosition(2, height - 4));
            var undergroundGate = Place(BuiltInMapObjects.SubterraneanGate, new MapPosition(2, height - 4, 1));
            ((SubterraneanGate)surfaceGate).LinkedGate = undergroundGate.Id;
            ((SubterraneanGate)undergroundGate).LinkedGate = surfaceGate.Id;
            Place(BuiltInMapObjects.MineId(ResourceType.Crystal), new MapPosition(width - 6, 2, 1));
            Place(BuiltInMapObjects.PileId(ResourceType.Mercury), new MapPosition(5, height - 5, 1));
        }

        // Starting tiles and object surroundings must be walkable whatever the random terrain was.
        foreach (var hero in heroes)
        {
            MakeWalkable(hero.Position);
        }

        return new GameState(map, players, heroes, objects);

        MapObject Place(string definitionId, MapPosition topLeft)
        {
            var mapObject = MapObject.Create(new MapObjectId(objects.Count + 1), catalog.Get(definitionId), topLeft);
            var footprint = mapObject.Definition.Footprint;
            for (var dy = -1; dy <= footprint.Height; dy++)
            {
                for (var dx = -1; dx <= footprint.Width; dx++)
                {
                    MakeWalkable(topLeft.Offset(dx, dy));
                }
            }

            objects.Add(mapObject);
            return mapObject;
        }

        void MakeWalkable(MapPosition position)
        {
            if (map.Contains(position) && !map.GetTerrain(position).IsPassableOnFoot())
            {
                map.SetTerrain(position, position.Level == 0 ? TerrainType.Grass : TerrainType.Subterranean);
            }
        }
    }
}
