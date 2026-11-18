using System.Globalization;

namespace EternalWars.Core.Domain.Magic.Parsing;

/// <summary>
/// One reading of a <c>battle</c> column (<see cref="SpellEffect.Parse"/>): the words, where the reading is, and everything read so far. The kind's
/// head (<see cref="SpellEffectHeads"/>) reads first, then each further word's parser (<see cref="SpellEffectWords"/>); <see cref="Build"/> checks
/// the words go together and makes the effect.
/// </summary>
internal sealed class SpellEffectReader(string text, string[] tokens, SpellEffectKind kind)
{
    /// <summary>The whole column, for the error messages.</summary>
    public string Text { get; } = text;

    /// <summary>The kind the first word names.</summary>
    public SpellEffectKind Kind { get; } = kind;

    /// <summary>The index of the next word to read.</summary>
    public int Index { get; set; } = 1;

    /// <summary>Whether words are left to read.</summary>
    public bool More => Index < tokens.Length;

    /// <summary>The number of words, the kind's included.</summary>
    public int Count => tokens.Length;

    public int Amount { get; set; }

    public int MoveRange { get; set; }

    public DispelScope Dispels { get; set; } = DispelScope.Any;

    public SpellReach Reach { get; set; } = SpellReach.Stack;

    public int Area { get; set; }

    public SpellFilter Filter { get; set; } = SpellFilter.None;

    public SpellModifiers Modifiers { get; set; } = SpellModifiers.None;

    public int? Rounds { get; set; }

    public bool HasRounds { get; set; }

    public int Walls { get; set; }

    public int Targets { get; set; } = 1;

    public bool SpareOwn { get; set; }

    public int Splash { get; set; }

    public int Chain { get; set; }

    public bool Pierce { get; set; }

    public int Stun { get; set; }

    public int Execute { get; set; }

    public int Debt { get; set; }

    public bool Ring { get; set; }

    public bool Column { get; set; }

    public bool Lasting { get; set; }

    public bool Fallen { get; set; }

    public int LineLength { get; set; }

    public int Wide { get; set; }

    public ZoneEffect Zone { get; set; } = new();

    /// <summary>Whether a zone word was read (they are for zones only).</summary>
    public bool ZoneWords { get; set; }

    public int? Turns { get; set; }

    public int HealthLimit { get; set; }

    public bool Shadow { get; set; }

    public SpellTurnAgain Again { get; set; }

    public int Drain { get; set; }

    public bool DrainRevives { get; set; }

    public int Survives { get; set; }

    public int MartyrDefense { get; set; }

    public bool Split { get; set; }

    public bool Cures { get; set; }

    public int RevivePercent { get; set; }

    public bool Rubble { get; set; }

    public bool SparesDemons { get; set; }

    public int OwnPercent { get; set; } = 100;

    public int SapPower { get; set; }

    public int Refund { get; set; }

    /// <summary>Whether the curse's amount came from <c>hit N</c>.</summary>
    public bool Hit { get; set; }

    /// <summary>The next word; the column ending here is an error.</summary>
    public string Next() => Index < tokens.Length ? tokens[Index++] : throw new FormatException($"'{Text}' ends too early");

