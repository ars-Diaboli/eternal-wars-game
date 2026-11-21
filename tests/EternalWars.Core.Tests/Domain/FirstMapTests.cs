using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Domain;

/// <summary>
/// A first two-player map (ADR-279): the wizard's blank map, a town placed for each player from the browser, the Problems panel's fixes for
/// what is left; at the end nothing is left.
/// </summary>
public class FirstMapTests
{
    private static readonly BrowserCatalog Browser = new(MapObjectCatalog.Default, CreatureCatalog.Default, ArtifactCatalog.Default);

    private static BrowserEntry Town(string type) => Browser.Find(BuiltInMapObjects.TownId(type))!;

    /// <summary>The wizard's map: 36 × 36, two players, a hero each in opposite corners (1, 1) and (34, 34).</summary>
    private static MapEditorSession Wizard() => new(BlankScenario.Create(36, 36));

    private static MapIssue Issue(MapEditorSession editor, string text) =>
        MapValidator.Validate(editor.Scenario).Single(i => i.Message.Contains(text, StringComparison.Ordinal));

    [Theory]
    [InlineData("first", "first.ewmap.json")]
    [InlineData("first.json", "first.ewmap.json")]
    [InlineData("first.json.ewmap.json", "first.ewmap.json")]
    [InlineData("first.ewmap.json", "first.ewmap.json")]
    [InlineData("First.EWMAP.JSON", "First.EWMAP.JSON")]
    [InlineData("first.map", "first.map.ewmap.json")]
    public void WithExtension_GivesTheMapExtensionOnce(string typed, string saved) =>
        Assert.Equal(saved, RecentFiles.WithExtension(typed, ".ewmap.json"));

    [Fact]
    public void SavePath_KeepsThePickedName_WhenTheTidiedOneIsAnotherFileThatExists()
    {
        Assert.Equal("a.json.ewmap.json", RecentFiles.SavePath("a.json.ewmap.json", ".ewmap.json", p => p == "a.ewmap.json"));
        Assert.Equal("a.ewmap.json", RecentFiles.SavePath("a.json.ewmap.json", ".ewmap.json", _ => false));
        Assert.Equal("a.ewmap.json", RecentFiles.SavePath("a.ewmap.json", ".ewmap.json", _ => true)); // the picked file itself: the dialog asked
    }

    [Fact]
    public void PlacingForAnUnknownPlayer_IsRefusedBeforeAnythingIsPlaced()
    {
        var editor = Wizard();
        var dwelling = Browser.Entries.First(e => e.Placement == BrowserPlacement.Dwelling);
        var nobody = new PlayerId(7);

        Assert.Equal(EditResult.UnknownPlayer, editor.Placing.Place(dwelling, new(10, 10, 0), out _, nobody));
        Assert.Equal(EditResult.UnknownPlayer, editor.Placing.Place(Town(TownIds.Haven), new(10, 10, 0), out _, nobody));
        Assert.Equal(EditResult.UnknownPlayer, editor.Placing.Check(Town(TownIds.Haven), new(10, 10, 0), nobody).Result);
        Assert.Empty(editor.Scenario.Objects);
        Assert.Equal(0, editor.History.Count);
    }

    [Fact]
    public void PlacingADwellingForAPlayer_KeepsItsCreature()
    {
        var editor = Wizard();
        var entry = Browser.Entries.First(e => e.Placement == BrowserPlacement.Dwelling);
        var red = editor.Scenario.Players[1].Id;

        editor.Placing.Place(entry, new(10, 10, 0), out var placed, red);

        var dwelling = (CreatureDwelling)editor.Scenario.GetObject(placed.Object!.Value);
        Assert.Equal((entry.Creature, (PlayerId?)red), ((CreatureId?)dwelling.Creature, dwelling.Owner));
        Assert.Equal(1, editor.History.Count);
    }

