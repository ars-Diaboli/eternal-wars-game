using EternalWars.Core.Domain.Armies;
using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.Heroes;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>A hero's army changed (exchange, battle casualties, joining neutrals, editor). Carries all seven slots.</summary>
public sealed record ArmyChanged(HeroId Hero, IReadOnlyList<CreatureStack?> Slots) : IEvent;

/// <summary>
/// A hero lost a battle (or fled from it) and left the map. <paramref name="Retreated"/>: it retreated or surrendered, and a roster hero now
/// waits in its player's taverns (ADR-081).
/// </summary>
public sealed record HeroDefeated(HeroId Hero, PlayerId Owner, bool Retreated) : IEvent;

/// <summary>A hero surrendered (ADR-081): its owner paid <paramref name="Gold"/> to <paramref name="To"/>; <c>BattleEnded</c> follows.</summary>
public sealed record HeroSurrendered(HeroId Hero, PlayerId Owner, PlayerId To, int Gold) : IEvent;

/// <summary>Editor: primary stats or Tactics of a hero changed.</summary>
public sealed record HeroStatsChanged(HeroId Hero, int Attack, int Defense, SkillLevel Tactics) : IEvent;
