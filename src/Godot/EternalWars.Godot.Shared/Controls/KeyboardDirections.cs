using EternalWars.Core.Domain.Common;
using Godot;

namespace EternalWars.Godot.Shared.Controls;

/// <summary>Arrow keys and numpad (HoMM-style 8-way) to map directions.</summary>
public static class KeyboardDirections
{
    public static Direction? ToDirection(Key key) => key switch
    {
        Key.Up or Key.Kp8 => Direction.North,
        Key.Kp9 => Direction.NorthEast,
        Key.Right or Key.Kp6 => Direction.East,
        Key.Kp3 => Direction.SouthEast,
        Key.Down or Key.Kp2 => Direction.South,
        Key.Kp1 => Direction.SouthWest,
        Key.Left or Key.Kp4 => Direction.West,
        Key.Kp7 => Direction.NorthWest,
        _ => null,
    };
}
