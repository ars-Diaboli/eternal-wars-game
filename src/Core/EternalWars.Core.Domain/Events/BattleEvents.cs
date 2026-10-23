using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>A unit as the battle starts: its footprint is <paramref name="Width"/> × <paramref name="Height"/> cells from <paramref name="Position"/>.</summary>
public sealed record BattleUnitInfo(int Unit, BattleSide Side, string Creature, int Count, CellPosition Position, int Width, int Height, WarMachineKind? Machine = null)
{
    public CellRect Rect => new(Position.X, Position.Y, Width, Height);
}

/// <summary>
/// The battlefield is set up (after the width choice, if any). Fighting begins at once, or after the deployment step
/// (<see cref="BattleDeploymentStarted"/> … <see cref="BattleDeploymentEnded"/>).
/// </summary>
public sealed record BattleStarted(
    HeroId? AttackerHero,
    HeroId? DefenderHero,
    MapObjectId? Guard,
    int Width,
    int Height,
    bool IsAmbush,
    IReadOnlyList<BattleUnitInfo> Units,
    IReadOnlyList<BattleObstacle> Obstacles) : IEvent;

/// <summary>The side with the higher Tactics may place its stacks within <paramref name="Columns"/> columns of its edge (ADR-072).</summary>
public sealed record BattleDeploymentStarted(BattleSide Side, int Columns) : IEvent;

/// <summary>A stack was placed elsewhere during the deployment step.</summary>
public sealed record BattleUnitDeployed(int Unit, CellPosition From, CellPosition To) : IEvent;

/// <summary>The deployer pressed "Start battle" (or handed the battle to quick combat); the first round follows.</summary>
public sealed record BattleDeploymentEnded(BattleSide Side) : IEvent;

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

/// <summary>The healing tent restored <paramref name="Healed"/> health to the stack's top creature (ADR-056).</summary>
public sealed record BattleUnitHealed(int Unit, int Healed, int TopHealth) : IEvent;

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

/// <summary>A stack gained Attack from a kill with Blood Rage (ADR-067); <paramref name="Attack"/> is the total it has gained.</summary>
public sealed record BattleRageGained(int Unit, int Attack) : IEvent;

/// <summary>
/// A hero used a warcry (ADR-067). <paramref name="Affected"/> are the stacks it works on: all of the side's own for Rally, all enemy stacks for
/// Intimidate, and the enemy stacks that lost their next action for Terror.
/// </summary>
public sealed record BattleWarcryUsed(BattleSide Side, Warcry Warcry, IReadOnlyList<int> Affected) : IEvent;

/// <summary>Terror made a stack lose its action (ADR-067); its turn passed.</summary>
public sealed record BattleUnitTerrified(int Unit) : IEvent;
