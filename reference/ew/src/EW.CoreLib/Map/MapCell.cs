namespace EW.CoreLib.Types;

public class MapCell
{
    public TerrainSurface Surface { get; init; }
    public RoadType Road { get; init; }
    public bool Blocked { get; set; }
    public bool Occupied { get; set; }
}