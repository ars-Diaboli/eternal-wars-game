using EternalWars.Core.Domain.Editing;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The editor's context menus (ADR-286): a group of commands left out leaves no stray separator line.</summary>
public class MenuItemsTests
{
    [Fact]
    public void Tidy_DropsSeparatorsAtEitherEnd_AndTwiceInARow()
    {
        Assert.Equal(["a", null, "b", "c"], MenuItems.Tidy([null, "a", null, null, "b", "c", null]));
        Assert.Equal(["a"], MenuItems.Tidy(["a", null]));
        Assert.Empty(MenuItems.Tidy([null, null]));
        Assert.Empty(MenuItems.Tidy([]));
        Assert.Equal(["a", null, "b"], MenuItems.Tidy(["a", null, "b"])); // a tidy list stays as it is
    }
}
