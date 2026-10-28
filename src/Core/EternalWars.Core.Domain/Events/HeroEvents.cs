using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>One step on the adventure map. A multi-step move publishes one per step, so views can animate them in order.</summary>
public sealed record HeroMoved(HeroId Hero, MapPosition From, MapPosition To, int RemainingMovementPoints) : IEvent;

public sealed record HeroTeleported(HeroId Hero, MapPosition From, MapPosition To) : IEvent;

public sealed record HeroMovementRestored(HeroId Hero, int MovementPoints) : IEvent;

/// <summary>A hero regained mana at the start of its owner's turn (ADR-063).</summary>
public sealed record HeroManaRestored(HeroId Hero, int Mana, int MaxMana) : IEvent;

public sealed record HeroExperienceGained(HeroId Hero, int Amount, int Total) : IEvent;

/// <summary>A map object added movement points for today (Fountain of Youth, Mineral Spring, Stables; ADR-068).</summary>
public sealed record HeroMovementGained(HeroId Hero, int Amount, int MovementPoints) : IEvent;

/// <summary>A map object raised a primary stat by <paramref name="Amount"/> to <paramref name="Value"/> (Arena, Colosseum, Library; ADR-068).</summary>
public sealed record HeroPrimaryStatRaised(HeroId Hero, Heroes.PrimaryStat Stat, int Amount, int Value) : IEvent;

/// <summary>A hero gained a temporary status from a map object (ADR-068); it replaced any status of the same source and kind.</summary>
public sealed record HeroStatusGained(HeroId Hero, Heroes.HeroStatus Status) : IEvent;

/// <summary>Temporary statuses of a hero ended: after its battle, at its owner's turn start, or when a week started (ADR-068).</summary>
public sealed record HeroStatusesExpired(HeroId Hero, IReadOnlyList<Heroes.HeroStatus> Statuses) : IEvent;

/// <summary>A war machine of the hero was destroyed in battle (ADR-056).</summary>
public sealed record WarMachineLost(HeroId Hero, Battles.WarMachineSlot Slot) : IEvent;

/// <summary>A hero handed a war machine to another hero of its player in their exchange (ADR-070); an offensive machine becomes the receiver's town's kind.</summary>
public sealed record WarMachineGiven(HeroId From, HeroId To, Battles.WarMachineSlot Slot) : IEvent;

/// <summary>A hero was hired in a town's tavern and stands at its entrance (ADR-060).</summary>
public sealed record HeroHired(MapObjectId Town, HeroId Hero, string Definition) : IEvent;

/// <summary>A hero bought a war machine at a town's blacksmith (ADR-056) or a War Machine Factory (ADR-068): <paramref name="Seller"/>.</summary>
public sealed record WarMachineBought(MapObjectId Seller, HeroId Hero, Battles.WarMachineSlot Slot) : IEvent;

/// <summary>A prisoner joined <paramref name="Owner"/> (ADR-068): a hero of <paramref name="Definition"/> at <paramref name="Level"/>, where the prison stood.</summary>
public sealed record HeroFreed(MapObjectId Prison, HeroId Hero, PlayerId Owner, string Definition, int Level) : IEvent;

/// <summary>
/// A hero reached a level, raised <paramref name="Skill"/> to <paramref name="SkillLevel"/> (null: nothing left to learn)
/// and <paramref name="Stat"/> to <paramref name="StatValue"/>.
/// </summary>
public sealed record HeroLeveledUp(HeroId Hero, int Level, string? Skill, Heroes.SkillLevel SkillLevel, Heroes.PrimaryStat Stat, int StatValue) : IEvent;

/// <summary>A hero stepped towards another hero of the same owner; an exchange decision follows.</summary>
public sealed record HeroesMet(HeroId Hero, HeroId Other) : IEvent;

/// <summary>
/// A hero attacked an enemy hero. <paramref name="IsAmbush"/>: the attacker could not see the defender
/// (refog). The battle itself follows (<see cref="BattleStarted"/>), unless the defender has no army.
/// </summary>
public sealed record BattleTriggered(HeroId Attacker, HeroId Defender, bool IsAmbush) : IEvent;
