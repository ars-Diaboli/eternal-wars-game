namespace EternalWars.Core.Domain.Common;

/// <summary>
/// A tile on the adventure map. <see cref="Level"/> 0 is the surface, 1 the underground.
/// </summary>
public readonly record struct MapPosition(int X, int Y, int Level = 0)
{
    public MapPosition Step(Direction direction)
    {
        var (dx, dy) = direction.ToOffset();
        return Offset(dx, dy);
    }

    public MapPosition Offset(int dx, int dy) => this with { X = X + dx, Y = Y + dy };

    public override string ToString() => $"({X},{Y},L{Level})";
}