    [Fact]
    public void TwoNeutralTownsAsNearToAPlayer_AreNotOffered()
    {
        var editor = Wizard();
        editor.Placing.Place(Town(TownIds.Haven), new(10, 10, 0), out var probe);
        var offset = editor.Scenario.GetObject(probe.Object!.Value).VisitPosition;
        editor.Undo();
        // Both entrances 15 steps from Blue's hero at (1, 1): (16, 16) and (16, 8).
        Assert.Equal(EditResult.Ok, editor.Placing.Place(Town(TownIds.Haven), new(16 - (offset.X - 10), 16 - (offset.Y - 10), 0), out _));
        Assert.Equal(EditResult.Ok, editor.Placing.Place(Town(TownIds.Academy), new(16 - (offset.X - 10), 8 - (offset.Y - 10), 0), out _));

        Assert.Null(Issue(editor, $"Player {editor.Scenario.Players[0].Name} starts without a town").Fix);
    }

    [Fact]
    public void AHeroWhoseOnlyTownIsRandom_IsOfferedNoArmy()
    {
        var editor = Wizard();
        var hero = editor.Scenario.Heroes[0];
        editor.Placing.Place(Town(TownIds.Haven), new(4, 4, 0), out var placed, hero.Owner);
        ((Town)editor.Scenario.GetObject(placed.Object!.Value)).IsRandom = true;

        Assert.Null(Issue(editor, $"Hero {hero.Name} has no army").Fix);
    }

    [Fact]
    public void HasOwner_IsTrueForTownsMinesAndDwellings_NotForTreasureOrMonsters()
    {
        Assert.True(EntryEditor.HasOwner(Town(TownIds.Haven)));
        Assert.True(EntryEditor.HasOwner(Browser.Find(BuiltInMapObjects.MineId(ResourceType.Gold))!));
        Assert.True(EntryEditor.HasOwner(Browser.Entries.First(e => e.Placement == BrowserPlacement.Dwelling)));
        Assert.False(EntryEditor.HasOwner(Browser.Find(BuiltInMapObjects.TreasureChest)!));
        Assert.False(EntryEditor.HasOwner(Browser.Entries.First(e => e.Placement == BrowserPlacement.Monster)));
        Assert.False(EntryEditor.HasOwner(Browser.Find(BrowserEntry.HeroId)!));
    }

    [Fact]
    public void PlacingATownForAPlayer_GivesItToThem_InTheSameUndoStep()
    {
        var editor = Wizard();
        var red = editor.Scenario.Players[1].Id;

        Assert.Equal(EditResult.Ok, editor.Placing.Place(Town(TownIds.Haven), new(24, 24, 0), out var placed, red));

        Assert.Equal(red, ((Town)editor.Scenario.GetObject(placed.Object!.Value)).Owner);
        Assert.Equal(1, editor.History.Count);
        Assert.True(editor.Undo());
        Assert.Empty(editor.Scenario.ObjectsOf<Town>());
    }

    [Fact]
    public void PlacingAThingWithAnOwnerForNobody_LeavesItNeutral()
    {
        var editor = Wizard();

        editor.Placing.Place(Town(TownIds.Haven), new(10, 10, 0), out var town);
        editor.Placing.Place(Browser.Find(BuiltInMapObjects.MineId(ResourceType.Gold))!, new(20, 4, 0), out var mine, editor.Scenario.Players[0].Id);

        Assert.Null(((Town)editor.Scenario.GetObject(town.Object!.Value)).Owner);
        Assert.Equal(editor.Scenario.Players[0].Id, ((Mine)editor.Scenario.GetObject(mine.Object!.Value)).Owner);
    }

    [Fact]
    public void APlayerWithoutATown_IsOfferedTheNearestNeutralTown()
    {
        var editor = Wizard();
        editor.Placing.Place(Town(TownIds.Haven), new(4, 4, 0), out var near);
        editor.Placing.Place(Town(TownIds.Academy), new(26, 26, 0), out var far);
        var blue = editor.Scenario.Players[0];

        var issue = Issue(editor, $"Player {blue.Name} starts without a town");
        var town = (Town)editor.Scenario.GetObject(near.Object!.Value);
        Assert.Equal(new OwnerFix($"Give {town.Name}, the nearest neutral town, to {blue.Name}", town.Id, null, blue.Id), issue.Fix);

        Assert.Equal(EditResult.Ok, editor.Fix(issue.Fix!));
        Assert.Equal(blue.Id, town.Owner);
        Assert.Null(((Town)editor.Scenario.GetObject(far.Object!.Value)).Owner);
    }

