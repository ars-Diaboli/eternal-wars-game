using EternalWars.Core.Data.Maps;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;

namespace EternalWars.Core.Tests.Domain;

/// <summary>The object browser's data and placement (EDIT-1, ADR-253).</summary>
public class EditorBrowserTests
{
    private static readonly BrowserCatalog Browser = new(MapObjectCatalog.Default, CreatureCatalog.Default, ArtifactCatalog.Default);

    [Fact]
    public void EveryKind_HasACategory() =>
        Assert.All(Enum.GetValues<MapObjectKind>(), kind => Assert.True(Enum.IsDefined(BrowserCatalog.CategoryOf(kind)), $"{kind}"));

    [Fact]
    public void EveryDefinition_IsOffered_InItsKindsCategory_WithARuleAndSearchWords()
    {
        Assert.All(MapObjectCatalog.Default.All, definition =>
        {
            var entries = Browser.Entries.Where(e => e.Definition == definition).ToList();
            Assert.True(entries.Count > 0, $"{definition.Id} is not in the browser");
            Assert.All(entries, e => Assert.Equal(BrowserCatalog.CategoryOf(definition.Kind), e.Category));
        });
        Assert.All(Browser.Entries, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.Name), e.Id);
            Assert.False(string.IsNullOrWhiteSpace(e.Rule), $"{e.Id} has no rule");
            Assert.DoesNotContain('\n', e.Rule);
            Assert.NotEmpty(e.Words);
            Assert.All(e.Words, w => Assert.Equal(w.ToLowerInvariant().Trim(), w));
            Assert.Same(e, Browser.Find(e.Id));
        });
        Assert.Equal(Browser.Entries.Count, Browser.Entries.Select(e => e.Id).Distinct().Count());
        Assert.All(Enum.GetValues<BrowserCategory>(), c => Assert.NotEmpty(Browser.Of(c)));
        Assert.Null(Browser.Find("no.such.thing"));
    }

    [Fact]
    public void Gold_FindsTheMine_ThePile_AndTheTreasury()
    {
        var found = Browser.Search("gold").Select(e => e.Id).ToList();

        Assert.Contains(BuiltInMapObjects.MineId(Core.Domain.Economy.ResourceType.Gold), found);
        Assert.Contains(BuiltInMapObjects.PileId(Core.Domain.Economy.ResourceType.Gold), found);
        Assert.Contains(BuiltInMapObjects.DwarvenTreasury, found); // not in its name: a bank that pays gold
        Assert.DoesNotContain(BuiltInMapObjects.Sign, found);

        // A name that starts with the word comes first, one found by its search words last.
        Assert.StartsWith("Gold", Browser.Search("gold")[0].Name, StringComparison.Ordinal);
        Assert.True(found.IndexOf(BuiltInMapObjects.MineId(Core.Domain.Economy.ResourceType.Gold)) < found.IndexOf(BuiltInMapObjects.AbandonedMineId(Core.Domain.Economy.ResourceType.Gold)));
        Assert.True(found.IndexOf(BuiltInMapObjects.AbandonedMineId(Core.Domain.Economy.ResourceType.Gold)) < found.IndexOf(BuiltInMapObjects.DwarvenTreasury));
    }

    [Fact]
    public void Search_NeedsEveryWord_IgnoresCase_AndFindsByCategoryFactionAndTier()
    {
        Assert.Equal(Browser.Entries, Browser.Search("  "));
        Assert.Equal(["Abandoned gold mine"], Browser.Search("ABANDONED gold").Select(e => e.Name));
        Assert.Empty(Browser.Search("gold zzz"));
        Assert.All(Browser.Search("bank"), e => Assert.Equal(BrowserCategory.Banks, e.Category));
        Assert.Equal(13, Browser.Search("bank").Count(e => e.Category == BrowserCategory.Banks));

        var haven = Browser.Search("haven tier1 monster");
        Assert.NotEmpty(haven);
        Assert.All(haven, e => Assert.Equal((BrowserPlacement.Monster, "Haven", 1), (e.Placement, e.Group, e.Order)));
        Assert.Contains(Browser.Search("teleport"), e => e.Id == BuiltInMapObjects.TwoWayMonolith);
    }

    [Fact]
    public void Monsters_AreGroupedByFaction_InTierOrder_AndDwellingsSellBaseCreaturesOnly()
    {
        var monsters = Browser.Of(BrowserCategory.Monsters).ToList();
        Assert.Equal(CreatureCatalog.Default.All.Count(), monsters.Count);
        Assert.All(monsters, e => Assert.Equal(CreatureCatalog.Default.Get(e.Creature!.Value).Tier, e.Order));
        Assert.Contains("Neutral", monsters.Select(e => e.Group));
        Assert.All(monsters.GroupBy(e => e.Group), group => Assert.Equal(group.OrderBy(e => e.Order), group)); // the catalog's order is the tier's

        var dwellings = Browser.Of(BrowserCategory.Dwellings).Where(e => e.Placement == BrowserPlacement.Dwelling).ToList();
        Assert.NotEmpty(dwellings);
        Assert.All(dwellings, e => Assert.False(CreatureCatalog.Default.IsUpgrade(e.Creature!.Value)));
    }

    [Fact]
    public void Artifacts_AreGroupedBySlot_InTierOrder_BesideTheRandomOnes()
    {
        var artifacts = Browser.Of(BrowserCategory.Artifacts).ToList();
        Assert.Equal(Enum.GetValues<ArtifactTier>().Length, artifacts.Count(e => e.Group == "Random" && e.Artifact is null));
        var named = artifacts.Where(e => e.Artifact is not null).ToList();
        Assert.Equal(ArtifactCatalog.Default.All.Count(a => !a.IsGrail), named.Count);
        Assert.All(named, e =>
        {
            var artifact = ArtifactCatalog.Default.Get(e.Artifact!.Value);
            Assert.Equal((artifact.Slot.ToString(), artifact.Tier), (e.Group, e.Definition!.ArtifactTier!.Value));
        });
        Assert.All(named.GroupBy(e => e.Group), group => Assert.Equal(group.OrderBy(e => e.Order), group));
    }

    [Fact]
    public void Scenery_IsGroupedByKind_AndTheTerrainFilterKeepsWhatSuits()
    {
        var scenery = Browser.Of(BrowserCategory.Scenery).ToList();
        Assert.Equal(MapObjectCatalog.Default.OfKind(MapObjectKind.Scenery).Count(), scenery.Count);
        Assert.Contains("Tree", scenery.Select(e => e.Group));
        Assert.Contains("Dead tree", scenery.Select(e => e.Group));

        var onLava = Browser.Search("", TerrainType.Lava);
        Assert.All(onLava.Where(e => e.Category == BrowserCategory.Scenery), e => Assert.Contains(TerrainType.Lava, e.Terrains));
        Assert.DoesNotContain(onLava, e => e.Id == "scenery.tree.grove"); // trees do not grow on lava
        Assert.Contains(onLava, e => e.Id == "scenery.rock.small");
        Assert.Contains(onLava, e => e.Id == BuiltInMapObjects.TreasureChest); // what is made for no terrain suits every one
        Assert.All(Browser.Search("rock", TerrainType.Lava), e => Assert.True(e.Suits(TerrainType.Lava)));
    }

    [Fact]
    public void Lists_KeepRecentNewestFirstOnceEach_AndToggleFavourites()
    {
        IReadOnlyList<string> recent = [];
        foreach (var id in Enumerable.Range(0, BrowserLists.RecentCapacity + 3).Select(i => $"e{i}"))
        {
            recent = BrowserLists.WithRecent(recent, id);
        }

        Assert.Equal(BrowserLists.RecentCapacity, recent.Count);
        Assert.Equal("e14", recent[0]);
        recent = BrowserLists.WithRecent(recent, "e10");
        Assert.Equal(["e10", "e14", "e13"], recent.Take(3));
        Assert.Equal(BrowserLists.RecentCapacity, recent.Count);

        var favourites = BrowserLists.Toggled(BrowserLists.Toggled([], "a"), "b");
        Assert.Equal(["a", "b"], favourites);
        Assert.Equal(["b"], BrowserLists.Toggled(favourites, "a"));
    }

    [Fact]
    public void Place_PutsEachSortOfEntryOnTheMap_AsOneStep_AndUndoTakesItAway()
    {
        var editor = Editor();
        var scenario = editor.Scenario;
        var blank = Snapshot(editor);
        var creature = CreatureCatalog.Default.All.First(c => c.Tier == 2 && !CreatureCatalog.Default.IsUpgrade(c.Id));
        var artifact = ArtifactCatalog.Default.All.First(a => a.Tier == ArtifactTier.Major && !a.IsGrail);

        Assert.Equal(EditResult.Ok, editor.Placing.Place(Browser.Find(BuiltInMapObjects.Sign)!, new(3, 3, 0), out var sign));
        Assert.IsType<SignPost>(scenario.GetObject(sign.Object!.Value));

        Assert.Equal(EditResult.Ok, editor.Placing.Place(Browser.Find($"{BuiltInMapObjects.Monster}/{creature.Id}")!, new(8, 3, 0), out var monster));
        var guard = Assert.IsType<MonsterGuard>(scenario.GetObject(monster.Object!.Value));
        Assert.Equal((creature.Id, (int?)null, GuardDisposition.Aggressive), (guard.Creature, guard.Count, guard.Disposition));

        Assert.Equal(EditResult.Ok, editor.Placing.Place(Browser.Find($"{BuiltInMapObjects.Dwelling}/{creature.Id}")!, new(12, 3, 0), out var dwelling));
        Assert.Equal(creature.Id, Assert.IsType<CreatureDwelling>(scenario.GetObject(dwelling.Object!.Value)).Creature);

        var pickupEntry = Browser.Entries.Single(e => e.Artifact == artifact.Id);
        Assert.Equal(EditResult.Ok, editor.Placing.Place(pickupEntry, new(18, 3, 0), out var pickup));
        Assert.Equal(artifact.Id, Assert.IsType<ArtifactPickup>(scenario.GetObject(pickup.Object!.Value)).Artifact);

        var second = scenario.Players[1].Id;
        Assert.Equal(EditResult.Ok, editor.Placing.Place(Browser.Find(BrowserEntry.HeroId)!, new(22, 3, 0), out var hero, second));
        Assert.Equal((second, new MapPosition(22, 3, 0)), (scenario.GetHero(hero.Hero!.Value).Owner, scenario.GetHero(hero.Hero.Value).Position));
        Assert.Null(hero.Object);

        Assert.Equal(5, editor.History.Count); // the dwelling and the artifact are one step each, set up and all
        Assert.Equal($"Place {pickupEntry.Name}", editor.History.Steps[3]);
        var full = Snapshot(editor);
        while (editor.Undo())
        {
        }

        Assert.Equal(blank, Snapshot(editor));
        while (editor.Redo())
        {
        }

        Assert.Equal(full, Snapshot(editor));
    }

    [Fact]
    public void Check_ShowsTheCellsAndWhichDoNotFit_AndARefusedPlaceRecordsNothing()
    {
        var editor = Editor();
        var mine = Browser.Find(BuiltInMapObjects.MineId(Core.Domain.Economy.ResourceType.Gold))!;
        var free = editor.Placing.Check(mine, new(10, 10, 0));
        Assert.Equal(EditResult.Ok, free.Result);
        Assert.Equal(mine.Footprint.OccupiedCells.Count, free.Cells.Count);
        Assert.All(free.Cells, c => Assert.True(c.Fits));

        editor.Placing.Place(Browser.Find(BuiltInMapObjects.TreasureChest)!, new(11, 10, 0), out _);
        var blocked = editor.Placing.Check(mine, new(10, 10, 0));
        Assert.Equal(EditResult.Overlaps, blocked.Result);
        Assert.Equal([new MapPosition(11, 10, 0)], blocked.Cells.Where(c => !c.Fits).Select(c => c.Position));

        var edge = editor.Placing.Check(mine, new(39, 29, 0));
        Assert.Equal(EditResult.OutOfBounds, edge.Result);
        Assert.Contains(edge.Cells, c => !c.Fits);

        Assert.Equal(EditResult.Overlaps, editor.Placing.Check(Browser.Find(BrowserEntry.HeroId)!, new(11, 10, 0)).Result);
        Assert.Equal(EditResult.Ok, editor.Placing.Check(Browser.Find(BrowserEntry.HeroId)!, new(12, 10, 0)).Result);

        var steps = editor.History.Count;
        var dwelling = Browser.Of(BrowserCategory.Dwellings).First(e => e.Placement == BrowserPlacement.Dwelling);
        Assert.Equal(EditResult.Overlaps, editor.Placing.Place(dwelling, new(10, 10, 0), out var placed));
        Assert.Equal(default, placed);
        Assert.Equal(EditResult.UnknownCreature, editor.Placing.Place(dwelling with { Creature = new CreatureId("no.such") }, new(20, 20, 0), out _));
        Assert.Equal(EditResult.InvalidValue, editor.Placing.Place(Browser.Entries.First(e => e.Artifact is not null) with { Artifact = new ArtifactId("no.such") }, new(20, 20, 0), out _));
        Assert.Equal(steps, editor.History.Count);
        Assert.Single(editor.Scenario.Objects);
    }

    [Fact]
    public void Check_SaysWhatPlaceWouldSay_AlsoForLimitsAndUnknownThings_AndThenNoCellFits()
    {
        var editor = Editor();
        var obelisk = Browser.Find(BuiltInMapObjects.Obelisk)!;
        var free = editor.Scenario.Map.Positions(0).Where(p => p.X % 3 == 0 && p.Y % 3 == 1 && p.Y > 3).GetEnumerator();
        for (var placed = 0; placed < GrailRules.MaxObelisks; placed++)
        {
            free.MoveNext();
            Assert.Equal(EditResult.Ok, editor.Placing.Place(obelisk, free.Current, out _));
        }

        free.MoveNext();
        Refused(EditResult.TooManyObelisks, obelisk, free.Current);

        var hero = Browser.Find(BrowserEntry.HeroId)!;
        Refused(EditResult.UnknownPlayer, hero, free.Current, new PlayerId(99));
        var owner = editor.Scenario.Players[0].Id;
        while (!editor.Scenario.HeroLimitReached(owner))
        {
            free.MoveNext();
            Assert.Equal(EditResult.Ok, editor.Placing.Place(hero, free.Current, out _, owner));
        }

        free.MoveNext();
        Refused(EditResult.HeroLimit, hero, free.Current, owner);

        var dwelling = Browser.Of(BrowserCategory.Dwellings).First(e => e.Placement == BrowserPlacement.Dwelling);
        var upgrade = CreatureCatalog.Default.All.First(c => CreatureCatalog.Default.IsUpgrade(c.Id)).Id;
        Refused(EditResult.UpgradedCreature, dwelling with { Creature = upgrade }, free.Current);
        Refused(EditResult.UnknownCreature, Browser.Of(BrowserCategory.Monsters).First() with { Creature = new CreatureId("no.such") }, free.Current);
        Refused(EditResult.InvalidTarget, Browser.Of(BrowserCategory.Monsters).First() with { Creature = null }, free.Current);
        Refused(EditResult.InvalidTarget, hero with { Placement = BrowserPlacement.Object }, free.Current);

        void Refused(EditResult why, BrowserEntry entry, MapPosition at, PlayerId? player = null)
        {
            var steps = editor.History.Count;
            var check = editor.Placing.Check(entry, at, player);
            Assert.Equal(why, check.Result);
            Assert.NotEmpty(check.Cells);
            Assert.All(check.Cells, c => Assert.False(c.Fits));
            Assert.Equal(why, editor.Placing.Place(entry, at, out _, player));
            Assert.Equal(steps, editor.History.Count);
        }
    }

    [Fact]
    public void Search_SplitsWordsAtAnyWhiteSpace() =>
        Assert.Equal(Browser.Search("gold mine"), Browser.Search("gold\tmine\n"));

    private static MapEditorSession Editor() => new(BlankScenario.Create(40, 30), copier: MapObjectCopier.Instance);

    private static string Snapshot(MapEditorSession editor) => MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
}
