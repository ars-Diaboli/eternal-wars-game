using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Generation;

/// <summary>The random map template model, its file format and its validator (Phase 4).</summary>
public class TemplateTests
{
    private static TemplateZone Start(string id, int owner) => new()
    {
        Id = id, Type = ZoneType.PlayerStart, Owner = owner, Size = 3, PlayerTowns = CountRange.Exactly(1),
        Mines = new Dictionary<ResourceType, int> { [ResourceType.Gold] = 1 }, Monsters = MonsterStrength.None,
    };

    /// <summary>Two starts joined through a junction: valid, and the base of most tests.</summary>
    private static MapTemplate Duel() => new()
    {
        Name = "Test duel",
        Sizes = [72, 96],
        Zones = [Start("a", 0), Start("b", 1), new TemplateZone { Id = "mid", Type = ZoneType.Junction, Size = 2 }],
        Connections =
        [
            new TemplateConnection { From = "a", To = "mid", GuardValue = 1000 },
            new TemplateConnection { From = "b", To = "mid", GuardValue = 1000 },
        ],
    };

    private static IEnumerable<string> Errors(MapTemplate template) =>
        TemplateValidator.Validate(template).Where(i => i.Severity == IssueSeverity.Error).Select(i => i.Message);

    private static IEnumerable<string> Warnings(MapTemplate template) =>
        TemplateValidator.Validate(template).Where(i => i.Severity == IssueSeverity.Warning).Select(i => i.Message);

    [Fact]
    public void AGoodTemplate_HasNoIssues()
    {
        Assert.Empty(TemplateValidator.Validate(Duel()));
        Assert.True(TemplateValidator.IsValid(Duel()));
    }

    [Fact]
    public void TheShippedTemplates_AreValidAndFair()
    {
        Assert.NotEmpty(BuiltInTemplates.All);
        foreach (var template in BuiltInTemplates.All)
        {
            Assert.Empty(TemplateValidator.Validate(template).Select(i => $"{template.Name}: {i.Message}"));
        }

        Assert.Equal("Duel", BuiltInTemplates.Find("duel")!.Name);
    }

    [Fact]
    public void AShippedTemplate_IsFoundByNameOrFileKey()
    {
        Assert.Equal(["2v2", "duel", "ffa-4"], BuiltInTemplates.Keys);
        Assert.Same(BuiltInTemplates.Find("Two against two"), BuiltInTemplates.Find("2v2"));
        Assert.Same(BuiltInTemplates.Find("Free for all"), BuiltInTemplates.Find("FFA-4"));
        Assert.Null(BuiltInTemplates.Find("nope"));
    }

    [Fact]
    public void ATemplate_SurvivesTheFileFormat()
    {
        var template = Duel() with
        {
            MaxHumans = 1,
            Underground = true,
            Zones = [.. Duel().Zones.Select(z => z with { Terrains = [TerrainType.Grass, TerrainType.Dirt] }), new TemplateZone { Id = "deep", Level = 1, Treasure = [new TreasureBand(500, 1500, 3)] }],
            Connections = [.. Duel().Connections, new TemplateConnection { From = "mid", To = "deep", Kind = ConnectionKind.Gate, GuardValue = 500 }],
        };

        var json = TemplateSerializer.Serialize(template);
        var loaded = TemplateSerializer.Deserialize(json);

        Assert.Contains("\"kind\": \"Gate\"", json);
        Assert.Equal(template.Zones.Select(z => (z.Id, z.Type, z.Owner, z.Size, z.Level, z.PlayerTowns)), loaded.Zones.Select(z => (z.Id, z.Type, z.Owner, z.Size, z.Level, z.PlayerTowns)));
        Assert.Equal(template.Connections, loaded.Connections);
        Assert.Equal([TerrainType.Grass, TerrainType.Dirt], loaded.Zones[0].Terrains);
        Assert.Equal(new TreasureBand(500, 1500, 3), loaded.Zones[3].Treasure.Single());
        Assert.Equal(ResourceType.Gold, loaded.Zones[0].Mines.Keys.Single());
        Assert.Equal((1, true, 2), (loaded.MaxHumans, loaded.Underground, loaded.Levels));
        Assert.Equal(template.Sizes, loaded.Sizes);
    }

