using EternalWars.Core.Data.Content;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Towns;

namespace EternalWars.Core.Tests.Domain;

public class CreatureTests
{
    [Theory]
    [InlineData(TownIds.Haven)]
    [InlineData(TownIds.Sylvan)]
    [InlineData(TownIds.Academy)]
    [InlineData(TownIds.Necropolis)]
    [InlineData(TownIds.Inferno)]
    [InlineData(TownIds.Dungeon)]
    [InlineData(TownIds.Stronghold)]
    [InlineData(TownIds.SwampBestiary)]
    public void PlayableTowns_HaveNineUpgradeableLines_TwoPerTierAndOneAtTierFive(string town)
    {
        var catalog = CreatureCatalog.Default;
        var lines = catalog.BaseCreaturesOf(town).ToList();

        Assert.Equal(9, lines.Count);
        Assert.Equal([2, 2, 2, 2, 1], Enumerable.Range(1, 5).Select(tier => lines.Count(c => c.Tier == tier)));
        Assert.All(lines, line =>
        {
            Assert.NotNull(line.UpgradesTo);
            var upgrade = catalog.Get(line.UpgradesTo!);
            Assert.Equal((line.Tier, line.Town, line.Size), (upgrade.Tier, upgrade.Town, upgrade.Size));
            Assert.True(upgrade.Value > line.Value, $"{upgrade.Id} should be stronger than {line.Id}");
        });
        Assert.Equal(18, catalog.All.Count(c => c.Town == town));
    }

    [Fact]
    public void Catalog_RejectsBrokenUpgrades()
    {
        var basic = new CreatureDefinition("x.base", "Base", 2, CreatureSize.Small)
        {
            Health = 10, MinDamage = 1, MaxDamage = 2, Speed = 5, MapSpeed = 5, Initiative = 5, UpgradesTo = "x.up",
        };
        var wrongTier = basic with { Id = "x.up", Tier = 3, UpgradesTo = null };

        Assert.Throws<FormatException>(() => new CreatureCatalog([basic]));
        Assert.Throws<FormatException>(() => new CreatureCatalog([basic, wrongTier]));
        Assert.Throws<FormatException>(() => new CreatureCatalog([basic with { Tier = 6, UpgradesTo = null }]));
    }

    [Fact]
    public void Definitions_RoundTripThroughJson_AndOverrideTheBuiltIns()
    {
        var json = CreatureDefinitionSerializer.Serialize(BuiltInCreatures.All);
        var loaded = CreatureDefinitionSerializer.Deserialize(json);

        Assert.Equal(BuiltInCreatures.All.Count, loaded.Count);
        Assert.All(BuiltInCreatures.All.Zip(loaded), pair =>
        {
            string[] noTags = [];
            CreatureCapability[] noAbilities = [];
            Assert.Equal(pair.First with { Tags = noTags, Capabilities = noAbilities }, pair.Second with { Tags = noTags, Capabilities = noAbilities });
            Assert.Equal(pair.First.Tags, pair.Second.Tags);
            Assert.Equal(pair.First.Capabilities, pair.Second.Capabilities);
        });

        var tougherWolf = CreatureDefinitionSerializer.Deserialize("""
            [ { "id": "neutral.wolf", "name": "Dire wolf", "tier": 1, "size": "Medium", "attack": 9, "defense": 6,
                "minDamage": 4, "maxDamage": 7, "health": 20, "speed": 14, "mapSpeed": 7, "initiative": 13,
                "capabilities": [ { "kind": "PackHunter", "value": 25 } ] } ]
            """);
        var catalog = CreatureCatalog.Default.With(tougherWolf);

        Assert.Equal(("Dire wolf", CreatureSize.Medium, 25), (catalog.Get(BuiltInCreatures.Wolf).Name, catalog.Get(BuiltInCreatures.Wolf).Size,
            catalog.Get(BuiltInCreatures.Wolf).CapabilityValue(CapabilityKind.PackHunter)));
    }

