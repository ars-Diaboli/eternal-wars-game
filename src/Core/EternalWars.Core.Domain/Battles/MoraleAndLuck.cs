using EternalWars.Core.Domain.Creatures;

namespace EternalWars.Core.Domain.Battles;

/// <summary>What a unit's morale did this round (ADR-053).</summary>
public enum MoraleEffect
{
    None,

    /// <summary>Good morale: the unit acts again right after its action.</summary>
    ExtraAction,

    /// <summary>Good morale: +50 % initiative this round.</summary>
    Haste,

    /// <summary>Bad morale: the unit loses its turn this round.</summary>
    SkipTurn,

    /// <summary>Bad morale: −50 % initiative this round.</summary>
    Slow,
}

/// <summary>Luck of one hit, from the attacker's point of view (ADR-053).</summary>
public enum HitLuck
{
    None,

    /// <summary>+75 % damage: the attacker's good luck or the defender's bad luck.</summary>
    Lucky,

    /// <summary>−50 % damage: the defender's good luck or the attacker's bad luck.</summary>
    Unlucky,
}

/// <summary>
/// Morale and luck (ADR-053, draft numbers). A unit's value is its creature's plus its hero's, limited to ±<see cref="Limit"/>.
/// Morale is rolled at the start of every round: <see cref="ExtraActionPercent"/> per point for an extra action (good) or
/// a lost turn (bad); otherwise <see cref="InitiativePercent"/> per point for ±50 % initiative. Luck is rolled on every
/// hit by both sides with <see cref="LuckPercent"/> per point; rolls in the attacker's favour and in the defender's
/// cancel out. Nothing is rolled for a value of 0, so battles without morale or luck draw the same random numbers.
/// <para>
/// Army morale (ADR-075, CMB-2): an army of creatures from several factions gets <see cref="MixedFactionsMorale"/> per faction
/// beyond the first (neutrals count as one faction; war machines are not creatures); the defenders of a town with the upgraded
/// tavern (Brotherhood) get <see cref="BrotherhoodDefenderMorale"/>. Undead, constructs and elementals have no morale at all (ADR-071, CMB-7).
/// </para>
/// </summary>
public static class MoraleAndLuck
{
    public const int Limit = 5;

    public const int ExtraActionPercent = 2;

    public const int InitiativePercent = 4;

    public const int LuckPercent = 4;

    public const int LuckyDamagePercent = 175;

    public const int UnluckyDamagePercent = 50;

    /// <summary>Morale per faction beyond the first in one army (HoMM3).</summary>
    public const int MixedFactionsMorale = -1;

    /// <summary>Morale for the defenders of a town with the Brotherhood (tavern level 2).</summary>
    public const int BrotherhoodDefenderMorale = 1;

    /// <summary>The tavern level that gives a town's defenders <see cref="BrotherhoodDefenderMorale"/>.</summary>
    public const int BrotherhoodLevel = 2;

    public static int Clamp(int value) => Math.Clamp(value, -Limit, Limit);

    /// <summary>Whether a creature's morale counts; undead, constructs and elementals have none (ADR-071, CMB-7).</summary>
    public static bool HasMorale(CreatureDefinition creature) =>
        !creature.HasTag(CreatureTags.Undead) && !creature.HasTag(CreatureTags.Construct) && !creature.HasTag(CreatureTags.Elemental);

    /// <summary>The mixed-factions morale of an army (0 for one faction or none): neutrals count as one faction.</summary>
    public static int MixedFactions(IEnumerable<CreatureDefinition> creatures) =>
        MixedFactionsMorale * Math.Max(0, creatures.Select(c => c.Town ?? "").Distinct(StringComparer.Ordinal).Count() - 1);

    /// <summary>Initiative for this round's order after Haste (+50 %) or Slow (−50 %, at least 1).</summary>
    public static int Initiative(int initiative, MoraleEffect effect) => effect switch
    {
        MoraleEffect.Haste => initiative * 3 / 2,
        MoraleEffect.Slow => Math.Max(1, initiative / 2),
        _ => initiative,
    };

    public static int DamagePercent(HitLuck luck) => luck switch
    {
        HitLuck.Lucky => LuckyDamagePercent,
        HitLuck.Unlucky => UnluckyDamagePercent,
        _ => 100,
    };
}
