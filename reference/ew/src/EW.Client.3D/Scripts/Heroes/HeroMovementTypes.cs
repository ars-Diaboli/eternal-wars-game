using Godot;

namespace EW.Scripts.Heroes;

public struct PathWaypoint
{
    public Vector3 WorldPosition;
    public bool CanReachThisTurn;
}

public struct MovementStep
{
    public Vector3 WorldPosition;
    public int MovementCost;
}
