using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>A town got, lost or changed its governor (ADR-063); null = none.</summary>
public sealed record GovernorChanged(Common.MapObjectId Town, Common.HeroId? Previous, Common.HeroId? Current) : IEvent;

/// <summary>A town changed hands: captured by a hero, or set in the editor.</summary>
public sealed record TownOwnerChanged(MapObjectId Town, PlayerId? Previous, PlayerId? Current) : IEvent;

/// <summary>A town building reached <paramref name="Level"/> (dwellings: <see cref="DwellingBuilt"/>).</summary>
public sealed record TownBuilt(MapObjectId Town, string Building, int Level) : IEvent;

/// <summary>A town building dropped to <paramref name="Level"/>: a captured Capitol whose new owner already has one (ADR-070).</summary>
public sealed record TownBuildingDowngraded(MapObjectId Town, string Building, int Level) : IEvent;

/// <summary>
/// The dwelling of a creature line was built (level 1, with its first week of creatures in
/// <paramref name="Available"/>) or upgraded (level 2).
/// </summary>
public sealed record DwellingBuilt(MapObjectId Town, string Line, int Level, int Available) : IEvent;

/// <summary>Creatures were bought from a dwelling into the garrison (<paramref name="Hero"/> null) or the visiting hero.</summary>
public sealed record CreaturesRecruited(MapObjectId Town, string Creature, int Count, HeroId? Hero, int Available) : IEvent;

/// <summary>A stack of the garrison (<paramref name="Hero"/> null) or the visiting hero was upgraded in a town.</summary>
public sealed record StackUpgraded(MapObjectId Town, HeroId? Hero, int Slot, string From, string To, int Count) : IEvent;

/// <summary>A town's garrison changed (recruits, stack moves, upgrades, battle, editor). Carries all seven slots.</summary>
public sealed record GarrisonChanged(MapObjectId Town, IReadOnlyList<CreatureStack?> Slots) : IEvent;

/// <summary>A new week's creatures arrived in a town's dwellings; <paramref name="Available"/> per creature line.</summary>
public sealed record TownCreaturesGrew(MapObjectId Town, IReadOnlyDictionary<string, int> Available) : IEvent;

/// <summary>A caravan left its source (a map dwelling or a town) for the town <paramref name="Destination"/>; it arrives in <paramref name="Days"/> days (ADR-054).</summary>
public sealed record CaravanDispatched(int Caravan, PlayerId Owner, MapObjectId Source, MapObjectId Destination, string Creature, int Count, int Days) : IEvent;

/// <summary>A caravan reached its town and its creatures joined the garrison.</summary>
public sealed record CaravanArrived(int Caravan, MapObjectId Destination, string Creature, int Count) : IEvent;

/// <summary>The route of a caravan got blocked: it went back to its source (a dwelling gets the creatures and the price back).</summary>
public sealed record CaravanReturned(int Caravan, MapObjectId Source, string Creature, int Count) : IEvent;

/// <summary>A caravan's creatures scattered: its destination was lost, or the way back had no room.</summary>
public sealed record CaravanLost(int Caravan, MapObjectId Destination, string Creature, int Count, string Reason) : IEvent;
