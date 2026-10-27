using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Creatures;

/// <summary>Battlefield footprint of a creature stack (ADR-005): the value is the side length in cells.</summary>
public enum CreatureSize
{
    Small = 2,
    Medium = 3,
    Large = 4,
}

public static class CreatureSizeExtensions
{
    /// <summary>Side length of the square footprint in battlefield cells.</summary>
    public static int Cells(this CreatureSize size) => (int)size;

    /// <summary>Ranged cover a stack of this size gives when it stands in the line of fire (ADR-035).</summary>
    public static int CoverEighths(this CreatureSize size) => size == CreatureSize.Large ? 2 : 1;
}

/// <summary>
/// Special rules a creature can have, HoMM4-style; at most <see cref="CreatureDefinition.MaxCapabilities"/> per
/// creature (ADR-042). Capabilities key off creature properties only, never off town or group identity (ADR-029).
/// <see cref="CreatureCapability.Value"/> and <see cref="CreatureCapability.Parameter"/> parameterise some of them.
/// </summary>
public enum CapabilityKind
{
    /// <summary>Moves to any free spot within its speed, ignoring units and obstacles on the way.</summary>
    Flying,

    /// <summary>Strikes twice per melee attack (the second strike after the retaliation).</summary>
    DoubleStrike,

    /// <summary>Shoots twice per ranged attack (the second shot after a ranged retaliation); one shot of ammunition each.</summary>
    ShootsTwice,

    /// <summary>Its attacks, melee or ranged, provoke no retaliation.</summary>
    NoEnemyRetaliation,

    /// <summary>Retaliates against every attack, not just the first one each round.</summary>
    UnlimitedRetaliation,

    /// <summary>Retaliates before the attacker strikes (normal order if the attacker has it too).</summary>
    FirstStrike,

    /// <summary>Shooter that shoots back when shot at, if it could shoot the attacker right now.</summary>
    RangedRetaliation,

    /// <summary>Shooter that deals only 50 % damage in melee (shooters without it fight at full strength).</summary>
    MeleePenalty,

    /// <summary>Melee attack also hits every unit (friend or foe) in the target-sized area behind the target.</summary>
    Breath,

    /// <summary>Melee attack also hits up to two other enemies adjacent to both attacker and target.</summary>
    Cleave,

    /// <summary>Melee attack also hits every other enemy adjacent to the attacker.</summary>
    MultiHead,

    /// <summary>+Value % damage for every other friendly unit already engaging the target (engagement hook).</summary>
    PackHunter,

    /// <summary>+Value % damage for every time the target was already hit this round (hit-history hook).</summary>
    Relentless,

    /// <summary>
    /// Unaffected by spells carrying the tag in <see cref="CreatureCapability.Parameter"/> (<see cref="SpellTags"/>).
    /// Data and query only until magic exists (Phase 5).
    /// </summary>
    SpellImmunity,

    /// <summary>
    /// Unaffected by spells of level <see cref="CreatureCapability.Value"/> and below; a spell upgraded twice counts as
    /// one level higher (<see cref="CreatureDefinition.ResistsSpell"/>). Data and query only until magic exists (Phase 5).
    /// </summary>
    SpellResistance,

    /// <summary>
    /// Can cast the spell <see cref="CreatureCapability.Parameter"/> <see cref="CreatureCapability.Value"/> times per
    /// battle. Data and casts-left bookkeeping only until magic exists (Phase 5).
    /// </summary>
    Caster,

    /// <summary>Teleports: moves like <see cref="Flying"/> (to any free spot within its speed, ignoring what lies between; ADR-070, CMB-4).</summary>
    Teleporting,
}

/// <param name="Kind">The rule.</param>
/// <param name="Value">Percentage (pack hunter, relentless), highest resisted spell level (spell resistance) or casts per battle (caster).</param>
/// <param name="Parameter">Spell tag (spell immunity) or spell id (caster).</param>
public sealed record CreatureCapability(CapabilityKind Kind, int Value = 0, string? Parameter = null)
{
    /// <summary>Throws <see cref="FormatException"/> when the kind's value or parameter is missing.</summary>
    public void Validate(string creature)
    {
        var problem = Kind switch
        {
            CapabilityKind.PackHunter or CapabilityKind.Relentless when Value <= 0 => "needs a positive percentage",
            CapabilityKind.SpellImmunity when string.IsNullOrWhiteSpace(Parameter) => "needs a spell tag",
            CapabilityKind.SpellResistance when Value is < 1 or >= SpellLevels.Max => $"needs a spell level from 1 to {SpellLevels.Max - 1}",
            CapabilityKind.Caster when string.IsNullOrWhiteSpace(Parameter) || Value < 1 => "needs a spell id and at least one cast",
            _ => null,
        };
        if (problem is not null)
        {
            throw new FormatException($"Creature '{creature}': {Kind} {problem}.");
        }
    }
}

