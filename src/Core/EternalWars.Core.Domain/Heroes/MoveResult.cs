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

    /// <summary>Took a pickup lying in a tile guarded only by Classic-mode stacks from outside, without entering it.</summary>
    CollectedFromAdjacent,

    /// <summary>A hero without creatures cannot attack.</summary>
    NoArmy,

    /// <summary>Stepped towards a Quest Guard, or a Quest Gate it may not pass: the quest was shown or offered; the hero did not move (EVT-5).</summary>
    QuestVisited,
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
    /// left (native rules from <see cref="GameState.Terrains"/>, the penalty above 100 cut by Pathfinding), +25 % when that tile has a river
    /// (ADR-079, MAP-26; Pathfinding does not cut it); diagonal ×1.41.
    /// </summary>
    public static int StepCost(GameState state, Hero hero, MapPosition from, MapPosition to, Direction direction) =>
        StepCost(state, hero, from, to, direction, terrainCosts: null);

    /// <summary>
    /// <see cref="StepCost(GameState, Hero, MapPosition, MapPosition, Direction)"/> with the hero's terrain costs remembered in
    /// <paramref name="terrainCosts"/> (indexed by <see cref="TerrainType"/>, -1 = not worked out yet; <see cref="TerrainTypeCount"/> long). Valid
    /// while nothing about the hero changes, as during one path search.
    /// </summary>
    public static int StepCost(GameState state, Hero hero, MapPosition from, MapPosition to, Direction direction, int[]? terrainCosts)
    {
        var map = state.Map;
        var fromRoad = map.GetRoad(from);
        var baseCost = fromRoad != RoadType.None && map.GetRoad(to) != RoadType.None
            ? fromRoad.MovementCost()
            : map.GetRiver(from) != RiverType.None
                ? RiverRules.WithPenalty(TerrainCost(state, hero, map.GetTerrain(from), terrainCosts))
                : TerrainCost(state, hero, map.GetTerrain(from), terrainCosts);
        return direction.IsDiagonal() ? (int)Math.Round(baseCost * HeroMovementService.DiagonalMultiplier) : baseCost;
    }

    /// <summary>The length of a terrain cost table for <see cref="StepCost(GameState, Hero, MapPosition, MapPosition, Direction, int[])"/>.</summary>
    public static readonly int TerrainTypeCount = Enum.GetValues<TerrainType>().Max(t => (int)t) + 1;

    private static int TerrainCost(GameState state, Hero hero, TerrainType terrain, int[]? remembered)
    {
        if (remembered is null)
        {
            return TerrainCost(state, hero, terrain);
        }

        ref var cost = ref remembered[(int)terrain];
        if (cost < 0)
        {
            cost = TerrainCost(state, hero, terrain);
        }

        return cost;
    }

    private static int TerrainCost(GameState state, Hero hero, TerrainType terrain)
    {
        if (hero.Bonus.NativeTerrains.Contains(terrain) || Magic.AdventureMagic.MakesNative(hero, terrain))
        {
            return TerrainCatalog.NativeCost; // an artifact (ADR-061) or Pathfinder's Call / Ashwalk (ADR-120) makes the army at home here
        }

        var cost = state.Terrains.MovementCost(terrain, ArmyTown(state, hero));
        var penalty = cost - TerrainCatalog.NativeCost;
        var reduction = Math.Min(100, SkillEffects.PenaltyReductionPercent(hero.SkillLevelOf(BuiltInSkills.Pathfinding)) + hero.Bonus.TerrainPenaltyReductionPercent);
        return penalty <= 0 ? cost : TerrainCatalog.NativeCost + (penalty * (100 - reduction) / 100);
    }

    /// <summary>
    /// The town every creature of the hero's army belongs to, or null (mixed, neutral or no army): native terrain
    /// only waives its penalty for an all-native army (ADR-032).
    /// </summary>
    public static string? ArmyTown(GameState state, Hero hero)
    {
        string? town = null;
        var any = false;
        foreach (var (_, stack) in hero.Army.Stacks)
        {
            var own = state.Creatures.Get(stack.Creature).Town;
            if (!any)
            {
                (town, any) = (own, true);
            }
            else if (own != town)
            {
                return null;
            }
        }

        return town;
    }
}
