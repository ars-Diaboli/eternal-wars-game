using System.Collections.Generic;
using System.Linq;
using EW.Scripts.DataTypes;
using Godot;

namespace EW.Scripts.Heroes;

public sealed class HeroGridMovement(Hero hero)
{
    private static readonly Vector2I[] InteractionOffsets =
    [
        new Vector2I(-1, -1),
        new Vector2I(0, -1),
        new Vector2I(1, -1),
        new Vector2I(-1, 0),
        new Vector2I(1, 0),
        new Vector2I(-1, 1),
        new Vector2I(0, 1),
        new Vector2I(1, 1)
    ];

    private AStarGrid2D _aStar;
    private readonly Queue<MovementStep> _movementWaypoints = new();
    private IInteractableMapObject _pendingInteractionTarget;
    private Vector3 _targetStepPosition;
    private bool _isMoving;
    private bool _stopAfterCurrentStep;
    private int _currentMovementPoints;

    public bool IsIdle => !_isMoving && _movementWaypoints.Count == 0;
    public int CurrentMovementPoints => _currentMovementPoints;

    public void Initialize()
    {
        _targetStepPosition = hero.GlobalPosition;
        _currentMovementPoints = hero.MaxMovementPoints;
        InitializeNavigationGrid();
    }

    public bool MoveToGridCell(Vector3I targetCoords)
    {
        return MoveToTile(new Vector2I(targetCoords.X, targetCoords.Z));
    }

    public bool MoveToTile(Vector2I targetTile)
    {
        if (_currentMovementPoints < 10)
        {
            GD.Print($"{hero.Name} has insufficient movement points ({_currentMovementPoints})!");
            return false;
        }

        Vector2I startPoint2D = hero.GridPosition;
        Vector2I endPoint2D = targetTile;
        IInteractableMapObject interactionTarget = FindInteractableAtTile(endPoint2D);

        if (!IsInsideNavigationRegion(startPoint2D) || !IsInsideNavigationRegion(endPoint2D))
        {
            GD.Print($"{hero.Name}: Cannot move outside map bounds. Start={startPoint2D}, Target={endPoint2D}");
            return false;
        }

        List<Vector2I> pointPath = interactionTarget != null
            ? GetPathToInteractionTarget(startPoint2D, interactionTarget)
            : GetPointPath(startPoint2D, endPoint2D);

        if (pointPath.Count == 0)
        {
            GD.Print($"{hero.Name}: Path directly blocked by an obstacle or opposing army! Start={startPoint2D}, Target={endPoint2D}");
            return false;
        }

        return QueueMovementPath(pointPath, interactionTarget);
    }

    public List<PathWaypoint> StopMovementAtCurrentCell()
    {
        List<PathWaypoint> remainingPath = new();
        if (!_isMoving && _movementWaypoints.Count == 0)
        {
            return remainingPath;
        }

        Vector3 anchorPosition = _isMoving ? _targetStepPosition : hero.GlobalPosition;
        remainingPath.Add(new PathWaypoint
        {
            WorldPosition = anchorPosition,
            CanReachThisTurn = true
        });

        foreach (MovementStep waypoint in _movementWaypoints)
        {
            remainingPath.Add(new PathWaypoint
            {
                WorldPosition = waypoint.WorldPosition,
                CanReachThisTurn = true
            });
        }

        _pendingInteractionTarget = null;

        if (_isMoving)
        {
            RefundQueuedMovementSteps();
            _stopAfterCurrentStep = true;
        }
        else
        {
            RefundQueuedMovementSteps();
        }

        return remainingPath;
    }

    public List<PathWaypoint> PreviewFullPathToCell(Vector3I targetCoords)
    {
        return PreviewFullPathToTile(new Vector2I(targetCoords.X, targetCoords.Z));
    }

