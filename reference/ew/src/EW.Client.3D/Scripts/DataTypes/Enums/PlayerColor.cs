using Godot;

namespace EW.Scripts.DataTypes.Enums;

public class PlayerColor(uint rgbaValue)
{
    public static PlayerColor Red = new(0xFF0000FFU);
    public static PlayerColor Blue = new(0x0000FFFFU);
    public static PlayerColor Green = new(0x32CD32FFU);
    public static PlayerColor Cyan = new(0x00FFFFU);
    public static PlayerColor Purple = new(0x8A2BE2FFU);
    public static PlayerColor Yellow = new(0xFFD700FFU);
    
    public static PlayerColor Orange = new(0xFF8C00FFU);
    public static PlayerColor Pink = new(0xDDA0DDFFU);
    public static PlayerColor Cornflower = new(0x6495EDFFU);
    public static PlayerColor Teal = new(0x008080FFU);
    public static PlayerColor Beige = new(0xDEB887FFU);
    public static PlayerColor Brown = new(0x8B4513FFU);
    
    public static PlayerColor White = new(0xFFFFFFFFU);
    public static PlayerColor Gray = new(0xBEBEBEFFU);
    public static PlayerColor Black = new(0x000000FFU);
    
    public static PlayerColor[] PlayableColors =
    [
        Red, Blue, Green,
        Cyan, Purple, Yellow,
        Orange, Pink, Cornflower,
        Teal, Beige, Brown,
    ];

    public uint Value { get; init; } = rgbaValue;
}