namespace EternalWars.Core.Domain.Common;

/// <summary>Eight-way movement on the square adventure grid. Y grows to the south.</summary>
public enum Direction
{
    North,
    NorthEast,
    East,
    SouthEast,
    South,
    SouthWest,
    West,
    NorthWest,
}

public static class DirectionExtensions
{
    public static IReadOnlyList<Direction> All { get; } = Enum.GetValues<Direction>();

    public static (int Dx, int Dy) ToOffset(this Direction direction) => direction switch
    {
        Direction.North => (0, -1),
        Direction.NorthEast => (1, -1),
        Direction.East => (1, 0),
        Direction.SouthEast => (1, 1),
        Direction.South => (0, 1),
        Direction.SouthWest => (-1, 1),
        Direction.West => (-1, 0),
        Direction.NorthWest => (-1, -1),
        _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null),
    };

    /// <summary>Finds the direction of a single-tile step, if (<paramref name="dx"/>, <paramref name="dy"/>) is one.</summary>
    public static Direction? FromOffset(int dx, int dy) =>
        All.Cast<Direction?>().FirstOrDefault(d => d!.Value.ToOffset() == (dx, dy));

    public static bool IsDiagonal(this Direction direction) =>
        direction is Direction.NorthEast or Direction.SouthEast or Direction.SouthWest or Direction.NorthWest;
}
