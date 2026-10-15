using EW.CoreLib.Interfaces;
using EW.CoreLib.Types;

namespace EW.CoreLib.Map;

public class AStarGrid2D
{
    public const float BaseMovementCost = 100f;
    
    private readonly Dictionary<Position, AStarNode2D> _nodes = new();
    private readonly Dictionary<Position, List<Position>> _connections = new();

    public void Init(MapCell[,] cells)
    {
        _nodes.Clear();
        _connections.Clear();

        for (var x = 0; x < cells.GetLength(0); x++)
        for (var y = 0; y < cells.GetLength(1); y++)
        {
            if (cells[x, y].Blocked) continue;
            
            var currentId = new Position(x, y);
            var node = new AStarNode2D(currentId, cells[x, y]);
            
            _nodes[node.Id] = node;
            _connections[node.Id] = GetConnections(cells, x, y);
        }
    }

    private static List<Position> GetConnections(MapCell[,] cells, int x, int y)
    {
        List<Position> connections = [];
            
        var height = cells.GetLength(0);
        var width = cells.GetLength(1);
            
        if (x - 1 >= 0 && !cells[x - 1, y].Blocked) 
            connections.Add(new Position(x - 1, y));
        if (y - 1 >= 0 && !cells[x, y - 1].Blocked) 
            connections.Add(new Position(x, y - 1));
            
        if (x - 1 >= 0 && y - 1 >= 0 && !cells[x - 1, y - 1].Blocked) 
            connections.Add(new Position(x - 1, y - 1));
            
        if (x + 1 < height && !cells[x + 1, y].Blocked) 
            connections.Add(new Position(x + 1, y));
        if (y + 1 < width && !cells[x, y + 1].Blocked) 
            connections.Add(new Position(x, y + 1));

        if (x + 1 < height && y + 1 < width && !cells[x + 1, y + 1].Blocked) 
            connections.Add(new Position(x + 1, y + 1));

        if (x - 1 >= 0 && y + 1 < width && !cells[x - 1, y + 1].Blocked) 
            connections.Add(new Position(x - 1, y + 1));
        if (x + 1 < height && y - 1 >= 0 && !cells[x + 1, y - 1].Blocked) 
            connections.Add(new Position(x + 1, y - 1));
        return connections;
    }

    // todo remove
    public void AddPoint(AStarNode2D node)
    {
        if (_nodes.TryGetValue(node.Id, out var existingNode))
        {
            existingNode.Surface = node.Surface;
        }
        else
        {
            _nodes[node.Id] = node;
            _connections[node.Id] = new List<Position>();
        }
    }
    
    // todo remove
    public void RemovePoint(Position id)
    {
        if (!_nodes.Remove(id)) return;

        _connections.Remove(id);

        foreach (var connectionList in _connections.Values)
        {
            connectionList.Remove(id);
        }
    }

    // // todo remove
    // public void ConnectPoints(Position idA, Position idB)
    // {
    //     if (_connections.TryGetValue(idA, out var value) && !value.Contains(idB))
    //         value.Add(idB);
    // }

    /// <summary>
    /// Computes the absolute path between two registered points. 
    /// Returns an empty array if no valid route is discoverable.
    /// </summary>
    private IReadOnlyCollection<AStarNode2D> GetPointPath(Position from, Position to, Faction faction)
    {
        if (!_nodes.ContainsKey(from) || !_nodes.ContainsKey(to)) return Array.Empty<AStarNode2D>();

        // 1. Reset metrics across the entire active subgraph graph before calculating
        foreach (var node in _nodes.Values) node.Reset();

        AStarNode2D startNode = _nodes[from];
        AStarNode2D targetNode = _nodes[to];

        // The open set keeps track of nodes awaiting evaluation.
        List<AStarNode2D> openSet = [startNode];
        HashSet<AStarNode2D> closedSet = new();

        startNode.GCost = 0f;
        startNode.HCost = CalculateHeuristic(startNode.Id, targetNode.Id);

        while (openSet.Count > 0)
        {
            // 2. Extract the node inside the open collection displaying the lowest total visual 'F' footprint cost
            AStarNode2D currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost || 
                   (IsEqualApprox(openSet[i].FCost, currentNode.FCost) && openSet[i].HCost < currentNode.HCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            // 3. Goal Condition reached: Trace back through parents to construct the path array
            if (currentNode == targetNode)
            {
                return targetNode.GetPath();
            }

            // 4. Process connected structural neighbor cells
            foreach (Position neighborId in _connections[currentNode.Id])
            {
                AStarNode2D neighbor = _nodes[neighborId];

                if (neighbor.Blocked || (neighbor.Occupied && neighbor != targetNode) || closedSet.Contains(neighbor))
                    continue;

                // Move cost = Current G + physical distance * custom weight modifier
                float moveCostToNeighbor = currentNode.GCost + 
                                           (currentNode.Id.DistanceTo(neighbor.Id) * neighbor.Surface.GetWeightMultiplier(faction));

                if (moveCostToNeighbor < neighbor.GCost)
                {
                    neighbor.GCost = moveCostToNeighbor;
                    neighbor.HCost = CalculateHeuristic(neighbor.Id, targetNode.Id);
                    neighbor.Parent = currentNode;

                    if (!openSet.Contains(neighbor))
                    {
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        return []; // No path found
    }

    // Standard Euclidean Distance Heuristic
    private float CalculateHeuristic(Position posA, Position posB) => posA.DistanceTo(posB);

    public PathWaypoint[] GetPath(
        Position from, Position to, Faction faction,
        int currentMovementBudget, int totalMovementBudget)
    {
        var pathNodes = GetPointPath(from, to, faction);
        if (pathNodes.Count == 0) return [];
        
        List<PathWaypoint> path = new(pathNodes.Count - 1);
        var previousNode = from;
        
        foreach (var node in pathNodes.Skip(1))
        {
            (int movementCost, float distance) = GetMovementCost(previousNode, node.Id, node.Surface, faction);
            
            currentMovementBudget -= movementCost;
            totalMovementBudget -= movementCost;

            var waypoint = new PathWaypoint(
                node.Id,
                movementCost,
                distance,
                currentMovementBudget >= 0,
                totalMovementBudget >= 0);
            
            path.Add(waypoint);
            previousNode = node.Id;
        }

        return path.ToArray();
    }
    
    private static (int movementCost, float distance) GetMovementCost(Position from, Position to, TerrainSurface surface, Faction faction)
    {
        var terrainWeightMultiplier = surface.GetWeightMultiplier(faction);
        var distance = from.DistanceTo(to);
        var movementCost = (int) (BaseMovementCost * distance * terrainWeightMultiplier);
        return (movementCost, distance);
    }

    private static bool IsEqualApprox(float a, float b)
    {
        if ((double) a == (double) b)
            return true;
        float num = 1E-06f * Math.Abs(a);
        if ((double) num < 9.999999974752427E-07)
            num = 1E-06f;
        return (double) Math.Abs(a - b) < (double) num;
    }
}
