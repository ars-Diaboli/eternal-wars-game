namespace EW.CoreLib.Types;

public class PlayerColor(uint value) : IEquatable<PlayerColor>
{
    public static readonly PlayerColor Red = new(0xFF0000FFU);
    public static readonly PlayerColor Blue = new(0x0000FFFFU);
    public static readonly PlayerColor Green = new(0x32CD32FFU);
    public static readonly PlayerColor Cyan = new(0x00FFFFU);
    public static readonly PlayerColor Purple = new(0x8A2BE2FFU);
    public static readonly PlayerColor Yellow = new(0xFFD700FFU);
    
    public static readonly PlayerColor Orange = new(0xFF8C00FFU);
    public static readonly PlayerColor Pink = new(0xDDA0DDFFU);
    public static readonly PlayerColor Cornflower = new(0x6495EDFFU);
    public static readonly PlayerColor Teal = new(0x008080FFU);
    public static readonly PlayerColor Beige = new(0xDEB887FFU);
    public static readonly PlayerColor Brown = new(0x8B4513FFU);
    
    public static readonly PlayerColor White = new(0xFFFFFFFFU);
    public static readonly PlayerColor Gray = new(0xBEBEBEFFU);
    public static readonly PlayerColor Black = new(0x000000FFU);

    public static readonly PlayerColor Neutral = Gray;
    
    public static readonly PlayerColor[] PlayableColors =
    [
        Red, Blue, Green,
        Cyan, Purple, Yellow,
        Orange, Pink, Cornflower,
        Teal, Beige, Brown,
    ];
    public static readonly PlayerColor[] EnabledColors =
    [
        .. PlayableColors,
        White,
        Black,
        Neutral
    ];
    
    public uint Value { get; } = value;

    public static bool operator ==(PlayerColor left, PlayerColor right) => left.Equals(right);
  
    public static bool operator !=(PlayerColor left, PlayerColor right) => !left.Equals(right); 
    
    public bool Equals(PlayerColor? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Value == other.Value;
    }

    public override bool Equals(object? obj)
    {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((PlayerColor)obj);
    }

    public override int GetHashCode()
    {
        return (int)Value;
    }
}