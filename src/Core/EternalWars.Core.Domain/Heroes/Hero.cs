using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Heroes;

/// <summary>Secondary skill levels (HoMM3: Basic / Advanced / Expert).</summary>
public enum SkillLevel
{
    None,
    Basic,
    Advanced,
    Expert,
}

public sealed class Hero(
    HeroId id,
    string name,
    PlayerId owner,
    MapPosition position,
    Army? army = null,
    int attack = 0,
    int defense = 0,
    SkillLevel tactics = SkillLevel.None)
{
    public const int DefaultScoutingRadius = 5;

    public HeroId Id { get; } = id;

    public string Name { get; internal set; } = name;

    public PlayerId Owner { get; internal set; } = owner;

    public MapPosition Position { get; internal set; } = position;

    /// <summary>Up to seven creature stacks. Speed (<see cref="Creatures.CreatureDefinition.MapSpeed"/>) drives movement.</summary>
    public Army Army { get; } = army ?? new Army();

    /// <summary>Primary stat added to every stack's Attack in battle. Levels and stat growth arrive in Phase 3.</summary>
    public int Attack { get; internal set; } = attack;

    /// <summary>Primary stat added to every stack's Defense in battle.</summary>
    public int Defense { get; internal set; } = defense;

    /// <summary>Tactics skill: lets the hero pick the battlefield width (ADR-033).</summary>
    public SkillLevel Tactics { get; internal set; } = tactics;

    /// <summary>Today's allowance, recomputed at the start of the owner's turn (<see cref="MovementAllowance"/>).</summary>
    public int MaxMovementPoints { get; internal set; } = MovementAllowance.NoArmyBase;

    public int MovementPoints { get; internal set; } = MovementAllowance.NoArmyBase;

    /// <summary>Percentage movement bonus from skills (e.g. Logistics +10/20/30).</summary>
    public int MovementBonusPercent { get; internal set; }

    /// <summary>Flat movement bonus from artifacts.</summary>
    public int MovementBonusFlat { get; internal set; }

    /// <summary>Tiles revealed around the hero, in tiles (rounded circle).</summary>
    public int ScoutingRadius { get; internal set; } = DefaultScoutingRadius;

    /// <summary>Accumulated experience. Levels arrive in Phase 3.</summary>
    public int Experience { get; internal set; }
}
