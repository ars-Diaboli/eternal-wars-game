using EternalWars.Core.Data.Settings;
using EternalWars.Core.Domain.Settings;

namespace EternalWars.Core.Tests.Data;

/// <summary>The speed settings and their file (ADR-090, UI-1).</summary>
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
