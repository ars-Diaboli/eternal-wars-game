using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Map;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

[SeenBy(Audience.Nobody)]
public sealed record TerrainPainted(IReadOnlyList<MapPosition> Positions, TerrainType Terrain) : IEvent;

[SeenBy(Audience.Nobody)]
public sealed record RoadsPainted(IReadOnlyList<MapPosition> Positions, RoadType Road) : IEvent;

/// <summary>The editor painted or erased rivers (ADR-079).</summary>
[SeenBy(Audience.Nobody)]
public sealed record RiversPainted(IReadOnlyList<MapPosition> Positions, RiverType River) : IEvent;

/// <summary>The editor painted tiles into a player's start zone, or out of every zone (<paramref name="Owner"/> null; MP-4, ADR-197).</summary>
[SeenBy(Audience.Nobody)]
public sealed record StartZonesPainted(IReadOnlyList<MapPosition> Positions, PlayerId? Owner) : IEvent;

/// <summary>
/// The editor changed the ground's height (ADR-182 g): every changed corner of <paramref name="Level"/> is in <paramref name="Area"/>.
/// Views re-read the heights there and move what stands on tiles the area <see cref="CornerArea.Touches"/>.
/// </summary>
[SeenBy(Audience.Nobody)]
public sealed record GroundHeightsChanged(int Level, CornerArea Area) : IEvent;

[SeenBy(Audience.Witnesses)]
public sealed record HeroPlaced(HeroId Hero, PlayerId Owner, MapPosition Position) : IEvent;

[SeenBy(Audience.Witnesses)]
public sealed record HeroRemoved(HeroId Hero) : IEvent;

[SeenBy(Audience.Nobody)]
public sealed record MapPropertiesChanged(string Name, string Description) : IEvent;

/// <summary>Player slots were added, removed or edited. Views re-read <c>GameState.Players</c>.</summary>
[SeenBy(Audience.Nobody)]
public sealed record PlayerSlotsChanged : IEvent;

/// <summary>
/// The editor's history changed: a step was recorded, undone or redone, or a jump moved over several. <paramref name="Position"/> of its
/// <paramref name="Count"/> steps are applied now; views re-read <c>MapEditorSession.History</c> for the steps.
/// </summary>
[SeenBy(Audience.Nobody)]
public sealed record UndoHistoryChanged(string? UndoDescription, string? RedoDescription, int Position, int Count) : IEvent
{
    public bool CanUndo => UndoDescription is not null;

    public bool CanRedo => RedoDescription is not null;
}

/// <summary>The editor changed a hero's spell book or spells (ADR-120).</summary>
[SeenBy(Audience.Witnesses)]
public sealed record HeroSpellsChanged(HeroId Hero) : IEvent;

/// <summary>The editor changed the map events (EVT-6): events, variables or event cells. Views re-read <c>GameState.MapEvents.Set</c>.</summary>
[SeenBy(Audience.Nobody)]
public sealed record MapEventsChanged : IEvent;

/// <summary>The author buried the Grail elsewhere or took it away (ADR-140); <paramref name="Position"/> null = drawn when the game starts.</summary>
[SeenBy(Audience.Nobody)]
public sealed record GrailMoved(MapPosition? Position) : IEvent;
