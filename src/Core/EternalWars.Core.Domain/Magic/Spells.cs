using System.Globalization;
using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Magic;

/// <summary>A spell's element besides its school (ADR-106): air, earth, fire or water; Magic Arrow is all four, Bind Elemental the bound kind.</summary>
public enum SpellElement
{
    Air,
    Earth,
    Fire,
    Water,
    All,
    Bound,
}

/// <summary>What a spell does in battle (rank I, ADR-099): damage, healing, or an effect on stacks for some rounds.</summary>
public enum SpellEffectKind
{
    /// <summary>Damage to enemy stacks (or to every stack of an area or of the field).</summary>
    Damage,

    /// <summary>Health back to an own stack's top creature.</summary>
    Heal,

    /// <summary>An effect on own stacks.</summary>
    Buff,

    /// <summary>An effect on enemy stacks.</summary>
    Curse,
}

/// <summary>Whom a spell reaches: one stack, a square area, every stack of the target side, or every stack on the field.</summary>
public enum SpellReach
{
    Stack,
    Area,
    Side,
    All,
}

/// <summary>Which stacks a spell works on at all: any, the living (not undead, constructs or machines), undead, undead and demons, shooters.</summary>
public enum SpellFilter
{
    None,
    Living,
    Undead,
    UndeadOrDemon,
    Shooter,
}

/// <summary>
/// What a buff or curse changes on a stack while it lasts (ADR-099's rank I numbers): flat Attack, Defense, speed, morale and luck; percent speed and
/// initiative; percent damage dealt in melee or at range; percent damage taken from every attack, from melee, from shots and from spells; the
/// damage roll (+1 maximum, Bless; −1 minimum, Curse).
/// </summary>
public sealed record SpellModifiers(
    int Attack = 0, int Defense = 0, int Speed = 0, int SpeedPercent = 0, int InitiativePercent = 0, int MeleePercent = 0, int RangedPercent = 0,
    int DamageTakenPercent = 0, int MeleeTakenPercent = 0, int RangedTakenPercent = 0, int SpellTakenPercent = 0, int Morale = 0, int Luck = 0,
    int Roll = 0)
{
    public static SpellModifiers None { get; } = new();

    public bool IsEmpty => this == None;

    public static SpellModifiers operator +(SpellModifiers a, SpellModifiers b) => new(
        a.Attack + b.Attack, a.Defense + b.Defense, a.Speed + b.Speed, a.SpeedPercent + b.SpeedPercent, a.InitiativePercent + b.InitiativePercent,
        a.MeleePercent + b.MeleePercent, a.RangedPercent + b.RangedPercent, a.DamageTakenPercent + b.DamageTakenPercent,
        a.MeleeTakenPercent + b.MeleeTakenPercent, a.RangedTakenPercent + b.RangedTakenPercent, a.SpellTakenPercent + b.SpellTakenPercent,
        a.Morale + b.Morale, a.Luck + b.Luck, a.Roll + b.Roll);
}

