using EternalWars.Core.Domain.Common;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>One step on the adventure map. A multi-step move publishes one per step, so views can animate them in order.</summary>
public sealed record HeroMoved(HeroId Hero, MapPosition From, MapPosition To, int RemainingMovementPoints) : IEvent;

public sealed record HeroTeleported(HeroId Hero, MapPosition From, MapPosition To) : IEvent;

public sealed record HeroMovementRestored(HeroId Hero, int MovementPoints) : IEvent;

public sealed record HeroExperienceGained(HeroId Hero, int Amount, int Total) : IEvent;

/// <summary>A hero reached a level and raised <paramref name="Skill"/> to <paramref name="SkillLevel"/> (null: nothing left to learn).</summary>
public sealed record HeroLeveledUp(HeroId Hero, int Level, string? Skill, Heroes.SkillLevel SkillLevel) : IEvent;

/// <summary>A hero stepped towards another hero of the same owner; an exchange decision follows.</summary>
public sealed record HeroesMet(HeroId Hero, HeroId Other) : IEvent;

/// <summary>
/// A hero attacked an enemy hero. <paramref name="IsAmbush"/>: the attacker could not see the defender
/// (refog). The battle itself follows (<see cref="BattleStarted"/>), unless the defender has no army.
/// </summary>
public sealed record BattleTriggered(HeroId Attacker, HeroId Defender, bool IsAmbush) : IEvent;
