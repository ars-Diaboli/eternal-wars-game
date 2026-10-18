using EternalWars.Core.Domain.Heroes;

namespace EternalWars.Core.Domain.Battles;

/// <summary>The warcries of Stronghold heroes (ADR-067), learned with the Warcry skill: Rally at Basic, Intimidate at Advanced, Terror at Expert.</summary>
public enum Warcry
{
    /// <summary>Own stacks get +1 morale for 3 rounds.</summary>
    Rally,

    /// <summary>Enemy stacks get −1 morale and −20 % initiative for 2 rounds.</summary>
    Intimidate,

    /// <summary>Each enemy creature stack loses its next action with 30 % chance.</summary>
    Terror,
}

/// <summary>
/// A morale and initiative change on every creature stack of a side for some rounds, from a warcry. It counts in the rounds after the one it
/// was used in: the morale and the order of a round are decided when the round starts.
/// </summary>
public sealed record BattleSideEffect(BattleSide Side, int Morale, int InitiativePercent, int UntilRound);

/// <summary>Stronghold's Primal Fury group (ADR-067, draft numbers): Blood Rage, Warcry and the faction's spell resistance.</summary>
public static class PrimalFury
{
    public const int RallyMorale = 1;

    public const int RallyRounds = 3;

    public const int IntimidateMorale = -1;

    public const int IntimidateInitiativePercent = -20;

    public const int IntimidateRounds = 2;

    public const int TerrorChancePercent = 30;

    /// <summary>Blood Rage: the most Attack a stack gains from kills, +2 per level (Master 8).</summary>
    public static int BloodRageMaxAttack(SkillLevel level) => 2 * (int)level;

    /// <summary>The warcries a Warcry level offers.</summary>
    public static bool Knows(SkillLevel level, Warcry warcry) => level >= warcry switch
    {
        Warcry.Rally => SkillLevel.Basic,
        Warcry.Intimidate => SkillLevel.Advanced,
        _ => SkillLevel.Expert,
    };

    /// <summary>The faction's spell resistance: Spell Power the enemy hero's spells lose, 2 per level. Magic (Phase 5) reads it.</summary>
    public static int SpellPowerPenalty(SkillLevel level) => 2 * Math.Min(3, (int)level);
}
