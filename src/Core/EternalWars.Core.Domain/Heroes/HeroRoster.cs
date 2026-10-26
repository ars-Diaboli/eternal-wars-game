using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Creatures;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>Every town has mages and warriors (ADR-048).</summary>
public enum HeroClass
{
    Warrior,
    Mage,
}

public enum SpecialtyKind
{
    /// <summary>A stronger version of one skill (<see cref="HeroSpecialty.Target"/> = skill id); effect with levels, draft.</summary>
    Skill,

    /// <summary>A stronger version of one spell (<see cref="HeroSpecialty.Target"/> = spell id); magic, Phase 5.</summary>
    Spell,

    /// <summary><see cref="HeroSpecialty.Amount"/> of a resource (<see cref="HeroSpecialty.Target"/>) every day.</summary>
    Resource,
}

/// <summary>HoMM3-style hero specialisation.</summary>
public sealed record HeroSpecialty(SpecialtyKind Kind, string Target, int Amount = 0)
{
    /// <summary>The daily income of a resource specialty, else nothing.</summary>
    public ResourceSet DailyIncome =>
        Kind == SpecialtyKind.Resource && Enum.TryParse<ResourceType>(Target, ignoreCase: true, out var resource)
            ? ResourceSet.Of(resource, Amount)
            : ResourceSet.Empty;

    public override string ToString() => Kind switch
    {
        SpecialtyKind.Resource => $"resource {Target} {Amount.ToString(CultureInfo.InvariantCulture)}",
        _ => $"{Kind.ToString().ToLowerInvariant()} {Target}",
    };

    /// <summary>Parses <c>skill offense.melee</c>, <c>spell light.bless</c> or <c>resource gold 350</c>; an old skill id reads as its successor (ADR-072).</summary>
    public static HeroSpecialty Parse(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words switch
        {
            ["skill", var skill] when BuiltInSkills.Contains(skill) => new(SpecialtyKind.Skill, BuiltInSkills.Resolve(skill)),
            ["skill", var skill] => throw new FormatException($"unknown skill '{skill}'"),
            ["spell", var spell] => new(SpecialtyKind.Spell, spell),
            ["resource", var name, var amount] when Enum.TryParse<ResourceType>(name, ignoreCase: true, out _)
                                                    && int.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0
                => new(SpecialtyKind.Resource, name.ToLowerInvariant(), n),
            _ => throw new FormatException($"'{text}' is not 'skill <id>', 'spell <id>' or 'resource <name> <amount>'"),
        };
    }
}

/// <summary>
/// A hero of the roster (ADR-048): 16 per playable town, a mage and a warrior for each of its eight tier 1–4 creature
/// lines, which is their preferred creature (flat bonus in battle, <see cref="SkillEffects"/>).
/// </summary>
public sealed record HeroDefinition(string Id, string Name, string Town, HeroClass Class, string PreferredCreature, HeroSpecialty Specialty)
{
    public IReadOnlyDictionary<string, SkillLevel> StartingSkills { get; init; } = new Dictionary<string, SkillLevel>();

    public int Attack { get; init; }

    public int Defense { get; init; }

    public int SpellPower { get; init; }

    public int Knowledge { get; init; }
}

/// <summary>The hero roster as a CSV table (<c>data/heroes.csv</c>), same conventions as the creature table.</summary>
public static class HeroTable
{
    public const string Id = "id";
    public const string Name = "name";
    public const string Town = "town";
    public const string Class = "class";
    public const string Preferred = "preferred";
    public const string Specialty = "specialty";
    public const string Skills = "skills";
    public const string Attack = "attack";
    public const string Defense = "defense";
    public const string SpellPower = "spell_power";
    public const string Knowledge = "knowledge";
    public const string Notes = "notes";

    public static IReadOnlyList<string> Columns { get; } = [Id, Name, Town, Class, Preferred, Specialty, Skills, Attack, Defense, SpellPower, Knowledge, Notes];

    public static IReadOnlyList<HeroDefinition> Parse(string text) =>
        CsvTable.Parse(text, "Hero", [Id, Name, Town, Class, Preferred, Specialty])
            .Read(row => row.Text(Id) is { Length: > 0 } id ? Read(row, id) : null)
            .OfType<HeroDefinition>()
            .ToList();

    public static string Format(IEnumerable<HeroDefinition> heroes, Func<HeroDefinition, string?>? notes = null) =>
        CsvTable.Format(Columns, heroes.Select(h => (IReadOnlyList<string>)
        [
            h.Id, h.Name, h.Town, h.Class.ToString(), h.PreferredCreature, h.Specialty.ToString(),
            FormatSkills(h.StartingSkills), CsvTable.Number(h.Attack), CsvTable.Number(h.Defense),
            CsvTable.Number(h.SpellPower), CsvTable.Number(h.Knowledge), notes?.Invoke(h) ?? "",
        ]));

    /// <summary><c>offense.melee Basic; tactics.tactics Advanced</c>.</summary>
    public static string FormatSkills(IReadOnlyDictionary<string, SkillLevel> skills) =>
        string.Join("; ", skills.Select(s => $"{s.Key} {s.Value}"));

