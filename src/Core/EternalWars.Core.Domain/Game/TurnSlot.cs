using EternalWars.Core.Domain.Battles;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Decisions;
using EternalWars.Core.Domain.MapEvents;

namespace EternalWars.Core.Domain.Game;

/// <summary>
/// What one turn has open (ADR-366): its battle, its question and whom the question asks, and its map events. A day played turn by turn has one,
/// shared by every turn; on a simultaneous day each person's open turn has its own, so several battles and questions are open at once
/// (<see cref="GameState.Battle"/> and <see cref="GameState.PendingDecision"/> are the slot of the turn the game is in).
/// </summary>
internal sealed class TurnSlot
{
    public BattleState? Battle { get; set; }

    public PendingDecision? Decision { get; set; }

    /// <summary>Whom <see cref="Decision"/> asks: its hero's owner, else the player whose turn asked it.</summary>
    public PlayerId? DecisionPlayer { get; set; }

    public MapEventQueue Events { get; } = new();

    /// <summary>Whether nothing is open: no battle, no question, no map event running or waiting.</summary>
    public bool IsIdle => Battle is null && Decision is null && Events.IsIdle;

    /// <summary>Whether <paramref name="player"/> commands a side of the slot's battle.</summary>
    public bool Fights(PlayerId player) => Battle is { } battle && (battle.Attacker.Owner == player || battle.Defender.Owner == player);

    /// <summary>The question the slot asks <paramref name="player"/>: a battle's when it fights it, else one asked of it; null otherwise.</summary>
    public PendingDecision? DecisionFor(PlayerId player) =>
        Decision is BattleDecision ? (Fights(player) ? Decision : null) : DecisionPlayer == player ? Decision : null;
}
