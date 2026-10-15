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

    /// <summary>Throws <see cref="FormatException"/> if kind-specific properties are missing.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new FormatException("Map object definition needs an id.");
        }

        if (Kind is MapObjectKind.ResourcePile or MapObjectKind.Mine && Resource is null)
        {
            throw new FormatException($"Map object '{Id}' of kind {Kind} needs a resource.");
        }

        if (Kind is MapObjectKind.Mine && DailyYield <= 0)
        {
            throw new FormatException($"Mine '{Id}' needs a positive daily yield.");
        }
    }
}
