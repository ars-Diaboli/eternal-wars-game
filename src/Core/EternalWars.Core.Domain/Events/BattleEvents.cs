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

/// <summary>
/// A siege's fortifications as the battle starts: the town's fort level, its structures and the moat's cells (ADR-106), and the town type whose
/// look they take (ADR-144; null when unknown).
/// </summary>
public sealed record SiegeLayout(int FortLevel, IReadOnlyList<SiegeStructureInfo> Structures, IReadOnlyList<CellRect> Moat, string? Town = null)
{
    public static SiegeLayout Of(SiegeState siege) =>
        new(siege.FortLevel, siege.Structures.Select(s => new SiegeStructureInfo(s.Id, s.Kind, s.Rect, s.Health)).ToList(), siege.Moat, siege.Spec.Town);
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

/// <summary>A siege structure was damaged by a stack hitting the gate (<paramref name="Unit"/>) or by a spell (<paramref name="Spell"/>, Earthquake; ADR-120).</summary>
public sealed record SiegeStructureDamaged(int Structure, int? Unit, string? Spell, int Damage, int Health) : IEvent;

/// <summary>The keep fell: its shooting stack is an ordinary stack now (ADR-120).</summary>
public sealed record BattleUnitLeftKeep(int Unit) : IEvent;

/// <summary>
/// The fortifications hurt a stack (ADR-106): a shot of the keep or a tower (<paramref name="Structure"/>), or the moat it entered.
/// <paramref name="RemainingCount"/> is the stack's size afterwards, <paramref name="TopHealth"/> its top creature's hit points (ADR-145).
/// </summary>
public sealed record BattleUnitSiegeDamaged(int Unit, SiegeDamageSource Source, int? Structure, int Damage, int Killed, int RemainingCount, int TopHealth) : IEvent;

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

/// <summary>A move spell (Teleport, Shadow Step, ADR-151) took the stack from one placement to another, over everything between.</summary>
public sealed record BattleUnitTeleported(int Unit, CellPosition From, CellPosition To, string Spell) : IEvent;

/// <summary>
/// A move spell's rank III (ADR-151) gave the stack, which had taken its turn this round, one more after the waiting stacks; with
/// <paramref name="InPlace"/> it does not move on it.
/// </summary>
public sealed record BattleUnitTurnRegained(int Unit, string Spell, bool InPlace) : IEvent;

/// <summary>
/// One hit. <paramref name="RemainingCount"/> is the target's stack size afterwards and <paramref name="TopHealth"/> its top creature's hit
/// points (ADR-145); <paramref name="Luck"/> what luck did.
/// </summary>
public sealed record BattleUnitAttacked(
    int Attacker,
    int Target,
    BattleAttackKind Kind,
    int Damage,
    int Killed,
    int RemainingCount,
    int TopHealth,
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

/// <summary>A creature caster cast its spell as its action (ADR-120); <paramref name="CastsLeft"/> of it are left this battle.</summary>
public sealed record BattleCreatureCast(int Unit, string Spell, int? Target, CellPosition? Cell, int CastsLeft) : IEvent;

/// <summary>A summon called creatures (ADR-120): <paramref name="Unit"/> as it is now (a new stack, or an own summoned stack that grew by <paramref name="Added"/>).</summary>
public sealed record BattleUnitSummoned(BattleUnitInfo Unit, string Spell, int Added) : IEvent;

/// <summary>A Phoenix rose from its ashes with <paramref name="Count"/> creatures (ADR-120).</summary>
public sealed record BattleUnitReborn(int Unit, int Count) : IEvent;

/// <summary>
/// A side's hero cast a spell (ADR-110), before its effects: at a stack (<paramref name="Target"/>) or a cell (an area's centre), or at nothing
/// for a spell that reaches a whole side or the field. <paramref name="Mana"/> is what the hero has left. A two-click spell (ADR-151) names its
/// second aim: the cell a move goes to (<paramref name="Cell"/>), the own stack a drain heals (<paramref name="Second"/>).
/// </summary>
public sealed record BattleSpellCast(BattleSide Side, string Spell, int? Target, CellPosition? Cell, int Cost, int Mana, int? Second = null) : IEvent;

/// <summary>A spell's damage to a stack (ADR-110); <paramref name="TopHealth"/> is its top creature's hit points afterwards (ADR-145).</summary>
public sealed record BattleUnitSpellDamaged(int Unit, string Spell, int Damage, int Killed, int RemainingCount, int TopHealth) : IEvent;

/// <summary>
/// A buff or curse took hold on a stack until the end of round <paramref name="UntilRound"/> (ADR-110); one that lasts turns (ADR-150) for the
/// stack's next <paramref name="Turns"/> turns.
/// </summary>
public sealed record BattleSpellApplied(int Unit, string Spell, int UntilRound, int? Turns = null) : IEvent;

/// <summary>
/// A stack now fights for <paramref name="Side"/> (ADR-150): a control spell took it over, or let it go (it ran out, was dispelled, or the stack
/// died).
/// </summary>
public sealed record BattleUnitSideChanged(int Unit, BattleSide Side) : IEvent;

/// <summary>A stack in the spell's reach was not touched by it: immune, resisted, or not the kind it works on (ADR-110).</summary>
public sealed record BattleSpellResisted(int Unit, string Spell) : IEvent;

/// <summary>
/// A buff or curse on a stack ran out as a round started (ADR-110), or ended early: Blind broken by a hit, Unseen by acting (ADR-123), its last
/// turn used (ADR-150).
/// </summary>
public sealed record BattleSpellExpired(int Unit, string Spell) : IEvent;

/// <summary>A dispel (<paramref name="By"/>) took a spell off a stack (ADR-123).</summary>
public sealed record BattleSpellDispelled(int Unit, string Spell, string By) : IEvent;

/// <summary>
/// A stack got health back above its top creature (ADR-123): Resurrection, Animate Dead, Vampirism. <paramref name="Count"/> and
/// <paramref name="TopHealth"/> are as they are now; a stack destroyed earlier stands again.
/// </summary>
public sealed record BattleUnitRevived(int Unit, string Spell, int Health, int Count, int TopHealth) : IEvent;

/// <summary>A stack's turn passed under a spell that holds it (Blind, Stasis, ADR-123).</summary>
public sealed record BattleUnitSpellbound(int Unit, string Spell) : IEvent;

/// <summary>A spell left a field zone on <paramref name="Rect"/> until the end of round <paramref name="UntilRound"/> (ADR-149).</summary>
public sealed record BattleZoneCreated(int Zone, string Spell, BattleSide Caster, CellRect Rect, int UntilRound) : IEvent;

/// <summary>A field zone ran out as a round started (ADR-149).</summary>
public sealed record BattleZoneExpired(int Zone, string Spell) : IEvent;

/// <summary>A spell's hit made the stack lose its next action (Holy Word III, Frost Bolt III, ADR-123).</summary>
public sealed record BattleUnitStunned(int Unit, string Spell) : IEvent;