/// <summary>
/// Well-known spell tags for <see cref="CapabilityKind.SpellImmunity"/>. Free-form strings: the magic system
/// (Phase 5) tags its spells with them and may add schools.
/// </summary>
public static class SpellTags
{
    /// <summary>Every spell.</summary>
    public const string All = "all";

    /// <summary>Mind control: fear, charm, berserk, …</summary>
    public const string Mind = "mind";

    public const string Fire = "fire";

    /// <summary>Hostile spells that weaken a stack.</summary>
    public const string Curse = "curse";
}

/// <summary>Spell levels and ranks (ADR-047): four levels, and every spell has three ranks (learned, then upgraded twice).</summary>
public static class SpellLevels
{
    public const int Max = 4;

    public const int MaxRank = 3;

    /// <summary>Level a spell counts as against resistance: a spell upgraded twice (rank 3) counts as the next level.</summary>
    public static int Effective(int level, int rank) => rank >= MaxRank ? level + 1 : level;
}

/// <summary>Well-known creature tags (free-form strings so content and mods can add more).</summary>
public static class CreatureTags
{
    public const string Human = "human";
    public const string Holy = "holy";
    public const string Demon = "demon";
    public const string Beast = "beast";
    public const string Mounted = "mounted";
    public const string Dragon = "dragon";
    public const string Undead = "undead";
    public const string Construct = "construct";
    public const string Elemental = "elemental";
    public const string Elf = "elf";
    public const string Fey = "fey";
    public const string Orc = "orc";
    public const string Giant = "giant";
    public const string Reptile = "reptile";
}

/// <summary>
/// Data definition of a creature type (one per base and per upgraded form). Two speeds on purpose:
/// <see cref="Speed"/> is battlefield cells per turn, <see cref="MapSpeed"/> is the HoMM3-scale value the
/// adventure-map movement table uses (ADR-037).
/// </summary>
public sealed record CreatureDefinition(string Id, string Name, int Tier, CreatureSize Size)
{
    public const int MinTier = 1;
    public const int MaxTier = 5;

    /// <summary>HoMM4-style limit on special rules per creature (ADR-042).</summary>
    public const int MaxCapabilities = 5;

    /// <summary>Town the creature belongs to (<c>TownIds</c>); null for neutrals.</summary>
    public string? Town { get; init; }

    /// <summary>Id of the upgraded form; null when the creature has no upgrade (or is the upgrade).</summary>
    public string? UpgradesTo { get; init; }

    public int Attack { get; init; }

    public int Defense { get; init; }

    public int MinDamage { get; init; }

    public int MaxDamage { get; init; }

    /// <summary>Hit points of one creature.</summary>
    public int Health { get; init; }

    /// <summary>Battlefield movement in cells per turn.</summary>
    public int Speed { get; init; }

    /// <summary>Adventure-map speed on the HoMM3 scale (≤3 … ≥11), used by <c>MovementAllowance</c>.</summary>
    public int MapSpeed { get; init; }

    /// <summary>Turn order within a battle round (ADR-009).</summary>
    public int Initiative { get; init; }

    /// <summary>Ammunition per battle; greater than zero makes the creature a shooter.</summary>
    public int Shots { get; init; }

    /// <summary>Base morale, −5…+5; the hero's is added in battle (ADR-053).</summary>
    public int Morale { get; init; }

    /// <summary>Base luck, −5…+5; the hero's is added in battle (ADR-053).</summary>
    public int Luck { get; init; }

    public int WeeklyGrowth { get; init; }

