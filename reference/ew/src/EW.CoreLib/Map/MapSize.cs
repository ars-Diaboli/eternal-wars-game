namespace EW.CoreLib.Types;

public class MapSize(uint height, uint width)
{
    public Position Size { get; } = new(height, width);

    public static MapSize Test10 = new(10, 10);
    public static MapSize Test16 = new(16, 16);
    public static MapSize _64x64 = new(64, 64);
    public static MapSize _96x96 = new(96, 96);
    public static MapSize _128x128 = new(128, 128);
    public static MapSize _160x160 = new(160, 160);
    public static MapSize _192x192 = new(192, 192);
    public static MapSize _256x256 = new(256, 256);

    public static MapSize[] List = [_64x64, _96x96, _128x128, _160x160, _192x192, _256x256];
}