    public List<PathWaypoint> PreviewFullPathToTile(Vector2I targetTile)
    {
        List<PathWaypoint> fullPath = new();

        Vector2I startPoint2D = hero.GridPosition;
        Vector2I endPoint2D = targetTile;
        IInteractableMapObject interactionTarget = FindInteractableAtTile(endPoint2D);

        if (!IsInsideNavigationRegion(startPoint2D) || !IsInsideNavigationRegion(endPoint2D))
        {
            return fullPath;
        }

        List<Vector2I> pointPath = interactionTarget != null
            ? GetPathToInteractionTarget(startPoint2D, interactionTarget)
            : GetPointPath(startPoint2D, endPoint2D);

        if (pointPath.Count <= 1) return fullPath;

        fullPath.Add(new PathWaypoint { WorldPosition = hero.GlobalPosition, CanReachThisTurn = true });

        Vector2I previousPoint = startPoint2D;
        int budgetTracker = _currentMovementPoints;
        bool hasExhaustedMovement = false;

        foreach (Vector2I currentPoint in pointPath.Skip(1))
        {
            int stepCost = GetStepCost(currentPoint, previousPoint);

            bool canReach = false;
            if (!hasExhaustedMovement && budgetTracker >= stepCost)
            {
                canReach = true;
                budgetTracker -= stepCost;
            }
            else
            {
                hasExhaustedMovement = true;
            }

            fullPath.Add(new PathWaypoint
            {
                WorldPosition = GetWorldPosition(currentPoint),
                CanReachThisTurn = canReach
            });
            previousPoint = currentPoint;
        }

        return fullPath;
    }

    public void Process(double delta)
    {
        if (_isMoving)
        {
            hero.GlobalPosition = hero.GlobalPosition.MoveToward(_targetStepPosition, hero.StepSpeed * (float)delta);
            hero.SnapHeroToTerrain();

            if (HasReachedTargetStep())
            {
                hero.GlobalPosition = _targetStepPosition;
                hero.SnapHeroToTerrain();
                _isMoving = false;

                if (_stopAfterCurrentStep)
                {
                    _movementWaypoints.Clear();
                    _pendingInteractionTarget = null;
                    _stopAfterCurrentStep = false;
                }

                if (_pendingInteractionTarget != null && _movementWaypoints.Count == 0)
                {
                    InteractWithPendingTarget();
                }
                else if (_movementWaypoints.Count == 0)
                {
                    CheckForMapObjectsAtCurrentTile();
                }

                hero.VisionSystem?.RevealVisionAroundPosition(hero.GlobalPosition);
            }
        }
        else if (_movementWaypoints.Count > 0)
        {
            _targetStepPosition = _movementWaypoints.Dequeue().WorldPosition;
            _isMoving = true;
            FaceDirection(_targetStepPosition);
        }
    }

    public void RefillMovementPoints()
    {
        _currentMovementPoints = hero.MaxMovementPoints;
        GD.Print($"{hero.Name} movement points refilled to {_currentMovementPoints}.");
    }

    private void InitializeNavigationGrid()
    {
        _aStar = new AStarGrid2D();

        if (hero.WorldMapSettings == null)
        {
            GD.PushWarning($"{hero.Name}: WorldMapSettings is not assigned. Falling back to 64x64 navigation bounds.");
            _aStar.Region = new Rect2I(0, 0, 64, 64);
        }
        else
        {
            _aStar.Region = hero.WorldMapSettings.TileBounds;
        }

        _aStar.CellSize = new Vector2(1, 1);
        _aStar.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Always;
        _aStar.DefaultComputeHeuristic = AStarGrid2D.Heuristic.Euclidean;
        _aStar.DefaultEstimateHeuristic = AStarGrid2D.Heuristic.Euclidean;
        _aStar.Update();

        BakeTerrainWeightMap();
        GD.Print($"{hero.Name}: AStar navigation region initialized as {_aStar.Region}.");
    }

    private void BakeTerrainWeightMap()
    {
        if (_aStar == null) return;

        for (int x = _aStar.Region.Position.X; x < _aStar.Region.End.X; x++)
        {
            for (int y = _aStar.Region.Position.Y; y < _aStar.Region.End.Y; y++)
            {
                _aStar.SetPointWeightScale(new Vector2I(x, y), 1.0f);
            }
        }

        GD.Print("AStar logical terrain weight grid baked.");
    }

