using EW.CoreLib.Interfaces;
using EW.CoreLib.Types;

namespace EW.CoreLib.Map;

public static class MapExtensions
{
    public static bool ValidatePlacement(this IWorldMapObject obj, int height, int width) =>
        obj.Position.Vertical + obj.Height <= height 
        && obj.Position.Horizontal + obj.Width <= width;
    
    public static void BakeBlockedCells(this MapCell[,] cells, IWorldMapObject obj)
    {
        for (uint x = 0; x < obj.Height; x++)
        for (uint y = 0; y < obj.Width; y++)
        {
            if (obj.Size[x, y] == 1)
                cells[obj.Position.Vertical + x, obj.Position.Horizontal + y].Blocked = true;
        }
    }  

    public static void BakeOccupiedCells(this MapCell[,] cells, IRemovableMapObject obj)
    {
        for (uint x = 0; x < obj.Height; x++)
        for (uint y = 0; y < obj.Width; y++)
        {
            if (obj.Size[x, y] == 1)
                cells[obj.Position.Vertical + x, obj.Position.Horizontal + y].Occupied = true;
        }
    }

    public static void BakeOccupiedCells(this MapCell[,] cells, IMapActor obj)
    {
        for (uint x = 0; x < obj.Height; x++)
        for (uint y = 0; y < obj.Width; y++)
        {
            if (obj.Size[x, y] == 1)
                cells[obj.Position.Vertical + x, obj.Position.Horizontal + y].Occupied = true;
        }
    }

    public static void UnOccupyCells(this MapCell[,] cells, IWorldMapObject obj, Position position)
    {
        for (uint x = 0; x < obj.Height; x++)
        for (uint y = 0; y < obj.Width; y++)
        {
            if (obj.Size[x, y] == 1)
                cells[position.Vertical + x, position.Horizontal + y].Occupied = false;
        }
    }
    
    public static void UnblockSector(
        this MapCell[,] cells, Position start, Position end)
    {
        for (uint x = start.Vertical; x < end.Vertical; x++)
        for (uint y = start.Horizontal; y < end.Horizontal; y++)
        {
            cells[x, y].Blocked = false;
        }
    }    
    
    public static void UnOccupySector(
        this MapCell[,] cells, Position start, Position end)
    {
        for (uint x = start.Vertical; x < end.Vertical; x++)
        for (uint y = start.Horizontal; y < end.Horizontal; y++)
        {
            cells[x, y].Occupied = false;
        }
    }
    
    public static IEnumerable<IWorldMapObject> ObjectsInSector(
        this Dictionary<string, IWorldMapObject> objects, Position start, Position end)
    {
        return objects.Values.Where(x =>
            x.Position.Vertical >= start.Vertical
            && x.Position.Vertical < end.Vertical
            && x.Position.Horizontal >= start.Horizontal
            && x.Position.Horizontal < end.Horizontal);
    }    
    
    public static IEnumerable<IRemovableMapObject> RemovableObjectsInSector(
        this Dictionary<string, IRemovableMapObject> objects, Position start, Position end)
    {
        return objects.Values.Where(x =>
            x.Position.Vertical >= start.Vertical
            && x.Position.Vertical < end.Vertical
            && x.Position.Horizontal >= start.Horizontal
            && x.Position.Horizontal < end.Horizontal);
    }
    
    public static bool OccupiesPosition(this IWorldMapObject mapObject, Position position)
    {
        if (position.Vertical < mapObject.Position.Vertical 
            || position.Horizontal < mapObject.Position.Horizontal)
        {
            return false;
        }

        var verticalOffset = position.Vertical - mapObject.Position.Vertical;
        var horizontalOffset = position.Horizontal - mapObject.Position.Horizontal;

        if (verticalOffset >= mapObject.Size.GetLength(0) 
            || horizontalOffset >= mapObject.Size.GetLength(1))
        {
            return false;
        }

        return mapObject.Size[verticalOffset, horizontalOffset] == 1;
    }

    public static List<AStarNode2D> GetPath(this AStarNode2D endNode)
    {
        List<AStarNode2D> path = new(16);
        AStarNode2D currentNode = endNode;

        while (path.Count <= 500)
        {
            path.Add(currentNode);
            
            if (currentNode.Parent == null) break;
            
            currentNode = currentNode.Parent!;
        }
        
        path.Reverse();
        return path;   
    }
}
