using System.Globalization;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Creatures;

/// <summary>
/// Creature definitions as a CSV table, one row per creature (ADR-044): the format of the shipped roster
/// (<c>EternalWars.Godot.Assets/data/creatures.csv</c>) so designers can edit stats in a spreadsheet or text editor.
/// The file conventions are <see cref="CsvTable"/>'s. List cells: tags <c>human; holy</c>, resources <c>mercury 1</c>,
/// capabilities <c>Flying; PackHunter 20; SpellImmunity mind; Caster lightning 2</c>.
/// </summary>
public static class CreatureTable
{
    public const string Id = "id";
    public const string Name = "name";
    public const string Town = "town";
    public const string Tier = "tier";
    public const string Size = "size";
    public const string Upgrade = "upgrade";
    public const string Attack = "attack";
    public const string Defense = "defense";
    public const string MinDamage = "min_damage";
    public const string MaxDamage = "max_damage";
    public const string Health = "health";
    public const string Speed = "speed";
    public const string MapSpeed = "map_speed";
    public const string Initiative = "initiative";
    public const string Shots = "shots";
    public const string Morale = "morale";
    public const string Luck = "luck";
    public const string Growth = "growth";
    public const string Gold = "gold";
    public const string Resources = "resources";
    public const string Tags = "tags";
    public const string Capabilities = "capabilities";
    public const string Notes = "notes";

    /// <summary>Columns written by <see cref="Format"/>, in order.</summary>
    public static IReadOnlyList<string> Columns { get; } =
    [
        Id, Name, Town, Tier, Size, Upgrade, Attack, Defense, MinDamage, MaxDamage, Health, Speed, MapSpeed, Initiative,
        Shots, Morale, Luck, Growth, Gold, Resources, Tags, Capabilities, Notes,
    ];

    private static readonly string[] Required = [Id, Name, Tier, Size, Attack, Defense, MinDamage, MaxDamage, Health, Speed, MapSpeed, Initiative];

    /// <summary>Parses and validates every row; throws <see cref="FormatException"/> naming the line and column.</summary>
    public static IReadOnlyList<CreatureDefinition> Parse(string text) =>
        CsvTable.Parse(text, "Creature", Required)
            .Read(row => row.Text(Id) is { Length: > 0 } id ? Read(row, id) : null)
            .OfType<CreatureDefinition>()
            .ToList();

    /// <summary>Writes a comma-separated table with <see cref="Columns"/>; <paramref name="notes"/> fills the notes column.</summary>
    public static string Format(IEnumerable<CreatureDefinition> definitions, Func<CreatureDefinition, string?>? notes = null) =>
        CsvTable.Format(Columns, definitions.Select(d => (IReadOnlyList<string>)
        [
            d.Id, d.Name, d.Town ?? "", N(d.Tier), d.Size.ToString(), d.UpgradesTo ?? "", N(d.Attack), N(d.Defense),
            N(d.MinDamage), N(d.MaxDamage), N(d.Health), N(d.Speed), N(d.MapSpeed), N(d.Initiative),
            d.Shots == 0 ? "" : N(d.Shots), d.Morale == 0 ? "" : N(d.Morale), d.Luck == 0 ? "" : N(d.Luck), N(d.WeeklyGrowth), N(d.Cost.Gold), FormatResources(d.Cost),
            string.Join("; ", d.Tags), string.Join("; ", d.Capabilities.Select(FormatCapability)), notes?.Invoke(d) ?? "",
        ]));

    public static string FormatCapability(CreatureCapability capability) =>
        string.Join(' ', new[]
        {
            capability.Kind.ToString(),
            capability.Parameter,
            capability.Value == 0 ? null : N(capability.Value),
        }.Where(p => p is not null));

    /// <summary>Other resources of a cost as <c>mercury 1; sulfur 2</c> (gold has its own column).</summary>
    public static string FormatResources(ResourceSet cost) =>
        string.Join("; ", Enum.GetValues<ResourceType>()
            .Where(t => t != ResourceType.Gold && cost[t] != 0)
            .Select(t => $"{t.ToString().ToLowerInvariant()} {N(cost[t])}"));

    /// <summary>Parses <c>mercury 1; sulfur 2</c> from a list cell.</summary>
    public static ResourceSet ParseResources(CsvTable.Row row, string column)
    {
        var total = ResourceSet.Empty;
        foreach (var item in row.List(column))
        {
            var parts = item.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts is not [var name, var amount]
                || !Enum.TryParse<ResourceType>(name, ignoreCase: true, out var type)
                || !int.TryParse(amount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var count))
            {
                throw row.Error(column, $"'{item}' is not '<resource> <amount>', e.g. 'mercury 1'");
            }

            total += ResourceSet.Of(type, count);
        }

        return total;
    }

    private static CreatureDefinition Read(CsvTable.Row row, string id)
    {
        var definition = new CreatureDefinition(id, row.Text(Name), row.Int(Tier), row.Enum<CreatureSize>(Size))
        {
            Town = row.Optional(Town),
            UpgradesTo = row.Optional(Upgrade),
            Attack = row.Int(Attack),
            Defense = row.Int(Defense),
            MinDamage = row.Int(MinDamage),
            MaxDamage = row.Int(MaxDamage),
            Health = row.Int(Health),
            Speed = row.Int(Speed),
            MapSpeed = row.Int(MapSpeed),
            Initiative = row.Int(Initiative),
            Shots = row.Int(Shots, 0),
            Morale = row.Int(Morale, 0),
            Luck = row.Int(Luck, 0),
            WeeklyGrowth = row.Int(Growth, 0),
            Cost = new ResourceSet(Gold: row.Int(Gold, 0)) + ParseResources(row, Resources),
            Tags = row.List(Tags),
            Capabilities = row.List(Capabilities).Select(c => ParseCapability(c, row)).ToList(),
        };
        definition.Validate();
        return definition;
    }

    private static CreatureCapability ParseCapability(string text, CsvTable.Row row)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!Enum.TryParse<CapabilityKind>(words[0], ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            throw row.Error(Capabilities, $"unknown capability '{words[0]}' (known: {string.Join(", ", Enum.GetNames<CapabilityKind>())})");
        }

        var value = 0;
        string? parameter = null;
        foreach (var word in words.Skip(1))
        {
            if (int.TryParse(word.Trim('%', 'x', 'X'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            {
                value = number;
            }
            else
            {
                parameter = word;
            }
        }

        return new CreatureCapability(kind, value, parameter);
    }

    private static string N(int value) => CsvTable.Number(value);
}
