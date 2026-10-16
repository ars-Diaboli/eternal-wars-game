using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Objects;

public enum MapObjectKind
{
    ResourcePile,
    Mine,
    TreasureChest,

    /// <summary>Linked pair; either end leads to the other (subterranean gate, two-way monolith).</summary>
    TwoWayGate,

    /// <summary>Leads to exactly one <see cref="OneWayGateExit"/>.</summary>
    OneWayGateEntrance,

    /// <summary>Landing spot of one-way gates; visiting it does nothing.</summary>
    OneWayGateExit,

    /// <summary>A neutral creature stack guarding its surroundings (<see cref="MonsterGuard"/>).</summary>
    Monster,

    /// <summary>A town of the type in <see cref="MapObjectDefinition.Town"/> (<see cref="Towns.Town"/>).</summary>
    Town,

    /// <summary>An artifact lying on the map (<see cref="ArtifactPickup"/>, ADR-061).</summary>
    Artifact,

    /// <summary>A dwelling of one creature line on the adventure map (<see cref="CreatureDwelling"/>, ADR-054).</summary>
    Dwelling,
}

/// <summary>
/// Static, data-driven description of a kind of map object ("Gold mine", "Lumber pile").
/// <see cref="Kind"/> selects the behaviour; the other properties parameterise it.
/// Instances on a map are <see cref="MapObject"/>s.
/// </summary>
public sealed record MapObjectDefinition(string Id, string Name, MapObjectKind Kind, Footprint Footprint)
{
    /// <summary>Resource of piles and mines.</summary>
    public ResourceType? Resource { get; init; }

    /// <summary>Daily production of mines.</summary>
    public int DailyYield { get; init; }

    /// <summary>Town type of towns (<see cref="Towns.TownIds"/>).</summary>
    public string? Town { get; init; }

    /// <summary>Tier an artifact pickup rolls its artifact from when the map leaves it open.</summary>
    public Artifacts.ArtifactTier? ArtifactTier { get; init; }

    /// <summary>Throws <see cref="FormatException"/> if kind-specific properties are missing.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new FormatException("Map object definition needs an id.");
        }

        if (Kind is MapObjectKind.Town && string.IsNullOrWhiteSpace(Town))
        {
            throw new FormatException($"Town '{Id}' needs a town type.");
        }

        if (Kind is MapObjectKind.ResourcePile or MapObjectKind.Mine && Resource is null)
        {
            throw new FormatException($"Map object '{Id}' of kind {Kind} needs a resource.");
        }

        if (Kind is MapObjectKind.Artifact && ArtifactTier is null)
        {
            throw new FormatException($"Artifact object '{Id}' needs an artifact tier.");
        }

        if (Kind is MapObjectKind.Mine && DailyYield <= 0)
        {
            throw new FormatException($"Mine '{Id}' needs a positive daily yield.");
        }
    }
}
