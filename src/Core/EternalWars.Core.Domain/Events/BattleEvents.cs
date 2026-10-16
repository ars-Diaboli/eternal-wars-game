using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

public sealed record BattleUnitInfo(int Unit, BattleSide Side, string Creature, int Count, CellPosition Position, int Size);

/// <summary>The battlefield is set up and fighting begins (after the width choice, if any).</summary>
public sealed record BattleStarted(
    HeroId? AttackerHero,
    HeroId? DefenderHero,
    MapObjectId? Guard,
    int Width,
    int Height,
    bool IsAmbush,
    IReadOnlyList<BattleUnitInfo> Units,
    IReadOnlyList<BattleObstacle> Obstacles) : IEvent;

public sealed record BattleRoundStarted(int Round) : IEvent;

/// <summary>It is <paramref name="Unit"/>'s turn.</summary>
public sealed record BattleTurnStarted(int Unit, int Round) : IEvent;

public sealed record BattleUnitMoved(int Unit, CellPosition From, CellPosition To) : IEvent;

/// <summary>One hit. <paramref name="RemainingCount"/> is the target's stack size afterwards; <paramref name="Luck"/> what luck did.</summary>
public sealed record BattleUnitAttacked(
    int Attacker,
    int Target,
    BattleAttackKind Kind,
    int Damage,
    int Killed,
    int RemainingCount,
    HitLuck Luck = HitLuck.None) : IEvent;

/// <summary>
/// Morale took effect (ADR-053): Haste / Slow when the round starts, an extra action as it begins, a lost turn when the
/// unit's turn would have come.
/// </summary>
public sealed record BattleMoraleTriggered(int Unit, MoraleEffect Effect) : IEvent;

public sealed record BattleUnitDied(int Unit) : IEvent;

public sealed record BattleUnitWaited(int Unit) : IEvent;

public sealed record BattleUnitDefended(int Unit) : IEvent;

public sealed record BattleCasualty(BattleSide Side, string Creature, int Lost);

/// <summary>
/// The battle is over. <paramref name="Experience"/> went to the winning hero (if any). Map consequences
/// (armies, defeated heroes, guards) follow as their own events, then <see cref="DecisionResolved"/>.
/// </summary>
public sealed record BattleEnded(
    BattleSide Winner,
    BattleEndReason Reason,
    IReadOnlyList<BattleCasualty> Casualties,
    int Experience) : IEvent;
