using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>Zone presets (ADR-273): the built-in ones, applying and adding them through the session, and the user's preset files.</summary>
public sealed class ZonePresetTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "ew-zone-presets-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static ZonePreset BuiltIn(string name) => ZonePresets.BuiltIn.Single(p => p.Name == name);

    [Fact]
    public void TheBuiltInPresets_AreRichTreasureMineJunctionAndGuardedStart_AndMakeValidZones()
    {
        Assert.Equal(["Rich treasure", "Mine junction", "Guarded start"], ZonePresets.BuiltIn.Select(p => p.Name));
        Assert.All(ZonePresets.BuiltIn, p => Assert.True(p.BuiltIn));

        var session = new TemplateEditorSession();
        foreach (var preset in ZonePresets.BuiltIn)
        {
            Assert.NotNull(session.AddZone(preset, 0, 5));
        }

        foreach (var zone in session.Template.Zones.Where(z => !z.Id.StartsWith("start-", StringComparison.Ordinal) || z.Id == "start-3"))
        {
            Assert.True(session.Connect("middle", zone.Id) || zone.Id == "middle");
        }

        Assert.DoesNotContain(session.Issues, i => i.Severity == IssueSeverity.Error);
        Assert.Equal(3, session.Template.MaxPlayers); // the guarded start is a third player's
    }

    [Fact]
    public void Of_KeepsTheSettings_WithoutWhatBelongsToTheZone()
    {
        var zone = new TemplateZone
        {
            Id = "start-2", Type = ZoneType.PlayerStart, Owner = 1, Team = 0, Level = 1, Mirror = "start-1", GraphX = 3, GraphY = 4, Size = 5,
            Terrains = [TerrainType.Snow], PlayerTowns = CountRange.Exactly(1),
        };

        var preset = ZonePresets.Of("  Cold start ", zone);

        Assert.Equal("Cold start", preset.Name);
        Assert.False(preset.BuiltIn);
        var kept = preset.Zone;
        Assert.Equal((ZonePresets.PresetId, (int?)null, (int?)null, 0, (string?)null, (double?)null, (double?)null),
            (kept.Id, kept.Owner, kept.Team, kept.Level, kept.Mirror, kept.GraphX, kept.GraphY));
        Assert.Equal((zone.Type, zone.Size, zone.PlayerTowns), (kept.Type, kept.Size, kept.PlayerTowns));
        Assert.Equal([TerrainType.Snow], kept.Terrains); // a copy of the list
        Assert.NotSame(zone.Terrains, kept.Terrains);
    }

    [Fact]
    public void ApplyPreset_GivesTheZoneItsSettings_KeepingIdSlotMirrorLevelAndPlace_AsOneStep()
    {
        var session = new TemplateEditorSession();
        var before = session.Template.Zone("middle")!;

        Assert.True(session.ApplyPreset("middle", BuiltIn("Rich treasure")));

        var after = session.Template.Zone("middle")!;
        Assert.Equal(ZoneType.Treasure, after.Type);
        Assert.Equal(2, after.Treasure.Count);
        Assert.Equal((before.Id, before.GraphX, before.GraphY, before.Level, before.Owner), (after.Id, after.GraphX, after.GraphY, after.Level, after.Owner));
        Assert.Equal(["Apply preset Rich treasure to zone middle"], session.History.Steps);

        Assert.True(session.ApplyPreset("middle", BuiltIn("Guarded start"))); // made a start zone: the lowest free slot
        Assert.Equal(2, session.Template.Zone("middle")!.Owner);
        Assert.Equal(3, session.Template.MaxPlayers);

        Assert.True(session.ApplyPreset("start-1", BuiltIn("Mine junction"))); // made a junction: no slot, no team
        Assert.Null(session.Template.Zone("start-1")!.Owner);

        Assert.False(session.ApplyPreset("nowhere", BuiltIn("Mine junction")));
    }

    [Fact]
    public void ApplyPreset_RefusesAStartZone_WhenNoSlotIsFree_AndCarriesToMirrors()
    {
        var full = new TemplateEditorSession();
        for (var i = 0; i < 6; i++)
        {
            Assert.NotNull(full.AddZone(ZoneType.PlayerStart, i, 4));
        }

        Assert.False(full.ApplyPreset("middle", BuiltIn("Guarded start")));
        Assert.Null(full.AddZone(BuiltIn("Guarded start"), 0, 8));
        Assert.Equal(ZoneType.Junction, full.Template.Zone("middle")!.Type);

        var blank = TemplateEditorSession.Blank();
        var mirrored = new TemplateEditorSession(blank with
        {
            Symmetry = TemplateSymmetry.Vertical,
            Zones = [.. blank.Zones.Select(z => z.Id switch { "start-1" => z with { Mirror = "start-2" }, "start-2" => z with { Mirror = "start-1" }, _ => z })],
        });
        Assert.True(mirrored.ApplyPreset("start-1", BuiltIn("Guarded start")));
        Assert.Equal(MineGuarding.Always, mirrored.Template.Zone("start-2")!.MineGuards); // in sync (ADR-272)
        Assert.Equal(1, mirrored.Template.Zone("start-2")!.Owner);
    }

    [Fact]
    public void AddZone_FromAPreset_TakesTheTypesIdAndNamesThePresetInTheStep()
    {
        var session = new TemplateEditorSession();

        Assert.Equal("junction-1", session.AddZone(BuiltIn("Mine junction"), 2, 3));
        Assert.Equal("Add zone junction-1 (Mine junction)", session.History.UndoDescription);
        var zone = session.Template.Zone("junction-1")!;
        Assert.Equal((2.0, 3.0), (zone.GraphX!.Value, zone.GraphY!.Value));
        Assert.Equal(MineGuarding.Always, zone.MineGuards);
    }

    [Fact]
    public void TheFolder_SavesListsAndDeletesPresets_ByName()
    {
        Assert.Empty(ZonePresetFolder.All(_folder)); // no folder yet

        var cold = ZonePresets.Of("cold: start", new TemplateZone { Id = "x", Size = 4, Terrains = [TerrainType.Snow], Owner = 3 });
        var path = ZonePresetFolder.Save(_folder, cold);
        ZonePresetFolder.Save(_folder, ZonePresets.Of("Aardvark", new TemplateZone { Id = "y", Type = ZoneType.Junction }));
        File.WriteAllText(Path.Combine(_folder, "broken" + ZonePresetFolder.FileExtension), "{ not json");
        File.WriteAllText(Path.Combine(_folder, "newer" + ZonePresetFolder.FileExtension), """{ "formatVersion": 999, "name": "Newer", "zone": { "id": "preset" } }""");

        Assert.Equal("cold_ start" + ZonePresetFolder.FileExtension, Path.GetFileName(path)); // no colon in a file name
        var all = ZonePresetFolder.All(_folder);
        Assert.Equal(["Aardvark", "cold: start"], all.Select(p => p.Name)); // the broken and the newer files are left out
        Assert.Equal(cold.Zone.Size, all[1].Zone.Size);
        Assert.Equal([TerrainType.Snow], all[1].Zone.Terrains);
        Assert.Null(all[1].Zone.Owner);

        Assert.True(ZonePresetFolder.Delete(_folder, "cold: start"));
        Assert.False(ZonePresetFolder.Delete(_folder, "cold: start"));
        Assert.Equal(["Aardvark"], ZonePresetFolder.All(_folder).Select(p => p.Name));
    }

    [Fact]
    public void Deserialize_RefusesWhatIsNoPreset()
    {
        Assert.Throws<FormatException>(() => ZonePresetFolder.Deserialize("[]"));
        Assert.Throws<FormatException>(() => ZonePresetFolder.Deserialize("""{ "name": "", "zone": { "id": "preset" } }"""));
        Assert.Throws<FormatException>(() => ZonePresetFolder.Deserialize("""{ "name": "No zone" }"""));
    }

    [Fact]
    public void Deserialize_MakesNullListsEmpty_AndRefusesWrongTypes()
    {
        var preset = ZonePresetFolder.Deserialize("""{ "name": "a", "zone": { "id": "x", "mines": null, "treasure": [null, { "minValue": 1, "maxValue": 2, "count": 1 }] } }""");
        Assert.Empty(preset.Zone.Mines);
        Assert.Single(preset.Zone.Treasure);

        var session = new TemplateEditorSession();
        Assert.True(session.ApplyPreset("middle", preset));
        Assert.NotNull(session.Issues); // the validator meets no null

        Assert.Throws<FormatException>(() => ZonePresetFolder.Deserialize("""{ "name": "a", "zone": { "id": "x", "size": "big" } }"""));
    }

    [Fact]
    public void ZonesMadeFromOnePreset_ShareNoTable()
    {
        var session = new TemplateEditorSession();
        var a = session.AddZone(BuiltIn("Rich treasure"), 0, 4)!;
        var b = session.AddZone(BuiltIn("Rich treasure"), 3, 4)!;
        Assert.NotSame(session.Template.Zone(a)!.Mines, session.Template.Zone(b)!.Mines);
        Assert.NotSame(BuiltIn("Rich treasure").Zone.Mines, session.Template.Zone(a)!.Mines);
    }

    [Fact]
    public void Delete_FindsAPresetWhoseFileWasRenamed()
    {
        var path = ZonePresetFolder.Save(_folder, ZonePresets.Of("Bar", new TemplateZone { Id = "x" }));
        File.Move(path, Path.Combine(_folder, "foo" + ZonePresetFolder.FileExtension));

        Assert.True(ZonePresetFolder.Delete(_folder, "bar"));
        Assert.Empty(ZonePresetFolder.All(_folder));
    }

    [Fact]
    public void ApplyPreset_ToAStartZoneWithATeam_DropsSlotAndTeam_AndIsRefusedWhenTheImagesFindNoSlot()
    {
        var session = new TemplateEditorSession();
        Assert.True(session.UpdateZone(session.Template.Zone("start-1")! with { Team = 1 }));
        Assert.NotNull(session.AddZone(ZoneType.PlayerStart, 0, 4)); // a third start, so two are left after the junction
        Assert.True(session.ApplyPreset("start-1", BuiltIn("Mine junction")));
        Assert.Equal(((int?)null, (int?)null), (session.Template.Zone("start-1")!.Owner, session.Template.Zone("start-1")!.Team));
        Assert.Equal(2, session.Template.MaxPlayers);

        var quarter = new TemplateEditorSession(new MapTemplate
        {
            Name = "Quarter", MaxPlayers = 4, Symmetry = TemplateSymmetry.QuarterTurn,
            Zones = [new TemplateZone { Id = "hub", Type = ZoneType.Junction, GraphX = 0, GraphY = 0 }, new TemplateZone { Id = "a", GraphX = -3, GraphY = 0 }],
            Connections = [new TemplateConnection { From = "a", To = "hub" }],
        });
        Assert.NotNull(quarter.MakeMirror(["a"]));
        for (var i = 0; i < 5; i++)
        {
            Assert.NotNull(quarter.AddZone(ZoneType.PlayerStart, i, 9)); // 5 of 8 slots: a and its 3 images would need 4
        }

        var before = quarter.Template;
        Assert.False(quarter.ApplyPreset("a", BuiltIn("Guarded start")));
        Assert.Same(before, quarter.Template);
    }
}