/// <summary>
/// A spell's battle effect at rank I, read from the <c>battle</c> column of <c>spells.csv</c>: <c>damage 35 area 3</c>, <c>heal 40</c>,
/// <c>buff attack 3 rounds 3</c>, <c>curse morale -2 rounds battle</c>, <c>damage 50 enemies undead-demon</c>, <c>damage 90 speed -2 rounds 2</c>
/// (damage that also leaves an effect). Amounts and rounds are base numbers at Spell Power 0 (ADR-072). <c>Rounds</c> is null for the rest of
/// the battle (not lengthened by Spell Power).
/// </summary>
public sealed record SpellEffect(
    SpellEffectKind Kind, int Amount, SpellReach Reach, int AreaSize, SpellFilter Filter, SpellModifiers Modifiers, int? Rounds)
{
    /// <summary>Damage and curses work on the enemy (damage to the whole field or an area hits own stacks too); healing and buffs on own stacks.</summary>
    public bool IsHostile => Kind is SpellEffectKind.Damage or SpellEffectKind.Curse;

    public static SpellEffect Parse(string text)
    {
        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 || !Enum.TryParse<SpellEffectKind>(tokens[0], ignoreCase: true, out var kind) || !Enum.IsDefined(kind))
        {
            throw new FormatException($"'{text}' does not start with damage, heal, buff or curse");
        }

        var i = 1;
        var amount = 0;
        if (kind is SpellEffectKind.Damage or SpellEffectKind.Heal)
        {
            amount = Number(Next());
        }

        var (reach, area, filter, modifiers, rounds) = (SpellReach.Stack, 0, SpellFilter.None, SpellModifiers.None, (int?)null);
        var hasRounds = false;
        while (i < tokens.Length)
        {
            var token = tokens[i++];
            switch (token)
            {
                case "area":
                    (reach, area) = (SpellReach.Area, Number(Next()));
                    break;
                case "enemies" or "allies":
                    reach = SpellReach.Side;
                    break;
                case "all":
                    reach = SpellReach.All;
                    break;
                case "living":
                    filter = SpellFilter.Living;
                    break;
                case "undead":
                    filter = SpellFilter.Undead;
                    break;
                case "undead-demon":
                    filter = SpellFilter.UndeadOrDemon;
                    break;
                case "shooter":
                    filter = SpellFilter.Shooter;
                    break;
                case "rounds":
                    var length = Next();
                    rounds = length == "battle" ? null : Number(length);
                    hasRounds = true;
                    break;
                case "roll":
                    modifiers = modifiers with { Roll = Next() switch { "max" => 1, "min" => -1, var r => throw new FormatException($"roll '{r}' is not max or min") } };
                    break;
                default:
                    modifiers = Modify(modifiers, token, Number(Next()));
                    break;
            }
        }

        if (kind is SpellEffectKind.Buff or SpellEffectKind.Curse && (!hasRounds || modifiers.IsEmpty))
        {
            throw new FormatException($"'{text}': a buff or curse needs what it changes and its rounds");
        }

        if (kind == SpellEffectKind.Damage && !modifiers.IsEmpty && !hasRounds)
        {
            throw new FormatException($"'{text}': the effect of a damage spell needs its rounds");
        }

        if (reach == SpellReach.Area && area < 1)
        {
            throw new FormatException($"'{text}': an area needs a size");
        }

        return new SpellEffect(kind, amount, reach, area, filter, modifiers, rounds);

        string Next() => i < tokens.Length ? tokens[i++] : throw new FormatException($"'{text}' ends too early");
    }

    private static int Number(string text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n : throw new FormatException($"'{text}' is not a whole number");

    private static SpellModifiers Modify(SpellModifiers m, string key, int value) => key switch
    {
        "attack" => m with { Attack = value },
        "defense" => m with { Defense = value },
        "speed" => m with { Speed = value },
        "speed%" => m with { SpeedPercent = value },
        "initiative%" => m with { InitiativePercent = value },
        "melee%" => m with { MeleePercent = value },
        "ranged%" => m with { RangedPercent = value },
        "taken%" => m with { DamageTakenPercent = value },
        "melee-taken%" => m with { MeleeTakenPercent = value },
        "ranged-taken%" => m with { RangedTakenPercent = value },
        "spell-taken%" => m with { SpellTakenPercent = value },
        "morale" => m with { Morale = value },
        "luck" => m with { Luck = value },
        _ => throw new FormatException($"'{key}' is not something a spell changes"),
    };
}

/// <summary>
/// A spell (ADR-047, ADR-099): its school (the town whose school it is), level 1–4, base mana, element, immunity tags, whether it is cast on the
/// adventure map, and its rank I battle effect where it has been built (null: not castable in battle yet).
/// </summary>
public sealed record SpellDefinition(
    string Id, string Name, string School, int Level, int Mana, SpellElement Element, IReadOnlyList<string> Tags, bool IsAdventure, SpellEffect? Effect,
    string Notes = "")
{
    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.Ordinal);
}

