using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>Published first whenever a hero enters an object's visit tile; specific outcome events follow.</summary>
public sealed record MapObjectVisited(HeroId Hero, MapObjectId Object) : IEvent;

/// <summary>
/// A hero took the reward of an object with a limited <see cref="Objects.VisitScope"/> (ADR-068), so the object's
/// <see cref="Objects.MapObject.Visits"/> changed; the reward's own events follow.
/// </summary>
public sealed record MapObjectCollected(HeroId Hero, MapObjectId Object) : IEvent;

/// <summary>A hero visited an object whose reward was already taken (this week, by this hero, or by the first visitor; ADR-068).</summary>
public sealed record MapObjectFoundEmpty(HeroId Hero, MapObjectId Object) : IEvent;

/// <summary>A hero received resources from a resource provider (ADR-068); <see cref="ResourcesChanged"/> came just before.</summary>
public sealed record ResourcesFound(HeroId Hero, MapObjectId Object, ResourceSet Resources) : IEvent;

public sealed record ResourcePileCollected(HeroId Hero, MapObjectId Object, ResourceType Resource, int Amount) : IEvent;

public sealed record MineOwnerChanged(MapObjectId Mine, PlayerId? Previous, PlayerId? Current) : IEvent;

/// <summary>A map dwelling got a new owner (a hero flagged it, its owner was defeated, or the editor set it); null = neutral.</summary>
public sealed record DwellingOwnerChanged(MapObjectId Dwelling, PlayerId? Previous, PlayerId? Current) : IEvent;

/// <summary>A new week's creatures arrived in a map dwelling; <paramref name="Available"/> is what waits there now.</summary>
public sealed record DwellingCreaturesGrew(MapObjectId Dwelling, int Available) : IEvent;

/// <summary>Creatures were bought from a map dwelling by a hero (<paramref name="Hero"/>) or for a caravan (null); <paramref name="Available"/> is what is left.</summary>
public sealed record DwellingRecruited(MapObjectId Dwelling, HeroId? Hero, string Creature, int Count, int Available) : IEvent;

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

/// <summary>Per-instance data of an object changed (editor: monster incl. its guard mode, mine guard, chest values; a guard that won a battle).</summary>
public sealed record MapObjectChanged(MapObjectId Object) : IEvent;
