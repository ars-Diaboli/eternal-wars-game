using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Heroes;

public sealed class Hero(HeroId id, string name, PlayerId owner, MapPosition position)
{
    public const int DefaultScoutingRadius = 5;

    public HeroId Id { get; } = id;

    public string Name { get; internal set; } = name;

    public PlayerId Owner { get; internal set; } = owner;

    public MapPosition Position { get; internal set; } = position;

    /// <summary>Today's allowance, recomputed at the start of the owner's turn (<see cref="MovementAllowance"/>).</summary>
    public int MaxMovementPoints { get; internal set; } = MovementAllowance.NoArmyBase;

    public int MovementPoints { get; internal set; } = MovementAllowance.NoArmyBase;

    /// <summary>
    /// Speed of the slowest creature in the army; null without an army.
    /// Placeholder until Phase 2 armies exist, then derived from the army instead of stored.
    /// </summary>
    public int? SlowestCreatureSpeed { get; internal set; }

    /// <summary>Percentage movement bonus from skills (e.g. Logistics +10/20/30).</summary>
    public int MovementBonusPercent { get; internal set; }

    /// <summary>Flat movement bonus from artifacts.</summary>
    public int MovementBonusFlat { get; internal set; }

    /// <summary>Tiles revealed around the hero, in tiles (rounded circle).</summary>
    public int ScoutingRadius { get; internal set; } = DefaultScoutingRadius;

    /// <summary>Accumulated experience. Levels arrive in Phase 3.</summary>
    public int Experience { get; internal set; }
}