    [Fact]
    public void EveryTierIsStrongerThanTheOneBelow_ByAverageValue()
    {
        var playable = new[] { TownIds.Haven, TownIds.Sylvan, TownIds.Academy, TownIds.Necropolis, TownIds.Inferno, TownIds.Dungeon, TownIds.Stronghold, TownIds.SwampBestiary };
        var bases = playable.SelectMany(CreatureCatalog.Default.BaseCreaturesOf).ToList();
        var averages = Enumerable.Range(1, 5).Select(tier => bases.Where(c => c.Tier == tier).Average(c => c.Value)).ToList();

        Assert.All(averages.Zip(averages.Skip(1)), pair => Assert.True(pair.Second > pair.First * 1.8, $"{pair.First:0} → {pair.Second:0}"));
        Assert.Contains(CreatureCatalog.Default.All, c => c.Town == TownIds.Conflux);
        Assert.True(CreatureCatalog.Default.Get("dragon-utopia.black-dragon").Value > 2 * CreatureCatalog.Default.Get(BuiltInCreatures.RedDragon).Value);
    }

    [Fact]
    public void ShippedTable_HasNoCopyPastedStatLines()
    {
        var duplicates = BuiltInCreatures.All
            .GroupBy(c => (c.Attack, c.Defense, c.MinDamage, c.MaxDamage, c.Health, c.Speed, c.Initiative, c.Shots))
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(" = ", g.Select(c => c.Id)))
            .ToList();

        Assert.Empty(duplicates);
    }

    [Fact]
    public void Table_RoundTripsEveryBuiltInCreature()
    {
        var text = CreatureTable.Format(BuiltInCreatures.All, c => $"note, with \"quotes\" for {c.Name}");
        var loaded = CreatureTable.Parse(text);

        Assert.Equal(BuiltInCreatures.All.Count, loaded.Count);
        Assert.All(BuiltInCreatures.All.Zip(loaded), pair =>
        {
            Assert.Equal(pair.First with { Tags = [], Capabilities = [] }, pair.Second with { Tags = [], Capabilities = [] });
            Assert.Equal(pair.First.Tags, pair.Second.Tags);
            Assert.Equal(pair.First.Capabilities, pair.Second.Capabilities);
        });
    }

    [Fact]
    public void Table_AcceptsExcelStyleSemicolons_AnyColumnOrder_AndExtraColumns()
    {
        var text = "﻿sep=;\r\n" +
                   "Name;ID;tier;size;attack;defense;min_damage;max_damage;health;speed;map_speed;initiative;shots;gold;resources;capabilities;my formula\r\n" +
                   "Wolf;neutral.wolf;1;small;6;4;3;5;12;14;7;13;;60;crystal 1;\"Flying; PackHunter 20%; Caster howl x2\";=A2*2\r\n" +
                   ";;;;;;;;;;;;;;;;\r\n";

        var wolf = Assert.Single(CreatureTable.Parse(text));

        Assert.Equal(("neutral.wolf", "Wolf", CreatureSize.Small, 0, null), (wolf.Id, wolf.Name, wolf.Size, wolf.Shots, wolf.Town));
        Assert.Equal(new ResourceSet(Gold: 60, Crystal: 1), wolf.Cost);
        Assert.Equal([new(CapabilityKind.Flying), new(CapabilityKind.PackHunter, 20), new(CapabilityKind.Caster, 2, "howl")], wolf.Capabilities);
    }

    [Theory]
    [InlineData("haven.x,X,2,Small,1,1,1,1,1,1,1,1,Flyng", "Line 2, column 'capabilities': unknown capability 'Flyng'")]
    [InlineData("haven.x,X,2,Small,1,1,1,1,ten,1,1,1,", "Line 2, column 'health': 'ten' is not a whole number")]
    [InlineData("haven.x,X,2,Huge,1,1,1,1,1,1,1,1,", "Line 2, column 'size'")]
    [InlineData("haven.x,X,7,Small,1,1,1,1,1,1,1,1,", "Line 2: Creature 'haven.x' has tier 7")]
    public void Table_ReportsTheLineAndColumnOfABadCell(string row, string expected)
    {
        const string header = "id,name,tier,size,attack,defense,min_damage,max_damage,health,speed,map_speed,initiative,capabilities\n";

        var error = Assert.Throws<FormatException>(() => CreatureTable.Parse(header + row));

        Assert.StartsWith(expected, error.Message);
    }

    [Fact]
    public void Table_NeedsTheCoreColumns()
    {
        var error = Assert.Throws<FormatException>(() => CreatureTable.Parse("id,name\nx,X\n"));

        Assert.Contains("tier", error.Message);
    }
}