    private bool QueueMovementPath(List<Vector2I> pointPath, IInteractableMapObject interactionTarget)
    {
        _stopAfterCurrentStep = false;
        _pendingInteractionTarget = null;

        if (pointPath.Count <= 1)
        {
            if (interactionTarget != null && CanInteractWithMapObject(interactionTarget, hero.GridPosition))
            {
                interactionTarget.OnPlayerInteract(hero);
            }
            else
            {
                CheckForMapObjectsAtCurrentTile();
            }

            return true;
        }

        _movementWaypoints.Clear();
        Vector2I previousPoint = pointPath[0];
        bool queuedAnyWaypoint = false;

        foreach (Vector2I currentPoint in pointPath.Skip(1))
        {
            int stepCost = GetStepCost(currentPoint, previousPoint);

            if (_currentMovementPoints < stepCost)
            {
                break;
            }

            _movementWaypoints.Enqueue(new MovementStep
            {
                WorldPosition = GetWorldPosition(currentPoint),
                MovementCost = stepCost
            });
            _currentMovementPoints -= stepCost;
            previousPoint = currentPoint;
            queuedAnyWaypoint = true;
        }

        if (!queuedAnyWaypoint)
        {
            return false;
        }

        if (interactionTarget != null && previousPoint == pointPath[^1])
        {
            _pendingInteractionTarget = interactionTarget;
        }

        return true;
    }

    private List<Vector2I> GetPathToInteractionTarget(Vector2I startPoint, IInteractableMapObject interactionTarget)
    {
        if (CanInteractWithMapObject(interactionTarget, startPoint))
        {
            return [startPoint];
        }

        if (interactionTarget is not Hero)
        {
            return GetPointPath(startPoint, interactionTarget.InteractionTile);
        }

        Vector2I targetTile = interactionTarget.GridPosition;
        List<Vector2I> bestPath = new();
        float bestScore = float.MaxValue;

        foreach (Vector2I candidate in GetInteractionCandidateTiles(startPoint, targetTile))
        {
            List<Vector2I> candidatePath = GetPointPath(startPoint, candidate);
            if (candidatePath.Count == 0)
            {
                continue;
            }

            float score = GetPathCost(candidatePath);
            if (score < bestScore)
            {
                bestPath = candidatePath;
                bestScore = score;
            }
        }

        return bestPath;
    }

    private Vector3 GetWorldPosition(Vector2I tilePosition)
    {
        if (hero.WorldMapSettings == null)
        {
            return new Vector3(tilePosition.X, hero.GlobalPosition.Y, tilePosition.Y);
        }

        Vector3 worldPosition = hero.WorldMapSettings.TileToWorldPosition(tilePosition, hero.GlobalPosition.Y);
        worldPosition.Y = hero.GetHeroTerrainHeight(worldPosition);
        return worldPosition;
    }

    private bool IsInsideNavigationRegion(Vector2I point)
    {
        return _aStar != null && _aStar.Region.HasPoint(point);
    }

    private List<Vector2I> GetPointPath(Vector2I startPoint, Vector2I endPoint)
    {
        if (!IsInsideNavigationRegion(startPoint) || !IsInsideNavigationRegion(endPoint))
        {
            return new List<Vector2I>();
        }

        ClearTemporaryObstacles();
        MarkTemporaryHeroObstacles();
        MarkTemporaryMapObjectObstacles();

        if (_aStar.IsPointSolid(endPoint))
        {
            return new List<Vector2I>();
        }

        return _aStar.GetIdPath(startPoint, endPoint).ToList();
    }

    private float GetPathCost(List<Vector2I> pointPath)
    {
        float cost = 0.0f;

        for (int i = 1; i < pointPath.Count; i++)
        {
            cost += GetStepCost(pointPath[i], pointPath[i - 1]);
        }

        return cost;
    }

    private IEnumerable<Vector2I> GetInteractionCandidateTiles(Vector2I startPoint, Vector2I targetTile)
    {
        return InteractionOffsets
            .Select(offset => targetTile + offset)
            .Where(IsInsideNavigationRegion)
            .OrderBy(candidate => candidate.DistanceSquaredTo(startPoint));
    }

