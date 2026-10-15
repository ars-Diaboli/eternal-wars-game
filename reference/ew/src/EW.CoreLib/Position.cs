using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json.Serialization;

namespace EW.CoreLib.Types;

[method: JsonConstructor]
public struct Position(uint vertical, uint horizontal) : IEquatable<Position>
{
    public uint Vertical { get; private set; } = vertical;
    public uint Horizontal { get; private set; } = horizontal;

    public Position(long vertical, long horizontal) : this((uint)vertical, (uint)horizontal) { }

    public static readonly Position Zero = new(0, 0);
    // private static readonly Position _one = new Position(1, 1);
    // private static readonly Position _down = new Position(0, 1);
    // private static readonly Position _right = new Position(1, 0);
    
    public readonly void Deconstruct(out uint vertical, out uint horizontal)
    {
        vertical = this.Vertical;
        horizontal = this.Horizontal;
    }
    
    public static Position operator +(Position left, Position right)
  {
    left.Vertical += right.Vertical;
    left.Horizontal += right.Horizontal;
    return left;
  }
    
  public static Position operator -(Position left, Position right)
  {
    left.Vertical -= right.Vertical;
    left.Horizontal -= right.Horizontal;
    return left;
  }
  
  public static Position operator *(Position vec, uint scale)
  {
    vec.Vertical *= scale;
    vec.Horizontal *= scale;
    return vec;
  }
  
  public static Position operator *(uint scale, Position vec)
  {
    vec.Vertical *= scale;
    vec.Horizontal *= scale;
    return vec;
  }
  
  public static Position operator *(Position left, Position right)
  {
    left.Vertical *= right.Vertical;
    left.Horizontal *= right.Horizontal;
    return left;
  }
  
  public static Position operator /(Position vec, uint divisor)
  {
    vec.Vertical /= divisor;
    vec.Horizontal /= divisor;
    return vec;
  }

  public static Position operator /(Position vec, Position divisorv)
  {
    vec.Vertical /= divisorv.Vertical;
    vec.Horizontal /= divisorv.Horizontal;
    return vec;
  }
  
  public static bool operator ==(Position left, Position right) => left.Equals(right);
  
  public static bool operator !=(Position left, Position right) => !left.Equals(right);
  
  public static bool operator <(Position left, Position right)
  {
    return left.Vertical == right.Vertical ? left.Horizontal < right.Horizontal : left.Vertical < right.Vertical;
  }
  
  public static bool operator >(Position left, Position right)
  {
    return left.Vertical == right.Vertical ? left.Horizontal > right.Horizontal : left.Vertical > right.Vertical;
  }
  
  public static bool operator <=(Position left, Position right)
  {
    return left.Vertical == right.Vertical ? left.Horizontal <= right.Horizontal : left.Vertical < right.Vertical;
  }
  
  public static bool operator >=(Position left, Position right)
  {
    return left.Vertical == right.Vertical ? left.Horizontal >= right.Horizontal : left.Vertical > right.Vertical;
  }
  
  public readonly override bool Equals([NotNullWhen(true)] object? obj)
  {
    return obj is Position other && Equals(other);
  }
  
  public readonly bool Equals(Position other) => Vertical == other.Vertical && Horizontal == other.Horizontal;
  
  public readonly override int GetHashCode() => HashCode.Combine<uint, uint>(Vertical, Horizontal);
  
  public readonly override string ToString() => this.ToString((string) null);
  
  public readonly string ToString(string? format)
  {
    return $"({Vertical.ToString(format, (IFormatProvider) CultureInfo.InvariantCulture)}, {Horizontal.ToString(format, (IFormatProvider) CultureInfo.InvariantCulture)})";
  }

  public readonly uint DistanceSquaredTo(Position to) => (to - this).LengthSquared();
  
  public readonly float DistanceTo(Position to) => (to - this).Length();
  
  public readonly float Length() => (float)Math.Sqrt((float) (Vertical * Vertical + Horizontal * Horizontal));
  
  public readonly uint LengthSquared() => Vertical * Vertical + Horizontal * Horizontal;
}