using EternalWars.Core.Data.Maps;
using EternalWars.Core.Data.Stamps;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;
using EternalWars.Core.Domain.Scenarios;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Data;

/// <summary>The stamp file (<c>*.ewstamp.json</c>, ADR-250): a fragment kept in a file of its own versioned format.</summary>
public class StampFormatTests
{
    private static readonly string Samples = Path.Combine(Pinned.Folder(), "StampSamples");

    private static string SamplePath(int version) => Path.Combine(Samples, $"v{version:00}{StampSerializer.FileExtension}");

    [Fact]
    public void TheFormat_WritesTheSampleFragmentAsPinned()
    {
        var text = StampSerializer.Serialize(StampSerializer.ToDocument(SampleFragment(), "Sample"));

        Pinned.Check(
            SamplePath(StampDocument.CurrentFormatVersion), text,
            "The stamp format writes the sample fragment differently than before: bump StampDocument.CurrentFormatVersion, keep the old sample and add a new one.");
    }

    /// <summary>Every sample, the older versions' too, reads into a fragment and writes as the current format writes it.</summary>
    [Fact]
    public void EverySample_Reads_AndSavesAsItsUpgradedForm()
    {
        var files = Directory.GetFiles(Samples, "*" + StampSerializer.FileExtension).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(
            Enumerable.Range(1, StampDocument.CurrentFormatVersion).Select(SamplePath).Order(StringComparer.Ordinal), files);
        foreach (var file in files)
        {
            var document = StampSerializer.Load(file);
            var fragment = StampSerializer.ToFragment(document, BlankScenario.Create(20, 20));
            var saved = StampSerializer.Serialize(StampSerializer.ToDocument(fragment, document.Name));

            var pinned = document.FormatVersion == StampDocument.CurrentFormatVersion
                ? file
                : file[..^StampSerializer.FileExtension.Length] + ".upgraded.json";
            Pinned.Check(pinned, saved, $"Reading {Path.GetFileName(file)} and saving it gives another file than before: an older stamp would now load differently.");
        }
    }

    [Fact]
    public void AStamp_PastesWhatTheFragmentItWasMadeOfPastes()
    {
        var fragment = SampleFragment();
        var document = StampSerializer.Deserialize(StampSerializer.Serialize(StampSerializer.ToDocument(fragment, "Sample")));
        var direct = Editor();
        var fromFile = Editor();

        Assert.Equal(EditResult.Ok, direct.Fragments.Paste(fragment, new MapPosition(3, 4)));
        Assert.Equal(EditResult.Ok, fromFile.Fragments.Paste(StampSerializer.ToFragment(document, fromFile.Scenario), new MapPosition(3, 4)));

        Assert.Equal(Snapshot(direct), Snapshot(fromFile));
        Assert.Equal("Sample", document.Name);
        Assert.Equal((StampDocument.CurrentFormatVersion, MapDocument.CurrentFormatVersion), (document.FormatVersion, document.MapFormat));
        var gates = fromFile.Scenario.ObjectsOf<TwoWayGate>().ToList();
        Assert.Equal(gates[1].Id, gates[0].LinkedGate); // the link inside the stamp came through the file
    }

    [Fact]
    public void AStampOfScatteredTiles_KeepsTheGapsEmpty()
    {
        var editor = Editor();
        editor.Terrain.Paint(new MapPosition(5, 5), TerrainType.Snow);
        editor.Selecting.Set(new SelectionItems([new MapPosition(5, 5), new MapPosition(7, 6)], [], [], []));
        var document = StampSerializer.ToDocument(editor.Fragments.Copy()!, "Two tiles");

        Assert.Equal(["n  ", "  g"], document.Terrain);
        Assert.Null(document.Roads);
        Assert.Equal(3, document.Heights!.Count);
        Assert.Equal(' ', document.Heights[0][2]);
        var fragment = StampSerializer.ToFragment(StampSerializer.Deserialize(StampSerializer.Serialize(document)), editor.Scenario);
        Assert.Equal([(0, 0), (2, 1)], fragment.Tiles.Select(t => (t.X, t.Y)));
        Assert.Equal(8, fragment.Corners.Count);
    }

