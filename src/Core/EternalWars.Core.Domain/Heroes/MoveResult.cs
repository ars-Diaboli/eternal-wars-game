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

    /// <summary>An allied hero stands there (no interaction with allies for now).</summary>
    Occupied,

    /// <summary>The hero has no movement points left at all (any positive amount allows one last step).</summary>
    NotEnoughMovementPoints,
    NoPath,

    /// <summary>The world changed under a planned path (e.g. a gate exit got occupied).</summary>
    Interrupted,

    /// <summary>Stepped towards an own hero: an exchange was opened; the hero did not move.</summary>
    Met,

    /// <summary>Stepped towards an enemy hero: a battle was triggered; the hero did not move.</summary>
    BattleStarted,

    /// <summary>Refog mode: the move stopped because a previously unseen enemy hero came into sight.</summary>
    EnemySpotted,

    /// <summary>Stepped onto a monster or into its guarded area: a guard offer or a battle followed; the hero did not move.</summary>
    GuardEncountered,

    /// <summary>Classic guard mode: took a pickup lying in a guarded tile from outside, without entering it.</summary>
    CollectedFromAdjacent,

    /// <summary>A hero without creatures cannot attack.</summary>
    NoArmy,
}

/// <summary>Result of following a path: how far the hero got and why it stopped.</summary>
public sealed record MovementOutcome(int StepsTaken, MoveResult StopReason, bool Arrived);

/// <summary>How a hero relates to another hero it runs into.</summary>
public enum HeroEncounter
{
    Own,
    Ally,
    Enemy,
}

/// <summary>Single source of truth for movement rules shared by movement and pathfinding.</summary>
internal static class MovementRules
{
    /// <summary>Bounds, terrain and objects (heroes are handled separately as encounters).</summary>
    public static MoveResult CheckTile(GameState state, MapPosition target)
    {
        if (!state.Map.Contains(target))
        {
            return MoveResult.OutOfBounds;
        }

        if (!state.Terrains.IsPassable(state.Map.GetTerrain(target)))
        {
            return MoveResult.Impassable;
        }

        return state.ObjectCellAt(target) is { Cell: FootprintCell.Blocked } ? MoveResult.Blocked : MoveResult.Moved;
    }

    public static HeroEncounter Classify(GameState state, PlayerId mover, Hero other) =>
        other.Owner == mover ? HeroEncounter.Own
        : state.AreAllies(mover, other.Owner) ? HeroEncounter.Ally
        : HeroEncounter.Enemy;

    /// <summary>
    /// HoMM3 rules: a road → road step costs the road of the tile being left; otherwise the terrain being
    /// left (native rules from <see cref="GameState.Terrains"/>, the penalty above 100 cut by Pathfinding); diagonal ×1.41.
    /// </summary>
    public static int StepCost(GameState state, Hero hero, MapPosition from, MapPosition to, Direction direction)
    {
        var map = state.Map;
        var fromRoad = map.GetRoad(from);
        var baseCost = fromRoad != RoadType.None && map.GetRoad(to) != RoadType.None
            ? fromRoad.MovementCost()
            : TerrainCost(state, hero, map.GetTerrain(from));
        return direction.IsDiagonal() ? (int)Math.Round(baseCost * HeroMovementService.DiagonalMultiplier) : baseCost;
    }

    private static int TerrainCost(GameState state, Hero hero, TerrainType terrain)
    {
        var cost = state.Terrains.MovementCost(terrain, ArmyTown(state, hero));
        var penalty = cost - TerrainCatalog.NativeCost;
        var reduction = SkillEffects.PenaltyReductionPercent(hero.Skills.Level(BuiltInSkills.Pathfinding));
        return penalty <= 0 ? cost : TerrainCatalog.NativeCost + (penalty * (100 - reduction) / 100);
    }

    /// <summary>
    /// The town every creature of the hero's army belongs to, or null (mixed, neutral or no army): native terrain
    /// only waives its penalty for an all-native army (ADR-032).
    /// </summary>
    public static string? ArmyTown(GameState state, Hero hero)
    {
        var towns = hero.Army.Stacks.Select(s => state.Creatures.Get(s.Stack.Creature).Town).Distinct().ToList();
        return towns is [{ } town] ? town : null;
    }
}
