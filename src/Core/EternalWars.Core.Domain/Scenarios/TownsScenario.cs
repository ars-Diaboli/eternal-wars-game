using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Players;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Domain.Scenarios;

/// <summary>
/// Sandbox for towns (ADR-046): a Haven town for Blue and an Inferno town for Red, each with its town hall, fort,
/// two tier-1 dwellings and a gold mine; a garrisoned neutral Necropolis town between them, and a road joining all three.
/// </summary>
public static class TownsScenario
{
    public const int Width = 32;
    public const int Height = 22;

    public static GameState Create(CreatureCatalog? creatures = null, HeroCatalog? heroRoster = null, BuildingCatalog? buildings = null)
    {
        var map = new AdventureMap(Width, Height, 1) { Name = "Two Towns" };
        for (var x = 1; x < Width - 1; x++)
        {
            map.SetRoad(new MapPosition(x, 10), RoadType.Dirt);
        }

        foreach (var position in map.Positions(0).Where(p => (p.X * 7 + p.Y * 3) % 23 == 0 && p.Y is not (9 or 10 or 11)))
        {
            map.SetTerrain(position, TerrainType.Dirt);
        }

        var players = new[]
        {
            new Player(new PlayerId(0), "Blue", isHuman: true),
            new Player(new PlayerId(1), "Red", isHuman: true),
        };
        var heroes = new[]
        {
            new Hero(new HeroId(1), "Orrin", players[0].Id, new MapPosition(4, 9),
                Army.Of(new(BuiltInCreatures.Pikeman, 20), new(BuiltInCreatures.Archer, 10)), attack: 2, defense: 1),
            new Hero(new HeroId(2), "Sandro", players[1].Id, new MapPosition(Width - 5, 9),
                Army.Of(new(BuiltInCreatures.HornedDemon, 22), new(BuiltInCreatures.HellHound, 8)), attack: 1, defense: 2),
        };

        var catalog = MapObjectCatalog.Default;
        var objects = new List<MapObject>();
        var lines = creatures ?? CreatureCatalog.Default;

        var blue = Town(TownIds.Haven, new MapPosition(1, 2), players[0].Id, "Castlebridge");
        var red = Town(TownIds.Inferno, new MapPosition(Width - 8, 2), players[1].Id, "Ashgate");
        foreach (var town in new[] { blue, red })
        {
            foreach (var line in lines.BaseCreaturesOf(town.TownType).Where(l => l.Tier == 1))
            {
                town.SetDwelling(line.Id, 1);
                town.SetAvailable(line.Id, TownRules.WeeklyGrowth(town, line));
            }
        }

        var neutral = Town(TownIds.Necropolis, new MapPosition((Width / 2) - 3, 12), null, "Grimhollow");
        neutral.Garrison.Set(0, new CreatureStack("necropolis.skeleton", 24));
        neutral.Garrison.Set(1, new CreatureStack("necropolis.ghoul", 6));

        Mine(ResourceType.Gold, new MapPosition(1, 12), players[0].Id);
        Mine(ResourceType.Gold, new MapPosition(Width - 4, 12), players[1].Id);
        Mine(ResourceType.Lumber, new MapPosition(10, 2), null);
        Mine(ResourceType.Ore, new MapPosition(Width - 13, 2), null);
        Place(BuiltInMapObjects.PileId(ResourceType.Lumber), new MapPosition(8, 8));
        Place(BuiltInMapObjects.PileId(ResourceType.Ore), new MapPosition(Width - 9, 8));
        var wolves = (MonsterGuard)Place(BuiltInMapObjects.Monster, new MapPosition(Width / 2, 6));
        (wolves.Creature, wolves.Count, wolves.Disposition) = (BuiltInCreatures.Wolf, 15, GuardDisposition.Aggressive);

        return new GameState(map, players, heroes, objects, creatures: creatures, heroRoster: heroRoster, buildings: buildings);

        Town Town(string type, MapPosition topLeft, PlayerId? owner, string name)
        {
            var town = (Town)Place(BuiltInMapObjects.TownId(type), topLeft);
            (town.Owner, town.Name) = (owner, name);
            return town;
        }

        void Mine(ResourceType resource, MapPosition topLeft, PlayerId? owner) =>
            ((Mine)Place(BuiltInMapObjects.MineId(resource), topLeft)).Owner = owner;

        MapObject Place(string definitionId, MapPosition topLeft)
        {
            var mapObject = MapObject.Create(new MapObjectId(objects.Count + 1), catalog.Get(definitionId), topLeft);
            objects.Add(mapObject);
            return mapObject;
        }
    }
}
