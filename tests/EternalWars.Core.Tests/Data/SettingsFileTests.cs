using EternalWars.Core.Data.Settings;
using EternalWars.Core.Domain.Settings;

namespace EternalWars.Core.Tests.Data;

/// <summary>The speed, interface and layout settings and their file (ADR-090, UI-1; ADR-154; ADR-156).</summary>
public class SettingsFileTests
{
    [Fact]
    public void TheSettings_RoundTripThroughTheFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ew-settings-{Guid.NewGuid():N}", SettingsFile.FileName);
        var settings = new SpeedSettings(AnimationSpeed.Fast, AnimationSpeed.Slow, ShowComputerHeroes: false, Battle: AnimationSpeed.Instant);
        try
        {
            SettingsFile.Save(path, settings);

            Assert.Equal(settings, SettingsFile.Load(path));
            Assert.Contains("\"ownHeroes\": \"Fast\"", File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void DetailedTooltips_AreKeptBesideTheSpeeds_AndOffWhenTheFileHasNone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ew-settings-{Guid.NewGuid():N}", SettingsFile.FileName);
        var speeds = new SpeedSettings(Battle: AnimationSpeed.Fast);
        try
        {
            SettingsFile.Save(path, speeds, new InterfaceSettings(DetailedTooltips: true));

            Assert.True(SettingsFile.LoadInterface(path).DetailedTooltips);
            Assert.Equal(speeds, SettingsFile.Load(path));
            Assert.False(SettingsFile.ReadInterface("{\"speed\": {\"battle\": \"Slow\"}}").DetailedTooltips);
            Assert.Equal(InterfaceSettings.Default, SettingsFile.ReadInterface("not json"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void TheGrid_IsKeptWithTheInterfaceSettings_AndOffWhenTheFileHasNone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ew-settings-{Guid.NewGuid():N}", SettingsFile.FileName);
        try
        {
            SettingsFile.Save(path, SpeedSettings.Default, new InterfaceSettings(Fog: FogLook.Clouds, ShowGrid: true));

            Assert.True(SettingsFile.LoadInterface(path).ShowGrid);
            Assert.Equal(FogLook.Clouds, SettingsFile.LoadInterface(path).Fog);
            Assert.Contains("\"showGrid\": true", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.False(InterfaceSettings.Default.ShowGrid);
            Assert.False(SettingsFile.ReadInterface("{\"interface\": {\"fog\": \"Clouds\"}}").ShowGrid);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void TheFogLook_IsKeptWithTheInterfaceSettings_AndDarkWhenTheFileHasNone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ew-settings-{Guid.NewGuid():N}", SettingsFile.FileName);
        try
        {
            SettingsFile.Save(path, SpeedSettings.Default, new InterfaceSettings(Fog: FogLook.Clouds));

            Assert.Equal(FogLook.Clouds, SettingsFile.LoadInterface(path).Fog);
            Assert.Contains("\"fog\": \"Clouds\"", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal(FogLook.Dark, SettingsFile.ReadInterface("{\"interface\": {\"detailedTooltips\": true}}").Fog);
            Assert.True(SettingsFile.ReadInterface("{\"interface\": {\"detailedTooltips\": true}}").DetailedTooltips);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void TheLayout_IsKeptBesideTheOtherSettings_AndTheDefaultWhenTheFileHasNone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ew-settings-{Guid.NewGuid():N}", SettingsFile.FileName);
        var layout = new LayoutSettings(ScreenEdge.Bottom, ScreenCorner.BottomLeft, ListOrientation.Horizontal, ScreenSide.Left);
        try
        {
            SettingsFile.Save(path, SpeedSettings.Default, new InterfaceSettings(DetailedTooltips: true), layout);

            Assert.Equal(layout, SettingsFile.LoadLayout(path));
            Assert.True(SettingsFile.LoadInterface(path).DetailedTooltips);
            Assert.Contains("\"minimap\": \"BottomLeft\"", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal(LayoutSettings.Default, SettingsFile.ReadLayout("{\"speed\": {\"battle\": \"Slow\"}}"));
            Assert.Equal(LayoutSettings.Default with { InitiativeBar = ScreenEdge.Bottom }, SettingsFile.ReadLayout("{\"layout\": {\"initiativeBar\": \"Bottom\"}}"));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void TheDefaultLayout_IsTheMinimapListsAndActionsInTheTopRightStack()
    {
        var layout = LayoutSettings.Default;

        Assert.Equal([HudPart.Minimap, HudPart.Lists, HudPart.Actions], HudArrangement.Stack(layout, ScreenCorner.TopRight));
        Assert.Empty(HudArrangement.Stack(layout, ScreenCorner.TopLeft));
        Assert.Empty(HudArrangement.Stack(layout, ScreenCorner.BottomRight));
        Assert.False(HudArrangement.ListsInBottomRow(layout));
    }

    [Fact]
    public void VerticalLists_FollowTheMinimap_AndTheActionsGoToTheTopOfTheirSide()
    {
        var layout = new LayoutSettings(Minimap: ScreenCorner.BottomLeft, ActionPanel: ScreenSide.Left);

        Assert.Equal([HudPart.Minimap, HudPart.Lists], HudArrangement.Stack(layout, ScreenCorner.BottomLeft));
        Assert.Equal([HudPart.Actions], HudArrangement.Stack(layout, ScreenCorner.TopLeft));
        Assert.Empty(HudArrangement.Stack(layout, ScreenCorner.TopRight));
        Assert.Equal(ScreenCorner.BottomLeft, HudArrangement.CornerOf(layout, HudPart.Lists));
    }

    [Fact]
    public void HorizontalLists_GoToTheBottomRow()
    {
        var layout = new LayoutSettings(Minimap: ScreenCorner.TopLeft, Lists: ListOrientation.Horizontal);

        Assert.True(HudArrangement.ListsInBottomRow(layout));
        Assert.Null(HudArrangement.CornerOf(layout, HudPart.Lists));
        Assert.Equal([HudPart.Minimap], HudArrangement.Stack(layout, ScreenCorner.TopLeft));
        Assert.Equal([HudPart.Actions], HudArrangement.Stack(layout, ScreenCorner.TopRight));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"speed\": {\"battle\": \"Warp\"}}")]
    public void AMissingOrBrokenFile_GivesTheDefaults(string json)
    {
        Assert.Equal(SpeedSettings.Default, SettingsFile.Read(json));
        Assert.Equal(SpeedSettings.Default, SettingsFile.Load(Path.Combine(Path.GetTempPath(), $"ew-missing-{Guid.NewGuid():N}.json")));
    }

    [Fact]
    public void WhatTheFileLeavesOut_KeepsItsDefault()
    {
        var settings = SettingsFile.Read("{\"speed\": {\"battle\": \"Slow\"}}");

        Assert.Equal(SpeedSettings.Default with { Battle = AnimationSpeed.Slow }, settings);
    }

    [Theory]
    [InlineData(AnimationSpeed.Slow, 0.3)]
    [InlineData(AnimationSpeed.Normal, 0.15)]
    [InlineData(AnimationSpeed.Fast, 0.075)]
    [InlineData(AnimationSpeed.Instant, 0)]
    public void AStep_TakesItsNormalTimeDividedByTheSpeed(AnimationSpeed speed, double seconds)
    {
        Assert.Equal(seconds, SpeedSettings.Seconds(speed, 0.15), 6);
        Assert.Equal(AnimationSpeed.Fast, new SpeedSettings(OwnHeroes: AnimationSpeed.Fast).ForHeroOf(human: true));
        Assert.Equal(AnimationSpeed.Normal, new SpeedSettings(OwnHeroes: AnimationSpeed.Fast).ForHeroOf(human: false));
    }
}
