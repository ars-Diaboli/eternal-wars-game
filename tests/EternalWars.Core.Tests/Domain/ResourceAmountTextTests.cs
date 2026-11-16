using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Tests.Domain;

/// <summary>Short resource amounts in the labels (ADR-186).</summary>
public class ResourceAmountTextTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(999_999, "999999")]
    [InlineData(1_000_000, "1.0m")]
    [InlineData(321_745_000, "321.7m")]
    [InlineData(999_999_999, "999.9m")]
    [InlineData(1_000_000_000, "1.0b")]
    [InlineData(int.MaxValue, "2.1b")]
    public void Gold_IsShownInFullUpToSixDigits_ThenInMillions(int amount, string expected) =>
        Assert.Equal(expected, ResourceAmountText.Short(ResourceType.Gold, amount));

    [Theory]
    [InlineData(ResourceType.Ore, 9_999, "9999")]
    [InlineData(ResourceType.Ore, 10_000, "10.0k")]
    [InlineData(ResourceType.Ore, 31_742, "31.7k")]
    [InlineData(ResourceType.Ore, 31_799, "31.7k")]
    [InlineData(ResourceType.Lumber, 999_999, "999.9k")]
    [InlineData(ResourceType.Crystal, 1_000_000, "1.0m")]
    [InlineData(ResourceType.Sulfur, 12_345_678, "12.3m")]
    [InlineData(ResourceType.Mercury, 2_000_000_000, "2.0b")]
    public void OtherResources_AreShownInFullUpToFourDigits_ThenShortenedAndRoundedDown(ResourceType type, int amount, string expected) =>
        Assert.Equal(expected, ResourceAmountText.Short(type, amount));

    [Fact]
    public void NegativeAmounts_KeepTheirSign()
    {
        Assert.Equal("-500", ResourceAmountText.Short(ResourceType.Gold, -500));
        Assert.Equal("-12.3k", ResourceAmountText.Short(ResourceType.Ore, -12_345));
        Assert.Equal("-2.1b", ResourceAmountText.Short(ResourceType.Gold, int.MinValue));
    }

    [Fact]
    public void IsShortened_TellsWhenTheLabelHidesDigits()
    {
        Assert.False(ResourceAmountText.IsShortened(ResourceType.Gold, 999_999));
        Assert.True(ResourceAmountText.IsShortened(ResourceType.Gold, 1_000_000));
        Assert.True(ResourceAmountText.IsShortened(ResourceType.Ore, 10_000));
        Assert.Equal("10000", ResourceAmountText.Full(10_000));
    }
}
