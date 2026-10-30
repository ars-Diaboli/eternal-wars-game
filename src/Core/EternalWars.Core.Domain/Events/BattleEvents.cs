using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>A unit as the battle starts: its footprint is <paramref name="Width"/> × <paramref name="Height"/> cells from <paramref name="Position"/>.</summary>
public sealed record BattleUnitInfo(int Unit, BattleSide Side, string Creature, int Count, CellPosition Position, int Width, int Height, WarMachineKind? Machine = null)
{
    public CellRect Rect => new(Position.X, Position.Y, Width, Height);
}

/// <summary>A siege structure as the battle starts (ADR-106).</summary>
public sealed record SiegeStructureInfo(int Structure, SiegeStructureKind Kind, CellRect Rect, int Health);

/// <summary>A siege's fortifications as the battle starts: the town's fort level, its structures and the moat's cells (ADR-106).</summary>
public sealed record SiegeLayout(int FortLevel, IReadOnlyList<SiegeStructureInfo> Structures, IReadOnlyList<CellRect> Moat)
{
    public static SiegeLayout Of(SiegeState siege) =>
        new(siege.FortLevel, siege.Structures.Select(s => new SiegeStructureInfo(s.Id, s.Kind, s.Rect, s.Health)).ToList(), siege.Moat);
}

/// <summary>
/// The battlefield is set up (after the width choice, if any). Fighting begins at once, or after the deployment step
/// (<see cref="BattleDeploymentStarted"/> … <see cref="BattleDeploymentEnded"/>). <paramref name="Siege"/> is set for a siege (ADR-106).
/// </summary>
public sealed record BattleStarted(
    HeroId? AttackerHero,
    HeroId? DefenderHero,
    MapObjectId? Guard,
    int Width,
    int Height,
    bool IsAmbush,
    IReadOnlyList<BattleUnitInfo> Units,
    IReadOnlyList<BattleObstacle> Obstacles,
    SiegeLayout? Siege = null) : IEvent;

/// <summary>
/// The attacker's offensive machine fired at a siege structure (ADR-106): <paramref name="Hit"/> whether it hit, <paramref name="Damage"/> the hit
/// points it took off, <paramref name="Health"/> what is left (0: destroyed; a wall section is a gap now).
/// </summary>
public sealed record SiegeStructureShot(int Machine, int Structure, bool Hit, int Damage, int Health) : IEvent;

/// <summary>
/// The fortifications hurt a stack (ADR-106): a shot of the keep or a tower (<paramref name="Structure"/>), or the moat it entered.
/// <paramref name="RemainingCount"/> is the stack's size afterwards.
/// </summary>
public sealed record BattleUnitSiegeDamaged(int Unit, SiegeDamageSource Source, int? Structure, int Damage, int Killed, int RemainingCount) : IEvent;

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

/// <summary>A human player changed its auto combat setting (ADR-090, CMB-10).</summary>
public sealed record AutoCombatChanged(PlayerId Player, AutoCombatMode Mode) : IEvent;

/// <summary>Necromancy raised undead after the battle (ADR-072, MAG-7); published after <see cref="BattleEnded"/> and before any level-up.</summary>
public sealed record UndeadRaised(HeroId Hero, IReadOnlyList<RaisedUndead> Raised) : IEvent;

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

/// <summary>
/// A side's hero cast a spell (ADR-110), before its effects: at a stack (<paramref name="Target"/>) or a cell (an area's centre), or at nothing
/// for a spell that reaches a whole side or the field. <paramref name="Mana"/> is what the hero has left.
/// </summary>
public sealed record BattleSpellCast(BattleSide Side, string Spell, int? Target, CellPosition? Cell, int Cost, int Mana) : IEvent;

/// <summary>A spell's damage to a stack (ADR-110).</summary>
public sealed record BattleUnitSpellDamaged(int Unit, string Spell, int Damage, int Killed, int RemainingCount) : IEvent;

/// <summary>A buff or curse took hold on a stack until the end of round <paramref name="UntilRound"/> (ADR-110).</summary>
public sealed record BattleSpellApplied(int Unit, string Spell, int UntilRound) : IEvent;

/// <summary>A stack in the spell's reach was not touched by it: immune, resisted, or not the kind it works on (ADR-110).</summary>
public sealed record BattleSpellResisted(int Unit, string Spell) : IEvent;

/// <summary>A buff or curse on a stack ran out as a round started (ADR-110).</summary>
public sealed record BattleSpellExpired(int Unit, string Spell) : IEvent;