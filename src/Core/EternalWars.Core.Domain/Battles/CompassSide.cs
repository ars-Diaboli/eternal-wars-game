namespace EternalWars.Core.Domain.Battles;

/// <summary>One of the eight sides of a cell or footprint on the battlefield (ADR-204); north is the field's top row (y grows south).</summary>
public enum CompassSide
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

/// <summary>Directions on the battlefield's grid as <see cref="CompassSide"/>s.</summary>
public static class CompassSides
{
    /// <summary>The side nearest the direction (<paramref name="dx"/>, <paramref name="dy"/>) in cell units, y growing south; east for no direction.</summary>
    public static CompassSide Nearest(float dx, float dy)
    {
        if (dx == 0 && dy == 0)
        {
            return CompassSide.East;
        }

        // Eighths of a turn from east, clockwise on the screen (y grows south): 0 east, 2 south, 4 west, 6 north.
        var eighth = (int)Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4));
        return (((eighth % 8) + 8) % 8) switch
        {
            0 => CompassSide.East,
            1 => CompassSide.SouthEast,
            2 => CompassSide.South,
            3 => CompassSide.SouthWest,
            4 => CompassSide.West,
            5 => CompassSide.NorthWest,
            6 => CompassSide.North,
            _ => CompassSide.NorthEast,
        };
    }

    /// <summary>The side's direction in radians from east, clockwise on the screen (as <see cref="Math.Atan2"/> of a y-south offset).</summary>
    public static double Angle(CompassSide side) => side switch
    {
        CompassSide.East => 0,
        CompassSide.SouthEast => Math.PI / 4,
        CompassSide.South => Math.PI / 2,
        CompassSide.SouthWest => 3 * Math.PI / 4,
        CompassSide.West => Math.PI,
        CompassSide.NorthWest => -3 * Math.PI / 4,
        CompassSide.North => -Math.PI / 2,
        _ => -Math.PI / 4,
    };
}
