using EternalWars.Core.Domain.Editing;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The command palette's search and the editor's preferences (EDIT-1, ADR-250).</summary>
public class EditorShellTests
{
    private static readonly string[] Names = ["New", "Save", "Save as", "Undo", "Switch level", "Hide all panels", "Road"];

    [Fact]
    public void Find_WithNothingTyped_OffersEveryNameInOrder() =>
        Assert.Equal([0, 1, 2, 3, 4, 5, 6], CommandSearch.Find("  ", Names));

    [Fact]
    public void Find_IgnoresCase() => Assert.Equal(["Undo"], Found("UNDO"));

    [Fact]
    public void Find_PutsANameThatStartsWithTheWordBeforeOneThatHoldsIt()
    {
        Assert.Equal(["Save", "Save as"], Found("sa"));
        Assert.Equal(["New", "Undo"], Found("n")[..2]); // starts with it, then the names that only hold it, in their order
        Assert.Equal(["Undo", "Road"], Found("o")); // both hold it inside: their order stays
    }

    [Fact]
    public void Find_PutsAWordsStartBeforeLettersInOrder()
    {
        // "Switch level" has a word starting with "le"; "Hide all panels" only has an l and later an e.
        Assert.Equal(["Switch level", "Hide all panels"], Found("le"));
        Assert.Equal(["Save as"], Found("svas"));
    }

    [Fact]
    public void Find_NeedsEveryTypedWord()
    {
        Assert.Equal(["Hide all panels"], Found("pan hide"));
        Assert.Empty(Found("save road"));
    }

    [Fact]
    public void Preferences_Normalized_BringsEveryValueIntoItsRange()
    {
        var low = new EditorPreferences
        {
            UiScalePercent = 10, AutosaveMinutes = 0, CameraPanSpeed = 0, CameraRotateSpeed = 0, CameraZoomSpeed = 0, DefaultMapSize = 3,
        }.Normalized();
        var high = new EditorPreferences
        {
            UiScalePercent = 900, AutosaveMinutes = 900, CameraPanSpeed = 900, CameraRotateSpeed = 900, CameraZoomSpeed = 900, DefaultMapSize = 900,
        }.Normalized();

        Assert.Equal(
            new EditorPreferences
            {
                UiScalePercent = 100, AutosaveMinutes = 1, CameraPanSpeed = 4, CameraRotateSpeed = 30, CameraZoomSpeed = 1, DefaultMapSize = 16,
            },
            low);
        Assert.Equal(
            new EditorPreferences
            {
                UiScalePercent = 200, AutosaveMinutes = 30, CameraPanSpeed = 40, CameraRotateSpeed = 180, CameraZoomSpeed = 5, DefaultMapSize = 256,
            },
            high);
    }

    [Fact]
    public void Preferences_Defaults_AreInsideTheirRanges() => Assert.Equal(new EditorPreferences(), new EditorPreferences().Normalized());

    [Fact]
    public void Preferences_HighContrast_IsOffAtStartAndKeptByNormalized()
    {
        Assert.False(new EditorPreferences().HighContrast);
        Assert.True(new EditorPreferences { HighContrast = true }.Normalized().HighContrast);
    }

    private static string[] Found(string query) => [.. CommandSearch.Find(query, Names).Select(i => Names[i])];
}
