using System.Reflection;
using EternalWars.Core.Data.Templates;
using EternalWars.Core.Domain.Artifacts;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Editing;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Magic;
using EternalWars.Core.Domain.Towns;
using EternalWars.Core.Generation;

namespace EternalWars.Core.Tests.Data;

/// <summary>
/// One schema test per shipped data table in <c>src/Core/data</c> (ADR-071, DATA-1): the columns are exactly the known ones,
/// ids are unique and every reference names something that exists.
/// </summary>
public class DataTableSchemaTests
{
    private static readonly HashSet<string> Towns = typeof(TownIds).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet(StringComparer.Ordinal);

    [Fact]
    public void Creatures_HaveTheKnownColumns_UniqueIds_AndKnownTownsAndUpgrades()
    {
        var text = BuiltInCreatures.EmbeddedTable();
        Assert.Equal(CreatureTable.Columns, Header(text, "Creature"));

        // The catalog rejects duplicate ids and upgrades to unknown creatures or across tiers and towns.
        var catalog = new CreatureCatalog(CreatureTable.Parse(text));
        Assert.All(catalog.All, c => Assert.True(c.Town is null || Towns.Contains(c.Town), $"{c.Id}: unknown town '{c.Town}'"));
        Assert.All(catalog.All.Where(c => c.Town is not null), c => Assert.StartsWith($"{c.Town}.", c.Id, StringComparison.Ordinal));
    }

    [Fact]
    public void Heroes_HaveTheKnownColumns_UniqueIds_AndKnownTownsAndCreatures()
    {
        var text = BuiltInHeroes.EmbeddedTable();
        Assert.Equal(HeroTable.Columns, Header(text, "Hero"));

        // Skills and specialty skills are checked while parsing; preferred creatures against the creature table.
        var catalog = new HeroCatalog(HeroTable.Parse(text));
        Assert.All(catalog.All, h => Assert.Contains(h.Town, Towns));
        Assert.Empty(catalog.Check(CreatureCatalog.Default));
    }

    [Fact]
    public void Buildings_HaveTheKnownColumns_GaplessLevels_AndKnownRequirements()
    {
        var text = BuiltInBuildings.EmbeddedTable();
        Assert.Equal(BuildingTable.Columns, Header(text, "Building"));

        // The catalog rejects level gaps, repeats, unknown requirements and missing dwelling tiers, for the shared list and every town's.
        var catalog = new BuildingCatalog(BuildingTable.Parse(text));
        Assert.All(catalog.TownTypes, town => Assert.Contains(town, Towns));
        string[] referencedByCode =
        [
            BuiltInBuildings.TownHall, BuiltInBuildings.Fort, BuiltInBuildings.Blacksmith, BuiltInBuildings.Marketplace,
            BuiltInBuildings.MageGuild, BuiltInBuildings.Tavern, BuiltInBuildings.Caravan,
        ];
        Assert.All(referencedByCode, id => Assert.True(catalog.Contains(id), $"code refers to '{id}'"));
        Assert.All(BuiltInBuildings.DefaultStart, b => Assert.InRange(b.Value, 1, catalog.MaxLevel(b.Key)));
    }

    [Fact]
    public void Artifacts_HaveTheKnownColumns_UniqueIds_AndKnownSets()
    {
        var artifacts = BuiltInArtifacts.EmbeddedTable(BuiltInArtifacts.ArtifactsResource);
        var sets = BuiltInArtifacts.EmbeddedTable(BuiltInArtifacts.SetsResource);
        Assert.Equal(ArtifactTable.ArtifactColumns, Header(artifacts, "Artifact"));
        Assert.Equal(ArtifactTable.SetColumns, Header(sets, "Artifact set"));

        // The catalog rejects duplicate ids, unknown sets and sets with fewer than two pieces.
        var catalog = new ArtifactCatalog(ArtifactTable.ParseArtifacts(artifacts), ArtifactTable.ParseSets(sets));
        Assert.NotEmpty(catalog.All);
    }

    [Fact]
    public void Spells_HaveTheKnownColumns_UniqueIds_AndSchoolsOfTheSixMagicTowns()
    {
        var text = BuiltInSpells.EmbeddedTable();
        Assert.Equal(SpellTable.Columns, Header(text, "Spell"));

        // The catalog rejects duplicate ids and adventure spells with a battle effect; battle effects are checked while parsing.
        var catalog = new SpellCatalog(SpellTable.Parse(text));
        Assert.All(catalog.All, s => Assert.True(BuiltInSkills.Schools.ContainsKey(s.School), $"{s.Id}: '{s.School}' is no magic school's town"));
        Assert.All(catalog.All, s => Assert.True(s.Mana >= 0, $"{s.Id} costs negative mana")); // Blood Price costs health, not mana
    }

    [Fact]
    public void Templates_AreUniquelyNamed_AndValid()
    {
        Assert.NotEmpty(BuiltInTemplates.All);
        Assert.Equal(BuiltInTemplates.All.Count, BuiltInTemplates.All.Select(t => t.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(BuiltInTemplates.All, t =>
            Assert.DoesNotContain(TemplateValidator.Validate(t), i => i.Severity == IssueSeverity.Error));
    }

    private static IReadOnlyList<string> Header(string text, string what) => CsvTable.Parse(text, what, []).Header;
}
