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

    /// <summary>A marketplace on the adventure map (<see cref="Marketplace"/>, ADR-067): a visiting hero opens trading at a fixed rate.</summary>
    Marketplace,

    /// <summary>A building that hands out resources once a week (<see cref="ResourceProvider"/>, ADR-068): Water Wheel, Warehouses, Windmill, Mystical Garden.</summary>
    ResourceProvider,

    /// <summary>One-time loot (<see cref="LootSite"/>, ADR-068): Campfire, Supply Crates, Traveler's Wagon, Corpse, Lean To, Warrior's Tomb.</summary>
    Loot,

    /// <summary>A temporary buff (<see cref="BuffSite"/>, ADR-068): Faerie Ring, Fountain of Youth, Temple, Mineral Spring, Stables, Magic Well.</summary>
    Buff,

    /// <summary>A permanent power-up (<see cref="TrainingSite"/>, ADR-068): Learning Stone, Gazebo, Library, Tree of Knowledge, Arena, Colosseum.</summary>
    Training,
}

/// <summary>
/// Static, data-driven description of a kind of map object ("Gold mine", "Lumber pile").
/// <see cref="Kind"/> selects the behaviour; the other properties parameterise it.
/// Instances on a map are <see cref="MapObject"/>s.
/// </summary>
public sealed record MapObjectDefinition(string Id, string Name, MapObjectKind Kind, Footprint Footprint)
{
    /// <summary>Resource of piles, mines and warehouses.</summary>
    public ResourceType? Resource { get; init; }

    /// <summary>Daily production of mines.</summary>
    public int DailyYield { get; init; }

    /// <summary>Town type of towns (<see cref="Towns.TownIds"/>).</summary>
    public string? Town { get; init; }

    /// <summary>Tier an artifact pickup rolls its artifact from when the map leaves it open.</summary>
    public Artifacts.ArtifactTier? ArtifactTier { get; init; }

    /// <summary>How often a visit gives the object's reward (ADR-068); resource providers are <see cref="VisitScope.Weekly"/>.</summary>
    public VisitScope VisitScope { get; init; }

    /// <summary>What a resource provider gives (ADR-068).</summary>
    public ProviderRule? Provider { get; init; }

    /// <summary>How much of <see cref="Resource"/> a warehouse gives.</summary>
    public int Amount { get; init; }

    /// <summary>What a loot site gives (ADR-068).</summary>
    public LootRule? Loot { get; init; }

    /// <summary>An abandoned mine's normal mine (definition id): what it turns into once its guard is beaten (ADR-068).</summary>
    public string? Becomes { get; init; }

    /// <summary>The creature that guards every instance unless the map sets a guard (abandoned mines: Troglodytes).</summary>
    public string? GuardCreature { get; init; }

    /// <summary>The range the guard count is rolled from when the game starts.</summary>
    public int GuardMin { get; init; }

    public int GuardMax { get; init; }

    /// <summary>Fights at this object take place on this terrain, whatever the map's (abandoned gold and ore mines: subterranean).</summary>
    public Map.TerrainType? BattleTerrain { get; init; }

    /// <summary>What a buff site gives (ADR-068).</summary>
    public BuffRule? Buff { get; init; }

    /// <summary>What a training site gives (ADR-068).</summary>
    public TrainingRule? Training { get; init; }

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

        if (Kind is MapObjectKind.ResourceProvider && Provider is null)
        {
            throw new FormatException($"Resource provider '{Id}' needs a provider rule.");
        }

        if (Kind is MapObjectKind.Loot && Loot is null)
        {
            throw new FormatException($"Loot site '{Id}' needs a loot rule.");
        }

        if ((Kind is MapObjectKind.Buff && Buff is null) || (Kind is MapObjectKind.Training && Training is null))
        {
            throw new FormatException($"Map object '{Id}' of kind {Kind} needs its rule.");
        }

        if (Provider is ProviderRule.Warehouse && (Resource is null || Amount <= 0))
        {
            throw new FormatException($"Warehouse '{Id}' needs a resource and a positive amount.");
        }

        if (Becomes is not null && Kind is not MapObjectKind.Mine)
        {
            throw new FormatException($"Only a mine can turn into another ('{Id}').");
        }

        if (GuardCreature is not null && (GuardMin < 1 || GuardMax < GuardMin))
        {
            throw new FormatException($"'{Id}' needs a guard range from 1 up.");
        }

        if (Kind is MapObjectKind.Mine && DailyYield <= 0)
        {
            throw new FormatException($"Mine '{Id}' needs a positive daily yield.");
        }
    }
}