    public static IReadOnlyDictionary<string, SkillLevel> ParseSkills(IEnumerable<string> items)
    {
        var skills = new Dictionary<string, SkillLevel>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var parts = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts is not [var id, var name] || !BuiltInSkills.Contains(id)
                || !Enum.TryParse<SkillLevel>(name, ignoreCase: true, out var level) || level == SkillLevel.None
                || (level > BuiltInSkills.Get(id).MaxLevel && !BuiltInSkills.Renamed.ContainsKey(id)))
            {
                throw new FormatException($"'{item}' is not '<skill id> <Basic|Advanced|Expert|Master>' within the skill's maximum");
            }

            skills[id] = level;
        }

        // Old ids read as their successors, capped at the new maximum (ADR-072, HERO-6).
        var migrated = BuiltInSkills.Migrate(skills);
        var groups = migrated.Keys.Select(k => BuiltInSkills.Get(k).Group).Distinct().Count();
        return groups <= HeroSkills.MaxGroups ? migrated : throw new FormatException($"more than {HeroSkills.MaxGroups} skill groups");
    }

    private static HeroDefinition Read(CsvTable.Row row, string id)
    {
        HeroSpecialty specialty;
        IReadOnlyDictionary<string, SkillLevel> skills;
        try
        {
            specialty = HeroSpecialty.Parse(row.Text(Specialty));
        }
        catch (FormatException e)
        {
            throw row.Error(Specialty, e.Message);
        }

        try
        {
            skills = ParseSkills(row.List(Skills));
        }
        catch (FormatException e)
        {
            throw row.Error(Skills, e.Message);
        }

        return new HeroDefinition(id, row.Text(Name), row.Text(Town), row.Enum<HeroClass>(Class), row.Text(Preferred), specialty)
        {
            StartingSkills = skills,
            Attack = row.Int(Attack, 0),
            Defense = row.Int(Defense, 0),
            SpellPower = row.Int(SpellPower, 0),
            Knowledge = row.Int(Knowledge, 0),
        };
    }
}

/// <summary>The hero roster by id, like <see cref="CreatureCatalog"/>.</summary>
public sealed class HeroCatalog
{
    private readonly Dictionary<string, HeroDefinition> _heroes = new(StringComparer.Ordinal);

    public HeroCatalog(IEnumerable<HeroDefinition> heroes)
    {
        foreach (var hero in heroes)
        {
            if (!_heroes.TryAdd(hero.Id, hero))
            {
                throw new ArgumentException($"Duplicate hero '{hero.Id}'.", nameof(heroes));
            }
        }
    }

    public static HeroCatalog Default { get; } = new(BuiltInHeroes.All);

    public IEnumerable<HeroDefinition> All => _heroes.Values;

    public HeroDefinition Get(string id) =>
        _heroes.TryGetValue(id, out var hero) ? hero : throw new KeyNotFoundException($"Unknown hero '{id}'.");

    public bool Contains(string id) => _heroes.ContainsKey(id);

    /// <summary>
    /// Problems against the creature roster: every preferred creature must be a tier 1–4 base line of the hero's town,
    /// and each such line has exactly one mage and one warrior (ADR-048).
    /// </summary>
    public IReadOnlyList<string> Check(CreatureCatalog creatures)
    {
        var problems = new List<string>();
        foreach (var hero in All)
        {
            if (!creatures.TryGet(hero.PreferredCreature, out var creature) || creature.Town != hero.Town || creature.Tier > 4
                || creatures.All.Any(c => c.UpgradesTo == creature.Id))
            {
                problems.Add($"{hero.Id}: preferred creature '{hero.PreferredCreature}' is not a tier 1–4 base line of {hero.Town}.");
            }
        }

        foreach (var town in All.Select(h => h.Town).Distinct())
        {
            foreach (var line in creatures.BaseCreaturesOf(town).Where(c => c.Tier <= 4))
            {
                foreach (var heroClass in Enum.GetValues<HeroClass>())
                {
                    var count = All.Count(h => h.Town == town && h.Class == heroClass && h.PreferredCreature == line.Id);
                    if (count != 1)
                    {
                        problems.Add($"{town}: {count} {heroClass.ToString().ToLowerInvariant()}s prefer {line.Id} (should be 1).");
                    }
                }
            }
        }

        return problems;
    }
}

/// <summary>The shipped hero roster: <c>data/heroes.csv</c>, embedded at build time (<c>src/Core/data</c>, ADR-071).</summary>
public static class BuiltInHeroes
{
    public const string ResourceName = "EternalWars.Heroes.csv";

    public static IReadOnlyList<HeroDefinition> All { get; } = HeroTable.Parse(EmbeddedTable());

    public static string EmbeddedTable()
    {
        using var stream = typeof(BuiltInHeroes).Assembly.GetManifestResourceStream(ResourceName)
                           ?? throw new InvalidOperationException($"The hero table '{ResourceName}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
