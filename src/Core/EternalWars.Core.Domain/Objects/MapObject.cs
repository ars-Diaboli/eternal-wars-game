using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;

namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// An object placed on the adventure map. <see cref="Position"/> is the top-left tile of the
/// definition's <see cref="Footprint"/>; heroes interact by stepping onto <see cref="VisitPosition"/>.
/// One subclass per <see cref="MapObjectKind"/> holds the mutable, per-instance state.
/// </summary>
public abstract class MapObject
{
    protected MapObject(MapObjectId id, MapObjectDefinition definition, MapPosition position, MapObjectKind expectedKind)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Kind != expectedKind)
        {
            throw new ArgumentException(
                $"Definition '{definition.Id}' is a {definition.Kind}, expected {expectedKind}.", nameof(definition));
        }

        Id = id;
        Definition = definition;
        Position = position;
    }

    public MapObjectId Id { get; }

    public MapObjectDefinition Definition { get; }

    public MapObjectKind Kind => Definition.Kind;

    public MapPosition Position { get; }

    public MapPosition VisitPosition => Position.Offset(Definition.Footprint.VisitOffset.Dx, Definition.Footprint.VisitOffset.Dy);

    public IEnumerable<(MapPosition Position, FootprintCell Cell)> OccupiedCells() =>
        Definition.Footprint.OccupiedCells.Select(c => (Position.Offset(c.Dx, c.Dy), c.Cell));

    /// <summary>
    /// Whether stepping onto the entrance ends a route (ADR-013: visiting is mandatory). Objects whose visit
    /// does nothing (one-way gate exits) can be walked through.
    /// </summary>
    public virtual bool EndsRoute => true;

    /// <summary>The object whose entrance a visit teleports the hero to, if this is a usable gate.</summary>
    public virtual MapObjectId? TeleportTarget => null;

    public override string ToString() => $"{Definition.Name} {Id} at {Position}";

    /// <summary>Creates an instance of the right subclass with default per-instance values.</summary>
    public static MapObject Create(MapObjectId id, MapObjectDefinition definition, MapPosition position) => definition.Kind switch
    {
        MapObjectKind.ResourcePile => new ResourcePile(id, definition, position, ResourcePile.DefaultAmount(definition.Resource!.Value)),
        MapObjectKind.Mine => new Mine(id, definition, position),
        MapObjectKind.TreasureChest => new TreasureChest(id, definition, position),
        MapObjectKind.TwoWayGate => new TwoWayGate(id, definition, position),
        MapObjectKind.OneWayGateEntrance => new OneWayGateEntrance(id, definition, position),
        MapObjectKind.OneWayGateExit => new OneWayGateExit(id, definition, position),
        MapObjectKind.Monster => new MonsterGuard(id, definition, position),
        _ => throw new ArgumentOutOfRangeException(nameof(definition), definition.Kind, null),
    };
}

/// <summary>Picked up on visit: resources go to the hero's owner and the pile disappears.</summary>
public sealed class ResourcePile(MapObjectId id, MapObjectDefinition definition, MapPosition position, int amount)
    : MapObject(id, definition, position, MapObjectKind.ResourcePile)
{
    public ResourceType Resource => Definition.Resource!.Value;

    public int Amount { get; internal set; } = amount;

    public static int DefaultAmount(ResourceType resource) => resource switch
    {
        ResourceType.Gold => 750,
        ResourceType.Lumber or ResourceType.Ore => 7,
        _ => 4,
    };
}

/// <summary>
/// Flagged by the visiting hero's owner; produces <see cref="DailyYield"/> for its owner every turn.
/// An optional <see cref="Guard"/> must be beaten before the first flagging (ADR-034); afterwards any hero
/// re-flags the mine by visiting it.
/// </summary>
public sealed class Mine(MapObjectId id, MapObjectDefinition definition, MapPosition position, PlayerId? owner = null, GuardStack? guard = null)
    : MapObject(id, definition, position, MapObjectKind.Mine)
{
    public ResourceType Resource => Definition.Resource!.Value;

    public int DailyYield => Definition.DailyYield;

    public PlayerId? Owner { get; internal set; } = owner;

    public GuardStack? Guard { get; internal set; } = guard;
}

/// <summary>A guarding creature stack; a null <see cref="Count"/> is rolled from the tier range at game start.</summary>
public sealed record GuardStack(string Creature, int? Count = null);

