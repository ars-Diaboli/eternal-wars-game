using System.Text.Json.Serialization;

namespace EW.CoreLib.Types;

[method: JsonConstructor]
public struct PathWaypoint(
    Position position,
    int movementCost,
    float distance,
    bool canReachWithCurrentBudget,
    bool canReachWithTotalBudget)
{
    public Position Position = position;
    public readonly int MovementCost = movementCost;
    public readonly float Distance = distance;
    public readonly bool CanReachWithCurrentBudget = canReachWithCurrentBudget;
    public readonly bool CanReachWithTotalBudget = canReachWithTotalBudget;
}
