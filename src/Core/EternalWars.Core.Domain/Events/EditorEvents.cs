using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

public sealed record TerrainPainted(IReadOnlyList<MapPosition> Positions, TerrainType Terrain) : IEvent;

public sealed record RoadsPainted(IReadOnlyList<MapPosition> Positions, RoadType Road) : IEvent;

/// <summary>The editor painted or erased rivers (ADR-079).</summary>
public sealed record RiversPainted(IReadOnlyList<MapPosition> Positions, RiverType River) : IEvent;

public sealed record HeroPlaced(HeroId Hero, PlayerId Owner, MapPosition Position) : IEvent;

public sealed record HeroRemoved(HeroId Hero) : IEvent;

public sealed record MapPropertiesChanged(string Name, string Description) : IEvent;

/// <summary>Player slots were added, removed or edited. Views re-read <c>GameState.Players</c>.</summary>
public sealed record PlayerSlotsChanged : IEvent;

public sealed record UndoHistoryChanged(string? UndoDescription, string? RedoDescription) : IEvent
{
    public bool CanUndo => UndoDescription is not null;

    public bool CanRedo => RedoDescription is not null;
}