    /// <summary>A whole number, possibly signed.</summary>
    public static int Number(string text) =>
        int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n) ? n : throw new FormatException($"'{text}' is not a whole number");

    /// <summary>Checks the words read go together and makes the effect.</summary>
    public SpellEffect Build()
    {
        if (Kind is SpellEffectKind.Buff or SpellEffectKind.Curse && (!HasRounds || Modifiers.IsEmpty))
        {
            throw new FormatException($"'{Text}': a buff or curse needs what it changes and its rounds");
        }

        if (Kind == SpellEffectKind.Damage && !Modifiers.IsEmpty && !HasRounds)
        {
            throw new FormatException($"'{Text}': the effect of a damage spell needs its rounds");
        }

        if (Reach == SpellReach.Area && Area < 1 && LineLength < 1)
        {
            throw new FormatException($"'{Text}': an area needs a size");
        }

        if (Targets < 1 || (Targets > 1 && Reach != SpellReach.Stack))
        {
            throw new FormatException($"'{Text}': 'targets' needs a number of at least 1 and a stack spell");
        }

        if (SpareOwn && Reach is not (SpellReach.Area or SpellReach.All) && Chain == 0)
        {
            throw new FormatException($"'{Text}': 'spare-own' is for area, field and chain spells");
        }

        if ((Splash > 0 || Chain > 0 || Pierce || Stun > 0 || Execute > 0 || Debt > 0) && Kind != SpellEffectKind.Damage)
        {
            throw new FormatException($"'{Text}': splash, chain, pierce, stun%, execute and debt% are for damage spells");
        }

        if ((Lasting || Fallen) && Kind != SpellEffectKind.Revive)
        {
            throw new FormatException($"'{Text}': 'lasting' and 'fallen' are for revives");
        }

        if (Ring && (Reach != SpellReach.Area || Column))
        {
            throw new FormatException($"'{Text}': 'ring' needs a square area");
        }

        if (Kind is SpellEffectKind.Revive or SpellEffectKind.Slay && Amount < 1)
        {
            throw new FormatException($"'{Text}': a revive or slay needs an amount of at least 1");
        }

        if ((ZoneWords || LineLength > 0 || Wide > 0) && Kind != SpellEffectKind.Zone)
        {
            throw new FormatException($"'{Text}': 'line', 'wide' and the zone words are for zones");
        }

        if (Turns is { } t && (t < 1 || Rounds is not null || Kind is not (SpellEffectKind.Buff or SpellEffectKind.Curse)
                               || tokens.Count(w => w is "turns" or "rounds") > 1))
        {
            throw new FormatException($"'{Text}': 'turns N' (at least 1) is for a buff or curse, instead of its rounds");
        }

        if (HealthLimit != 0 && (HealthLimit < 1 || !Modifiers.Has(SpellStatus.Controlled)))
        {
            throw new FormatException($"'{Text}': 'health N' (at least 1) is for a 'controlled' spell");
        }

        if (Modifiers.Has(SpellStatus.Controlled | SpellStatus.Frenzied) && (Kind != SpellEffectKind.Curse || Reach != SpellReach.Stack || Targets > 1))
        {
            throw new FormatException($"'{Text}': 'controlled' and 'frenzied' are for a curse on one stack");
        }

        if ((Shadow || Again != SpellTurnAgain.None) && Kind != SpellEffectKind.Move)
        {
            throw new FormatException($"'{Text}': 'shadow', 'again' and 'again-attack' are for moves");
        }

        if (Kind == SpellEffectKind.Move && (Reach != SpellReach.Stack || !Modifiers.IsEmpty || HasRounds || Targets > 1 || Filter != SpellFilter.None || Walls > 0))
        {
            throw new FormatException($"'{Text}': a move is 'move N|field [shadow] [again|again-attack]'");
        }

        if ((tokens.Contains("drain%") || DrainRevives) && (Drain < 1 || Kind != SpellEffectKind.Damage || Reach != SpellReach.Stack))
        {
            throw new FormatException($"'{Text}': 'drain% N' (at least 1) and 'drain-revive' are for a damage spell aimed at a stack");
        }

        if (Kind == SpellEffectKind.Copy && (Rounds is not > 0 || Turns is not null || Reach != SpellReach.Stack || Targets > 1 || !Modifiers.IsEmpty
                                             || Filter != SpellFilter.None || Walls > 0 || Survives < 0))
        {
            throw new FormatException($"'{Text}': a copy is 'copy rounds N [survives N]', with at least 1 round");
        }

        if (Survives != 0 && Kind != SpellEffectKind.Copy)
        {
            throw new FormatException($"'{Text}': 'survives' is for copies");
        }

        if ((Modifiers.MartyrPercent != 0 || MartyrDefense != 0)
            && (Kind != SpellEffectKind.Buff || Reach != SpellReach.Stack || Targets > 1 || Modifiers.MartyrPercent is < 1 or > 100 || MartyrDefense < 0))
        {
            throw new FormatException($"'{Text}': 'martyr% N' (1 to 100) and 'martyr-defense N' are for a buff on one stack");
        }

        if (Split && (!Modifiers.Has(SpellStatus.Controlled) || HealthLimit == 0))
        {
            throw new FormatException($"'{Text}': 'split' is for a 'controlled' spell with a health limit");
        }

        if (RevivePercent is < 0 or > 100 || Refund is < 0 or > 100 || OwnPercent is < 0 or > 100 || Amount < 0 || SapPower < 0 || (Hit && Amount < 1))
        {
            throw new FormatException($"'{Text}': revive%, refund% and own% run from 0 to 100; heal and sap-power are not negative, hit is at least 1");
        }

        if (Rubble && (Reach != SpellReach.Area || Column || Ring || LineLength > 0))
        {
            throw new FormatException($"'{Text}': 'rubble' needs a square area");
        }

        if (SparesDemons && Reach is not (SpellReach.Area or SpellReach.All))
        {
            throw new FormatException($"'{Text}': 'spare-own-demons' is for area and field spells");
        }

        var (area, height) = (Area, 0);
        if (Kind == SpellEffectKind.Zone)
        {
            if (Rounds is not > 0 || (LineLength > 0) == (Area > 0) || Ring || Column || (Wide > 0 && LineLength == 0))
            {
                throw new FormatException($"'{Text}': a zone is 'zone area N|line N [wide W] … rounds N', with at least 1 round");
            }

            if (Modifiers != new SpellModifiers(Defense: Modifiers.Defense))
            {
                throw new FormatException($"'{Text}': a zone changes only the Defense of own stacks in it");
            }

            if (LineLength > 0)
            {
                (area, height) = (Math.Max(1, Wide), LineLength);
            }
        }

        return new SpellEffect(Kind, Amount, Reach, area, Filter, Modifiers, Rounds, Walls, null, false, Targets, SpareOwn)
        {
            SplashPercent = Splash,
            Chain = Chain,
            Pierce = Pierce,
            StunPercent = Stun,
            ExecutePercent = Execute,
            DebtPercent = Debt,
            Ring = Ring,
            Column = Column,
            Lasting = Lasting,
            Fallen = Fallen,
            Dispels = Dispels,
            AreaHeight = height,
            Zone = Kind == SpellEffectKind.Zone ? Zone : null,
            Turns = Turns,
            HealthLimit = HealthLimit,
            MoveRange = MoveRange,
            Shadow = Shadow,
            Again = Again,
            DrainPercent = Drain,
            DrainRevives = DrainRevives,
            CopyHits = 1 + Survives,
            MartyrDefense = MartyrDefense,
            Split = Split,
            Cures = Cures,
            RevivePercent = RevivePercent,
            Rubble = Rubble,
            SparesOwnDemons = SparesDemons,
            OwnPercent = OwnPercent,
            SapPower = SapPower,
            RefundPercent = Refund,
        };
    }
}
