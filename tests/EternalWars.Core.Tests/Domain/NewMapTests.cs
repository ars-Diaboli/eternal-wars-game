using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The new-map wizard's maps and the editor's sample maps (ADR-259).</summary>
public class NewMapTests
{
    [Fact]
    public void TheWizardsSettings_AreChecked()
    {
        var fine = NewMapSettings.Default(72);
        Assert.Null(fine.Problem());
        Assert.NotNull((fine with { Width = MapSizes.MinDimension - 1 }).Problem());
        Assert.NotNull((fine with { Height = MapSizes.MaxDimension + 1 }).Problem());
        Assert.NotNull((fine with { Levels = 3 }).Problem());
        Assert.NotNull((fine with { Levels = 0 }).Problem());
        Assert.NotNull((fine with { Terrain = TerrainType.Water }).Problem());
        Assert.NotNull((fine with { Terrain = TerrainType.Rock }).Problem());
        Assert.NotNull((fine with { Terrain = TerrainType.Subterranean }).Problem());
        Assert.NotNull((fine with { Humans = [] }).Problem());
        Assert.NotNull((fine with { Humans = [.. Enumerable.Repeat(true, 9)] }).Problem());
        Assert.Null((fine with { Width = 16, Height = 256, Levels = 2, Terrain = TerrainType.Snow, Humans = [true] }).Problem());
        Assert.Throws<ArgumentException>(() => BlankScenario.Create(fine with { Levels = 3 }));
    }

    [Fact]
    public void ABlankMap_HasTheSizeLevelsGroundAndPlayersChosen_EachWithAHero()
    {
        var settings = new NewMapSettings(40, 24, 2, TerrainType.Sand, [true, false, true, false, false, true, true, false]);
        var scenario = BlankScenario.Create(settings);

        Assert.Equal((40, 24, 2), (scenario.Map.Width, scenario.Map.Height, scenario.Map.Levels));
        Assert.Equal(TerrainType.Sand, scenario.Map.GetTerrain(new MapPosition(20, 12)));
        Assert.Equal(TerrainType.Subterranean, scenario.Map.GetTerrain(new MapPosition(20, 12, 1)));
        Assert.Equal(settings.Humans, scenario.Players.Select(p => p.IsHuman));
        Assert.Equal(["Blue", "Red", "Yellow", "Green", "Orange", "Purple", "Teal", "Pink"], scenario.Players.Select(p => p.Name));
        Assert.Equal(8, scenario.Heroes.Count);
        Assert.Equal(8, scenario.Heroes.Select(h => h.Position).Distinct().Count());
        Assert.All(scenario.Heroes, h => Assert.True(scenario.Map.Contains(h.Position)));
        Assert.Equal(scenario.Players.Select(p => p.Id), scenario.Heroes.Select(h => h.Owner));
    }

    [Fact]
    public void TheHeroStarts_AreApart_OnTheSmallestMap()
    {
        var starts = BlankScenario.HeroStarts(MapSizes.MinDimension, MapSizes.MinDimension);
        Assert.Equal(8, starts.Distinct().Count());
        Assert.All(starts, s => Assert.InRange(s.X, 1, MapSizes.MinDimension - 2));
    }

    [Fact]
    public void ATemplatesSize_IsTheOneNearestTheChosen()
    {
        Assert.Equal(72, MapSizes.Nearest([48, 72, 96], 80));
        Assert.Equal(72, MapSizes.Nearest([48, 72, 96], 84)); // as near as 96: the smaller
        Assert.Equal(96, MapSizes.Nearest([48, 72, 96], 256));
        Assert.Equal(48, MapSizes.Nearest([96, 48], 16));
        Assert.Throws<ArgumentException>(() => MapSizes.Nearest([], 16));
    }

    [Fact]
    public void TheWizardsKinds_MakeGeneratedPlayersComputerOnes()
    {
        var duel = BuiltInTemplates.Find("duel")!;
        var scenario = EditorMapGenerator.Generate(duel, 72, 11, humans: [true, false]).Scenario;

        Assert.Equal([true, false], scenario.Players.Select(p => p.IsHuman));
        Assert.Equal([false, true], EditorMapGenerator.Players(duel, humans: [false]).Select(p => p.IsHuman)); // a slot it does not reach keeps its default
    }

    [Fact]
    public void TheWizardsKinds_KeepAnAiOnlySlotAComputerOne_AndAComputerSlotLeavesTheHumanLimit()
    {
        var ffa = BuiltInTemplates.Find("ffa-4")!;
        var aiOnly = ffa with { Zones = [.. ffa.Zones.Select(z => z.Owner == 1 && z.Type == ZoneType.PlayerStart ? z with { Type = ZoneType.AiStart } : z)] };

        Assert.Equal([true, false, true, true], EditorMapGenerator.Players(aiOnly, humans: [true, true, true, true]).Select(p => p.IsHuman));
        Assert.Equal([false, true, true, false], EditorMapGenerator.Players(ffa with { MaxHumans = 2 }, humans: [false, true, true, true]).Select(p => p.IsHuman));
    }

    [Theory]
    [InlineData(16, 256)]
    [InlineData(256, 16)]
    [InlineData(17, 19)]
    public void ABlankMap_TakesOneToEightPlayers_OnAnyAllowedSize(int width, int height)
    {
        for (var players = 1; players <= 8; players++)
        {
            var scenario = BlankScenario.Create(new NewMapSettings(width, height, 1, TerrainType.Grass, [.. Enumerable.Repeat(true, players)]));
            Assert.Equal(players, scenario.Heroes.Select(h => h.Position).Distinct().Count());
            Assert.All(scenario.Heroes, h => Assert.True(scenario.Map.Contains(h.Position)));
        }
    }

    [Fact]
    public void EverySampleMap_GoesThroughTheMapFormat_AndOpensInTheEditor()
    {
        Assert.NotEmpty(SampleMaps.All);
        Assert.Equal(SampleMaps.All.Count, SampleMaps.All.Select(s => s.Name).Distinct().Count());
        foreach (var sample in SampleMaps.All)
        {
            var scenario = MapConverter.ToGameState(MapConverter.ToDocument(sample.Create(null)));
            var editor = new MapEditorSession(scenario);
            Assert.False(editor.History.CanUndo);
            Assert.NotEmpty(scenario.Players);
            Assert.False(string.IsNullOrWhiteSpace(sample.About));
        }
    }
}