    [Fact]
    public void AFileThatIsNoStamp_IsRefusedWithAFormatException()
    {
        var target = BlankScenario.Create(20, 20);
        var good = StampSerializer.ToDocument(SampleFragment(), "Sample");
        var chest = good.Objects.First(o => o.Definition == BuiltInMapObjects.TreasureChest);

        Assert.Throws<FormatException>(() => StampSerializer.Deserialize("not json"));
        Assert.Throws<FormatException>(() => StampSerializer.Deserialize("null"));
        var text = StampSerializer.Serialize(good);
        Assert.Throws<FormatException>(() => StampSerializer.Deserialize(text.Replace("\"kind\": \"chest\",", "", StringComparison.Ordinal)));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { Terrain = null! }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { Objects = null! }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { Objects = [null!] }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { Objects = [chest with { Definition = null! }] }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { FormatVersion = StampDocument.CurrentFormatVersion + 1 }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { MapFormat = MapDocument.CurrentFormatVersion + 1 }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { Width = good.Width + 1 }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { Terrain = [.. good.Terrain.Select(r => r.Replace('g', '?'))] }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { Heights = null }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { Objects = [.. good.Objects, chest] }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { Objects = [chest with { X = good.Width }] }, target));
        Assert.Throws<FormatException>(() => StampSerializer.ToFragment(good with { Objects = [chest with { Definition = "no-such-object" }] }, target));
    }

    [Fact]
    public void TheStampFolder_SavesListsAndLoadsByName()
    {
        var directory = Path.Combine(Path.GetTempPath(), "ew-stamps-" + Guid.NewGuid().ToString("N"));
        try
        {
            Assert.Empty(StampFolder.Names(directory));

            var path = StampFolder.Save(directory, "Village", SampleFragment());
            StampFolder.Save(directory, "A/B: pass?", SampleFragment());
            StampFolder.Save(directory, "  ", SampleFragment());

            Assert.Equal(Path.Combine(directory, "Village.ewstamp.json"), path);
            Assert.Equal(["A_B_ pass_", "Village", "stamp"], StampFolder.Names(directory));
            var loaded = StampFolder.Load(directory, "Village", BlankScenario.Create(20, 20));
            Assert.Equal(SampleFragment().Tiles, loaded.Tiles);
            Assert.Equal(SampleFragment().Objects.Count, loaded.Objects.Count);

            File.WriteAllText(StampFolder.PathOf(directory, "Broken"), "{ \"name\": 3 }");
            Assert.Throws<FormatException>(() => StampFolder.Load(directory, "Broken", BlankScenario.Create(20, 20)));
            Assert.ThrowsAny<IOException>(() => StampFolder.Load(directory, "Missing", BlankScenario.Create(20, 20)));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private static MapEditorSession Editor() => new(BlankScenario.Create(40, 30), copier: MapObjectCopier.Instance);

    /// <summary>Snow, a road, a river, a hill, a chest with fixed values, a guarding monster, a named town and two linked gates.</summary>
    private static MapFragment SampleFragment()
    {
        var editor = Editor();
        editor.Terrain.Paint(new MapPosition(4, 4), TerrainType.Snow);
        editor.Terrain.PaintRoad(new MapPosition(5, 4), RoadType.Cobblestone);
        editor.Terrain.PaintRiver(new MapPosition(4, 5), RiverType.Clear);
        Assert.True(editor.Heights.Raise(new MapPosition(6, 9), 1, 6) > 0);
        Ok(editor.Objects.Place(BuiltInMapObjects.TreasureChest, new MapPosition(4, 8), out var chest));
        Ok(editor.Contents.SetChestValues(chest, new ChestValues(1500, 1000)));
        Ok(editor.Objects.Place(BuiltInMapObjects.TwoWayMonolith, new MapPosition(4, 10), out var a));
        Ok(editor.Objects.Place(BuiltInMapObjects.TwoWayMonolith, new MapPosition(7, 10), out var b));
        Ok(editor.Objects.LinkGates(a, b));
        Ok(editor.Monsters.Place(new MapPosition(5, 9), BuiltInCreatures.Wolf, 12, GuardDisposition.Hostile, true, out _));
        Ok(editor.Objects.Place(BuiltInMapObjects.TownId(TownIds.Haven), new MapPosition(10, 4), out var town));
        Ok(editor.Towns.SetName(town, "Brightwater"));
        editor.Selecting.Set(editor.Selecting.In(new TileArea(4, 4, 17, 10, 0), SelectionFilter.All & ~SelectionFilter.Heroes));
        return editor.Fragments.Copy()!;
    }

    private static void Ok(EditResult result) => Assert.Equal(EditResult.Ok, result);

    private static string Snapshot(MapEditorSession editor) => MapSerializer.Serialize(MapConverter.ToDocument(editor.Scenario));
}
