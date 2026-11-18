namespace EternalWars.Core.Domain.Magic.Parsing;

/// <summary>
/// The parser of one further word of a <c>battle</c> column, after the kind's head: it reads the word's number, if any, into the
/// <see cref="SpellEffectReader"/>. With <paramref name="Only"/> set it reads the word for that kind only; for the other kinds the word means what
/// a later parser of the same word says.
/// </summary>
internal sealed record SpellEffectWord(string Word, SpellEffectKind? Only, Action<SpellEffectReader> Read);

/// <summary>
/// The table of the further words of a <c>battle</c> column (<see cref="SpellEffect.Parse"/>): the options, the status words of buffs and curses
/// (ADR-123) and the numbers they change. A word with no parser for the kind is read as a change with its number and refused.
/// </summary>
internal static class SpellEffectWords
{
    /// <summary>Every word's parsers; a word's kind-bound parsers come before its general one.</summary>
    public static IReadOnlyList<SpellEffectWord> All { get; } =
    [
        new("cure", SpellEffectKind.Heal, r => r.Cures = true),
        new("revive%", SpellEffectKind.Heal, r => r.RevivePercent = Number(r)),
        new("heal", SpellEffectKind.Dispel, r => r.Amount = Number(r)),
        new("sap-power", SpellEffectKind.Dispel, r => r.SapPower = Number(r)),
        new("refund%", SpellEffectKind.Dispel, r => r.Refund = Number(r)),
        new("hit", SpellEffectKind.Curse, r => (r.Amount, r.Hit) = (Number(r), true)),
        new("rubble", SpellEffectKind.Damage, r => r.Rubble = true),
        new("spare-own-demons", SpellEffectKind.Damage, r => r.SparesDemons = true),
        new("own%", SpellEffectKind.Damage, r => r.OwnPercent = Number(r)),
        new("survives", null, r => r.Survives = Number(r)),
        new("martyr-defense", null, r => r.MartyrDefense = Number(r)),
        new("split", null, r => r.Split = true),
        new("machine", null, r => r.Filter = SpellFilter.Machine),
        new("shadow", null, r => r.Shadow = true),
        new("again", null, r => r.Again = SpellTurnAgain.Act),
        new("again-attack", null, r => r.Again = SpellTurnAgain.Attack),
        new("drain%", null, r => r.Drain = Number(r)),
        new("drain-revive", null, r => r.DrainRevives = true),
        new("area", null, r => (r.Reach, r.Area) = (SpellReach.Area, Number(r))),
        new("line", null, r => (r.Reach, r.LineLength) = (SpellReach.Area, Number(r))),
        new("wide", null, r => r.Wide = Number(r)),
        new("enter", null, r => Zone(r, r.Zone with { Enter = Number(r) })),
        new("start", null, r => Zone(r, r.Zone with { Start = Number(r) })),
        new("leave", null, r => Zone(r, r.Zone with { Leave = Number(r) })),
        new("beside", null, r => Zone(r, r.Zone with { Beside = Number(r) })),
        new("heal", null, r => Zone(r, r.Zone with { Heal = Number(r) })),
        new("blocks", null, r => Zone(r, r.Zone with { BlocksMovement = true })),
        new("blocks-shots", null, r => Zone(r, r.Zone with { BlocksShots = true })),
        new("own-undead", null, r => Zone(r, r.Zone with { OwnUndeadPass = true })),
        new("dims-shots", null, r => Zone(r, r.Zone with { DimsShots = true })),
        new("column", null, r => (r.Reach, r.Area, r.Column) = (SpellReach.Area, Number(r), true)),
        new("ring", null, r => r.Ring = true),
        new("splash", null, r => r.Splash = Number(r)),
        new("chain", null, r => r.Chain = Number(r)),
        new("pierce", null, r => r.Pierce = true),
        new("stun%", null, r => r.Stun = Number(r)),
        new("execute", null, r => r.Execute = Number(r)),
        new("debt%", null, r => r.Debt = Number(r)),
        new("lasting", null, r => r.Lasting = true),
        new("fallen", null, r => r.Fallen = true),
        .. Statuses.Select(s => new SpellEffectWord(s.Key, null, r => r.Modifiers = r.Modifiers with { Status = r.Modifiers.Status | s.Value })),
        new("enemies", null, r => r.Reach = SpellReach.Side),
        new("allies", null, r => r.Reach = SpellReach.Side),
        new("all", null, r => r.Reach = SpellReach.All),
        new("living", null, r => r.Filter = SpellFilter.Living),
        new("undead", null, r => r.Filter = SpellFilter.Undead),
        new("undead-demon", null, r => r.Filter = SpellFilter.UndeadOrDemon),
        new("shooter", null, r => r.Filter = SpellFilter.Shooter),
        new("melee", null, r => r.Filter = SpellFilter.Melee),
        new("ground", null, r => r.Filter = SpellFilter.Ground),
        new("targets", null, r => r.Targets = Number(r)),
        new("spare-own", null, r => r.SpareOwn = true),
        new("rounds", null, Rounds),
        new("turns", null, r => (r.Turns, r.HasRounds) = (Number(r), true)),
        new("health", null, r => r.HealthLimit = Number(r)),
        new("walls", null, r => r.Walls = Number(r)),
        new("roll", null, Roll),
        .. Changes.Select(c => new SpellEffectWord(c.Key, null, r => r.Modifiers = c.Value(r.Modifiers, Number(r)))),
    ];

