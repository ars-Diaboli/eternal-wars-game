using System.Text.Json.Nodes;
using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Records;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.MapEvents;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Data;

/// <summary>
/// The map's difficulty (ADR-101, map format v26) and the game's (ADR-225): the levels Easy … Master; maps before v41, saves before v5 and ranking
/// files before v2 named each level one step lower, and are read with the names moved up.
/// </summary>
public class MapDifficultyTests
{
    [Fact]
    public void TheEditor_SetsTheDifficultyUndoably()
    {
        var editor = TestEditor.World("A..", "..B");

        Assert.Equal(EditResult.Ok, editor.Map.SetRules(editor.Scenario.Rules with { Difficulty = Difficulty.Expert }));
        Assert.Equal(Difficulty.Expert, editor.Scenario.Rules.Difficulty);
        Assert.False(editor.Scenario.Rules.IsDefault);

        Assert.True(editor.Undo());
        Assert.Null(editor.Scenario.Rules.Difficulty);
    }

    [Fact]
    public void TheMapFile_KeepsTheDifficulty()
    {
        var state = Create("A..", "..B");
        state.SetRules(state.Rules with { Difficulty = Difficulty.Master });

        var json = TestFormat.Save(state);

        Assert.Contains("\"difficulty\": \"Master\"", json);
        Assert.Equal(Difficulty.Master, TestFormat.Load(json).Rules.Difficulty);
    }

    [Fact]
    public void AMapWithoutADifficulty_LeavesItOutAndOlderMapsLoadWithout()
    {
        var document = MapConverter.ToDocument(Create("A..", "..B"));

        Assert.DoesNotContain("difficulty", MapSerializer.Serialize(document));
        Assert.True(MapDocument.CurrentFormatVersion >= 26);
        Assert.Null(MapConverter.ToGameState(document with { FormatVersion = 25 }).Rules.Difficulty);
    }

    [Theory]
    [InlineData("Beginner", Difficulty.Easy)]
    [InlineData("Easy", Difficulty.Normal)]
    [InlineData("Normal", Difficulty.Hard)]
    [InlineData("Hard", Difficulty.Expert)]
    [InlineData("Expert", Difficulty.Master)]
    public void AnOlderMapsDifficulty_IsTheLevelAtItsPlace(string old, Difficulty level)
    {
        var json = JsonNode.Parse(MapSerializer.Serialize(MapConverter.ToDocument(Create("A..", "..B"))))!.AsObject();
        json["formatVersion"] = 40;
        json["rules"] = new JsonObject { ["difficulty"] = old };

        Assert.Equal(level, MapConverter.ToGameState(MapSerializer.Deserialize(json.ToJsonString())).Rules.Difficulty);
        Assert.Equal((int)level, Array.IndexOf(["Beginner", "Easy", "Normal", "Hard", "Expert"], old)); // every level keeps its numbers
    }

    [Fact]
    public void AnOlderSave_ReadsItsComplexityAsTheDifficulty_OneNameUp()
    {
        var state = Create("A..", "..B");
        var record = GameReplay.Record(MapConverter.ToDocument(state), new GameSession(state));
        var json = JsonNode.Parse(GameRecordSerializer.Serialize(record))!.AsObject();
        json["formatVersion"] = 4;
        json["options"]!.AsObject().Remove("difficulty");
        json["options"]!["complexity"] = "Easy";

        Assert.Equal(Difficulty.Normal, GameRecordSerializer.Deserialize(json.ToJsonString()).Options.Difficulty);

        json["formatVersion"] = GameRecordDocument.CurrentFormatVersion;
        json["options"]!.AsObject().Remove("complexity");
        json["options"]!["difficulty"] = "Easy";
        Assert.Equal(Difficulty.Easy, GameRecordSerializer.Deserialize(json.ToJsonString()).Options.Difficulty);
    }

    [Fact]
    public void ASaveWithADifficulty_KeepsIt()
    {
        var state = Create("A..", "..B");
        var record = GameReplay.Record(MapConverter.ToDocument(state), new GameSession(state)) with { Options = new GameOptions(Difficulty: Difficulty.Master) };

        Assert.Equal(Difficulty.Master, GameRecordSerializer.Deserialize(GameRecordSerializer.Serialize(record)).Options.Difficulty);
    }

    [Fact]
    public void AnOlderRankingFile_MovesItsNamesUpOne_AndANewOneKeepsThem()
    {
        var row = new JsonObject
        {
            ["player"] = "Ann", ["faction"] = "haven", ["map"] = "Map", ["custom"] = false, ["difficulty"] = "Beginner", ["days"] = 10, ["score"] = 152,
            ["title"] = "Imp", ["date"] = "2026-10-29",
        };
        var old = new JsonObject { ["randomMaps"] = new JsonArray(), ["maps"] = new JsonArray(row) };

        var read = RankingFile.Read(old.ToJsonString());
        Assert.Equal(Difficulty.Easy, Assert.Single(read.Maps).Difficulty);

        var written = RankingFile.Write(read);
        Assert.Contains("\"formatVersion\": 2", written);
        Assert.Equal(Difficulty.Easy, Assert.Single(RankingFile.Read(written).Maps).Difficulty);
    }

    [Fact]
    public void AMapEventsDifficulty_IsTheGamesLevel_ElseTheMapsElseNormal()
    {
        static int Read(GameState state) => MapEventEvaluator.Evaluate(state, default, new QuantityExpr(Quantity.Difficulty));

        Assert.Equal((int)Difficulty.Normal, Read(Create("A..", "..B")));
        Assert.Equal(1, (int)Difficulty.Normal);

        var mapped = Create("A..", "..B");
        mapped.SetRules(mapped.Rules with { Difficulty = Difficulty.Expert });
        Assert.Equal((int)Difficulty.Expert, Read(mapped));

        var chosen = Create(new GameOptions(Difficulty: Difficulty.Easy), "A..", "..B");
        chosen.SetRules(chosen.Rules with { Difficulty = Difficulty.Expert });
        Assert.Equal((int)Difficulty.Easy, Read(chosen));
    }
}