    [Fact]
    public void ABrokenOrNewerFile_IsRefusedWithAClearError()
    {
        Assert.Throws<FormatException>(() => TemplateSerializer.Deserialize("{ nope"));
        Assert.Throws<FormatException>(() => TemplateSerializer.Deserialize("null"));
        Assert.Throws<NotSupportedException>(() => TemplateSerializer.Deserialize("""{ "formatVersion": 99, "name": "x" }"""));
    }

    [Fact]
    public void Players_SizesAndNames_AreChecked()
    {
        Assert.Contains(Errors(Duel() with { Name = " " }), e => e.Contains("name"));
        Assert.Contains(Errors(Duel() with { MinPlayers = 1 }), e => e.Contains("player count"));
        Assert.Contains(Errors(Duel() with { MinPlayers = 3 }), e => e.Contains("player count"));
        Assert.Contains(Errors(Duel() with { MaxPlayers = 9 }), e => e.Contains("player count"));
        Assert.Contains(Errors(Duel() with { MaxHumans = 0 }), e => e.Contains("MaxHumans"));
        Assert.Contains(Errors(Duel() with { Sizes = [] }), e => e.Contains("at least one map size"));
        Assert.Contains(Errors(Duel() with { Sizes = [100] }), e => e.Contains("not a standard size"));
        Assert.Contains(Errors(Duel() with { Zones = [] }), e => e.Contains("no zones"));
    }

    [Fact]
    public void ZonesPerMapSize_AreWarnedAboutWhenTooCrowded()
    {
        var crowded = Duel() with { Sizes = [48], Zones = [.. Duel().Zones, .. Enumerable.Range(0, 20).Select(i => new TemplateZone { Id = $"z{i}" })] };

        Assert.Contains(Warnings(crowded), w => w.Contains("too small"));
        Assert.DoesNotContain(Warnings(Duel() with { Sizes = [48] }), w => w.Contains("too small"));
    }

