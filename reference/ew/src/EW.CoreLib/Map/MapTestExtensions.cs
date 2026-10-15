using EW.CoreLib.Types;

namespace EW.CoreLib.Tests;

public static class MapTestExtensions
{
    public static TerrainSurface[,] FillLayer(this MapSize size, TerrainSurface surface)
    {
        var layer = new TerrainSurface[size.Size.Vertical, size.Size.Horizontal];
        
        for (uint x = 0; x < size.Size.Vertical; x++)
        for (uint y = 0; y < size.Size.Horizontal; y++)
        {
            layer[x, y] = surface;
        }
        
        return layer;
    }

}