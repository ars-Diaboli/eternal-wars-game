using EternalWars.Core.Data.Settings;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Settings;

namespace EternalWars.Core.Tests.Data;

/// <summary>The speed, interface and layout settings and their file (ADR-090, UI-1; ADR-154; ADR-156; ADR-309).</summary>
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
        var layout = new LayoutSettings(ScreenEdge.Bottom, Mirrored: true);
        try
        {
            SettingsFile.Save(path, SpeedSettings.Default, new InterfaceSettings(DetailedTooltips: true), layout);

            Assert.Equal(layout, SettingsFile.LoadLayout(path));
            Assert.True(SettingsFile.LoadInterface(path).DetailedTooltips);
            Assert.Contains("\"mirrored\": true", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.DoesNotContain("sidePanel", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal(LayoutSettings.Default, SettingsFile.ReadLayout("{\"speed\": {\"battle\": \"Slow\"}}"));
            Assert.Equal(LayoutSettings.Default with { InitiativeBar = ScreenEdge.Bottom }, SettingsFile.ReadLayout("{\"layout\": {\"initiativeBar\": \"Bottom\"}}"));
            Assert.False(LayoutSettings.Default.Mirrored);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Theory]
    [InlineData("\"sidePanel\": \"Right\", \"minimap\": \"TopLeft\"", false)] // the panel held the minimap, whatever its corner said
    [InlineData("\"sidePanel\": \"Left\"", true)]
    [InlineData("\"sidePanel\": \"left\"", true)]
    [InlineData("\"sidePanel\": 1", true)] // a hand-edited number: the old enum's Left
    [InlineData("\"sidePanel\": 2, \"minimap\": 3", true)]
    [InlineData("\"sidePanel\": [], \"minimap\": {}", false)] // nonsense: the default, and the rest of the file still loads
    [InlineData("\"sidePanel\": \"Off\", \"minimap\": \"TopRight\", \"lists\": \"Horizontal\"", false)]
    [InlineData("\"sidePanel\": \"Off\", \"minimap\": \"BottomLeft\", \"actionButtons\": \"BottomRight\"", true)]
    [InlineData("\"minimap\": \"TopLeft\"", false)] // written before the side panel: it was on the right
    [InlineData("\"minimap\": \"TopLeft\", \"actionPanel\": \"Left\"", false)]
    [InlineData("\"sidePanel\": \"Left\", \"mirrored\": false", false)] // a file the game wrote says it itself
    public void AnOlderLayout_LoadsAsTheDefaultOrItsMirror_AndKeepsItsInitiativeBar(string older, bool mirrored)
    {
        var layout = SettingsFile.ReadLayout($"{{\"layout\": {{\"initiativeBar\": \"Bottom\", {older}}}}}");

        Assert.Equal(new LayoutSettings(ScreenEdge.Bottom, mirrored), layout);
    }

    [Fact]
    public void TheDefaultArrangement_HasTheHeroesOnTheLeft_AndTheMinimapTownsAndActionsOnTheRight()
    {
        var layout = LayoutSettings.Default;

        Assert.Equal(
            [ScreenSide.Left, ScreenSide.Right, ScreenSide.Left, ScreenSide.Right, ScreenSide.Left, ScreenSide.Left, ScreenSide.Right, ScreenSide.Right],
            Enum.GetValues<HudPart>().Select(p => HudArrangement.SideOf(layout, p)));
        Assert.Equal([HudPart.HeroCard, HudPart.ArmyStrip, HudPart.Actions, HudPart.EndTurn], HudArrangement.BottomRow(layout));
    }

    [Fact]
    public void TheMirror_SwapsLeftAndRight_ForEveryPart()
    {
        var layout = LayoutSettings.Default;
        var mirror = layout with { Mirrored = true };

        Assert.All(Enum.GetValues<HudPart>(), p => Assert.Equal(HudArrangement.Opposite(HudArrangement.SideOf(layout, p)), HudArrangement.SideOf(mirror, p)));
        Assert.Equal(HudArrangement.BottomRow(layout).Reverse(), HudArrangement.BottomRow(mirror));
        Assert.Equal(ScreenSide.Left, HudArrangement.SideOf(mirror, HudPart.Minimap));
        Assert.Equal(ScreenSide.Right, HudArrangement.SideOf(mirror, HudPart.HeroList));
    }

    [Theory]
    [InlineData(150, 150)]
    [InlineData(200, 200)]
    [InlineData(50, 100)]
    [InlineData(400, 200)]
    [InlineData(125, 100)] // between two: the smaller
    [InlineData(140, 150)]
    [InlineData(175, 150)]
    [InlineData(180, 200)]
    public void TheUiScale_IsKeptWithTheInterfaceSettings_InItsRangeAndOnAnOfferedScale(int written, int read)
    {
        Assert.Equal(read, SettingsFile.ReadInterface($"{{\"interface\": {{\"uiScalePercent\": {written}}}}}").UiScalePercent);
        Assert.Equal(read, new InterfaceSettings(UiScalePercent: written).Normalized().UiScalePercent);
    }

    [Fact]
    public void TheUiScale_RoundTripsThroughTheFile_AndIs100WhenTheFileHasNone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ew-settings-{Guid.NewGuid():N}", SettingsFile.FileName);
        try
        {
            SettingsFile.Save(path, SpeedSettings.Default, new InterfaceSettings(ShowGrid: true, UiScalePercent: 150));

            Assert.Equal(new InterfaceSettings(ShowGrid: true, UiScalePercent: 150), SettingsFile.LoadInterface(path));
            Assert.Equal(100, SettingsFile.ReadInterface("{\"interface\": {\"showGrid\": true}}").UiScalePercent);
            Assert.Equal([100, 150, 200], InterfaceSettings.UiScales);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Theory]
    [InlineData(40, 40)]
    [InlineData(60, 60)]
    [InlineData(80, 80)]
    [InlineData(100, 100)]
    [InlineData(0, 40)]
    [InlineData(150, 100)]
    [InlineData(50, 40)] // between two: the smaller
    [InlineData(55, 60)]
    [InlineData(95, 100)]
    public void ThePanelOpacity_IsKeptWithTheInterfaceSettings_OnAnOfferedOpacity(int written, int read)
    {
        Assert.Equal(read, SettingsFile.ReadInterface($"{{\"interface\": {{\"panelOpacityPercent\": {written}}}}}").PanelOpacityPercent);
        Assert.Equal(read, new InterfaceSettings(PanelOpacityPercent: written).Normalized().PanelOpacityPercent);
    }

    [Fact]
    public void ThePanelOpacity_RoundTripsThroughTheFile_AndIs100WhenTheFileHasNone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ew-settings-{Guid.NewGuid():N}", SettingsFile.FileName);
        try
        {
            SettingsFile.Save(path, SpeedSettings.Default, new InterfaceSettings(PanelOpacityPercent: 60));

            Assert.Equal(60, SettingsFile.LoadInterface(path).PanelOpacityPercent);
            Assert.Equal(100, SettingsFile.ReadInterface("{\"interface\": {\"uiScalePercent\": 150}}").PanelOpacityPercent);
            Assert.Equal([40, 60, 80, 100], InterfaceSettings.PanelOpacities);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
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
    public void TheWindowMode_IsKeptWithTheInterfaceSettings_AndWindowedWhenTheFileHasNone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ew-settings-{Guid.NewGuid():N}", SettingsFile.FileName);
        try
        {
            SettingsFile.Save(path, SpeedSettings.Default, new InterfaceSettings(Window: WindowMode.Fullscreen));

            Assert.Equal(WindowMode.Fullscreen, SettingsFile.LoadInterface(path).Window);
            Assert.Contains("\"window\": \"Fullscreen\"", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Equal(WindowMode.Windowed, InterfaceSettings.Default.Window);
            Assert.Equal(WindowMode.Windowed, SettingsFile.ReadInterface("{\"interface\": {\"showGrid\": true}}").Window);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void TheMapListsSortAndFilter_AreKeptWithTheInterfaceSettings_AndByNameWithEverySizeWhenTheFileHasNone()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ew-settings-{Guid.NewGuid():N}", SettingsFile.FileName);
        var view = new MapListView(MapSort.Size, Reversed: true, Size: 128);
        try
        {
            SettingsFile.Save(path, SpeedSettings.Default, new InterfaceSettings(ShowGrid: true).WithMapList(view));

            var loaded = SettingsFile.LoadInterface(path);
            Assert.Equal(view, loaded.MapList());
            Assert.True(loaded.ShowGrid);
            Assert.Contains("\"mapSort\": \"Size\"", File.ReadAllText(path), StringComparison.Ordinal);

            // A file written before the map lists were sorted keeps its other settings and sorts by name, every size shown.
            var older = SettingsFile.ReadInterface("{\"interface\": {\"detailedTooltips\": true, \"fog\": \"Clouds\", \"showGrid\": true, \"window\": \"Fullscreen\"}}");
            Assert.Equal(new InterfaceSettings(true, FogLook.Clouds, true, WindowMode.Fullscreen), older);
            Assert.Equal(MapListView.Default, older.MapList());

            // A size that is no preset shows every map.
            Assert.Equal(MapListView.AllSizes, SettingsFile.ReadInterface("{\"interface\": {\"mapSizeFilter\": 100}}").MapList().Size);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public void AltEnter_SwitchesTheWindowMode_AndKeepsTheOtherSettings()
    {
        var settings = new InterfaceSettings(ShowGrid: true);

        Assert.Equal(settings with { Window = WindowMode.Fullscreen }, settings.WithWindowToggled());
        Assert.Equal(settings, settings.WithWindowToggled().WithWindowToggled());
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