/// <summary>Gold or experience a chest offers.</summary>
public sealed record ChestValues(int Gold, int Experience);

/// <summary>
/// On visit the hero chooses between gold and experience; then the chest disappears. Values are rolled
/// HoMM3-style when the chest is opened (<see cref="RollTable"/>) unless the map author fixed them.
/// </summary>
public sealed class TreasureChest(MapObjectId id, MapObjectDefinition definition, MapPosition position, ChestValues? fixedValues = null)
    : MapObject(id, definition, position, MapObjectKind.TreasureChest)
{
    /// <summary>HoMM3 tiers without the artifact outcome, equally likely (ADR-039).</summary>
    public static IReadOnlyList<ChestValues> RollTable { get; } = [new(1000, 500), new(1500, 1000), new(2000, 1500)];

    /// <summary>Values chosen by the map author; null = rolled with the game RNG on opening.</summary>
    public ChestValues? FixedValues { get; internal set; } = fixedValues;
}

/// <summary>How a neutral stack reacts to a hero (HoMM3 dispositions, deterministic thresholds, ADR-040).</summary>
public enum GuardDisposition
{
    Compliant,
    Friendly,
    Aggressive,
    Hostile,
    Savage,
}

/// <summary>Map-wide rule for the area neutral monsters guard (chosen in the editor, ADR-040).</summary>
public enum GuardMode
{
    /// <summary>HoMM3: entering a tile next to a monster starts the fight, but pickups there can be taken from outside.</summary>
    Classic,

    /// <summary>The whole 3×3 area is guarded: any interaction inside it, pickups included, starts the fight.</summary>
    Area,
}

/// <summary>
/// A neutral creature stack on the adventure map. Its tile is the entrance (attack it by stepping on it) and it
/// guards the eight tiles around it (<see cref="GuardMode"/>).
/// </summary>
public sealed class MonsterGuard(
    MapObjectId id,
    MapObjectDefinition definition,
    MapPosition position,
    string creature = MonsterGuard.DefaultCreature,
    int? count = null,
    GuardDisposition disposition = GuardDisposition.Aggressive,
    bool neverFlees = false)
    : MapObject(id, definition, position, MapObjectKind.Monster)
{
    public const string DefaultCreature = Creatures.BuiltInCreatures.Wolf;

    public string Creature { get; internal set; } = creature;

    /// <summary>Null until rolled at game start from the creature's tier range.</summary>
    public int? Count { get; internal set; } = count;

    public GuardDisposition Disposition { get; internal set; } = disposition;

    public bool NeverFlees { get; internal set; } = neverFlees;

    /// <summary>Tiles guarded besides its own (the 8 neighbours on the same level).</summary>
    public IEnumerable<MapPosition> GuardedTiles()
    {
        foreach (var direction in DirectionExtensions.All)
        {
            yield return Position.Step(direction);
        }
    }
}

/// <summary>
/// One end of a linked pair: teleports the visiting hero to the entrance of <see cref="LinkedGate"/>.
/// Subterranean gates (across levels) and two-way monoliths (same level) are definitions of this kind.
/// </summary>
public sealed class TwoWayGate(MapObjectId id, MapObjectDefinition definition, MapPosition position, MapObjectId? linkedGate = null)
    : MapObject(id, definition, position, MapObjectKind.TwoWayGate)
{
    public MapObjectId? LinkedGate { get; internal set; } = linkedGate;

    public override MapObjectId? TeleportTarget => LinkedGate;
}

/// <summary>Teleports the visiting hero to <see cref="Exit"/>; there is no way back through it.</summary>
public sealed class OneWayGateEntrance(MapObjectId id, MapObjectDefinition definition, MapPosition position, MapObjectId? exit = null)
    : MapObject(id, definition, position, MapObjectKind.OneWayGateEntrance)
{
    public MapObjectId? Exit { get; internal set; } = exit;

    public override MapObjectId? TeleportTarget => Exit;
}

/// <summary>Where one-way gates deliver heroes. Visiting it does nothing, so routes may pass over it.</summary>
public sealed class OneWayGateExit(MapObjectId id, MapObjectDefinition definition, MapPosition position)
    : MapObject(id, definition, position, MapObjectKind.OneWayGateExit)
{
    public override bool EndsRoute => false;
}