    [Fact]
    public void ANeutralTownAsNearToAnotherPlayerWithoutATown_IsNotOffered()
    {
        var editor = Wizard();
        // Its entrance at (5, 30): 29 steps from the heroes at (1, 1) and at (34, 34) alike.
        editor.Placing.Place(Town(TownIds.Haven), new(10, 10, 0), out var probe);
        var offset = editor.Scenario.GetObject(probe.Object!.Value).VisitPosition;
        editor.Undo();
        editor.Placing.Place(Town(TownIds.Haven), new(5 - (offset.X - 10), 30 - (offset.Y - 10), 0), out var placed);
        Assert.Equal(new MapPosition(5, 30, 0), editor.Scenario.GetObject(placed.Object!.Value).VisitPosition);

        Assert.All(MapValidator.Validate(editor.Scenario).Where(i => i.Message.Contains("starts without a town", StringComparison.Ordinal)), i => Assert.Null(i.Fix));
    }

    [Fact]
    public void AHeroOfNoTypeWithoutAnArmy_IsOfferedAWeekOfItsTownsFirstCreature_OnceItsPlayerHasATown()
    {
        var editor = Wizard();
        var hero = editor.Scenario.Heroes[0];
        Assert.Null(Issue(editor, $"Hero {hero.Name} has no army").Fix); // no town: nothing tells the creature

        editor.Placing.Place(Town(TownIds.Haven), new(4, 4, 0), out _, hero.Owner);
        var first = CreatureCatalog.Default.BaseCreaturesOf(TownIds.Haven).First(c => c.WeeklyGrowth > 0);
        var fix = Assert.IsType<HeroArmyFix>(Issue(editor, $"Hero {hero.Name} has no army").Fix);
        Assert.Equal(new CreatureStack(first.Id, first.WeeklyGrowth), fix.Stack);

        Assert.Equal(EditResult.Ok, editor.Fix(fix));
        Assert.Equal([(0, new CreatureStack(first.Id, first.WeeklyGrowth))], hero.Army.Stacks);
        Assert.Equal(EditResult.InvalidTarget, editor.Fix(fix)); // it has an army now
    }

    [Fact]
    public void ATypedHeroWithoutAnArmy_IsOfferedItsStarterStack()
    {
        var editor = Wizard();
        var definition = editor.Scenario.HeroRoster.All.First(h => TavernRules.StarterStack(editor.Scenario, h) is not null);
        Assert.Equal(EditResult.Ok, editor.Heroes.SetType(editor.Scenario.Heroes[0].Id, definition.Id));

        var fix = Assert.IsType<HeroArmyFix>(Issue(editor, $"Hero {editor.Scenario.Heroes[0].Name} has no army").Fix);

        Assert.Equal(TavernRules.StarterStack(editor.Scenario, definition), fix.Stack);
    }

    [Fact]
    public void TheWizardsMap_WithATownForEachPlayerAndTheFixes_HasNoProblemsLeft()
    {
        var editor = Wizard();
        var (blue, red) = (editor.Scenario.Players[0].Id, editor.Scenario.Players[1].Id);
        editor.Placing.Place(Town(TownIds.Haven), new(4, 4, 0), out _, blue);
        editor.Placing.Place(Town(TownIds.Academy), new(25, 25, 0), out _, red);

        foreach (var fix in MapValidator.Validate(editor.Scenario).Select(i => i.Fix).OfType<HeroArmyFix>().ToList())
        {
            Assert.Equal(EditResult.Ok, editor.Fix(fix));
        }

        Assert.Empty(MapValidator.Validate(editor.Scenario));
    }
}
