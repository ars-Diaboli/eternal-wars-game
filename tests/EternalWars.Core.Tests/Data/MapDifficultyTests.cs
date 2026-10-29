using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using static EternalWars.Core.Tests.TestWorld;

namespace EternalWars.Core.Tests.Data;

/// <summary>The map's difficulty (ADR-101, map format v26): what "Map default" counts as in the ranking.</summary>
public class MapDifficultyTests
{
    [Fact]
    public void TheEditor_SetsTheDifficultyUndoably()
    {
        var editor = new MapEditorSession(Create("A..", "..B"));

        Assert.Equal(EditResult.Ok, editor.SetScenarioRules(editor.Scenario.Rules with { Difficulty = Complexity.Hard }));
        Assert.Equal(Complexity.Hard, editor.Scenario.Rules.Difficulty);
        Assert.False(editor.Scenario.Rules.IsDefault);

        Assert.True(editor.Undo());
        Assert.Null(editor.Scenario.Rules.Difficulty);
    }

    [Fact]
    public void TheMapFile_KeepsTheDifficulty()
    {
        var state = Create("A..", "..B");
        state.SetRules(state.Rules with { Difficulty = Complexity.Expert });

        var json = MapSerializer.Serialize(MapConverter.ToDocument(state));

        Assert.Contains("\"difficulty\": \"Expert\"", json);
        Assert.Equal(Complexity.Expert, MapConverter.ToGameState(MapSerializer.Deserialize(json)).Rules.Difficulty);
    }

    [Fact]
    public void AMapWithoutADifficulty_LeavesItOutAndOlderMapsLoadWithout()
    {
        var document = MapConverter.ToDocument(Create("A..", "..B"));

        Assert.DoesNotContain("difficulty", MapSerializer.Serialize(document));
        Assert.True(MapDocument.CurrentFormatVersion >= 26);
        Assert.Null(MapConverter.ToGameState(document with { FormatVersion = 25 }).Rules.Difficulty);
    }
}
