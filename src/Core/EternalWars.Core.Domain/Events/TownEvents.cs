using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>A town changed hands: captured by a hero, or set in the editor.</summary>
public sealed record TownOwnerChanged(MapObjectId Town, PlayerId? Previous, PlayerId? Current) : IEvent;

/// <summary>A town building reached <paramref name="Level"/> (dwellings: <see cref="DwellingBuilt"/>).</summary>
public sealed record TownBuilt(MapObjectId Town, string Building, int Level) : IEvent;

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
