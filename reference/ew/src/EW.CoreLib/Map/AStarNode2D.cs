using EW.CoreLib.Types;

namespace EW.CoreLib.Map;

public class AStarNode2D(Position id, MapCell mapCell)
{
    public Position Id { get; private set; } = id;
    public TerrainSurface Surface { get; set; } = mapCell.Surface;
    
    /// <summary>
    /// Used to disable nodes that are not walkable
    /// </summary>
    public bool Blocked { get; set; } = mapCell.Blocked;
    /// <summary>
    /// Used to disable nodes that are temporarily Occupied
    /// </summary>
    public bool Occupied
    {
        get => mapCell.Occupied;
        set => mapCell.Occupied = value;
    }

    // Execution markers used during path calculation loops
    public float GCost { get; set; } = float.MaxValue;
    public float HCost { get; set; } = 0f;
    public float FCost => GCost + HCost;
    public AStarNode2D? Parent { get; set; }

    public void Reset()
    {
        GCost = float.MaxValue;
        HCost = 0f;
        Parent = null;
    }
}
