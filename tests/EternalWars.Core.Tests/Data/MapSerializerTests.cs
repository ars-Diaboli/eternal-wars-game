using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Data;

public class MapSerializerTests
{
    [Fact]
    public void GameState_RoundTripsThroughJson()
    {
        var original = DemoScenario.Create(width: 10, height: 6, seed: 42);

        var json = MapSerializer.Serialize(MapConverter.ToDocument(original));
        var restored = MapConverter.ToGameState(MapSerializer.Deserialize(json));

        Assert.Equal(original.Map.Name, restored.Map.Name);
        Assert.Equal((10, 6), (restored.Map.Width, restored.Map.Height));
        Assert.All(original.Map.Positions(0), p => Assert.Equal(original.Map.GetTerrain(p), restored.Map.GetTerrain(p)));
        Assert.Equal(original.Players.Select(p => p.Name), restored.Players.Select(p => p.Name));
        Assert.Equal(original.Heroes.Select(h => (h.Id, h.Owner, h.Position)), restored.Heroes.Select(h => (h.Id, h.Owner, h.Position)));
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
    public void TerrainRows_AreHumanReadable()
    {
        var state = DemoScenario.Create(width: 6, height: 4);
        state.Map.SetTerrain(new MapPosition(0, 1), TerrainType.Water);
        state.Map.SetTerrain(new MapPosition(1, 1), TerrainType.Rock);

        var document = MapConverter.ToDocument(state);

        Assert.Equal(4, document.Terrain[0].Count);
        Assert.All(document.Terrain[0], row => Assert.Equal(6, row.Length));
        Assert.StartsWith("~#", document.Terrain[0][1]);
    }
}
