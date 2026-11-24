using EternalWars.Core.Domain.Settings;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The UI scale on a window (ADR-309): the chosen scale, lowered so the HUD keeps 960 by 540 of its own pixels, never below 100 %.</summary>
public class InterfaceScaleTests
{
    [Theory]
    [InlineData(100, 1920, 1080, 1f)]
    [InlineData(150, 1920, 1080, 1.5f)]
    [InlineData(200, 1920, 1080, 2f)] // exactly 960 by 540 left
    [InlineData(200, 2560, 1440, 2f)]
    [InlineData(200, 2560, 1080, 2f)] // 21:9
    public void ABigEnoughWindow_KeepsTheChosenScale(int percent, int width, int height, float expected) =>
        Assert.Equal(expected, new InterfaceSettings(UiScalePercent: percent).ScaleFor(width, height), 3);

    [Theory]
    [InlineData(150, 1280, 720, 4f / 3)]
    [InlineData(200, 1280, 720, 4f / 3)]
    [InlineData(200, 1280, 800, 4f / 3)] // the width is the limit
    [InlineData(200, 1600, 900, 5f / 3)]
    public void ASmallWindow_LowersTheScaleToFit(int percent, int width, int height, float expected) =>
        Assert.Equal(expected, new InterfaceSettings(UiScalePercent: percent).ScaleFor(width, height), 3);

    [Fact]
    public void AWindowSmallerThanTheHud_StaysAt100Percent() =>
        Assert.Equal(1f, new InterfaceSettings(UiScalePercent: 200).ScaleFor(800, 450));

    [Fact]
    public void AScaleOutsideTheRange_IsNormalizedFirst() =>
        Assert.Equal(2f, new InterfaceSettings(UiScalePercent: 400).ScaleFor(3840, 2160));
}