    private IInteractableMapObject FindInteractableAtTile(Vector2I tilePosition)
    {
        foreach (Node node in hero.GetParent().GetChildren())
        {
            if (node is IInteractableMapObject mapObject
                && mapObject != hero
                && (mapObject.OccupiesTile(tilePosition) || mapObject.InteractionTile == tilePosition))
            {
                return mapObject;
            }
        }

        return null;
    }

    private void ClearTemporaryObstacles()
    {
        for (int x = _aStar.Region.Position.X; x < _aStar.Region.End.X; x++)
        {
            for (int y = _aStar.Region.Position.Y; y < _aStar.Region.End.Y; y++)
            {
                Vector2I coord = new(x, y);
                if (_aStar.IsPointSolid(coord))
                {
                    _aStar.SetPointSolid(coord, false);
                }
            }
        }
    }

    private void MarkTemporaryHeroObstacles()
    {
        foreach (Node node in hero.GetParent().GetChildren())
        {
            if (node is Hero otherHero && otherHero != hero)
            {
                Vector2I otherHeroTile = otherHero.GridPosition;
                if (_aStar.Region.HasPoint(otherHeroTile))
                {
                    _aStar.SetPointSolid(otherHeroTile, true);
                }
            }
        }
    }

    private void MarkTemporaryMapObjectObstacles()
    {
        foreach (Node node in hero.GetParent().GetChildren())
        {
            if (node is IWorldMapObject mapObject && mapObject != hero)
            {
                foreach (Vector2I occupiedTile in mapObject.OccupiedTiles)
                {
                    if (_aStar.Region.HasPoint(occupiedTile))
                    {
                        _aStar.SetPointSolid(occupiedTile, true);
                    }
                }
            }
        }
    }

    private int GetStepCost(Vector2I currentPoint, Vector2I previousPoint)
    {
        bool isDiagonal = currentPoint.X != previousPoint.X && currentPoint.Y != previousPoint.Y;
        float baseDistance = isDiagonal ? 1.414f : 1.0f;
        float terrainWeight = _aStar.GetPointWeightScale(currentPoint);
        return Mathf.RoundToInt(10.0f * baseDistance * terrainWeight);
    }

    private bool HasReachedTargetStep()
    {
        Vector2 currentPosition2D = new(hero.GlobalPosition.X, hero.GlobalPosition.Z);
        Vector2 targetPosition2D = new(_targetStepPosition.X, _targetStepPosition.Z);
        return currentPosition2D.DistanceSquaredTo(targetPosition2D) <= 0.0001f;
    }

    private void CheckForMapObjectsAtCurrentTile()
    {
        Vector2I currentCoords2D = hero.GridPosition;
        foreach (Node node in hero.GetParent().GetChildren())
        {
            if (node is IInteractableMapObject mapObject && mapObject != hero && CanInteractWithMapObject(mapObject, currentCoords2D))
            {
                mapObject.OnPlayerInteract(hero);
                break;
            }
        }
    }

    private void InteractWithPendingTarget()
    {
        IInteractableMapObject interactionTarget = _pendingInteractionTarget;
        _pendingInteractionTarget = null;

        if (interactionTarget != null && GodotObject.IsInstanceValid((GodotObject)interactionTarget) && CanInteractWithMapObject(interactionTarget, hero.GridPosition))
        {
            interactionTarget.OnPlayerInteract(hero);
        }
    }

    private static bool CanInteractWithMapObject(IInteractableMapObject mapObject, Vector2I currentCoords2D)
    {
        return mapObject.CanInteractFromTile(currentCoords2D);
    }

    private void FaceDirection(Vector3 target)
    {
        Vector3 directionLook = new(target.X, hero.GlobalPosition.Y, target.Z);
        if (!directionLook.IsEqualApprox(hero.GlobalPosition))
        {
            hero.LookAt(directionLook, Vector3.Up);
        }
    }

    private void RefundQueuedMovementSteps()
    {
        while (_movementWaypoints.Count > 0)
        {
            _currentMovementPoints += _movementWaypoints.Dequeue().MovementCost;
        }
    }
}