    public ResourceSet Cost { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<CreatureCapability> Capabilities { get; init; } = [];

    public bool IsRanged => Shots > 0;

    public bool IsFlying => Has(CapabilityKind.Flying);

    public bool IsTeleporting => Has(CapabilityKind.Teleporting);

    /// <summary>Flyers and teleporters reach any free spot within their speed, whatever stands between (ADR-070).</summary>
    public bool MovesFreely => IsFlying || IsTeleporting;

    public bool Has(CapabilityKind kind) => Capabilities.Any(a => a.Kind == kind);

    public int CapabilityValue(CapabilityKind kind) => Capabilities.FirstOrDefault(a => a.Kind == kind)?.Value ?? 0;

    /// <summary>Hook for magic: whether a spell with these tags leaves the creature unaffected.</summary>
    public bool IsImmuneTo(IEnumerable<string> spellTags)
    {
        var immunities = Capabilities.Where(c => c.Kind == CapabilityKind.SpellImmunity).Select(c => c.Parameter!).ToHashSet(StringComparer.Ordinal);
        return immunities.Contains(SpellTags.All) || spellTags.Any(immunities.Contains);
    }

    /// <summary>
    /// Hook for magic: whether <see cref="CapabilityKind.SpellResistance"/> stops a spell of this level and rank
    /// (1 learned … 3 upgraded twice); a twice-upgraded spell counts as the next level (<see cref="SpellLevels.Effective"/>).
    /// </summary>
    public bool ResistsSpell(int level, int rank) => SpellLevels.Effective(level, rank) <= CapabilityValue(CapabilityKind.SpellResistance);

    /// <summary>Spells the creature can cast, with casts per battle (hook for magic).</summary>
    public IReadOnlyDictionary<string, int> SpellCasts =>
        Capabilities.Where(c => c.Kind == CapabilityKind.Caster).ToDictionary(c => c.Parameter!, c => c.Value, StringComparer.Ordinal);

    public bool HasTag(string tag) => Tags.Contains(tag, StringComparer.Ordinal);

    /// <summary>
    /// Rough strength of one creature, used by the battle AI and neutral dispositions:
    /// health × average damage × (20 + Attack + Defense) / 40, ×1.5 for shooters, ×1.2 for flyers.
    /// </summary>
    public int Value
    {
        get
        {
            var value = (long)Health * (MinDamage + MaxDamage) * (20 + Attack + Defense) / 80;
            if (IsRanged)
            {
                value = value * 3 / 2;
            }

            if (MovesFreely)
            {
                value = value * 6 / 5;
            }

            return (int)Math.Max(1, value);
        }
    }

    /// <summary>Throws <see cref="FormatException"/> for values the rules cannot work with.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new FormatException("Creature definition needs an id.");
        }

        if (Tier is < MinTier or > MaxTier)
        {
            throw new FormatException($"Creature '{Id}' has tier {Tier}; tiers are {MinTier}–{MaxTier}.");
        }

        if (!Enum.IsDefined(Size))
        {
            throw new FormatException($"Creature '{Id}' has an unknown size {Size}.");
        }

        if (Health < 1 || MinDamage < 1 || MaxDamage < MinDamage || Speed < 1 || MapSpeed < 1 || Initiative < 1)
        {
            throw new FormatException($"Creature '{Id}' needs health, damage (min ≤ max), speeds and initiative of at least 1.");
        }

        if (Attack < 0 || Defense < 0 || Shots < 0)
        {
            throw new FormatException($"Creature '{Id}' has negative attack, defense or shots.");
        }

        if (Math.Abs(Morale) > Battles.MoraleAndLuck.Limit || Math.Abs(Luck) > Battles.MoraleAndLuck.Limit)
        {
            throw new FormatException($"Creature '{Id}' has morale or luck outside ±{Battles.MoraleAndLuck.Limit}.");
        }

        if (Capabilities.Count > MaxCapabilities)
        {
            throw new FormatException($"Creature '{Id}' has {Capabilities.Count} capabilities; at most {MaxCapabilities} are allowed.");
        }

        foreach (var capability in Capabilities)
        {
            capability.Validate(Id);
        }

        if (Capabilities.GroupBy(c => (c.Kind, c.Parameter)).Any(g => g.Count() > 1))
        {
            throw new FormatException($"Creature '{Id}' lists a capability twice.");
        }

        if (!IsRanged && Capabilities.Any(c => c.Kind is CapabilityKind.ShootsTwice or CapabilityKind.RangedRetaliation or CapabilityKind.MeleePenalty))
        {
            throw new FormatException($"Creature '{Id}' has a shooter capability but no shots.");
        }
    }
}