/// <summary>The spell table as CSV (<c>data/spells.csv</c>): <c>id, name, school, level, mana, element, tags, adventure, battle, notes</c>.</summary>
public static class SpellTable
{
    public const string Id = "id";
    public const string Name = "name";
    public const string School = "school";
    public const string Level = "level";
    public const string Mana = "mana";
    public const string Element = "element";
    public const string Tags = "tags";
    public const string Adventure = "adventure";
    public const string Battle = "battle";
    public const string Notes = "notes";

    /// <summary>The columns of the shipped spell table, in order.</summary>
    public static IReadOnlyList<string> Columns { get; } = [Id, Name, School, Level, Mana, Element, Tags, Adventure, Battle, Notes];

    public static IReadOnlyList<SpellDefinition> Parse(string text) =>
        CsvTable.Parse(text, "Spell", [Id, Name, School, Level, Mana, Element])
            .Read(row => row.Text(Id) is { Length: > 0 } id ? Read(row, id) : null)
            .OfType<SpellDefinition>()
            .ToList();

    private static SpellDefinition Read(CsvTable.Row row, string id)
    {
        var level = row.Int(Level);
        if (level is < 1 or > Creatures.SpellLevels.Max)
        {
            throw row.Error(Level, $"{level} is not a spell level from 1 to {Creatures.SpellLevels.Max}");
        }

        SpellEffect? effect;
        try
        {
            effect = row.Optional(Battle) is { } battle ? SpellEffect.Parse(battle) : null;
        }
        catch (FormatException e)
        {
            throw row.Error(Battle, e.Message);
        }

        return new SpellDefinition(
            id, row.Text(Name), row.Text(School), level, row.Int(Mana), row.Enum<SpellElement>(Element), row.List(Tags),
            row.Optional(Adventure) is "yes", effect, row.Optional(Notes) ?? "");
    }
}

/// <summary>The spells of a game by id. Start from <see cref="Default"/> (the embedded table).</summary>
public sealed class SpellCatalog
{
    private readonly Dictionary<string, SpellDefinition> _spells = new(StringComparer.Ordinal);

    public SpellCatalog(IEnumerable<SpellDefinition> spells)
    {
        foreach (var spell in spells)
        {
            if (!_spells.TryAdd(spell.Id, spell))
            {
                throw new FormatException($"Duplicate spell '{spell.Id}'.");
            }

            if (spell.IsAdventure && spell.Effect is not null)
            {
                throw new FormatException($"Adventure spell '{spell.Id}' has a battle effect.");
            }
        }
    }

    public static SpellCatalog Default { get; } = new(SpellTable.Parse(BuiltInSpells.EmbeddedTable()));

    public IEnumerable<SpellDefinition> All => _spells.Values;

    public bool Contains(string id) => _spells.ContainsKey(id);

    public bool TryGet(string id, out SpellDefinition spell) => _spells.TryGetValue(id, out spell!);

    public SpellDefinition Get(string id) =>
        _spells.TryGetValue(id, out var spell) ? spell : throw new KeyNotFoundException($"Unknown spell '{id}'.");

    /// <summary>The spells of a school (town id) at one level, by id.</summary>
    public IReadOnlyList<SpellDefinition> Of(string school, int level) =>
        _spells.Values.Where(s => s.School == school && s.Level == level).OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
}

/// <summary>The shipped spell table, embedded at build time (<c>data/spells.csv</c>).</summary>
public static class BuiltInSpells
{
    public const string Resource = "EternalWars.Spells.csv";

    public static string EmbeddedTable()
    {
        using var stream = typeof(BuiltInSpells).Assembly.GetManifestResourceStream(Resource)
                           ?? throw new InvalidOperationException($"The spell table '{Resource}' is not embedded.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
