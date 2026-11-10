using EternalWars.Core.Domain.Game;
using EternalWars.Core.Domain.Objects;

namespace EternalWars.Core.Domain.Editing;

public enum EditResult
{
    Ok,
    OutOfBounds,

    /// <summary>Overlaps another object or a hero.</summary>
    Overlaps,

    /// <summary>The tile a hero stands on, or an object's entrance, would be impassable terrain.</summary>
    ImpassableTerrain,
    UnknownPlayer,

    /// <summary>The target does not exist or is the wrong kind of object.</summary>
    InvalidTarget,

    /// <summary>A player slot still owns heroes, mines or towns, or it is the last slot.</summary>
    PlayerInUse,

    /// <summary>Already <see cref="GameState.MaxPlayers"/> player slots.</summary>
    TooManyPlayers,

    /// <summary>The creature id is not in the scenario's creature catalog.</summary>
    UnknownCreature,

    /// <summary>A number is out of range (negative stats, empty stacks, bad slot).</summary>
    InvalidValue,

    /// <summary>The line is not one of the town's, or its dwellings would break the 7-of-9 rule (ADR-030).</summary>
    DwellingRule,

    /// <summary>A town building would lack a building it requires, or one that requires it would lose it.</summary>
    RequirementMissing,

    /// <summary>The player would have more heroes than it may (ADR-068).</summary>
    HeroLimit,

    /// <summary>Map dwellings sell base creatures only (ADR-073); the creature is an upgrade.</summary>
    UpgradedCreature,

    /// <summary>A colour already has <see cref="MagiWindow.MaxEyes"/> Eyes of the Magi (ADR-070).</summary>
    TooManyEyes,

    /// <summary>The map already has <see cref="GrailRules.MaxObelisks"/> obelisks (ADR-140).</summary>
    TooManyObelisks,

    /// <summary>The Grail would lie too near the map's edge, or the map is too small for one (ADR-140, <see cref="GrailRules.EdgeDistance"/>).</summary>
    TooNearEdge,
}
