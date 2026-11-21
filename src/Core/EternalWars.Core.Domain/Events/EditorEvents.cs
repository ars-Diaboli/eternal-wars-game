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

/// <summary>
/// The editor gave the map a new frame (a resize, a crop, a shift or the undo of one; EDIT-8): it is now <paramref name="Width"/> ×
/// <paramref name="Height"/> tiles and what stayed on it moved by (<paramref name="Dx"/>, <paramref name="Dy"/>). Views of the map build
/// themselves again from the scenario.
/// </summary>
[SeenBy(Audience.Nobody)]
public sealed record MapResized(int Width, int Height, int Dx, int Dy) : IEvent;

[SeenBy(Audience.Witnesses)]
public sealed record HeroPlaced(HeroId Hero, PlayerId Owner, MapPosition Position) : IEvent;

[SeenBy(Audience.Witnesses)]
public sealed record HeroRemoved(HeroId Hero) : IEvent;

/// <summary>
/// The editor moved an object (its id, links, owner and contents stay) or turned a piece of scenery, which may stay where it is;
/// <paramref name="From"/> and <paramref name="To"/> are its top-left tiles.
/// </summary>
[SeenBy(Audience.Nobody)]
public sealed record MapObjectMoved(MapObjectId Object, MapPosition From, MapPosition To) : IEvent;

/// <summary>The editor moved a hero to another tile.</summary>
[SeenBy(Audience.Nobody)]
public sealed record HeroRepositioned(HeroId Hero, MapPosition From, MapPosition To) : IEvent;

/// <summary>The editor's selection changed. Views re-read the session's <c>MapSelection</c>.</summary>
[SeenBy(Audience.Nobody)]
public sealed record SelectionChanged : IEvent;

/// <summary>The editor's layers changed (ADR-262): which kinds are hidden and which locked.</summary>
[SeenBy(Audience.Nobody)]
public sealed record MapLayersChanged(Editing.MapLayer Hidden, Editing.MapLayer Locked) : IEvent;

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

/// <summary>
/// The edited map started (<paramref name="IsDirty"/>) or stopped having work that is not saved: an edit, undo or redo left the saved
/// step or came back to it, or the map was saved (<c>MapEditorSession.MarkSaved</c>).
/// </summary>
[SeenBy(Audience.Nobody)]
public sealed record UnsavedWorkChanged(bool IsDirty) : IEvent;

/// <summary>The editor changed a hero's spell book or spells (ADR-120).</summary>
[SeenBy(Audience.Witnesses)]
public sealed record HeroSpellsChanged(HeroId Hero) : IEvent;

/// <summary>The editor changed the map events (EVT-6): events, variables or event cells. Views re-read <c>GameState.MapEvents.Set</c>.</summary>
[SeenBy(Audience.Nobody)]
public sealed record MapEventsChanged : IEvent;

/// <summary>The author buried the Grail elsewhere or took it away (ADR-140); <paramref name="Position"/> null = drawn when the game starts.</summary>
[SeenBy(Audience.Nobody)]
public sealed record GrailMoved(MapPosition? Position) : IEvent;
