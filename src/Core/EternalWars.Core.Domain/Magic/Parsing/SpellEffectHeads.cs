namespace EternalWars.Core.Domain.Magic.Parsing;

/// <summary>
/// How a <c>battle</c> column of one <see cref="SpellEffectKind"/> starts, right after the kind's word: <paramref name="Read"/> reads the words the
/// kind takes first and returns the whole effect when the kind takes nothing else (summon, raise, sacrifice), or null when the further words follow.
/// </summary>
internal sealed record SpellEffectHead(SpellEffectKind Kind, Func<SpellEffectReader, SpellEffect?> Read);

/// <summary>
/// The registry of the kinds' heads: the one <see cref="SpellEffectHead"/> of each <see cref="SpellEffectKind"/>, which <see cref="SpellEffect.Parse"/>
/// looks up by the first word (a test checks every kind has exactly one).
/// </summary>
internal static class SpellEffectHeads
{
    /// <summary>Every head, in <see cref="SpellEffectKind"/> order.</summary>
    public static IReadOnlyList<SpellEffectHead> All { get; } =
    [
        new(SpellEffectKind.Damage, ReadAmount),
        new(SpellEffectKind.Heal, ReadAmount),
        new(SpellEffectKind.Buff, _ => null),
        new(SpellEffectKind.Curse, _ => null),
        new(SpellEffectKind.Summon, Summon),
        new(SpellEffectKind.Revive, ReadAmount),
        new(SpellEffectKind.Dispel, Dispel),
        new(SpellEffectKind.Slay, ReadAmount),
        new(SpellEffectKind.Zone, _ => null),
        new(SpellEffectKind.Move, Move),
        new(SpellEffectKind.Copy, _ => null),
        new(SpellEffectKind.Raise, Raise),
        new(SpellEffectKind.Sacrifice, Sacrifice),
    ];

    private static readonly Dictionary<SpellEffectKind, SpellEffectHead> ByKind = All.ToDictionary(h => h.Kind);

    /// <summary>The head of the kind.</summary>
    public static SpellEffectHead For(SpellEffectKind kind) =>
        ByKind.TryGetValue(kind, out var head) ? head : throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unregistered spell effect kind.");

    /// <summary><c>damage|heal|revive|slay N …</c>: the amount comes first.</summary>
    private static SpellEffect? ReadAmount(SpellEffectReader reader)
    {
        reader.Amount = SpellEffectReader.Number(reader.Next());
        return null;
    }

    /// <summary><c>summon &lt;creature&gt;[|&lt;creature&gt; …] &lt;health&gt; [rooted] [stays] [kinds N]</c>, the whole effect (ADR-106, ADR-120, ADR-177).</summary>
    private static SpellEffect Summon(SpellEffectReader reader)
    {
        const string form = "a summon is 'summon <creature> <health> [rooted] [stays] [kinds N]', N at most its kinds";
        var creatures = reader.Next().Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var amount = SpellEffectReader.Number(reader.Next());
        var (rooted, stays, kinds) = (false, false, 1);
        while (reader.More)
        {
            switch (reader.Next())
            {
                case "rooted":
                    rooted = true;
                    break;
                case "stays":
                    stays = true;
                    break;
                case "kinds":
                    kinds = SpellEffectReader.Number(reader.Next());
                    break;
                default:
                    throw new FormatException($"'{reader.Text}': {form}");
            }
        }

        if (amount < 1 || kinds < 1 || kinds > creatures.Length)
        {
            throw new FormatException($"'{reader.Text}': {form}");
        }

        return new SpellEffect(reader.Kind, amount, SpellReach.Side, 0, SpellFilter.None, SpellModifiers.None, null, 0, creatures, rooted)
        {
            Stays = stays,
            Kinds = kinds,
        };
    }

    /// <summary><c>dispel own|enemy|any …</c>: whose spells come off (ADR-123).</summary>
    private static SpellEffect? Dispel(SpellEffectReader reader)
    {
        reader.Dispels = reader.Next() switch
        {
            "own" => DispelScope.Own,
            "enemy" => DispelScope.Enemy,
            "any" => DispelScope.Any,
            var scope => throw new FormatException($"'{reader.Text}': a dispel is 'dispel own|enemy|any', not '{scope}'"),
        };
        return null;
    }

    /// <summary><c>move N|field …</c>: how far the stack goes (ADR-151).</summary>
    private static SpellEffect? Move(SpellEffectReader reader)
    {
        var range = reader.Next();
        reader.MoveRange = range == "field" ? 0 : SpellEffectReader.Number(range);
        if (reader.MoveRange < (range == "field" ? 0 : 1))
        {
            throw new FormatException($"'{reader.Text}': a move is 'move N|field', N at least 1");
        }

        return null;
    }

    /// <summary><c>raise N [once|each-round]</c>, the whole effect: no aim, the whole battle's dead (ADR-152).</summary>
    private static SpellEffect Raise(SpellEffectReader reader)
    {
        var amount = SpellEffectReader.Number(reader.Next());
        var rises = reader.More ? reader.Next() switch
        {
            "once" => SpellRises.Once,
            "each-round" => SpellRises.EachRound,
            var word => throw new FormatException($"'{reader.Text}': Raise Dead is 'raise N [once|each-round]', not '{word}'"),
        } : SpellRises.None;
        if (reader.More || amount < 1)
        {
            throw new FormatException($"'{reader.Text}': Raise Dead is 'raise N [once|each-round]', N at least 1");
        }

        return new SpellEffect(reader.Kind, amount, SpellReach.Side, 0, SpellFilter.None, SpellModifiers.None, null) { Rises = rises };
    }

    /// <summary><c>sacrifice N per M</c>, the whole effect: N % of the creatures, 1 mana per M health (ADR-152).</summary>
    private static SpellEffect Sacrifice(SpellEffectReader reader)
    {
        var amount = SpellEffectReader.Number(reader.Next());
        if (reader.Index + 2 != reader.Count || reader.Next() != "per" || SpellEffectReader.Number(reader.Next()) is not (> 0 and var per)
            || amount is < 1 or > 100)
        {
            throw new FormatException($"'{reader.Text}': a sacrifice is 'sacrifice N per M', N from 1 to 100, M at least 1");
        }

        return new SpellEffect(reader.Kind, amount, SpellReach.Stack, 0, SpellFilter.None, SpellModifiers.None, null) { HealthPerMana = per };
    }
}
