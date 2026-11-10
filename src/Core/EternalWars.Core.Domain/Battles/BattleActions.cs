namespace EternalWars.Core.Domain.Battles;

/// <summary>An action of the active battle unit.</summary>
public abstract record BattleAction;

public sealed record MoveAction(CellPosition To) : BattleAction;

/// <summary>Melee; <paramref name="From"/> is where to stand (null = the closest placement next to the target).</summary>
public sealed record AttackAction(int Target, CellPosition? From = null) : BattleAction;

public sealed record ShootAction(int Target) : BattleAction;

/// <summary>The siege engine fires at a wall section, the gate, the keep or a tower (ADR-106).</summary>
public sealed record ShootStructureAction(int Structure) : BattleAction;

/// <summary>A stack hits a siege structure in melee, the gate only (ADR-120); <paramref name="From"/> is where to stand (null = the closest placement).</summary>
public sealed record AttackStructureAction(int Structure, CellPosition? From = null) : BattleAction;

/// <summary>The active creature caster casts its spell at a stack or a cell (ADR-120); it is the stack's action.</summary>
public sealed record CreatureCastAction(int? Target = null, CellPosition? Cell = null) : BattleAction;

public sealed record WaitAction : BattleAction;

public sealed record DefendAction : BattleAction;
