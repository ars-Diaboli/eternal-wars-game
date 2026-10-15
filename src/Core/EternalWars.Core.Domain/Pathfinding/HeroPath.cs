using EternalWars.Core.Domain.Common;

namespace EternalWars.Core.Domain.Pathfinding;

public enum PathStepKind
{
    Walk,

    /// <summary>Instant transfer through a linked gate; costs nothing.</summary>
    Teleport,
}

/// <summary>
/// One step of a planned route. <see cref="Day"/> is 0 for steps reachable with the hero's current
/// movement points, 1 for tomorrow, and so on. Views use it for HoMM-style day markers.
/// </summary>
public sealed record PathStep(MapPosition Position, PathStepKind Kind, int Cost, int TotalCost, int Day);

public sealed record HeroPath(HeroId Hero, IReadOnlyList<PathStep> Steps)
{
    public MapPosition Destination => Steps[^1].Position;

    public int TotalCost => Steps[^1].TotalCost;

    /// <summary>Number of turns needed, counting the current one.</summary>
    public int Turns => Steps[^1].Day + 1;

    public bool IsReachableThisTurn => Steps[^1].Day == 0;
}
