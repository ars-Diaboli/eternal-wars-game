using EternalWars.Core.Domain.Game;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The new-game screen's map lists, sorted and filtered (ADR-204).</summary>
public class MapListOrderTests
{
    private static readonly Item[] Items =
    [
        new("ember March", [72]),
        new("Arena", [48]),
        new("Big Template", [96, 128, 256]),
        new("Broken map", []),
        new("Demo Valley", [48]),
        new("Small Template", [72, 96]),
    ];

    [Theory]
    [InlineData(35, 18, 48)]
    [InlineData(48, 48, 48)]
    [InlineData(49, 10, 72)]
    [InlineData(20, 100, 128)]
    [InlineData(256, 256, 256)]
    [InlineData(300, 40, 256)]
    public void AMap_CountsAsTheSmallestPresetHoldingItsLargerSide(int width, int height, int size)
    {
        Assert.Equal(size, MapSizeClass.Of(width, height));
    }

    [Fact]
    public void AMapOfUnknownSize_HasNoClass_AndATemplateHasTheClassOfEverySize()
    {
        Assert.Null(MapSizeClass.Of(0, 0));
        Assert.Equal([48, 72, 256], MapSizeClass.OfTemplate([72, 36, 48, 300, 256]));
    }

    [Fact]
    public void ByDefault_TheListIsByName_AToZ_IgnoringCase_WithEverySize()
    {
        Assert.Equal(
            ["Arena", "Big Template", "Broken map", "Demo Valley", "ember March", "Small Template"],
            Names(MapListView.Default));
    }

    [Fact]
    public void BySize_SmallFirstThenByName_ATemplateByItsSmallestSize_UnknownLast()
    {
        Assert.Equal(
            ["Arena", "Demo Valley", "ember March", "Small Template", "Big Template", "Broken map"],
            Names(MapListView.Default.Pressed(MapSort.Size)));
    }

    [Fact]
    public void PressingTheActiveSortAgain_ReversesIt_AndAnotherSortStartsInItsOwnOrder()
    {
        var bySize = MapListView.Default.Pressed(MapSort.Size);
        var reversed = bySize.Pressed(MapSort.Size);

        Assert.True(reversed.Reversed);
        Assert.Equal(Enumerable.Reverse(Names(bySize)), Names(reversed));
        Assert.Equal(new MapListView(MapSort.Name), reversed.Pressed(MapSort.Name));
        Assert.Equal(new MapListView(MapSort.Name, Reversed: true), MapListView.Default.Pressed(MapSort.Name));
    }

    [Fact]
    public void TheSizeFilter_ShowsTheMapsOfThatClass_AndATemplateUnderEverySizeItSupports()
    {
        Assert.Equal(["Big Template", "Small Template"], Names(MapListView.Default.Filtered(96)));
        Assert.Equal(["ember March", "Small Template"], Names(MapListView.Default.Filtered(72)));
        Assert.Empty(Names(MapListView.Default.Filtered(160)));
        Assert.Equal(["Big Template"], Names(MapListView.Default.Filtered(256).Pressed(MapSort.Size)));
    }

    [Fact]
    public void ASizeThatIsNoPreset_ShowsEveryMap()
    {
        Assert.Equal(MapListView.AllSizes, MapListView.Default.Filtered(100).Size);
        Assert.Equal(Items.Length, Names(new MapListView(Size: 100)).Count);
    }

    private static List<string> Names(MapListView view) => view.Arrange(Items, i => i.Name, i => i.Sizes).Select(i => i.Name).ToList();

    private sealed record Item(string Name, IReadOnlyList<int> Sizes);
}
