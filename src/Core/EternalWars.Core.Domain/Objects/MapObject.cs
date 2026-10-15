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

    public override string ToString() => $"{Definition.Name} {Id} at {Position}";

    /// <summary>Creates an instance of the right subclass with default per-instance values.</summary>
    public static MapObject Create(MapObjectId id, MapObjectDefinition definition, MapPosition position) => definition.Kind switch
    {
        MapObjectKind.ResourcePile => new ResourcePile(id, definition, position, ResourcePile.DefaultAmount(definition.Resource!.Value)),
        MapObjectKind.Mine => new Mine(id, definition, position),
        MapObjectKind.TreasureChest => new TreasureChest(id, definition, position),
        MapObjectKind.SubterraneanGate => new SubterraneanGate(id, definition, position),
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

/// <summary>Flagged by the visiting hero's owner; produces <see cref="DailyYield"/> for its owner every turn.</summary>
public sealed class Mine(MapObjectId id, MapObjectDefinition definition, MapPosition position, PlayerId? owner = null)
    : MapObject(id, definition, position, MapObjectKind.Mine)
{
    public ResourceType Resource => Definition.Resource!.Value;

    public int DailyYield => Definition.DailyYield;

    public PlayerId? Owner { get; internal set; } = owner;
}

/// <summary>On visit the hero chooses between gold and experience; then the chest disappears.</summary>
public sealed class TreasureChest(
    MapObjectId id,
    MapObjectDefinition definition,
    MapPosition position,
    int gold = TreasureChest.DefaultGold,
    int experience = TreasureChest.DefaultExperience)
    : MapObject(id, definition, position, MapObjectKind.TreasureChest)
{
    public const int DefaultGold = 1000;
    public const int DefaultExperience = 500;

    public int Gold { get; internal set; } = gold;

    public int Experience { get; internal set; } = experience;
}

/// <summary>Teleports the visiting hero to the entrance of <see cref="LinkedGate"/> (usually on the other level).</summary>
public sealed class SubterraneanGate(MapObjectId id, MapObjectDefinition definition, MapPosition position, MapObjectId? linkedGate = null)
    : MapObject(id, definition, position, MapObjectKind.SubterraneanGate)
{
    public MapObjectId? LinkedGate { get; internal set; } = linkedGate;
}