    private static readonly Dictionary<string, SpellEffectWord[]> ByWord =
        All.GroupBy(w => w.Word, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);

    /// <summary>Reads the word just taken from the reader, with the parser for the reader's kind.</summary>
    public static void Read(SpellEffectReader reader, string word)
    {
        if (ByWord.TryGetValue(word, out var parsers) && parsers.FirstOrDefault(p => p.Only is null || p.Only == reader.Kind) is { } parser)
        {
            parser.Read(reader);
            return;
        }

        // An unknown word is read as a change: its number first, then the refusal.
        _ = Number(reader);
        throw new FormatException($"'{word}' is not something a spell changes");
    }

    private static int Number(SpellEffectReader reader) => SpellEffectReader.Number(reader.Next());

    private static void Zone(SpellEffectReader reader, ZoneEffect zone) => (reader.Zone, reader.ZoneWords) = (zone, true);

    /// <summary><c>rounds N|battle</c>: battle lasts the rest of the battle.</summary>
    private static void Rounds(SpellEffectReader reader)
    {
        var length = reader.Next();
        reader.Rounds = length == "battle" ? null : SpellEffectReader.Number(length);
        reader.HasRounds = true;
    }

    /// <summary><c>roll max|min</c>: +1 maximum (Bless) or −1 minimum (Curse) on the damage roll.</summary>
    private static void Roll(SpellEffectReader reader) =>
        reader.Modifiers = reader.Modifiers with
        {
            Roll = reader.Next() switch { "max" => 1, "min" => -1, var r => throw new FormatException($"roll '{r}' is not max or min") },
        };

    /// <summary>The status words of buffs and curses (ADR-123).</summary>
    private static Dictionary<string, SpellStatus> Statuses => new(StringComparer.Ordinal)
    {
        ["blind"] = SpellStatus.Blind,
        ["confused"] = SpellStatus.Confused,
        ["entangled"] = SpellStatus.Entangled,
        ["no-shoot"] = SpellStatus.NoShoot,
        ["no-attack"] = SpellStatus.NoAttack,
        ["no-retaliation"] = SpellStatus.NoRetaliation,
        ["fearless"] = SpellStatus.Fearless,
        ["vigil"] = SpellStatus.Vigil,
        ["true-shot"] = SpellStatus.TrueShot,
        ["sanctuary"] = SpellStatus.Sanctuary,
        ["stasis"] = SpellStatus.Stasis,
        ["unseen"] = SpellStatus.Unseen,
        ["phase"] = SpellStatus.Phase,
        ["ward-mind"] = SpellStatus.WardMind,
        ["frenzied"] = SpellStatus.Frenzied,
        ["controlled"] = SpellStatus.Controlled,
        ["twice"] = SpellStatus.Twice,
        ["screened"] = SpellStatus.Screened,
    };

    /// <summary>The numbers a buff or curse changes, each followed by its value.</summary>
    private static Dictionary<string, Func<SpellModifiers, int, SpellModifiers>> Changes => new(StringComparer.Ordinal)
    {
        ["attack"] = (m, v) => m with { Attack = v },
        ["defense"] = (m, v) => m with { Defense = v },
        ["speed"] = (m, v) => m with { Speed = v },
        ["speed%"] = (m, v) => m with { SpeedPercent = v },
        ["initiative%"] = (m, v) => m with { InitiativePercent = v },
        ["melee%"] = (m, v) => m with { MeleePercent = v },
        ["ranged%"] = (m, v) => m with { RangedPercent = v },
        ["taken%"] = (m, v) => m with { DamageTakenPercent = v },
        ["melee-taken%"] = (m, v) => m with { MeleeTakenPercent = v },
        ["ranged-taken%"] = (m, v) => m with { RangedTakenPercent = v },
        ["spell-taken%"] = (m, v) => m with { SpellTakenPercent = v },
        ["morale"] = (m, v) => m with { Morale = v },
        ["luck"] = (m, v) => m with { Luck = v },
        ["burn"] = (m, v) => m with { Burn = v },
        ["regen"] = (m, v) => m with { Regen = v },
        ["decay%"] = (m, v) => m with { DecayPercent = v },
        ["immune-level"] = (m, v) => m with { ImmuneLevel = v },
        ["shroud"] = (m, v) => m with { ShroudLevel = v },
        ["thorns%"] = (m, v) => m with { ThornsPercent = v },
        ["lifesteal%"] = (m, v) => m with { LifestealPercent = v },
        ["martyr%"] = (m, v) => m with { MartyrPercent = v },
        ["ambush%"] = (m, v) => m with { AmbushPercent = v },
    };
}
