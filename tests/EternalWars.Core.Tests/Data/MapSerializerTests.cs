using EternalWars.Core.Data.Content;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Data;

public class MapSerializerTests
{
    [Fact]
    public void GameState_RoundTripsThroughJson()
    {
        var original = DemoScenario.Create(width: 20, height: 14, seed: 42);
        original.ObjectsOf<Mine>().First().Owner = new PlayerId(1);
        original.Map.SetTerrain(new MapPosition(0, 0, 1), TerrainType.ForestFloor);
        original.Map.SetRoad(new MapPosition(1, 0, 1), RoadType.Cobblestone);
        original.AssignTeams(new Dictionary<PlayerId, TeamId> { [new PlayerId(1)] = new(0) });
        original.SetTeamsLocked(true);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(original));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(original.Map.Name, restored.Map.Name);
        Assert.Equal((20, 14, 2), (restored.Map.Width, restored.Map.Height, restored.Map.Levels));
        for (var level = 0; level < original.Map.Levels; level++)
        {
            Assert.All(original.Map.Positions(level), p =>
                Assert.Equal((original.Map.GetTerrain(p), original.Map.GetRoad(p)), (restored.Map.GetTerrain(p), restored.Map.GetRoad(p))));
        }

        Assert.Equal(original.Players.Select(p => (p.Name, p.Team)), restored.Players.Select(p => (p.Name, p.Team)));
        Assert.True(restored.TeamsLocked);
        Assert.Equal(original.Heroes.Select(h => (h.Id, h.Owner, h.Position)), restored.Heroes.Select(h => (h.Id, h.Owner, h.Position)));
        Assert.Equal(Describe(original.Objects), Describe(restored.Objects));
    }

    [Fact]
    public void MapsWithoutRoads_OmitTheRoadLayer()
    {
        var document = MapConverter.ToDocument(BlankScenario.Create(8, 6));

        Assert.Null(document.Roads);
        Assert.DoesNotContain("\"roads\"", MapSerializer.Serialize(document));
        Assert.Equal(3, document.FormatVersion);
    }

    [Fact]
    public void Version2Map_StillLoads()
    {
        const string json = """
            {
              "formatVersion": 2, "name": "Old", "width": 3, "height": 2, "terrain": [ [ "ggg", "ggg" ] ],
              "players": [ { "name": "Blue", "isHuman": true }, { "name": "Red", "isHuman": false } ],
              "objects": [ { "kind": "gate", "id": 1, "definition": "gate.subterranean", "x": 0, "y": 0, "linkedGate": null } ]
            }
            """;

        var state = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.IsType<TwoWayGate>(Assert.Single(state.Objects));
        Assert.False(state.Map.HasRoads);
        Assert.True(state.AreEnemies(new PlayerId(0), new PlayerId(1)));
    }

    [Fact]
    public void Deserialize_RejectsMalformedRows()
    {
        const string json = """
            { "name": "Bad", "width": 3, "height": 1, "terrain": [ [ "gg" ] ] }
            """;

        Assert.Throws<FormatException>(() => MapConverter.ToMap(MapSerializer.Deserialize(json)));
    }

    [Fact]
    public void Version1Map_WithoutObjects_StillLoads()
    {
        const string json = """
            {
              "formatVersion": 1, "name": "Old", "width": 2, "height": 1, "terrain": [ [ "g~" ] ],
              "players": [ { "name": "Blue", "isHuman": true } ],
              "heroes": [ { "id": 1, "name": "Orrin", "owner": 0, "x": 0, "y": 0 } ]
            }
            """;

        var state = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Empty(state.Objects);
        Assert.Equal(TerrainType.Water, state.Map.GetTerrain(new MapPosition(1, 0)));
    }

    [Fact]
    public void Objects_AcceptDiscriminatorAnywhereAndRejectUnknownDefinitions()
    {
        const string template = """
            {
              "name": "Objects", "width": 3, "height": 1, "terrain": [ [ "ggg" ] ],
              "players": [ { "name": "Blue", "isHuman": true } ],
              "objects": [ { "id": 1, "definition": "DEF", "x": 1, "y": 0, "amount": 9, "kind": "pile" } ]
            }
            """;

        var state = MapConverter.ToGameState(MapSerializer.Deserialize(template.Replace("DEF", "pile.lumber")));
        Assert.Equal(9, Assert.IsType<ResourcePile>(Assert.Single(state.Objects)).Amount);

        Assert.Throws<FormatException>(() => MapConverter.ToGameState(MapSerializer.Deserialize(template.Replace("DEF", "pile.unobtainium"))));
    }

    [Fact]
    public void OverlappingObjects_AreRejected()
    {
        var state = BlankScenario.Create(8, 6);
        var document = MapConverter.ToDocument(state) with
        {
            Objects =
            [
                new ResourcePileDocument { Id = 1, Definition = "pile.gold", X = 4, Y = 4, Amount = 1 },
                new TreasureChestDocument { Id = 2, Definition = "chest", X = 4, Y = 4, Gold = 1, Experience = 1 },
            ],
        };

        Assert.Throws<FormatException>(() => MapConverter.ToGameState(document));
    }

    [Fact]
    public void TerrainRows_AreHumanReadable()
    {
        var state = DemoScenario.Create(withUnderground: false);
        state.Map.SetTerrain(new MapPosition(0, 1), TerrainType.Water);
        state.Map.SetTerrain(new MapPosition(1, 1), TerrainType.Rock);

        var document = MapConverter.ToDocument(state);

        Assert.Single(document.Terrain);
        Assert.All(document.Terrain[0], row => Assert.Equal(24, row.Length));
        Assert.StartsWith("~#", document.Terrain[0][1]);
    }

    [Fact]
    public void ObjectDefinitions_RoundTripAndExtendCatalog()
    {
        var json = MapObjectDefinitionSerializer.Serialize(MapObjectCatalog.Default.All);
        var definitions = MapObjectDefinitionSerializer.Deserialize(json);

        Assert.Equal(MapObjectCatalog.Default.All.Select(d => d.Id), definitions.Select(d => d.Id));
        Assert.Contains("\"kind\": \"Mine\"", json);

        var modded = MapObjectCatalog.Default.With(MapObjectDefinitionSerializer.Deserialize("""
            [ { "id": "mine.gold", "name": "Deep gold mine", "kind": "Mine", "footprint": ["BBBB", "BBVB"], "resource": "Gold", "dailyYield": 1500 } ]
            """));
        Assert.Equal(1500, modded.Get("mine.gold").DailyYield);
        Assert.Equal((2, 1), modded.Get("mine.gold").Footprint.VisitOffset);
        Assert.Equal(1000, MapObjectCatalog.Default.Get("mine.gold").DailyYield);
    }

    [Fact]
    public void InvalidDefinitions_AreRejected()
    {
        Assert.Throws<FormatException>(() => MapObjectDefinitionSerializer.Deserialize("""
            [ { "id": "mine.nothing", "name": "Empty", "kind": "Mine", "footprint": ["V"] } ]
            """));
        Assert.Throws<FormatException>(() => new Footprint(["BBB"]));
    }

    private static IEnumerable<string> Describe(IEnumerable<MapObject> objects) =>
        objects.Select(o => o switch
        {
            ResourcePile p => $"{p.Id} {p.Definition.Id} {p.Position} {p.Amount}",
            Mine m => $"{m.Id} {m.Definition.Id} {m.Position} {m.Owner}",
            TreasureChest c => $"{c.Id} {c.Definition.Id} {c.Position} {c.Gold}/{c.Experience}",
            TwoWayGate g => $"{g.Id} {g.Definition.Id} {g.Position} <-> {g.LinkedGate}",
            OneWayGateEntrance e => $"{e.Id} {e.Definition.Id} {e.Position} -> {e.Exit}",
            OneWayGateExit x => $"{x.Id} {x.Definition.Id} {x.Position}",
            _ => o.ToString()!,
        });
}
