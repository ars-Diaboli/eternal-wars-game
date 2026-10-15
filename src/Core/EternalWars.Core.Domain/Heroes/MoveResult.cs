using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Heroes;

public enum MoveResult
{
    Moved,
    NotYourTurn,
    DecisionPending,
    OutOfBounds,
    Impassable,

    /// <summary>A blocked cell of a map object.</summary>
    Blocked,

    /// <summary>Another hero stands there (meeting/battles come later).</summary>
    Occupied,

    /// <summary>The hero has no movement points left at all (any positive amount allows one last step).</summary>
    NotEnoughMovementPoints,
    NoPath,

    /// <summary>The world changed under a planned path (e.g. a gate exit got occupied).</summary>
    Interrupted,
}

/// <summary>Result of following a path: how far the hero got and why it stopped.</summary>
public sealed record MovementOutcome(int StepsTaken, MoveResult StopReason, bool Arrived);

/// <summary>Single source of truth for "may a hero stand on this tile", shared by movement and pathfinding.</summary>
internal static class MovementRules
{
    /// <param name="state">The game.</param>
    /// <param name="target">Tile to enter.</param>
    /// <param name="viewer">
    /// When planning for a player, heroes that player cannot currently see do not block, because the player
    /// doesn't know about them. Actual movement passes null and bumps into them.
    /// </param>
    public static MoveResult CheckEnter(GameState state, MapPosition target, PlayerId? viewer = null)
    {
        if (!state.Map.Contains(target))
        {
            return MoveResult.OutOfBounds;
        }

        if (!state.Map.GetTerrain(target).IsPassableOnFoot())
        {
            return MoveResult.Impassable;
        }

        if (state.ObjectCellAt(target) is { Cell: FootprintCell.Blocked })
        {
            return MoveResult.Blocked;
        }

        return state.HeroAt(target) is { } hero && (viewer is not { } player || state.CanSee(player, hero))
            ? MoveResult.Occupied
            : MoveResult.Moved;
    }
}
