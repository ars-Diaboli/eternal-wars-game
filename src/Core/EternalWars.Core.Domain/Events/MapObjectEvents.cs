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

/// <summary>A neutral guard accepted the hero's offer and joined its army.</summary>
public sealed record GuardJoined(HeroId Hero, MapObjectId Guard, string Creature, int Count) : IEvent;

/// <summary>The hero let a fleeing neutral guard go; it left the map (no experience).</summary>
public sealed record GuardFled(HeroId Hero, MapObjectId Guard) : IEvent;

/// <summary>A mine's guard was defeated; the mine can be flagged freely from now on.</summary>
public sealed record MineGuardDefeated(MapObjectId Mine) : IEvent;

/// <summary>Per-instance data of an object changed (editor: monster, mine guard, chest values; a guard that won a battle).</summary>
public sealed record MapObjectChanged(MapObjectId Object) : IEvent;

/// <summary>Editor: the map's guard mode changed.</summary>
public sealed record GuardModeChanged(Objects.GuardMode Mode) : IEvent;
