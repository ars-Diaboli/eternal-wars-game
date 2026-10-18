using System.Globalization;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Domain.Map;

namespace EternalWars.Core.Domain.Artifacts;

/// <summary>Where an artifact is worn (the paper doll of ADR-061).</summary>
public enum ArtifactSlot
{
    Head,
    Neck,
    Cloak,
    Body,
    Weapon,
    Shield,
    Ring,
    Boots,
    Misc,
}

public static class ArtifactSlots
{
    /// <summary>The backpack holds this many unworn artifacts.</summary>
    public const int BackpackSize = 50;

    /// <summary>How many artifacts of a kind a hero wears: two rings, three miscellaneous, one of the rest.</summary>
    public static int Capacity(ArtifactSlot slot) => slot switch
    {
        ArtifactSlot.Ring => 2,
        ArtifactSlot.Misc => 3,
        _ => 1,
    };

    /// <summary>Slots with their capacity, in paper-doll order.</summary>
    public static IEnumerable<ArtifactSlot> All => Enum.GetValues<ArtifactSlot>();
}

/// <summary>How rare an artifact is, which decides what drops it (map pickups roll by tier).</summary>
public enum ArtifactTier
{
    Minor,
    Major,

    /// <summary>One of each per game when rolled; named, with a story.</summary>
    Relic,
}

/// <summary>What an artifact does to its wearer. The value's unit depends on the kind.</summary>
public enum ArtifactEffectKind
{
    Attack,
    Defense,
    SpellPower,
    Knowledge,
    Morale,
    Luck,

    /// <summary>Percent of the daily movement points.</summary>
    Movement,

    /// <summary>Flat movement points.</summary>
    MovementFlat,

    /// <summary>Battlefield speed of every own stack.</summary>
    BattleSpeed,

    /// <summary>Percent more melee damage.</summary>
    MeleeDamage,

    /// <summary>Percent more ranged damage.</summary>
    RangedDamage,

    /// <summary>Percent less damage taken from everything.</summary>
    DamageTaken,

    /// <summary>Percent less damage taken from ranged attacks.</summary>
    RangedDamageTaken,

    /// <summary><c>Target</c> is a resource: a daily amount.</summary>
    Income,

    /// <summary><c>Target</c> is a terrain: the hero's army walks it as if native (no penalty).</summary>
    NativeTerrain,

    /// <summary>Percent of the non-native terrain penalty removed everywhere (adds to Pathfinding, at most all of it).</summary>
    TerrainPenalty,

    /// <summary><c>Target</c> is a skill id: levels added to it, up to its maximum.</summary>
    Skill,

    /// <summary>Levels added to every skill the hero has started.</summary>
    AllSkills,
}

/// <param name="Kind">What changes.</param>
/// <param name="Value">By how much (see <see cref="ArtifactEffectKind"/>).</param>
/// <param name="Target">The resource, terrain or skill of the kinds that name one.</param>
public sealed record ArtifactEffect(ArtifactEffectKind Kind, int Value, string? Target = null)
{
    private static readonly Dictionary<string, ArtifactEffectKind> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["attack"] = ArtifactEffectKind.Attack,
        ["defense"] = ArtifactEffectKind.Defense,
        ["spell-power"] = ArtifactEffectKind.SpellPower,
        ["knowledge"] = ArtifactEffectKind.Knowledge,
        ["morale"] = ArtifactEffectKind.Morale,
        ["luck"] = ArtifactEffectKind.Luck,
        ["movement"] = ArtifactEffectKind.Movement,
        ["movement-flat"] = ArtifactEffectKind.MovementFlat,
        ["battle-speed"] = ArtifactEffectKind.BattleSpeed,
        ["melee-damage"] = ArtifactEffectKind.MeleeDamage,
        ["ranged-damage"] = ArtifactEffectKind.RangedDamage,
        ["damage-taken"] = ArtifactEffectKind.DamageTaken,
        ["ranged-damage-taken"] = ArtifactEffectKind.RangedDamageTaken,
        ["income"] = ArtifactEffectKind.Income,
        ["native-terrain"] = ArtifactEffectKind.NativeTerrain,
        ["terrain-penalty"] = ArtifactEffectKind.TerrainPenalty,
        ["skill"] = ArtifactEffectKind.Skill,
        ["all-skills"] = ArtifactEffectKind.AllSkills,
    };

    /// <summary>The word of a kind in the table: <c>spell-power</c>.</summary>
    public static string WordOf(ArtifactEffectKind kind) => Words.First(w => w.Value == kind).Key;

    /// <summary>
    /// Parses <c>attack 2</c>, <c>income gold 100</c>, <c>native-terrain grass</c> (value 1), <c>skill machines.ballistics 1</c> or
    /// <c>all-skills 1</c>. Throws <see cref="FormatException"/> for anything else.
    /// </summary>
    public static ArtifactEffect Parse(string text)
    {
        var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !Words.TryGetValue(parts[0], out var kind))
        {
            throw new FormatException($"'{text}' does not start with an effect word ({string.Join(", ", Words.Keys)})");
        }

        return kind switch
        {
            ArtifactEffectKind.Income when parts is [_, var resource, var amount] && Enum.TryParse<ResourceType>(resource, true, out var r) && Int(amount) is { } n
                => new(kind, n, r.ToString().ToLowerInvariant()),
            ArtifactEffectKind.NativeTerrain when parts is [_, var terrain] && Enum.TryParse<TerrainType>(terrain.Replace("-", ""), true, out var t)
                => new(kind, 1, t.ToString()),
            ArtifactEffectKind.Skill when parts is [_, var skill, var levels] && BuiltInSkills.Contains(skill) && Int(levels) is { } n
                => new(kind, n, skill),
            ArtifactEffectKind.Income or ArtifactEffectKind.NativeTerrain or ArtifactEffectKind.Skill
                => throw new FormatException($"'{text}': {parts[0]} needs a resource/terrain/skill and (for income and skill) a number"),
            _ when parts is [_, var value] && Int(value) is { } n => new(kind, n),
            _ => throw new FormatException($"'{text}' is not '<effect> <number>'"),
        };

        static int? Int(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    public override string ToString() => Kind switch
    {
        ArtifactEffectKind.Income or ArtifactEffectKind.Skill => $"{WordOf(Kind)} {Target} {Value.ToString(CultureInfo.InvariantCulture)}",
        ArtifactEffectKind.NativeTerrain => $"{WordOf(Kind)} {Target}",
        _ => $"{WordOf(Kind)} {Value.ToString(CultureInfo.InvariantCulture)}",
    };
}

/// <summary>
/// One artifact of the catalog (<c>data/artifacts.csv</c>). <c>Set</c> names the <see cref="ArtifactSetDefinition"/> it belongs to
/// (or null); <c>Notes</c> is free text: the story, and effects the game cannot apply yet.
/// </summary>
public sealed record ArtifactDefinition(
    string Id, string Name, ArtifactSlot Slot, ArtifactTier Tier, string? Set, IReadOnlyList<ArtifactEffect> Effects, string Notes = "");

/// <summary>Worn together, the pieces of a set add <see cref="Bonus"/> to the individual effects.</summary>
public sealed record ArtifactSetDefinition(string Id, string Name, IReadOnlyList<ArtifactEffect> Bonus, string Notes = "");
