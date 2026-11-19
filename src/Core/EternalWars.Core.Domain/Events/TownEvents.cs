using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Economy;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>A town got, lost or changed its governor (ADR-063); null = none.</summary>
[SeenBy(Audience.Witnesses)]
public sealed record GovernorChanged(Common.MapObjectId Town, Common.HeroId? Previous, Common.HeroId? Current) : IEvent;

/// <summary>A town changed hands: captured by a hero, or set in the editor.</summary>
[SeenBy(Audience.Witnesses)]
public sealed record TownOwnerChanged(MapObjectId Town, PlayerId? Previous, PlayerId? Current) : IEvent;

/// <summary>A town building reached <paramref name="Level"/> (dwellings: <see cref="DwellingBuilt"/>).</summary>
[SeenBy(Audience.Witnesses)]
public sealed record TownBuilt(MapObjectId Town, Common.BuildingId Building, int Level) : IEvent;

/// <summary>
/// A hero took a unique building's visit bonus in a town (ADR-126, ADR-138); the bonus itself follows as its own events (a stat raised, experience,
/// movement, mana).
/// </summary>
[SeenBy(Audience.Witnesses)]
public sealed record UniqueBuildingVisited(HeroId Hero, MapObjectId Town, Common.BuildingId Building) : IEvent;

/// <summary>A hero brought the Grail into its owner's town and raised the town's Grail building, <paramref name="Name"/> (ADR-130, ADR-140); <see cref="TownBuilt"/> came first.</summary>
[SeenBy(Audience.Witnesses)]
public sealed record GrailBuildingRaised(HeroId Hero, MapObjectId Town, string Name) : IEvent;

/// <summary>A town building dropped to <paramref name="Level"/>: a captured Capitol whose new owner already has one (ADR-070).</summary>
[SeenBy(Audience.Witnesses)]
public sealed record TownBuildingDowngraded(MapObjectId Town, Common.BuildingId Building, int Level) : IEvent;

/// <summary>
/// The dwelling of a creature line was built (level 1, with its first week of creatures in
/// <paramref name="Available"/>) or upgraded (level 2).
/// </summary>
[SeenBy(Audience.Witnesses)]
public sealed record DwellingBuilt(MapObjectId Town, CreatureId Line, int Level, int Available) : IEvent;

/// <summary>Creatures were bought from a dwelling into the garrison (<paramref name="Hero"/> null) or the visiting hero.</summary>
[SeenBy(Audience.Witnesses)]
public sealed record CreaturesRecruited(MapObjectId Town, CreatureId Creature, int Count, HeroId? Hero, int Available) : IEvent;

/// <summary>A stack of the garrison (<paramref name="Hero"/> null) or the visiting hero was upgraded in a town.</summary>
[SeenBy(Audience.Witnesses)]
public sealed record StackUpgraded(MapObjectId Town, HeroId? Hero, int Slot, CreatureId From, CreatureId To, int Count) : IEvent;

/// <summary>
/// A stack of the garrison (<paramref name="Hero"/> null) or the visiting hero went through the Undead Transformer (ADR-137): <paramref name="Count"/>
/// of <paramref name="From"/> became <paramref name="Result"/> of <paramref name="To"/>.
/// </summary>
[SeenBy(Audience.Witnesses)]
public sealed record CreaturesTransformed(MapObjectId Town, HeroId? Hero, int Slot, CreatureId From, int Count, CreatureId To, int Result) : IEvent;

/// <summary>A town's garrison changed (recruits, stack moves, upgrades, battle, editor). Carries all seven slots.</summary>
[SeenBy(Audience.Witnesses)]
public sealed record GarrisonChanged(MapObjectId Town, IReadOnlyList<CreatureStack?> Slots) : IEvent;

/// <summary>A new week's creatures arrived in a town's dwellings; <paramref name="Available"/> per creature line.</summary>
[SeenBy(Audience.Witnesses)]
public sealed record TownCreaturesGrew(MapObjectId Town, IReadOnlyDictionary<CreatureId, int> Available) : IEvent;

/// <summary>
/// A caravan left its source (a map dwelling, a town or a resource provider) for the town <paramref name="Destination"/>; it arrives in
/// <paramref name="Days"/> days (ADR-054). A resource caravan (ADR-079) has <paramref name="Cargo"/>, no creature and a count of 0.
/// </summary>
[SeenBy(Audience.Owner)]
public sealed record CaravanDispatched(
    int Caravan, PlayerId Owner, MapObjectId Source, MapObjectId Destination, CreatureId Creature, int Count, int Days, ResourceSet? Cargo = null) : IEvent;

/// <summary>A caravan reached its town: its creatures joined the garrison, or its cargo the owner's resources.</summary>
[SeenBy(Audience.Witnesses)]
public sealed record CaravanArrived(int Caravan, MapObjectId Destination, CreatureId Creature, int Count, ResourceSet? Cargo = null) : IEvent;

/// <summary>The route of a caravan got blocked: it went back to its source (a dwelling or provider gets its goods and the price back).</summary>
[SeenBy(Audience.Witnesses)]
public sealed record CaravanReturned(int Caravan, MapObjectId Source, CreatureId Creature, int Count, ResourceSet? Cargo = null) : IEvent;

/// <summary>A caravan's goods were lost: its destination was lost, or the way back had no room.</summary>
[SeenBy(Audience.Witnesses)]
public sealed record CaravanLost(int Caravan, MapObjectId Destination, CreatureId Creature, int Count, string Reason, ResourceSet? Cargo = null) : IEvent;
