using EternalWars.Core.Domain.Common;
using EternalWars.Core.Domain.MapEvents;
using EternalWars.Core.Events;

namespace EternalWars.Core.Domain.Events;

/// <summary>
/// A map event started to run (ADR-115): a hero stepped onto its cell, or a turn or day began. Its actions publish their own events after this
/// one; a message or question asks through <see cref="DecisionRequested"/>.
/// </summary>
public sealed record MapEventRan(string Event, MapEventKind Kind, PlayerId? Player, HeroId? Hero, MapObjectId? Town) : IEvent;

/// <summary>A map event changed a map variable (EVT-3 c).</summary>
public sealed record MapVariableChanged(string Variable, int Value) : IEvent;

/// <summary>A map event enabled or disabled an event (itself, "once", or another).</summary>
public sealed record MapEventEnabledChanged(string Event, bool Enabled) : IEvent;

/// <summary>A map event removed the cell that ran it; the event's other cells stay.</summary>
public sealed record MapEventCellRemoved(MapPosition Cell) : IEvent;

/// <summary>A map event gave the player a roster hero (EVT-4 c), like a freed prisoner; it stands at its place on the map now.</summary>
public sealed record MapEventHeroGiven(HeroId Hero, PlayerId Owner, string Definition, int Level) : IEvent;

/// <summary>A map event raised a hero's secondary skill to <paramref name="Level"/>.</summary>
public sealed record MapEventSkillGained(HeroId Hero, string Skill, Heroes.SkillLevel Level) : IEvent;

/// <summary>A map event gave a hero a spell book.</summary>
public sealed record MapEventSpellBookGained(HeroId Hero) : IEvent;

/// <summary>A map event taught a hero a spell.</summary>
public sealed record MapEventSpellLearned(HeroId Hero, string Spell) : IEvent;

/// <summary>A map event gave a hero a war machine (taking one publishes <see cref="WarMachineLost"/>).</summary>
public sealed record MapEventWarMachineGained(HeroId Hero, Battles.WarMachineSlot Slot) : IEvent;

/// <summary>A hero completed a quest's stage at a quest object (EVT-5); the stage's actions ran before it.</summary>
public sealed record QuestStageCompleted(MapObjectId Site, string Quest, int Stage, HeroId Hero, PlayerId Player) : IEvent;

/// <summary>The player's quest log changed (<see cref="MapEventState.QuestLog"/>): an entry was added, moved on, done or dropped.</summary>
public sealed record QuestLogChanged(PlayerId Player) : IEvent;
