using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;

namespace EternalWars.Core.Domain.Commands;

/// <summary>
/// A player intent: the only way front-ends, AI, tests and replays change a running game (ADR-036).
/// Commands are plain serializable data; <see cref="Game.GameSession.Execute"/> validates and applies them, and
/// every accepted command is appended to the session's command log, so seed + map + log replay the game.
/// </summary>
/// <param name="Player">Who issues the command; checked against whose turn (or battle side) it is.</param>
public abstract record GameCommand(PlayerId Player);

// Adventure map ----------------------------------------------------------------------------------------

/// <summary>One step in a direction (keyboard movement).</summary>
public sealed record StepHeroCommand(PlayerId Player, HeroId Hero, Direction Direction) : GameCommand(Player);

/// <summary>Plan a route to <paramref name="Target"/> and follow it as far as movement points allow (click-to-move).</summary>
public sealed record MoveHeroCommand(PlayerId Player, HeroId Hero, MapPosition Target) : GameCommand(Player);

public sealed record EndTurnCommand(PlayerId Player) : GameCommand(Player);

// Decisions --------------------------------------------------------------------------------------------

public sealed record ResolveTreasureChestCommand(PlayerId Player, TreasureChestReward Reward) : GameCommand(Player);

/// <summary>Closes the exchange screen.</summary>
public sealed record CompleteExchangeCommand(PlayerId Player) : GameCommand(Player);

/// <summary>
/// Moves creatures between army slots of one hero, or between two heroes whose exchange is open.
/// <paramref name="Count"/> null = the whole stack (a whole stack onto a different creature swaps them).
/// </summary>
public sealed record MoveStackCommand(PlayerId Player, HeroId FromHero, int FromSlot, HeroId ToHero, int ToSlot, int? Count = null)
    : GameCommand(Player);

/// <summary>Answer to a <see cref="GuardEncounterDecision"/>: accept the join/flee offer, or decline and fight.</summary>
public sealed record RespondToGuardCommand(PlayerId Player, bool Accept) : GameCommand(Player);

// Battle -----------------------------------------------------------------------------------------------

public sealed record ChooseBattleWidthCommand(PlayerId Player, int Width) : GameCommand(Player);

public sealed record BattleMoveCommand(PlayerId Player, CellPosition To) : GameCommand(Player);

/// <summary>Melee attack; <paramref name="From"/> is where to stand (null = closest placement next to the target).</summary>
public sealed record BattleAttackCommand(PlayerId Player, int Target, CellPosition? From = null) : GameCommand(Player);

public sealed record BattleShootCommand(PlayerId Player, int Target) : GameCommand(Player);

public sealed record BattleWaitCommand(PlayerId Player) : GameCommand(Player);

public sealed record BattleDefendCommand(PlayerId Player) : GameCommand(Player);

/// <summary>The player's hero flees: its army is lost and it leaves the map (ADR-038).</summary>
public sealed record BattleRetreatCommand(PlayerId Player) : GameCommand(Player);

/// <summary>Quick combat: the AI fights the rest of the battle for both sides.</summary>
public sealed record AutoResolveBattleCommand(PlayerId Player) : GameCommand(Player);

/// <summary>Outcome of <see cref="Game.GameSession.Execute"/>.</summary>
/// <param name="IsAccepted">False when the command was refused; then nothing changed and it was not logged.</param>
/// <param name="Error">Why it was refused.</param>
/// <param name="Outcome">Command-specific result: <see cref="Heroes.MoveResult"/> for steps, <see cref="Heroes.MovementOutcome"/> for moves.</param>
public sealed record CommandResult(bool IsAccepted, string? Error = null, object? Outcome = null)
{
    public static CommandResult Accepted(object? outcome = null) => new(true, null, outcome);

    public static CommandResult Rejected(string error) => new(false, error);

    /// <summary>Throws <see cref="InvalidOperationException"/> for a refused command; returns the result otherwise.</summary>
    public CommandResult ThrowIfRejected() => IsAccepted ? this : throw new InvalidOperationException(Error);
}
