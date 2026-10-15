using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Heroes;

public sealed class Hero(HeroId id, string name, PlayerId owner, MapPosition position)
{
    public const int DefaultMaxMovementPoints = 1500;

    public HeroId Id { get; } = id;

    public string Name { get; } = name;

    public PlayerId Owner { get; internal set; } = owner;

    public MapPosition Position { get; internal set; } = position;

    public int MaxMovementPoints { get; internal set; } = DefaultMaxMovementPoints;

    public int MovementPoints { get; internal set; } = DefaultMaxMovementPoints;
}
