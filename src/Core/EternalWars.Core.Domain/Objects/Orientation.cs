namespace EternalWars.Core.Domain.Objects;

/// <summary>
/// How a piece of scenery lies on the grid (EDIT-6, ADR-252): mirrored left to right first, then turned clockwise by
/// <see cref="Turns"/> quarter turns (0 to 3). The default is the piece as its definition draws it. Game objects have no orientation:
/// their entrance always faces the same way.
/// </summary>
public readonly record struct Orientation
{
    public Orientation(int turns, bool mirrored = false)
    {
        if (turns is < 0 or > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(turns), turns, "Scenery turns by 0 to 3 quarter turns.");
        }

        (Turns, Mirrored) = (turns, mirrored);
    }

    /// <summary>Quarter turns clockwise, 0 to 3, made after the mirroring.</summary>
    public int Turns { get; }

    /// <summary>Whether the piece is mirrored left to right before it is turned.</summary>
    public bool Mirrored { get; }

    public bool IsNone => Turns == 0 && !Mirrored;

    /// <summary>A quarter turn clockwise further.</summary>
    public Orientation Rotated() => new((Turns + 1) % 4, Mirrored);

    /// <summary>The piece as it lies now, mirrored left to right.</summary>
    public Orientation Flipped() => new((4 - Turns) % 4, !Mirrored);

    public override string ToString() => IsNone ? "as drawn" : $"{Turns * 90}°{(Mirrored ? ", mirrored" : "")}";
}
