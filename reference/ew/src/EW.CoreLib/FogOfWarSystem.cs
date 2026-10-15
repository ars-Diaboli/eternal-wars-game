namespace EW.CoreLib;

public class FogOfWarSystem
{
    public Dictionary<string, bool> _objectVisibility { get; } = new();
    public byte[,] _mapVisibility { get; init; }
}