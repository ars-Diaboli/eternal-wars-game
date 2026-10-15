using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>Published first whenever a hero enters an object's visit tile; specific outcome events follow.</summary>
public sealed record MapObjectVisited(HeroId Hero, MapObjectId Object) : IEvent;

public sealed record ResourcePileCollected(HeroId Hero, MapObjectId Object, ResourceType Resource, int Amount) : IEvent;

public sealed record MineOwnerChanged(MapObjectId Mine, PlayerId? Previous, PlayerId? Current) : IEvent;

/// <summary>An object appeared on the map (editor placement or undo of a removal).</summary>
public sealed record MapObjectPlaced(MapObjectId Object, string DefinitionId, MapPosition Position) : IEvent;

/// <summary>An object left the map (picked up, opened, or removed in the editor).</summary>
public sealed record MapObjectRemoved(MapObjectId Object) : IEvent;

public sealed record GateLinkChanged(MapObjectId Gate, MapObjectId? LinkedGate) : IEvent;
