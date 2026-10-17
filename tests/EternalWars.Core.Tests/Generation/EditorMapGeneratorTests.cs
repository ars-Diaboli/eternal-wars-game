using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The editor's "Generate from template": a generated map as an ordinary, editable scenario (ADR-064, RMG-7).</summary>
public class EditorMapGeneratorTests
{
    private static MapTemplate Duel() => BuiltInTemplates.Find("duel")!;

    private static MapTemplate FreeForAll() => BuiltInTemplates.Find("ffa-4")!;

    [Fact]
    public void Generate_FillsEveryStartSlot_WithAColouredPlayer_TheirTownAndHero_AndDescribesHowItWasMade()
    {
        var map = EditorMapGenerator.Generate(Duel(), 72, 11, towns: new Dictionary<int, string> { [1] = TownIds.Dungeon });
        var scenario = map.Scenario;

        Assert.Equal(["Blue", "Red"], scenario.Players.Select(p => p.Name));
        Assert.All(scenario.Players, p => Assert.True(p.IsHuman));
        Assert.Equal(TownIds.Dungeon, scenario.ObjectsOf<Town>().Single(t => t.Owner == new PlayerId(1)).TownType);
        Assert.Single(scenario.ObjectsOf<Town>(), t => t.Owner == new PlayerId(0));
        Assert.Equal(2, scenario.Heroes.Count);
        Assert.Empty(map.Problems);
        Assert.DoesNotContain(map.Issues, i => i.Severity == IssueSeverity.Error);
        Assert.Contains("\"Duel\" at 72×72, seed 11", scenario.Map.Description, StringComparison.Ordinal);
        Assert.Contains($"generator version {MapGenerator.Version}", scenario.Map.Description, StringComparison.Ordinal);
    }

    [Fact]
    public void Generate_ForFewerPlayers_LeavesTheOtherStartTownsNeutral()
    {
        var scenario = EditorMapGenerator.Generate(FreeForAll(), 96, 3, players: 2).Scenario;

        Assert.Equal(["Blue", "Red"], scenario.Players.Select(p => p.Name));
        var towns = scenario.ObjectsOf<Town>().ToList();
        Assert.Equal(2, towns.Count(t => t.Owner is not null));
        Assert.True(towns.Count(t => t.Owner is null) >= 2);
    }

    [Fact]
    public void Players_AreHumanUpToTheTemplatesLimit()
    {
        var players = EditorMapGenerator.Players(FreeForAll() with { MaxHumans = 1 });

        Assert.Equal([true, false, false, false], players.Select(p => p.IsHuman));
        Assert.All(players, p => Assert.Equal(PlayerSetup.RandomTown, p.TownType));
    }

    [Fact]
    public void Generate_RefusesSizesPlayersAndTownsTheTemplateCannotTake()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EditorMapGenerator.Generate(Duel(), 64, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => EditorMapGenerator.Generate(Duel(), 72, 1, players: 3));
        Assert.Throws<ArgumentException>(() => EditorMapGenerator.Generate(Duel(), 72, 1, players: 1, towns: new Dictionary<int, string> { [1] = TownIds.Haven }));
        Assert.Throws<ArgumentException>(() => EditorMapGenerator.Generate(Duel(), 72, 1, towns: new Dictionary<int, string> { [0] = "nowhere" }));
        Assert.Throws<ArgumentException>(() => EditorMapGenerator.Generate(Duel() with { Zones = [] }, 72, 1));
    }

    [Fact]
    public void Generate_IsDeterministic()
    {
        Assert.Equal(
            GameStateDigest.Compute(EditorMapGenerator.Generate(Duel(), 72, 8).Scenario),
            GameStateDigest.Compute(EditorMapGenerator.Generate(Duel(), 72, 8).Scenario));
    }

    [Fact]
    public void AGeneratedMap_CanBeEdited_Undone_AndSavedAsAMap()
    {
        var scenario = EditorMapGenerator.Generate(Duel(), 72, 5).Scenario;
        var editor = new MapEditorSession(scenario);
        var hero = scenario.Heroes[0];
        var before = scenario.Map.GetTerrain(hero.Position);

        Assert.Equal(1, editor.PaintTerrain(hero.Position, before == TerrainType.Snow ? TerrainType.Sand : TerrainType.Snow));
        Assert.True(editor.Undo());
        Assert.Equal(before, scenario.Map.GetTerrain(hero.Position));
        Assert.Equal(EditResult.Ok, editor.RemoveHero(hero.Id));

        var saved = MapSerializer.Serialize(MapConverter.ToDocument(scenario));
        var loaded = MapConverter.ToGameState(MapSerializer.Deserialize(saved));
        Assert.Equal(saved, MapSerializer.Serialize(MapConverter.ToDocument(loaded)));
        Assert.Equal(scenario.Map.Description, loaded.Map.Description);
        Assert.Single(loaded.Heroes);
    }
}
