using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
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
/// The shipped two-player map of the vertical slice (ADR-114): <b>Ember March</b>, 36 × 30, Haven (Blue, a person) against Inferno (Red, the
/// computer). Each side has its town on native ground, a gold, lumber and ore mine, its rare-resource mine behind a guard, a map dwelling and some
/// loot. A rock ridge splits the map; the road crosses it through the guarded middle pass, and a narrow northern pass holds a Major artifact and
/// a stronger guard. A witch hut and a sphinx sit by the passes. Default victory: defeat the enemy (no towns and no heroes).
/// </summary>
public static class EmberMarchScenario
{
    public const string Name = "Ember March";

    public const int Width = 36;
    public const int Height = 30;

    /// <summary>The middle and northern gaps in the ridge (columns <see cref="RidgeLeft"/> and <see cref="RidgeLeft"/> + 1).</summary>
    public const int RidgeLeft = 17;

    public static GameState Create(ContentCatalogs? content = null)
    {
        content ??= ContentCatalogs.Default;
        var lines = content.Creatures;
        var roster = content.Heroes;
        var map = new AdventureMap(Width, Height, 1)
        {
            Name = Name,
            Description = "Haven holds the green west, Inferno the burning east. One road crosses the ridge; a narrow pass in the north guards a relic of the old war. Defeat the other side.",
        };

        foreach (var position in map.Positions(0))
        {
            var (x, y) = (position.X, position.Y);
            var terrain = x < 13 ? TerrainType.Grass : x > 22 ? TerrainType.Lava : TerrainType.Dirt;
            if ((x * 5 + y * 7) % 19 == 0 && x is > 1 and < Width - 2)
            {
                terrain = x < 13 ? TerrainType.Highlands : x > 22 ? TerrainType.Wasteland : TerrainType.Rough;
            }

            // The ridge: rock, with the northern pass (rows 3–4) and the middle pass (rows 14–16, all in the wolves' zone of control).
            if (x is RidgeLeft or RidgeLeft + 1 && y is not (3 or 4) and not (>= 14 and <= 16))
            {
                terrain = TerrainType.Rock;
            }

            map.SetTerrain(position, terrain);
        }

        // The road: from Castlebridge's gate south to row 15, then east through the middle pass to the square below Ashgate's gate.
        for (var y = 9; y <= 15; y++)
        {
            map.SetRoad(new MapPosition(5, y), RoadType.Cobblestone);
        }

        for (var x = 5; x <= 30; x++)
        {
            map.SetRoad(new MapPosition(x, 15), x is >= 13 and <= 22 ? RoadType.Dirt : RoadType.Cobblestone);
        }


        var players = new[]
        {
            new Player(new PlayerId(0), "Blue", isHuman: true),
            new Player(new PlayerId(1), "Red", isHuman: false),
        };

        var catalog = MapObjectCatalog.Default;
        var objects = new List<MapObject>();

        // Towns: entrance at top-left + (3, 6). Castlebridge's gate is (5, 8), Ashgate's (30, 14); both heroes start on the road below.
        var blue = Town(TownIds.Haven, new MapPosition(2, 2), players[0].Id, "Castlebridge");
        var red = Town(TownIds.Inferno, new MapPosition(27, 8), players[1].Id, "Ashgate");
        foreach (var town in new[] { blue, red })
        {
            foreach (var line in lines.BaseCreaturesOf(town.TownType).Where(l => l.Tier == 1))
            {
                town.SetDwelling(line.Id, 1);
                town.SetAvailable(line.Id, TownRules.WeeklyGrowth(content.Buildings, town, line));
            }
        }

        var heroes = new[]
        {
            Hero.Create(new HeroId(1), players[0].Id, new MapPosition(5, 9),
                Army.Of(new(BuiltInCreatures.Pikeman, 18), new(BuiltInCreatures.Archer, 8)), roster.Get("haven.gerrit")),
            Hero.Create(new HeroId(2), players[1].Id, new MapPosition(30, 15),
                Army.Of(new(BuiltInCreatures.HornedDemon, 20), new(BuiltInCreatures.HellHound, 7)), roster.Get("inferno.zarduk")),
        };

        // Each side: gold, lumber and ore near home, its rare resource behind a guard.
        Mine(ResourceType.Gold, new MapPosition(2, 18), players[0].Id);
        Mine(ResourceType.Lumber, new MapPosition(10, 3), null);
        Mine(ResourceType.Ore, new MapPosition(9, 11), null);
        Mine(ResourceType.Crystal, new MapPosition(3, 25), null, new GuardStack(BuiltInCreatures.Wolf, 14));
        Mine(ResourceType.Gold, new MapPosition(31, 3), players[1].Id);
        Mine(ResourceType.Lumber, new MapPosition(24, 25), null);
        Mine(ResourceType.Ore, new MapPosition(23, 10), null);
        Mine(ResourceType.Mercury, new MapPosition(32, 24), null, new GuardStack(BuiltInCreatures.Wolf, 14));

        // Shared rare mines in the middle, south of the pass.
        Mine(ResourceType.Sulfur, new MapPosition(14, 21), null, new GuardStack("neutral.bandit", 10));
        Mine(ResourceType.Crystal, new MapPosition(20, 7), null, new GuardStack("neutral.bandit", 10));

        // Map dwellings of the side's tier-2 line (base creatures, ADR-073).
        Dwelling("haven.archer", new MapPosition(10, 20));
        Dwelling("inferno.hell-hound", new MapPosition(24, 4));

        // Loot on each side.
        Place(BuiltInMapObjects.PileId(ResourceType.Lumber), new MapPosition(8, 9));
        Place(BuiltInMapObjects.PileId(ResourceType.Ore), new MapPosition(3, 13));
        Place(BuiltInMapObjects.PileId(ResourceType.Gold), new MapPosition(11, 16));
        Place(BuiltInMapObjects.TreasureChest, new MapPosition(7, 24));
        Place(BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), new MapPosition(12, 7));
        Place(BuiltInMapObjects.Campfire, new MapPosition(1, 22));
        Place(BuiltInMapObjects.PileId(ResourceType.Lumber), new MapPosition(27, 20));
        Place(BuiltInMapObjects.PileId(ResourceType.Ore), new MapPosition(34, 18));
        Place(BuiltInMapObjects.PileId(ResourceType.Gold), new MapPosition(24, 14));
        Place(BuiltInMapObjects.TreasureChest, new MapPosition(27, 5));
        Place(BuiltInMapObjects.ArtifactId(ArtifactTier.Minor), new MapPosition(23, 22));
        Place(BuiltInMapObjects.Campfire, new MapPosition(34, 27));

