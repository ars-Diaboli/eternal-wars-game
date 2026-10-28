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

/// <summary>A map object turned the hero away, with a reason to show (Library below level 10, a price the player cannot pay; ADR-068).</summary>
public sealed record MapObjectRefused(HeroId Hero, MapObjectId Object, string Reason) : IEvent;

/// <summary>A hero read a sign (ADR-068).</summary>
public sealed record SignRead(HeroId Hero, MapObjectId Sign, string Message) : IEvent;

/// <summary>
/// A hero looked out from an observatory or visited the Ruins (ADR-068); <c>VisibilityService</c> reveals the terrain within <paramref name="Radius"/>
/// of <paramref name="Center"/> to <paramref name="Owner"/>.
/// </summary>
public sealed record SurroundingsRevealed(HeroId Hero, MapObjectId Object, PlayerId Owner, MapPosition Center, int Radius) : IEvent;

/// <summary>A hero beat a creature bank's guard and took its reward (ADR-068); <paramref name="Artifact"/> null: none, or lost to a full backpack.</summary>
public sealed record CreatureBankPlundered(MapObjectId Bank, HeroId Hero, ResourceSet Resources, string? Artifact) : IEvent;

/// <summary>A plundered creature bank filled again with a newly rolled guard and reward (ADR-068).</summary>
public sealed record CreatureBankRefilled(MapObjectId Bank) : IEvent;

/// <summary>A hero upgraded a stack at a hill fort for 150 % of the upgrade price (ADR-068).</summary>
public sealed record HillFortUpgraded(MapObjectId Fort, HeroId Hero, int Slot, string From, string To, int Count) : IEvent;

/// <summary>A hero received resources from a resource provider (ADR-068, ADR-079); <see cref="ResourcesChanged"/> came just before.</summary>
public sealed record ResourcesFound(HeroId Hero, MapObjectId Object, ResourceSet Resources) : IEvent;

/// <summary>
/// The first visitor of a loot site searched it (ADR-068): <paramref name="Resources"/> (maybe none) and <paramref name="Artifact"/> (null: none, or
/// lost to a full backpack, see <c>ArtifactBackpackFull</c>). Nothing at all is a possible outcome. Vanishing sites are removed just before.
/// </summary>
public sealed record LootFound(HeroId Hero, MapObjectId Object, ResourceSet Resources, string? Artifact) : IEvent;

public sealed record ResourcePileCollected(HeroId Hero, MapObjectId Object, ResourceType Resource, int Amount) : IEvent;

public sealed record MineOwnerChanged(MapObjectId Mine, PlayerId? Previous, PlayerId? Current) : IEvent;

/// <summary>A player bought a cartographer's map of a level (ADR-079); <c>SurroundingsRevealed</c> follows.</summary>
public sealed record MapBought(MapObjectId Cartographer, HeroId Hero, PlayerId Owner, int Level) : IEvent;

/// <summary>An artifact merchant laid out new stock (game start, first day of a month; ADR-079).</summary>
public sealed record MerchantRestocked(MapObjectId Merchant, IReadOnlyList<string?> Stock) : IEvent;

/// <summary>A hero bought an artifact from a merchant (ADR-079); <c>ArtifactGained</c> came just before.</summary>
public sealed record ArtifactBought(MapObjectId Merchant, HeroId Hero, int Slot, string Artifact, ResourceSet Price) : IEvent;

/// <summary>A refugee camp's week: <paramref name="Available"/> creatures of <paramref name="Creature"/> to buy (ADR-079).</summary>
public sealed record RefugeesArrived(MapObjectId Camp, string Creature, int Available) : IEvent;

/// <summary>A hero sacrificed creatures or an artifact at an altar (ADR-079); the experience follows.</summary>
public sealed record SacrificeMade(MapObjectId Altar, HeroId Hero, int Experience, string? Creature = null, int Count = 0, string? Artifact = null) : IEvent;

/// <summary>A hero answered a sphinx (ADR-079): right brings an artifact or experience, wrong costs the day's movement and morale.</summary>
public sealed record SphinxAnswered(MapObjectId Sphinx, HeroId Hero, bool Right, string? Artifact = null, int Experience = 0) : IEvent;

/// <summary>A hero learned or improved a skill at a witch hut (ADR-079).</summary>
public sealed record WitchHutSkillLearned(MapObjectId Hut, HeroId Hero, string Skill, Heroes.SkillLevel Level) : IEvent;

/// <summary>A hero visited a Den of Thieves (ADR-079): the front-end shows its player the whole <c>ThievesGuild</c> table.</summary>
public sealed record ThievesDenVisited(HeroId Hero, MapObjectId Den) : IEvent;

/// <summary>An Astrologer's Tower told the visitor next week's event (ADR-077, MAP-32); <see cref="Turns.WeekEvent.None"/> when there is none.</summary>
public sealed record AstrologerConsulted(HeroId Hero, MapObjectId Tower, Turns.WeekEvent Next) : IEvent;

/// <summary>A resource provider got a new owner (a hero flagged it, its owner was defeated, or the editor set it); null = neutral (ADR-079).</summary>
public sealed record ProviderOwnerChanged(MapObjectId Provider, PlayerId? Previous, PlayerId? Current) : IEvent;

/// <summary>What waits at a resource provider changed (a new week, a visit, a capture, a resource caravan; ADR-079).</summary>
public sealed record ProviderStockChanged(MapObjectId Provider, ResourceSet Stored) : IEvent;

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

/// <summary>A player paid for a deposit, which became the mine <paramref name="Mine"/> (same id, ADR-070); <see cref="MapObjectChanged"/> and <see cref="MineOwnerChanged"/> follow.</summary>
public sealed record DepositBought(MapObjectId Mine, HeroId Hero, PlayerId Owner, ResourceSet Price) : IEvent;

/// <summary>A Keymaster's Tent gave a player the key of its colour (ADR-071).</summary>
public sealed record KeyReceived(MapObjectId Tent, HeroId Hero, PlayerId Owner, Objects.ObjectColour Colour) : IEvent;

/// <summary>A key holder passed a border guard, which left the map (ADR-071); <see cref="MapObjectRemoved"/> follows.</summary>
public sealed record BorderGuardPassed(MapObjectId Guard, HeroId Hero) : IEvent;

/// <summary>A garrison changed hands (ADR-070): beaten troops, or an empty one flagged.</summary>
public sealed record GarrisonOwnerChanged(MapObjectId Garrison, PlayerId? Previous, PlayerId? Current) : IEvent;

/// <summary>A mine's guard was defeated; the mine can be flagged freely from now on.</summary>
public sealed record MineGuardDefeated(MapObjectId Mine) : IEvent;

/// <summary>Per-instance data of an object changed (editor: monster incl. its guard mode, mine guard, chest values; a guard that won a battle).</summary>
public sealed record MapObjectChanged(MapObjectId Object) : IEvent;
