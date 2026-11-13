using System.Text.Json;
using System.Text.Json.Nodes;
using EternalWars.Core.Data;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Data;

/// <summary>Map format v39: the ground's height, one character per tile corner (ADR-182); older maps get their terrain's heights when read.</summary>
public class HeightFormatTests
{
    private const string V38 = """
        {
          "formatVersion": 38, "name": "Old", "width": 6, "height": 3,
          "terrain": [ [ "~~gghh", "~~gghh", "~~gg##" ] ],
          "players": [ { "name": "Blue", "isHuman": true } ],
          "objects": []
        }
        """;

    [Fact]
    public void EveryStoredHeight_HasItsOwnCode_ThatJsonDoesNotEscape()
    {
        var heights = Enumerable.Range(ReliefLimits.StoredMin, ReliefLimits.StoredMax - ReliefLimits.StoredMin + 1).ToList();
        var codes = new string([.. heights.Select(HeightCodes.ToCode)]);

        Assert.Equal(heights, codes.Select(HeightCodes.FromCode));
        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.Equal($"\"{codes}\"", JsonSerializer.Serialize(codes, JsonDefaults.Options));
        Assert.Equal('8', HeightCodes.ToCode(ReliefTable.Of(TerrainType.Grass).Base));
        Assert.Throws<FormatException>(() => HeightCodes.FromCode('<'));
    }

    [Fact]
    public void EveryCorner_RoundTrips_OnEveryLevel()
    {
        var state = BlankScenario.Create(5, 3, levels: 2);
        state.Map.SetCorner(0, 0, 0, ReliefLimits.StoredMin);
        state.Map.SetCorner(0, 5, 3, ReliefLimits.StoredMax);
        state.Map.SetCorner(1, 2, 1, -3);
        state.Map.SetCorner(1, 4, 2, 27);

        var json = TestFormat.Save(state);
        var restored = TestFormat.Load(json).Map;

        Assert.Contains("\"heights\"", json);
        for (var level = 0; level < 2; level++)
        {
            for (var cy = 0; cy <= 3; cy++)
            {
                for (var cx = 0; cx <= 5; cx++)
                {
                    Assert.Equal(state.Map.GetCorner(level, cx, cy), restored.GetCorner(level, cx, cy));
                }
            }
        }
    }

    [Fact]
    public void AMapNeverShaped_WritesNoHeights_AndReadsFlat()
    {
        var document = MapConverter.ToDocument(new AdventureMap(4, 3, fill: TerrainType.Snow));

        var map = MapConverter.ToMap(MapSerializer.Deserialize(MapSerializer.Serialize(document)));

        Assert.Null(document.Heights);
        Assert.False(map.HasGroundHeights);
    }

    [Fact]
    public void AV38Map_GetsItsTerrainsBlendedHeights()
    {
        var map = TestFormat.Load(V38).Map;

        var expected = new AdventureMap(map.Width, map.Height);
        foreach (var position in map.Positions(0))
        {
            expected.SetTerrain(position, map.GetTerrain(position));
        }

        ReliefShaper.FromTerrain(expected);
        Assert.True(map.HasGroundHeights);
        AssertSameCorners(expected, map);
        Assert.Equal(0, map.GetCorner(0, 2, 1)); // the shoreline
        Assert.Equal(ReliefLimits.WalkableMax, map.GetCorner(0, 5, 2)); // the cliff's foot
        Assert.Equal(MapDocument.CurrentFormatVersion, MapConverter.ToDocument(map).FormatVersion);
    }

    [Fact]
    public void ASavedGameFromV38_LoadsWithHeights()
    {
        var state = TestFormat.Load(V38);
        var record = GameReplay.Record(MapConverter.ToDocument(state), new GameSession(state));
        var json = JsonNode.Parse(GameRecordSerializer.Serialize(record))!.AsObject();
        json["map"] = JsonNode.Parse(V38);

        var loaded = MapConverter.ToGameState(GameRecordSerializer.Deserialize(json.ToJsonString()).Map).Map;

        AssertSameCorners(state.Map, loaded);
        Assert.True(loaded.HasGroundHeights);
    }

    [Fact]
    public void AHeightLayerOfTheWrongSize_IsRefused()
    {
        var document = MapConverter.ToDocument(BlankScenario.Create(4, 3));

        Assert.Throws<FormatException>(() => MapConverter.ToMap(document with { Heights = [["8888", "8888", "8888", "8888"]] }));
        Assert.Throws<FormatException>(() => MapConverter.ToMap(document with { Heights = [["88888", "88888", "88888"]] }));
        Assert.Throws<FormatException>(() => MapConverter.ToMap(document with { Heights = [["88888", "88888", "88888", "888\"8"]] }));
    }

    private static void AssertSameCorners(AdventureMap expected, AdventureMap actual)
    {
        for (var level = 0; level < expected.Levels; level++)
        {
            for (var cy = 0; cy <= expected.Height; cy++)
            {
                for (var cx = 0; cx <= expected.Width; cx++)
                {
                    Assert.Equal(expected.GetCorner(level, cx, cy), actual.GetCorner(level, cx, cy));
                }
            }
        }
    }
}