    [Fact]
    public void Zones_AreChecked()
    {
        var a = Start("a", 0);
        var zones = Duel().Zones;
        Assert.Contains(Errors(Duel() with { Zones = [.. zones, a] }), e => e.Contains("used twice"));
        Assert.Contains(Errors(Duel() with { Zones = [.. zones.Skip(1), a with { Id = "" }] }), e => e.Contains("needs an id"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { Size = 0 }, .. zones.Skip(1)] }), e => e.Contains("size of at least 1"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { Level = 1 }, .. zones.Skip(1)] }), e => e.Contains("level 1"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { Owner = null }, .. zones.Skip(1)] }), e => e.Contains("owner slot"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { Owner = 5 }, .. zones.Skip(1)] }), e => e.Contains("owner slot"));
        Assert.Contains(Errors(Duel() with { Zones = [.. zones.Take(2), zones[2] with { Owner = 1 }] }), e => e.Contains("has no owner"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { PlayerTowns = CountRange.None }, .. zones.Skip(1)] }), e => e.Contains("at least one player town"));
        Assert.Contains(Errors(Duel() with { Zones = [.. zones.Take(2), zones[2] with { PlayerTowns = CountRange.Exactly(1) }] }), e => e.Contains("cannot have player towns"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { NeutralTowns = new CountRange(3, 1) }, .. zones.Skip(1)] }), e => e.Contains("neutral towns"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { TownTypes = ["atlantis"] }, .. zones.Skip(1)] }), e => e.Contains("unknown town type"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { Mines = new Dictionary<ResourceType, int> { [ResourceType.Ore] = -1 } }, .. zones.Skip(1)] }), e => e.Contains("negative number of Ore"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { ExtraPiles = -1 }, .. zones.Skip(1)] }), e => e.Contains("extra piles"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { Treasure = [new TreasureBand(500, 100, 1)] }, .. zones.Skip(1)] }), e => e.Contains("treasure band"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { Treasure = [new TreasureBand(100, 500, 0)] }, .. zones.Skip(1)] }), e => e.Contains("treasure band"));
        Assert.Contains(Errors(Duel() with { Zones = [a with { Terrains = [TerrainType.Lava, TerrainType.Rock] }, .. zones.Skip(1)] }), e => e.Contains("Rock") && e.Contains("cannot be walked on"));
    }

    [Fact]
    public void ATemplateNeedsAMatchingNumberOfStartZones_WithDistinctOwners()
    {
        var zones = Duel().Zones;
        Assert.Contains(Errors(Duel() with { MaxPlayers = 3 }), e => e.Contains("3 start zones, not 2"));
        Assert.Contains(Errors(Duel() with { Zones = [zones[0], Start("b", 0), zones[2]] }), e => e.Contains("slot 0 owns more than one"));
        Assert.Contains(Errors(Duel() with { Zones = [zones[0] with { Type = ZoneType.AiStart }, zones[1] with { Type = ZoneType.AiStart }, zones[2]] }), e => e.Contains("no place to start"));
        Assert.Empty(Errors(Duel() with { Zones = [zones[0], zones[1] with { Type = ZoneType.AiStart }, zones[2]], MaxHumans = 1 }));
    }

    [Fact]
    public void Connections_AreChecked()
    {
        var connections = Duel().Connections;
        Assert.Contains(Errors(Duel() with { Connections = [.. connections, new TemplateConnection { From = "a", To = "nowhere" }] }), e => e.Contains("does not exist"));
        Assert.Contains(Errors(Duel() with { Connections = [.. connections, new TemplateConnection { From = "a", To = "a" }] }), e => e.Contains("to itself"));
        Assert.Contains(Errors(Duel() with { Connections = [.. connections, new TemplateConnection { From = "mid", To = "a" }] }), e => e.Contains("listed twice"));
        Assert.Contains(Errors(Duel() with { Connections = [connections[0] with { GuardValue = -1 }, connections[1]] }), e => e.Contains("negative guard"));
        Assert.Contains(Warnings(Duel() with { Connections = [connections[0] with { Kind = ConnectionKind.Wide }, connections[1]] }), w => w.Contains("ignored"));
    }

    [Fact]
    public void LevelsAreOnlyJoinedByGates()
    {
        var template = Duel() with
        {
            Underground = true,
            Zones = [.. Duel().Zones, new TemplateZone { Id = "deep", Level = 1 }],
        };

        var normal = template with { Connections = [.. template.Connections, new TemplateConnection { From = "mid", To = "deep" }] };
        var gate = template with { Connections = [.. template.Connections, new TemplateConnection { From = "mid", To = "deep", Kind = ConnectionKind.Gate }] };

        Assert.Contains(Errors(normal), e => e.Contains("only a gate"));
        Assert.Empty(Errors(gate));
    }

    [Fact]
    public void EveryZoneMustBeReachable()
    {
        var island = new TemplateZone { Id = "island" };
        var lonely = Duel() with { Zones = [.. Duel().Zones, island] };
        var split = lonely with { Zones = [.. lonely.Zones, new TemplateZone { Id = "other" }], Connections = [.. lonely.Connections, new TemplateConnection { From = "island", To = "other" }] };

        Assert.Contains(Errors(lonely), e => e.Contains("Zone 'island' has no connection"));
        Assert.Contains(Errors(split), e => e.Contains("not connected") && e.Contains("island") && e.Contains("other"));
    }

    [Fact]
    public void UnfairStartZones_AreWarnedAbout()
    {
        var zones = Duel().Zones;
        Assert.Contains(Warnings(Duel() with { Zones = [zones[0], zones[1] with { Size = 5 }, zones[2]] }), w => w.Contains("differ in size"));
        Assert.Contains(Warnings(Duel() with { Zones = [zones[0], zones[1] with { ExtraPiles = 4 }, zones[2]] }), w => w.Contains("differ in towns, mines"));
        Assert.Contains(Warnings(Duel() with { Zones = [zones[0], zones[1] with { Treasure = [new TreasureBand(1, 2, 1)] }, zones[2]] }), w => w.Contains("differ in towns, mines"));
        Assert.Contains(Warnings(Duel() with { Zones = [zones[0], zones[1] with { Mines = new Dictionary<ResourceType, int> { [ResourceType.Ore] = 1 } }, zones[2]] }), w => w.Contains("differ in towns, mines"));

        var far = Duel() with
        {
            Zones = [.. zones, new TemplateZone { Id = "extra" }, Start("c", 2)],
            MaxPlayers = 3,
            Connections = [.. Duel().Connections, new TemplateConnection { From = "mid", To = "extra" }, new TemplateConnection { From = "extra", To = "c" }, new TemplateConnection { From = "a", To = "b" }],
        };
        Assert.Contains(Warnings(far), w => w.Contains("nearest rival"));
    }
}