        // The passes: the middle one is guarded on the road; the northern one hides a Major artifact behind stronger troops.
        Monster(BuiltInCreatures.Wolf, 24, new MapPosition(RidgeLeft, 15), GuardDisposition.Aggressive);
        Monster("neutral.troll", 8, new MapPosition(RidgeLeft + 1, 3), GuardDisposition.Hostile);
        Place(BuiltInMapObjects.ArtifactId(ArtifactTier.Major), new MapPosition(RidgeLeft, 4));

        // Learning by the passes.
        var hut = (WitchHut)Place(BuiltInMapObjects.WitchHut, new MapPosition(13, 12));
        hut.Skills = [BuiltInSkills.Logistics, BuiltInSkills.Armor];
        var sphinx = (Sphinx)Place(BuiltInMapObjects.Sphinx, new MapPosition(21, 17));
        sphinx.Riddle = new Riddle("I burn without fire and fall without wings. What am I?", "A falling star", "A torch", "A hawk");
        Place(BuiltInMapObjects.LearningStone, new MapPosition(14, 26));
        Place(BuiltInMapObjects.LearningStone, new MapPosition(21, 26));

        return new GameState(map, players, heroes, objects, content: content);

        Town Town(string type, MapPosition topLeft, PlayerId? owner, string name)
        {
            var town = (Town)Place(BuiltInMapObjects.TownId(type), topLeft);
            (town.Owner, town.Name) = (owner, name);
            return town;
        }

        void Mine(ResourceType resource, MapPosition topLeft, PlayerId? owner, GuardStack? guard = null)
        {
            var mine = (Mine)Place(BuiltInMapObjects.MineId(resource), topLeft);
            (mine.Owner, mine.Guard) = (owner, guard);
        }

        void Dwelling(string creature, MapPosition topLeft) => ((CreatureDwelling)Place(BuiltInMapObjects.Dwelling, topLeft)).Creature = creature;

        void Monster(string creature, int count, MapPosition at, GuardDisposition disposition)
        {
            var monster = (MonsterGuard)Place(BuiltInMapObjects.Monster, at);
            (monster.Creature, monster.Count, monster.Disposition) = (creature, count, disposition);
        }

        MapObject Place(string definitionId, MapPosition topLeft)
        {
            var mapObject = MapObject.Create(new MapObjectId(objects.Count + 1), catalog.Get(definitionId), topLeft);
            objects.Add(mapObject);
            return mapObject;
        }
    }
}
